# Payment Ledger Service

Production-grade backend service for an idempotent digital wallet and double-entry ledger system built with **.NET 10**, **ASP.NET Core**, **PostgreSQL**, and **Entity Framework Core**.

---

## Current Status & Capabilities

The service implements Clean Architecture with CQRS via MediatR, FluentValidation, strict double-entry ledger accounting, idempotent transaction processing, and enterprise observability.

### Implemented Features

1. **User Management**:
   - `POST /api/v1/users`: Create user with unique email validation.
   - `GET /api/v1/users`: Paginated user listing with search and status filters.
   - `GET /api/v1/users/{id}`: Retrieve user details by ID.
   - `PUT /api/v1/users/{id}`: Update user name and email.
   - `DELETE /api/v1/users/{id}`: Delete user (with protection against deleting users with active wallets).

2. **Wallet Lifecycle Management**:
   - `POST /api/v1/wallets`: Create currency-specific wallet for active users.
   - `GET /api/v1/wallets/{id}`: Retrieve wallet metadata.
   - `GET /api/v1/wallets/{id}/balance`: Query real-time wallet balance.
   - `GET /api/v1/wallets?userId={userId}`: Query all wallets belonging to a user.
   - `PATCH /api/v1/wallets/{id}/block`: Block wallet from operations.
   - `PATCH /api/v1/wallets/{id}/unblock`: Unblock wallet.
   - `PATCH /api/v1/wallets/{id}/close`: Close empty wallet (enforces zero-balance invariant).

3. **Financial Transactions & Double-Entry Ledger**:
   - `POST /api/v1/wallets/{id}/credit`: Credit funds to user wallet from Treasury with balanced double-entry ledger entries.
   - `POST /api/v1/wallets/{id}/debit`: Debit funds from user wallet to Treasury with pessimistic row locking (`FOR UPDATE`) and balance checks.
   - **Idempotency Engine**: Supports `Idempotency-Key` (header or payload) with SHA-256 request payload hashing to prevent duplicate transactions.

4. **Observability, Serilog & Correlation Tracking**:
   - **Serilog**: Structured console logging and daily rolling file logs (`logs/payment-ledger-.log`).
   - **Correlation Tracking**: `CorrelationIdMiddleware` extracts or generates `X-Correlation-ID`, populating Serilog `LogContext`, HTTP response headers, and ambient `CorrelationContext.Current`.

5. **Global Exception Handling & Standardized Response Envelopes**:
   - **Zero Raw Leaks**: All client and server errors are trapped by `GlobalExceptionHandlerMiddleware` and converted into standardized `ErrorResponse` envelopes without stack trace leakage.
   - **HTTP Mapping**:
     - `ValidationException` &rarr; `400 Bad Request` with field-level `ErrorDetail` objects.
     - `DomainException` (`USER_NOT_FOUND`, `WALLET_NOT_FOUND`) &rarr; `404 Not Found`.
     - `DomainException` (`INSUFFICIENT_BALANCE`, `IDEMPOTENCY_CONFLICT`, `CONCURRENCY_CONFLICT`) &rarr; `409 Conflict`.
     - `DomainException` (domain constraint violation) &rarr; `400 Bad Request`.
     - Unhandled / System `Exception` &rarr; `500 Internal Server Error` with generic safe error message and error code `INTERNAL_SERVER_ERROR`.
   - **Automatic Correlation Enrichment**: `ApiResponse<T>` and `ErrorResponse` automatically resolve `CorrelationId` from ambient `CorrelationContext` and MVC result filter.

---

## Clean Architecture Structure

```
src/
├── PaymentLedger.Domain/          # Core domain models, entities, enums, domain exceptions
├── PaymentLedger.Application/     # CQRS Commands/Queries, MediatR handlers, FluentValidation, DTOs, interfaces
├── PaymentLedger.Infrastructure/  # EF Core, PostgreSQL persistence, repository implementations, migrations, seeding
├── PaymentLedger.Api/             # ASP.NET Core Web API controllers, middleware, filters, Serilog bootstrap
└── PaymentLedger.Shared/          # Standard response envelopes (ApiResponse, ErrorResponse), CorrelationContext, ErrorCodes

tests/
├── PaymentLedger.UnitTests/       # Domain logic, command handlers, validators, middleware, and mapper tests
├── PaymentLedger.IntegrationTests/# End-to-end API integration tests with test containers / PostgreSQL
└── PaymentLedger.ArchitectureTests/# NetArchTest rules enforcing strict layer dependencies
```

---

## Standard Response Envelopes

All API endpoints return standard JSON envelopes.

### Success Response (`ApiResponse<T>`)
```json
{
  "success": true,
  "message": "Wallet debited successfully.",
  "data": {
    "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "amount": 50.00,
    "currency": "USD",
    "status": "Completed"
  },
  "correlationId": "4f9d6c7b-3b7c-482f-bf2a-9e12613d9a18"
}
```

### Error Response (`ErrorResponse`)
```json
{
  "success": false,
  "message": "Insufficient balance to debit.",
  "data": null,
  "errors": [
    {
      "code": "INSUFFICIENT_BALANCE",
      "field": null,
      "message": "Insufficient balance to debit."
    }
  ],
  "correlationId": "4f9d6c7b-3b7c-482f-bf2a-9e12613d9a18"
}
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) with Docker Compose
- Optional (for local runs without Docker): PostgreSQL 16+

---

## Run Locally with .NET CLI

1. Ensure PostgreSQL is running and update the connection string in `src/PaymentLedger.Api/appsettings.Development.json`.
2. Run the API:

```powershell
dotnet run --project src/PaymentLedger.Api
```

On startup, the application automatically:
- Applies pending EF Core migrations.
- Seeds the system Treasury user and Treasury wallets (`USD`, `EUR`, `GBP`).

Default Local URLs:
- **HTTP**: `http://localhost:5275`
- **HTTPS**: `https://localhost:7155`
- **Swagger UI**: `http://localhost:5275/swagger`
- **Health Check**: `http://localhost:5275/health`

---

## Run Locally with Docker Compose

Start the full stack (API + PostgreSQL):

```powershell
docker compose up --build
```

Endpoints:
- **API Base**: `http://localhost:8080`
- **Swagger UI**: `http://localhost:8080/swagger`
- **Health Check**: `http://localhost:8080/health`

Stop the stack:

```powershell
docker compose down
```

Stop and clear persisted database volumes:

```powershell
docker compose down -v
```

---

## Database Migrations

Add a new migration:

```powershell
dotnet ef migrations add <MigrationName> --project src/PaymentLedger.Infrastructure --startup-project src/PaymentLedger.Api --output-dir Persistence/Migrations
```

Apply migrations manually:

```powershell
dotnet ef database update --project src/PaymentLedger.Infrastructure --startup-project src/PaymentLedger.Api
```

Remove the latest unapplied migration:

```powershell
dotnet ef migrations remove --project src/PaymentLedger.Infrastructure --startup-project src/PaymentLedger.Api
```

---

## Testing & Quality Gates

Run all test suites across the solution:

```powershell
dotnet test
```

### Run Specific Test Projects

- **Unit Tests** (118+ tests):
  ```powershell
  dotnet test tests/PaymentLedger.UnitTests/PaymentLedger.UnitTests.csproj
  ```

- **Architecture Tests** (Clean Architecture layer enforcement):
  ```powershell
  dotnet test tests/PaymentLedger.ArchitectureTests/PaymentLedger.ArchitectureTests.csproj
  ```

- **Integration Tests**:
  ```powershell
  dotnet test tests/PaymentLedger.IntegrationTests/PaymentLedger.IntegrationTests.csproj
  ```
