using System.Data;

namespace commerceApi.Data;

// =============================================================================
// LEARNING NOTE: The DbConnectionFactory Pattern
// =============================================================================
//
// WHY DO WE NEED THIS?
// Dapper works with raw IDbConnection objects (unlike EF which uses DbContext).
// We need a way to create database connections that:
//   1. Can be injected via DI (Dependency Injection) — so we can swap it for tests
//   2. Uses the connection string from configuration — not hardcoded
//   3. Creates NEW connections each call — connections should be short-lived
//
// WHY AN INTERFACE?
// An interface defines a **contract** — it says "you must have this method" without
// specifying HOW it works. This gives us two huge benefits:
//
//   Benefit 1 — TESTABILITY:
//     In unit tests, we can create a FakeDbConnectionFactory that returns an
//     in-memory SQLite connection instead of a real PostgreSQL connection.
//     The repository doesn't know or care — it just calls CreateConnectionAsync().
//
//   Benefit 2 — FLEXIBILITY:
//     If we ever switch from PostgreSQL to SQL Server, we only change the
//     DbConnectionFactory implementation. Every repository that depends on
//     IDbConnectionFactory keeps working with zero code changes.
//
// HOW DI WORKS HERE:
//   - We register IDbConnectionFactory as a Singleton in Program.cs
//   - Any class that needs a database connection asks for IDbConnectionFactory
//     in its constructor (this is called "Constructor Injection")
//   - The DI container automatically provides the registered implementation
//   - The class never creates its own dependencies — they're "injected" from outside
//
// HOW THIS DIFFERS FROM EF CORE:
//   In EF Core, you'd inject a DbContext directly (which already manages connections).
//   With Dapper, we inject a factory that creates raw connections. This is more
//   explicit — you see exactly when connections are opened and closed.
// =============================================================================

/// <summary>
/// Creates database connections for Dapper to use.
/// Registered in DI as a singleton — the factory itself is stateless,
/// but each connection it creates is short-lived and disposed after use.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates and opens a new database connection.
    /// The caller is responsible for disposing the connection (use "using" statements).
    /// </summary>
    Task<IDbConnection> CreateConnectionAsync();
}
