using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommerceApi.IntegrationTests.Fixtures;

// ============================================================================
// POSTGRESFIXTURE — SHARED DATABASE INFRASTRUCTURE FOR INTEGRATION TESTS
// ============================================================================
//
// In xUnit, a "fixture" is a shared object that owns expensive setup/teardown work.
// Think of this class as the test suite's database caretaker:
//   "Start PostgreSQL before tests run"
//   "Load schema and seed data"
//   "Reset back to a known clean state between tests"
//   "Tear everything down when the suite finishes"
//
// WHY DO WE NEED A FIXTURE AT ALL?
// ────────────────────────────────
// Starting Docker containers and initializing databases is relatively expensive.
// If every single test created its own container, the suite would become slow and noisy.
// Instead, we create ONE shared PostgreSQL container for the integration test collection
// and then use Respawn to quickly clean it between tests.
//
// TEST ISOLATION STRATEGY:
// ┌────────────────────────────┬───────────────────────────────────────────────┐
// │ Concern                     │ Strategy                                      │
// ├────────────────────────────┼───────────────────────────────────────────────┤
// │ Real database behavior      │ Use PostgreSQL in Docker via Testcontainers   │
// │ Fast repeated test runs     │ Share one container across the collection      │
// │ Clean state per test        │ Use Respawn + re-seeding before each test      │
// │ Automatic cleanup           │ Dispose the container when the collection ends │
// └────────────────────────────┴───────────────────────────────────────────────┘
//
// WHY TESTCONTAINERS INSTEAD OF OTHER OPTIONS?
// ────────────────────────────────────────────
// 1. Shared developer database:
//    Fragile. Tests depend on outside state and can interfere with each other.
//
// 2. In-memory database:
//    Not realistic for this app. We want PostgreSQL-specific behavior,
//    SQL execution, constraints, and real connection handling.
//
// 3. SQLite as a stand-in:
//    Better than in-memory for some apps, but still not PostgreSQL.
//    Different SQL dialect, different type handling, different behavior.
//
// 4. Testcontainers PostgreSQL:
//    Best fit here. Disposable, isolated, reproducible, and close to production.
//
// LIFECYCLE OVERVIEW:
// ───────────────────
// xUnit calls InitializeAsync() once when the shared fixture is created.
// Tests call ResetAsync() before each test to get back to the seed state.
// xUnit calls DisposeAsync() once after all tests in the collection are done.
public sealed class PostgresFixture : IAsyncLifetime
{
    // Build a PostgreSQL container definition up front.
    //
    // LEARNING NOTE: PostgreSqlBuilder uses a fluent API.
    // Each WithXyz(...) call returns the builder so we can chain configuration steps
    // together and then call Build() once at the end.
    //
    // Think of this as filling out a launch checklist:
    // - which image?
    // - which database name?
    // - which username/password?
    // - now create the container object
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        // Pin the image explicitly so test runs stay predictable.
        // Using a floating tag like `latest` can make tests change behavior unexpectedly.
        .WithImage("postgres:16")
        // Create a dedicated database inside the container for this test suite.
        .WithDatabase("commerce_test")
        // Configure credentials the app under test will use.
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    // Respawner is created ONCE after the schema exists and then reused for every reset.
    //
    // WHY REUSE IT?
    // Respawn does work up front to inspect the schema and compute a safe deletion order.
    // Rebuilding that metadata before every test would be unnecessary overhead.
    private Respawner _respawner = null!;

    // Expose the container-generated connection string so the API can talk to THIS database.
    // The WebApplicationFactory uses this to override the app's normal connection string.
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        // IAsyncLifetime.InitializeAsync is xUnit's async setup hook.
        // It runs once when the fixture is first created, before any tests use it.
        await _container.StartAsync();

        // Apply the schema from the repository's SQL file.
        //
        // WHY READ FROM THE REPO INSTEAD OF EMBEDDING SQL STRINGS HERE?
        // We want tests to use the SAME schema definition the rest of the project uses.
        // Keeping the SQL in one canonical file avoids configuration drift.
        var repoRoot = FindRepoRoot();
        var schemaSql = await File.ReadAllTextAsync(
            Path.Combine(repoRoot, "db", "init", "01-schema.sql"));

        // ExecScriptAsync runs SQL INSIDE the PostgreSQL container.
        //
        // WHY USE ExecScriptAsync INSTEAD OF OPENING AN NpgsqlConnection AND EXECUTING IT THERE?
        // Either approach can work. ExecScriptAsync is convenient for large bootstrap scripts
        // because it mirrors the idea of "run this SQL file against the container" and keeps
        // initialization close to the container lifecycle.
        var result = await _container.ExecScriptAsync(schemaSql);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Schema script failed (exit {result.ExitCode}): {result.Stderr}");

        // Load seed data immediately after the schema so the initial database state is known.
        // Tests can assume reference rows and sample data already exist.
        var seedSql = await File.ReadAllTextAsync(
            Path.Combine(repoRoot, "db", "seed.sql"));

        result = await _container.ExecScriptAsync(seedSql);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Seed script failed (exit {result.ExitCode}): {result.Stderr}");

        // Create Respawner ONCE after the database is fully initialized.
        //
        // LEARNING NOTE: Respawner needs an OPEN connection so it can inspect the current
        // schema and generate the SQL it will later use for fast cleanup.
        //
        // We deliberately do this after schema + seed setup because Respawn needs the real
        // tables to exist before it can understand what should be cleaned between tests.
        //
        // The connection open uses retry logic because some Docker environments report the
        // container as started slightly before port forwarding is fully ready.
        await using var conn = await OpenConnectionWithRetryAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            // Limit cleanup to the public schema, which is where this application's tables live.
            SchemasToInclude = ["public"],

            // Set the adapter explicitly so Respawn generates PostgreSQL-specific cleanup SQL.
            DbAdapter = DbAdapter.Postgres
        });
    }

    /// <summary>
    /// Resets the database to the known seed state.
    /// Called before each test to ensure isolation.
    /// </summary>
    public async Task ResetAsync()
    {
        // Open a fresh connection for the reset operation.
        // Each reset is independent; we do not keep a long-lived shared NpgsqlConnection.
        await using var conn = await OpenConnectionWithRetryAsync();

        // Step 1: Respawn deletes application data in a foreign-key-safe order.
        // This gives us a fast "empty but structured" database.
        await _respawner.ResetAsync(conn);

        // Step 2: Reapply seed data so every test starts from the SAME baseline.
        //
        // Reset flow in one sentence:
        // Respawn clears rows → seed script repopulates baseline data → test runs in isolation.
        var seedSql = await File.ReadAllTextAsync(
            Path.Combine(FindRepoRoot(), "db", "seed.sql"));
        var result = await _container.ExecScriptAsync(seedSql);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Seed script failed (exit {result.ExitCode}): {result.Stderr}");
    }

    public async Task DisposeAsync()
    {
        // IAsyncLifetime.DisposeAsync is xUnit's async teardown hook.
        // It runs once when the shared fixture is being destroyed.
        //
        // Because Testcontainers manages a real Docker container, disposal matters.
        // This is what stops the container and releases resources after the suite completes.
        await _container.DisposeAsync();
    }

    private async Task<NpgsqlConnection> OpenConnectionWithRetryAsync()
    {
        // Containers usually become reachable quickly, but there can be a short delay between:
        //   "container process has started" and
        //   "the database is actually accepting TCP connections from the host"
        //
        // This helper smooths over that startup window so tests don't fail due to timing noise.
        const int maxRetries = 10;
        const int delayMs = 500;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                // Return an OPEN connection because both Respawn.CreateAsync and ResetAsync
                // expect to work with a live database connection.
                var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                return conn;
            }
            catch (NpgsqlException) when (i < maxRetries - 1)
            {
                // Retry only for the transient startup window.
                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException("Could not connect to PostgreSQL container after retries.");
    }

    private static string FindRepoRoot()
    {
        // Test execution happens from compiled output directories like:
        //   bin/Debug/net9.0/
        // not from the repository root.
        //
        // We walk upward until we find the solution file, which acts as a stable marker for:
        //   "this is the repo root"
        //
        // WHY THIS PATTERN?
        // Hard-coding absolute paths would make the tests machine-specific and brittle.
        // This approach keeps path resolution portable across developer machines and CI.
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "commerceApi.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find repository root (looking for commerceApi.sln).");
    }
}
