# Payment Ledger Service

Production-style backend foundation for a wallet and ledger system built with `.NET 10`, `ASP.NET Core`, `PostgreSQL`, and `Entity Framework Core`.

## Current status

Phase 1 is complete:

- Clean Architecture solution structure
- Domain model, shared contracts, and application abstractions
- EF Core PostgreSQL infrastructure and repositories
- Initial migration and startup database bootstrap
- Dockerized local setup and baseline documentation
- Testing baseline and architecture guardrails

Business endpoints such as users, wallets, credits, debits, and refunds are not implemented yet.

## Architecture

- `src/PaymentLedger.Api` : HTTP host, Swagger, health checks, startup bootstrap
- `src/PaymentLedger.Application` : application abstractions and contracts
- `src/PaymentLedger.Domain` : core entities, enums, value objects
- `src/PaymentLedger.Infrastructure` : EF Core, PostgreSQL, repositories, migrations, seeding
- `src/PaymentLedger.Shared` : API response wrappers and shared constants
- `tests/` : unit, integration, and architecture tests

## Prerequisites

- `.NET 10 SDK`
- `Docker Desktop` with Docker Compose
- Optional for non-Docker run: a local PostgreSQL instance

## Run locally with dotnet

1. Make sure PostgreSQL is running and matches the connection string in `src/PaymentLedger.Api/appsettings.json` or `appsettings.Development.json`.
2. Start the API:

```powershell
dotnet run --project src/PaymentLedger.Api
```

Startup automatically:

- applies pending EF Core migrations
- seeds the treasury user and treasury wallets

Default local URLs from launch settings:

- `http://localhost:5275`
- `https://localhost:7155`

Useful endpoints:

- Health: `http://localhost:5275/health`
- Swagger: `http://localhost:5275/swagger`

## Run locally with Docker Compose

Start the stack:

```powershell
docker compose up --build
```

The local Docker setup is HTTP-only by design.

URLs:

- API: `http://localhost:8080`
- Health: `http://localhost:8080/health`
- Swagger: `http://localhost:8080/swagger`

Compose behavior:

- PostgreSQL starts first
- API waits for PostgreSQL to become healthy
- API applies migrations and seeds treasury data on startup

Stop the stack:

```powershell
docker compose down
```

Stop and remove the Postgres volume too:

```powershell
docker compose down -v
```

## Migration commands

Add a migration:

```powershell
dotnet ef migrations add <MigrationName> --project src/PaymentLedger.Infrastructure --startup-project src/PaymentLedger.Api --output-dir Persistence/Migrations
```

Remove the last migration:

```powershell
dotnet ef migrations remove --project src/PaymentLedger.Infrastructure --startup-project src/PaymentLedger.Api
```

Update the database manually:

```powershell
dotnet ef database update --project src/PaymentLedger.Infrastructure --startup-project src/PaymentLedger.Api
```

## Validation

Build:

```powershell
dotnet build PaymentLedgerService.sln
```

Run integration tests:

```powershell
dotnet test tests/PaymentLedger.IntegrationTests/PaymentLedger.IntegrationTests.csproj
```

Run unit tests:

```powershell
dotnet test tests/PaymentLedger.UnitTests/PaymentLedger.UnitTests.csproj
```

Run architecture tests:

```powershell
dotnet test tests/PaymentLedger.ArchitectureTests/PaymentLedger.ArchitectureTests.csproj
```
