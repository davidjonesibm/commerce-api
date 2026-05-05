// ============================================================================
// ORDERTESTS.CS — ORDER API INTEGRATION TESTS
// ============================================================================
//
// Orders are the richest aggregate in this sample because an order is not just
// a single row: it belongs to one customer and contains many order items.
// These tests verify both the order endpoints and the related item data that
// should come back with them.
//
// WHAT THIS FILE COVERS:
// 1. Reading seeded orders
// 2. Loading one order together with its items
// 3. Querying orders by customer
// 4. Creating a new order with line items and derived totals
// ============================================================================

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CommerceApi.IntegrationTests.Fixtures;
using commerceApi.Models;
using Xunit;

namespace CommerceApi.IntegrationTests.Orders;

// LEARNING NOTE: This class participates in the shared Integration collection.
// xUnit uses that collection definition to inject the same PostgresFixture used
// by the other integration test classes, so everyone points at one shared
// PostgreSQL Testcontainer.
[Collection("Integration")]
// LEARNING NOTE: IAsyncLifetime gives the class async hooks around each test:
// construct -> InitializeAsync -> run test -> DisposeAsync.
// That is especially useful for integration tests because setup/cleanup often
// involve I/O such as database resets and host disposal.
public sealed class OrderTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly CommerceApiFactory _factory;
    private readonly HttpClient _client;

    // LEARNING NOTE: Allows the tests to deserialize JSON payloads without
    // being brittle about property-name casing.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // LEARNING NOTE: xUnit provides PostgresFixture through the collection
    // mechanism. This constructor converts that shared database fixture into a
    // per-test-class application factory and HttpClient.
    public OrderTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new CommerceApiFactory(postgres);
        _client = _factory.CreateClient();
    }

    // LEARNING NOTE: Reset before each test to guarantee the seeded order and
    // order_item rows are always present in their expected state.
    public async Task InitializeAsync() => await _postgres.ResetAsync();

    // LEARNING NOTE: Dispose only what this class created. The collection
    // fixture owns the database container lifetime.
    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ─── Test 16: GET /api/orders → 200, 2 orders ────────────────────────

    [Fact]
    public async Task GetAll_ReturnsAllSeededOrders()
    {
        var response = await _client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>(JsonOptions);
        Assert.NotNull(orders);
        Assert.Equal(2, orders.Count);

        var ids = orders.Select(o => o.Id).OrderBy(id => id).ToList();
        Assert.Equal([1, 2], ids);

        var order1 = orders.Single(o => o.Id == 1);
        Assert.Equal(1, order1.CustomerId);
        Assert.Equal("Confirmed", order1.Status);
        Assert.Equal(124.98m, order1.TotalAmount);
    }

    // ─── Test 17: GET /api/orders/1 → 200, order with items ──────────────

    [Fact]
    public async Task GetById_ExistingOrder_ReturnsOrderWithItems()
    {
        var response = await _client.GetAsync("/api/orders/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(1, order.Id);
        Assert.Equal(1, order.CustomerId);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(124.98m, order.TotalAmount);

        // LEARNING NOTE: Orders have a one-to-many relationship with Items.
        // This test checks more than the root order row because the repository
        // is expected to load the child collection too. In this codebase that
        // happens through Dapper multi-mapping, so asserting on Items verifies
        // the relationship materialization path, not just the scalar columns.
        Assert.NotNull(order.Items);
        Assert.Equal(2, order.Items.Count);

        var productIds = order.Items.Select(i => i.ProductId).OrderBy(id => id).ToList();
        Assert.Equal([1, 2], productIds);
    }

    // ─── Test 18: GET /api/orders/999 → 404 ──────────────────────────────

    [Fact]
    public async Task GetById_NonExistentOrder_Returns404()
    {
        var response = await _client.GetAsync("/api/orders/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Test 19: GET /api/orders/customer/1 → 200, 1 order ──────────────

    [Fact]
    public async Task GetByCustomerId_CustomerWithOrders_ReturnsOrders()
    {
        // LEARNING NOTE: This endpoint follows the customer -> orders
        // relationship. We test a customer who DOES have seeded orders so the
        // API must return a populated collection instead of an empty array.
        var response = await _client.GetAsync("/api/orders/customer/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>(JsonOptions);
        Assert.NotNull(orders);
        Assert.Single(orders);
        Assert.Equal(1, orders[0].Id);
        Assert.Equal(1, orders[0].CustomerId);
        Assert.Equal("Confirmed", orders[0].Status);
    }

    // ─── Test 20: GET /api/orders/customer/3 → 200, empty ────────────────

    [Fact]
    public async Task GetByCustomerId_CustomerWithNoOrders_ReturnsEmptyArray()
    {
        // LEARNING NOTE: We also need the opposite case. Query endpoints are
        // easy to accidentally implement as 404 or null when there are no rows,
        // but for a collection resource the more natural contract is 200 OK with
        // an empty array.
        var response = await _client.GetAsync("/api/orders/customer/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>(JsonOptions);
        Assert.NotNull(orders);
        Assert.Empty(orders);
    }

    // ─── Test 21: POST /api/orders → 200/201, new order ──────────────────

    [Fact]
    public async Task Create_ValidOrder_ReturnsNewOrderWithItems()
    {
        // LEARNING NOTE: Creating an order exercises several business rules at
        // once:
        //   - customerId must reference an existing customer
        //   - each item must reference an existing product
        //   - totalAmount should be calculated from the submitted line items
        //   - status should default to "Pending" for a brand-new order
        //
        // That makes this a good end-to-end test of aggregate creation rather
        // than just a simple row insert.
        var payload = new
        {
            customerId = 2,
            items = new[]
            {
                new { productId = 2, quantity = 1, unitPrice = 34.99 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/orders", payload);

        Assert.True(new[] { 200, 201 }.Contains((int)response.StatusCode),
            $"Expected 200 or 201 but got {(int)response.StatusCode}");

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.True(order.Id > 2);
        Assert.Equal(2, order.CustomerId);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(34.99m, order.TotalAmount);

        Assert.NotNull(order.Items);
        Assert.Single(order.Items);
        Assert.Equal(2, order.Items[0].ProductId);
        Assert.Equal(1, order.Items[0].Quantity);
        Assert.Equal(34.99m, order.Items[0].UnitPrice);
    }
}
