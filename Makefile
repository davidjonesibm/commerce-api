# =============================================================================
# Makefile — Commerce API dev tasks
# =============================================================================
# Reads DB credentials from .env (same source as docker-compose).
# Requires: psql, docker compose
# =============================================================================

include .env
export

DB_URL := postgres://$(POSTGRES_USER):$(POSTGRES_PASSWORD)@localhost:5433/$(POSTGRES_DB)

.PHONY: seed test

## Reset the database to the known seed state before running tests
seed:
	psql "$(DB_URL)" -f db/seed.sql

## Run integration tests (Testcontainers + Respawn — no manual seed needed)
test:
	dotnet test tests/CommerceApi.IntegrationTests/CommerceApi.IntegrationTests.csproj
