using Xunit;

namespace CommerceApi.IntegrationTests.Fixtures;

// ============================================================================
// INTEGRATIONTESTBASE / COLLECTION DEFINITION — SHARE EXPENSIVE TEST FIXTURES
// ============================================================================
//
// xUnit has a few different ways to share setup objects. This file defines a TEST COLLECTION,
// which is xUnit's mechanism for sharing one fixture instance across MULTIPLE test classes.
//
// WHY A COLLECTION FIXTURE?
// ───────────────────────
// Our PostgreSQL fixture starts a real Docker container and initializes a real database.
// That is exactly the kind of expensive setup we want to reuse.
//
// If every test class started its own container, the suite would still work, but it would be
// much slower. By sharing one PostgresFixture across the whole integration-test collection,
// we pay the startup cost once and then reset state between tests.
//
// CLASS FIXTURE VS COLLECTION FIXTURE
// ───────────────────────────────────
// Class fixture:
//   - Shared only inside ONE test class
//   - Good when setup is class-specific
//
// Collection fixture:
//   - Shared across MULTIPLE test classes
//   - Good when setup is expensive and the same infrastructure is reused everywhere
//
// For this project, CollectionFixture is the better fit because Customers, Products, and Orders
// integration tests can all share the same PostgreSQL container.
//
// WHY IS THE COLLECTION NAMED "Integration"?
// ────────────────────────────────────────
// The string name is just xUnit's grouping label. Test classes decorated with:
//   [Collection("Integration")]
// join this shared group and receive the same PostgresFixture instance.
//
// Think of the collection name as the "room" all integration-test classes enter so they can
// use the same shared infrastructure.
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<PostgresFixture>
{
    // This class intentionally has no code.
    //
    // LEARNING NOTE: In xUnit, the presence of ICollectionFixture<PostgresFixture> on a
    // [CollectionDefinition] class is itself the configuration. The class acts like metadata:
    //   "For the Integration collection, create and share one PostgresFixture."
}
