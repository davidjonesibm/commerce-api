using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CommerceApi.IntegrationTests.Fixtures;

// ============================================================================
// COMMERCEAPIFACTORY — BOOT THE REAL API INSIDE THE TEST PROCESS
// ============================================================================
//
// WebApplicationFactory<Program> is ASP.NET Core's built-in test host for integration tests.
// It creates an in-process test server using the application's real Program.cs entry point.
//
// WHAT DOES "IN-PROCESS TEST SERVER" MEAN?
// ───────────────────────────────────────────
// It means the API runs inside the SAME process as the test runner rather than as a separate
// `dotnet run` process you must start manually.
//
// Compare the two approaches:
//
// Real external server:
//   Tests → network → separately running API process → database
//
// WebApplicationFactory server:
//   Tests → in-memory test host / handler pipeline → API → database
//
// WHY THIS IS USEFUL:
// 1. The tests still exercise the real ASP.NET Core middleware/routing/DI pipeline
// 2. Startup is simpler because the tests control the host directly
// 3. We can override settings before the app boots
//
// LEARNING NOTE: The base class exposes CreateClient(), which returns an HttpClient already
// wired to talk to this in-memory host. From a test's perspective, it still feels like making
// normal HTTP requests, but no separately managed server process is required.
public sealed class CommerceApiFactory : WebApplicationFactory<Program>
{
    // Hold a reference to the shared PostgreSQL fixture so we can read its connection string
    // when configuring the app under test.
    private readonly PostgresFixture _postgres;

    public CommerceApiFactory(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ConfigureWebHost is the customization hook for the app-under-test host.
        //
        // Think of this as: "Start the real app, but swap a few test-specific settings first."
        // This is how integration tests override production/development configuration without
        // changing the application code itself.

        // Replace the application's normal connection string with the Testcontainer connection.
        //
        // WHY UseSetting?
        // ASP.NET Core configuration is key/value based. Supplying the
        // `ConnectionStrings:DefaultConnection` key here overrides what would normally come from:
        // - appsettings.json
        // - appsettings.Development.json
        // - environment variables
        // etc.
        //
        // Result: when the app resolves its database connection string, it talks to the
        // disposable PostgreSQL container instead of any developer-local or shared database.
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            _postgres.ConnectionString);

        // Force the app to use the Development environment while running integration tests.
        //
        // WHY DEVELOPMENT?
        // This keeps behavior aligned with the local developer setup and ensures the app uses
        // its development-oriented configuration path unless the tests explicitly override it.
        builder.UseEnvironment("Development");
    }
}
