// ============================================================================
// CUSTOMERTESTS.CS — CUSTOMER API INTEGRATION TESTS
// ============================================================================
//
// These tests exercise the Customer HTTP endpoints end-to-end:
//   HTTP request -> ASP.NET Core pipeline -> handlers/repositories -> PostgreSQL
//
// Because these are INTEGRATION tests, we are not mocking the database or the
// web server. Each test talks to a real application instance backed by the
// shared PostgreSQL Testcontainer created by PostgresFixture.
//
// WHAT THIS FILE COVERS:
// 1. Reading seeded customers
// 2. Creating new customers
// 3. Updating existing customers
// 4. Deleting customers, including foreign key restriction behavior
// ============================================================================

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CommerceApi.IntegrationTests.Fixtures;
using commerceApi.Models;
using Xunit;

namespace CommerceApi.IntegrationTests.Customers;

// LEARNING NOTE: [Collection("Integration")] tells xUnit that this test class
// belongs to the shared "Integration" collection defined in IntegrationTestBase.
// That collection is wired to ICollectionFixture<PostgresFixture>, which means:
//   - xUnit creates ONE PostgresFixture instance for the collection
//   - all integration test classes can reuse that same PostgreSQL container
//   - tests avoid paying the cost of booting a brand-new container per class
//
// WHY USE A COLLECTION FIXTURE?
// Testcontainers are intentionally realistic, but starting a database container
// is much heavier than instantiating a normal object. Sharing the fixture keeps
// the tests fast while Respawn still gives each test a clean database state.
[Collection("Integration")]
// LEARNING NOTE: IAsyncLifetime gives the test class async setup/teardown hooks.
// For EACH test method, xUnit does this in order:
//   1. Construct the test class
//   2. Call InitializeAsync()
//   3. Run the test method
//   4. Call DisposeAsync()
//
// This is the right fit here because both database reset and factory disposal
// are asynchronous operations.
public sealed class CustomerTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly CommerceApiFactory _factory;
    private readonly HttpClient _client;

    // LEARNING NOTE: System.Text.Json is case-sensitive by default.
    // Our API models use PascalCase property names in C# (FirstName, LastName),
    // while JSON payload conventions often use camelCase. Enabling
    // PropertyNameCaseInsensitive makes deserialization more tolerant so these
    // tests focus on behavior, not on casing trivia.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // LEARNING NOTE: xUnit injects the shared PostgresFixture into the
    // constructor because this class belongs to the Integration collection.
    //
    // From that fixture we create:
    //   - a WebApplicationFactory configured to point at the container database
    //   - an HttpClient that sends real in-memory HTTP requests to the test host
    //
    // WHY CREATE THE CLIENT HERE?
    // The constructor is the natural place to build per-test-class resources.
    // The expensive shared dependency is the container fixture; the factory and
    // client are lightweight wrappers around the application-under-test.
    public CustomerTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new CommerceApiFactory(postgres);
        _client = _factory.CreateClient();
    }

    // LEARNING NOTE: ResetAsync is the isolation guarantee.
    // Respawn clears data back to a known baseline, then the fixture re-runs the
    // seed script. That means every test starts from the same deterministic state
    // no matter what the previous test inserted, updated, or deleted.
    //
    // WHY RESET BEFORE EACH TEST INSTEAD OF AFTER?
    // If a test fails halfway through, post-test cleanup might never run.
    // Pre-test reset is more reliable because the next test always starts clean.
    public async Task InitializeAsync() => await _postgres.ResetAsync();

    // LEARNING NOTE: We dispose both the HttpClient and the factory because this
    // class owns both of them. The shared PostgreSQL container is NOT disposed
    // here; that is the collection fixture's responsibility when the whole test
    // collection finishes.
    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ─── Test 1: GET /api/Customers → 200, 3 customers ───────────────────

    [Fact]
    public async Task GetAll_ReturnsAllSeededCustomers()
    {
        // LEARNING NOTE: This first test is the template for the rest of the
        // file. It follows the classic AAA pattern:
        //   Arrange: rely on the known seed state created by ResetAsync
        //   Act:     issue one HTTP request to the API
        //   Assert:  verify both the HTTP result and the returned business data
        //
        // WHY ASSERT AGAINST SEED DATA?
        // Integration tests are strongest when they start from known facts.
        // The seed script inserts three customers with fixed IDs and emails, so
        // we can assert exact values and catch regressions in routing, SQL,
        // JSON serialization, and repository mapping all at once.
        var response = await _client.GetAsync("/api/Customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // LEARNING NOTE: ReadFromJsonAsync<T>() reads the response body stream
        // and deserializes the JSON into the requested CLR type. Here we expect
        // the API to return a JSON array that maps cleanly to List<Customer>.
        var customers = await response.Content.ReadFromJsonAsync<List<Customer>>(JsonOptions);
        Assert.NotNull(customers);
        Assert.Equal(3, customers.Count);

        var ids = customers.Select(c => c.Id).OrderBy(id => id).ToList();
        Assert.Equal([1, 2, 3], ids);

        var alice = customers.Single(c => c.Id == 1);
        Assert.Equal("Alice", alice.FirstName);
        Assert.Equal("Johnson", alice.LastName);
        Assert.Equal("alice@example.com", alice.Email);
    }

    // ─── Test 2: GET /api/Customers/1 → 200, Alice ───────────────────────

    [Fact]
    public async Task GetById_ExistingCustomer_ReturnsCustomer()
    {
        var response = await _client.GetAsync("/api/Customers/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var customer = await response.Content.ReadFromJsonAsync<Customer>(JsonOptions);
        Assert.NotNull(customer);
        Assert.Equal(1, customer.Id);
        Assert.Equal("Alice", customer.FirstName);
        Assert.Equal("Johnson", customer.LastName);
        Assert.Equal("alice@example.com", customer.Email);
    }

    // ─── Test 3: GET /api/Customers/999 → 404 ────────────────────────────

    [Fact]
    public async Task GetById_NonExistentCustomer_Returns404()
    {
        var response = await _client.GetAsync("/api/Customers/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Test 4: POST /api/Customers → 201, new customer ─────────────────

    [Fact]
    public async Task Create_ValidCustomer_Returns201WithNewCustomer()
    {
        var payload = new { firstName = "Test", lastName = "User", email = "testuser@example.com" };

        var response = await _client.PostAsJsonAsync("/api/Customers", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var customer = await response.Content.ReadFromJsonAsync<Customer>(JsonOptions);
        Assert.NotNull(customer);
        Assert.True(customer.Id > 3);
        Assert.Equal("Test", customer.FirstName);
        Assert.Equal("User", customer.LastName);
        Assert.Equal("testuser@example.com", customer.Email);
    }

    // ─── Test 5: POST /api/Customers (duplicate email) → error ───────────

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsError()
    {
        var payload = new { firstName = "Alice", lastName = "Johnson", email = "alice@example.com" };

        var response = await _client.PostAsJsonAsync("/api/Customers", payload);

        // LEARNING NOTE: Duplicate email hits a UNIQUE constraint in PostgreSQL.
        // Different implementations surface that failure differently:
        //   400 -> validation/business rule translated into a client error
        //   409 -> explicit conflict response
        //   500 -> raw database exception bubbled up without custom mapping
        //
        // The important behavior under test is "the duplicate must NOT succeed".
        Assert.True(new[] { 400, 409, 500 }.Contains((int)response.StatusCode),
            $"Expected 400, 409, or 500 but got {(int)response.StatusCode}");
    }

    // ─── Test 6: PUT /api/Customers/3 → 204, verify update ───────────────

    [Fact]
    public async Task Update_ExistingCustomer_ReturnsNoContentAndUpdatesData()
    {
        var payload = new { id = 3, firstName = "Charlie", lastName = "Williams", email = "charlie.updated@example.com" };

        var response = await _client.PutAsJsonAsync("/api/Customers/3", payload);

        Assert.True(new[] { 200, 204 }.Contains((int)response.StatusCode),
            $"Expected 200 or 204 but got {(int)response.StatusCode}");

        // LEARNING NOTE: This is the common write-then-read verification pattern.
        // A successful PUT status code only tells us the endpoint accepted the
        // request. The follow-up GET proves the change actually persisted to the
        // database and comes back through the normal read path.
        var getResponse = await _client.GetAsync("/api/Customers/3");
        var customer = await getResponse.Content.ReadFromJsonAsync<Customer>(JsonOptions);
        Assert.NotNull(customer);
        Assert.Equal("charlie.updated@example.com", customer.Email);
    }

    // ─── Test 7: DELETE /api/Customers/1 (has orders) → error ─────────────

    [Fact]
    public async Task Delete_CustomerWithOrders_ReturnsError()
    {
        // LEARNING NOTE: In 01-schema.sql, orders.customer_id references
        // customers.id with ON DELETE RESTRICT. That means PostgreSQL refuses to
        // delete a customer row while dependent orders still exist. This test is
        // verifying that database-level referential integrity is preserved all
        // the way up through the API.
        var response = await _client.DeleteAsync("/api/Customers/1");

        Assert.True(new[] { 400, 409, 500 }.Contains((int)response.StatusCode),
            $"Expected 400, 409, or 500 but got {(int)response.StatusCode}");
    }

    // ─── Test 8: DELETE /api/Customers/3 (no orders) → 204 ───────────────

    [Fact]
    public async Task Delete_CustomerWithoutOrders_ReturnsSuccess()
    {
        var response = await _client.DeleteAsync("/api/Customers/3");

        Assert.True(new[] { 200, 204 }.Contains((int)response.StatusCode),
            $"Expected 200 or 204 but got {(int)response.StatusCode}");
    }
}
