// ============================================================================
// PRODUCTTESTS.CS — PRODUCT API INTEGRATION TESTS
// ============================================================================
//
// These tests verify Product CRUD behavior against the full application stack.
// Each request goes through real routing, validation, handlers, repositories,
// and PostgreSQL instead of using mocks.
//
// WHAT THIS FILE COVERS:
// 1. Reading the seeded product catalog
// 2. Creating a product and verifying database-generated IDs
// 3. Updating product data
// 4. Deleting products, including foreign key protection from order history
// ============================================================================

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CommerceApi.IntegrationTests.Fixtures;
using commerceApi.Models;
using Xunit;

namespace CommerceApi.IntegrationTests.Products;

// LEARNING NOTE: This attribute links the class to the shared Integration
// collection, which xUnit backs with a single PostgresFixture instance.
// The fixture owns the PostgreSQL Testcontainer, so multiple test classes can
// reuse the same database container without restarting it every time.
[Collection("Integration")]
// LEARNING NOTE: IAsyncLifetime provides async setup and teardown around each
// test method. xUnit constructs the class, runs InitializeAsync(), executes one
// test, then runs DisposeAsync(). That keeps the lifecycle explicit and matches
// the async nature of database reset and host disposal.
public sealed class ProductTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly CommerceApiFactory _factory;
    private readonly HttpClient _client;

    // LEARNING NOTE: Case-insensitive deserialization keeps the tests resilient
    // to JSON naming differences between C# property casing and HTTP payloads.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // LEARNING NOTE: PostgresFixture is injected by xUnit from the collection.
    // We then create a WebApplicationFactory configured with that connection
    // string and obtain an HttpClient for making real requests into the app.
    public ProductTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new CommerceApiFactory(postgres);
        _client = _factory.CreateClient();
    }

    // LEARNING NOTE: ResetAsync restores the database to the known seed state
    // before every test. This makes tests independent and idempotent.
    public async Task InitializeAsync() => await _postgres.ResetAsync();

    // LEARNING NOTE: Dispose per-test resources here. The shared container lives
    // longer than this class and is cleaned up by the fixture itself.
    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ─── Test 9: GET /api/Products → 200, 5 products ─────────────────────

    [Fact]
    public async Task GetAll_ReturnsAllSeededProducts()
    {
        // LEARNING NOTE: Same high-level Arrange / Act / Assert pattern as the
        // first customer integration test. See CustomerTests for the fuller
        // walkthrough of how the seed-state assumptions and JSON deserialization
        // work.
        var response = await _client.GetAsync("/api/Products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<Product>>(JsonOptions);
        Assert.NotNull(products);
        Assert.Equal(5, products.Count);

        var ids = products.Select(p => p.Id).OrderBy(id => id).ToList();
        Assert.Equal([1, 2, 3, 4, 5], ids);

        var keyboard = products.Single(p => p.Id == 1);
        Assert.Equal("Mechanical Keyboard", keyboard.Name);
        Assert.Equal(89.99m, keyboard.Price);
    }

    // ─── Test 10: GET /api/Products/1 → 200, Keyboard ────────────────────

    [Fact]
    public async Task GetById_ExistingProduct_ReturnsProduct()
    {
        var response = await _client.GetAsync("/api/Products/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<Product>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(1, product.Id);
        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal(89.99m, product.Price);
        Assert.Equal(50, product.StockQuantity);
    }

    // ─── Test 11: GET /api/Products/999 → 404 ────────────────────────────

    [Fact]
    public async Task GetById_NonExistentProduct_Returns404()
    {
        var response = await _client.GetAsync("/api/Products/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Test 12: POST /api/Products → 201, new product ──────────────────

    [Fact]
    public async Task Create_ValidProduct_Returns201WithNewProduct()
    {
        var payload = new { name = "Test Widget", description = "A test product", price = 9.99, stockQuantity = 10 };

        var response = await _client.PostAsJsonAsync("/api/Products", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<Product>(JsonOptions);
        Assert.NotNull(product);
        // LEARNING NOTE: Five products are inserted by seed.sql with IDs 1..5.
        // Asserting the new ID is > 5 proves we received a database-generated
        // identity value instead of accidentally overwriting seeded rows.
        Assert.True(product.Id > 5);
        Assert.Equal("Test Widget", product.Name);
        Assert.Equal("A test product", product.Description);
        Assert.Equal(9.99m, product.Price);
        Assert.Equal(10, product.StockQuantity);
    }

    // ─── Test 13: PUT /api/Products/5 → 204, verify update ───────────────

    [Fact]
    public async Task Update_ExistingProduct_ReturnsNoContentAndUpdatesData()
    {
        var payload = new
        {
            id = 5,
            name = "Laptop Stand Pro",
            description = "Adjustable aluminum stand, fits up to 17\"",
            price = 39.99,
            stockQuantity = 200
        };

        var response = await _client.PutAsJsonAsync("/api/Products/5", payload);

        Assert.True(new[] { 200, 204 }.Contains((int)response.StatusCode),
            $"Expected 200 or 204 but got {(int)response.StatusCode}");

        // Verify the update persisted
        var getResponse = await _client.GetAsync("/api/Products/5");
        var product = await getResponse.Content.ReadFromJsonAsync<Product>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Laptop Stand Pro", product.Name);
        Assert.Equal(39.99m, product.Price);
    }

    // ─── Test 14: DELETE /api/Products/1 (has order refs) → error ─────────

    [Fact]
    public async Task Delete_ProductWithOrderReferences_ReturnsError()
    {
        // LEARNING NOTE: order_items.product_id uses a foreign key with
        // ON DELETE RESTRICT. If a product appears in existing order history,
        // PostgreSQL blocks the delete to preserve historical correctness.
        var response = await _client.DeleteAsync("/api/Products/1");

        Assert.True(new[] { 400, 409, 500 }.Contains((int)response.StatusCode),
            $"Expected 400, 409, or 500 but got {(int)response.StatusCode}");
    }

    // ─── Test 15: DELETE /api/Products/5 (no order refs) → 204 ───────────

    [Fact]
    public async Task Delete_ProductWithoutOrderReferences_ReturnsSuccess()
    {
        // LEARNING NOTE: Product 5 exists in the seed catalog but is not
        // referenced by seeded order_items, so the foreign key restriction does
        // not apply and the delete should succeed.
        var response = await _client.DeleteAsync("/api/Products/5");

        Assert.True(new[] { 200, 204 }.Contains((int)response.StatusCode),
            $"Expected 200 or 204 but got {(int)response.StatusCode}");
    }
}
