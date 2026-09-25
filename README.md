# Payment Ledger Service

Production-grade backend service for an idempotent digital wallet and double-entry ledger system built with **.NET 10**, **ASP.NET Core**, **PostgreSQL**, and **Entity Framework Core**.

---

## Architecture & Design

The service follows a streamlined, direct **3-tier architecture** designed for high maintainability, minimal indirection, and strict separation of concerns:

```
Controller (Api) ──> Business (Logic & Validations) ──> Repository (Database Operations)
```

### Architectural Principles

1. **Direct Layer Flow**:
   - **`PaymentLedger.Api` (Presentation)**: Exposes RESTful HTTP endpoints, manages middleware (correlation tracking, global exception handling), and coordinates dependency injection. Controllers directly invoke strongly-typed Business services (`IUserBusiness`, `IWalletBusiness`). Zero MediatR / CQRS pipeline overhead.
   - **`PaymentLedger.Business` (Business & Validation)**: Contains all business logic, invariant enforcement, request validations, edge-case handling, and financial orchestration (double-entry ledger creation, row-locking serialization, and idempotency guarantees).
   - **`PaymentLedger.Repository` (Data & Persistence)**: Encapsulates EF Core `AppDbContext`, database entities, entity configurations, repository pattern implementations, database migrations, and treasury seeding.
   - **`PaymentLedger.Shared` (Cross-Cutting Contracts)**: Shared standard response envelopes (`ApiResponse<T>`, `ErrorResponse`, `PagedResponse<T>`), application-wide error codes, and correlation tracking context.

2. **Consolidated Per-API Model Files**:
   To avoid file bloat and disconnected models across multiple directories, all Request DTOs, Response DTOs, View Models, and data transfer representations for a given API domain are organized into a single dedicated model file:
   - **`UserModels.cs`** (`PaymentLedger.Business/Models/UserModels.cs`):
     - Requests: `CreateUserRequest`, `UpdateUserRequest`, `GetUsersRequest`
     - Responses: `UserResponse`
     - DTOs: `UserDto`
   - **`WalletModels.cs`** (`PaymentLedger.Business/Models/WalletModels.cs`):
     - Requests: `CreateWalletRequest`, `CreditWalletRequest`, `DebitWalletRequest`
     - Responses: `WalletResponse`, `WalletBalanceResponse`, `TransactionResponse`
     - DTOs: `LedgerEntryDto`, `WalletDto`, `WalletBalanceDto`, `TransactionDto`

3. **Isolated Database Entities**:
   Database models are separated from API transfer models and reside cleanly in `PaymentLedger.Repository.Entities`:
   - `User`, `Wallet`, `Transaction`, `LedgerEntry`, `IdempotencyRequest`.

---

## Project Structure

```
PaymentLedgerService/
├── src/
│   ├── PaymentLedger.Api/              # ASP.NET Core Web API
│   │   ├── Controllers/               # UsersController, WalletsController
│   │   ├── Middleware/                # CorrelationIdMiddleware, GlobalExceptionHandlerMiddleware
│   │   ├── Filters/                   # StandardApiResponseFilter
│   │   ├── Extensions/                # Dependency injection & service configuration
│   │   └── Program.cs                 # Host builder, Serilog, middleware pipeline
│   │
│   ├── PaymentLedger.Business/         # Business Logic & Validation Layer
│   │   ├── Services/                  # UserBusinessService, WalletBusinessService
│   │   ├── Models/                    # Consolidated Per-API Models (UserModels.cs, WalletModels.cs)
│   │   ├── Common/                    # Centralized IdempotencyService, DateTimeProvider
│   │   ├── Exceptions/                # ValidationException, DomainException, ExceptionToResponseMapper
│   │   └── DependencyInjection.cs     # Business layer service registration
│   │
│   ├── PaymentLedger.Repository/       # Data Access & Persistence Layer
│   │   ├── Entities/                  # User, Wallet, Transaction, LedgerEntry, IdempotencyRequest
│   │   ├── Persistence/               # AppDbContext, Configurations (EF Core mapping), Migrations
│   │   ├── Repositories/              # UserRepository, WalletRepository, TransactionRepository, etc.
│   │   ├── Abstractions/              # IUserRepository, IWalletRepository, IUnitOfWork, etc.
│   │   ├── Seeding/                   # TreasuryWalletSeeder
│   │   └── DependencyInjection.cs     # Persistence & DbContext registration
│   │
│   └── PaymentLedger.Shared/           # Cross-Cutting Shared Libraries
│       ├── Common/                    # ApiResponse<T>, ErrorResponse, PagedResponse<T>, ErrorDetail
│       ├── Constants/                 # ErrorCodes
│       └── Correlation/               # CorrelationContext, ICorrelationIdProvider
│
└── tests/
    ├── PaymentLedger.UnitTests/         # 137 tests: Business services, idempotency, edge cases, validations
    ├── PaymentLedger.ArchitectureTests/ # 7 tests: Enforcing strict Api -> Business -> Repository boundaries
    └── PaymentLedger.IntegrationTests/  # 9 tests: Real PostgreSQL Testcontainers integration & concurrency tests
```

---

## Implemented Features & Endpoints

### 1. User Management
- `POST /api/v1/users`: Create user with unique email validation.
- `GET /api/v1/users`: Paginated user listing with search query support.
- `GET /api/v1/users/{id}`: Retrieve user details by ID.
- `PUT /api/v1/users/{id}`: Update user name and email.
- `DELETE /api/v1/users/{id}`: Delete user (prevents deletion if active wallets exist).

### 2. Wallet Lifecycle Management
- `POST /api/v1/wallets`: Create currency-specific wallet for active users.
- `GET /api/v1/wallets/{id}`: Retrieve wallet metadata and status.
- `GET /api/v1/wallets/{id}/balance`: Query real-time wallet balance.
- `GET /api/v1/wallets?userId={userId}`: Query all wallets belonging to a user.
- `PATCH /api/v1/wallets/{id}/block`: Block wallet from financial transactions.
- `PATCH /api/v1/wallets/{id}/unblock`: Unblock wallet.
- `PATCH /api/v1/wallets/{id}/close`: Close empty wallet (enforces zero-balance invariant).

### 3. Financial Transactions & Double-Entry Ledger
- `POST /api/v1/wallets/{id}/credit`: Credit funds to user wallet from System Treasury with balanced double-entry ledger entries.
- `POST /api/v1/wallets/{id}/debit`: Debit funds from user wallet to System Treasury using pessimistic row locking (`SELECT ... FOR UPDATE`) and real-time balance validation.

### 4. Refund & Auditability (Phase 4)
- `POST /api/v1/transactions/{id}/refund`: Process full or partial refunds for debit transactions with transaction reference linking, user wallet crediting, and balanced double-entry ledger entries (Treasury Debit, User Credit).
- `GET /api/v1/transactions/{id}`: Detailed transaction lookup including child double-entry ledger records and linked refund summaries.
- `GET /api/v1/transactions/{id}/verify`: Per-transaction consistency audit verifying `TotalDebits == TotalCredits == Transaction.Amount`.
- `GET /api/v1/wallets/{id}/transactions`: Server-side paginated transaction history with optional filtering by type, status, and date range.
- `GET /api/v1/wallets/{id}/verify`: Wallet balance reconciliation verifying `wallet.Balance == Sum(Credits) - Sum(Debits)`.
- `POST /api/v1/ledger/verify` / `GET /api/v1/ledger/verify`: System-wide double-entry consistency verification auditing all accounts and transactions.

### 5. Centralized Idempotency Engine (`IIdempotencyService`)
- **Header-Based Idempotency**: Evaluates `Idempotency-Key` (via header or request payload) and enforces SHA-256 payload fingerprinting.
- **Deterministic Business Keys**: Supports optional `ReferenceId` (e.g. order ID, external payment reference) constructing `"{action}:{walletId}:{referenceId}"` to guarantee zero double-spend even if client UUID generation fails.
- **Anti-Double-Click Guard**: Memory-cached request fingerprinting (`SHA256(action:walletId:amount)`) rejecting rapid duplicate clicks within a 3-second throttle window with HTTP 429 (`DUPLICATE_REQUEST`).
- **Conflict Detection**: Throws `IDEMPOTENCY_CONFLICT` (409) if an idempotency key is reused with a different payload, and `CONCURRENCY_CONFLICT` (409) if an identical mutation is already being processed concurrently.

### 6. Observability & Global Exception Handling
- **Serilog Logging**: Structured console logging and daily rolling file logs (`logs/payment-ledger-.log`).
- **Correlation Tracking**: `CorrelationIdMiddleware` extracts or generates `X-Correlation-ID`, populating Serilog `LogContext`, response headers, and ambient `CorrelationContext.Current`.
- **Standardized Response Envelopes**: All client and server errors are trapped by `GlobalExceptionHandlerMiddleware` and converted into standardized `ErrorResponse` envelopes without stack trace leakage:
  - `ValidationException` &rarr; `400 Bad Request` with field-level `ErrorDetail` objects.
  - `DomainException` (`USER_NOT_FOUND`, `WALLET_NOT_FOUND`, `TRANSACTION_NOT_FOUND`) &rarr; `404 Not Found`.
  - `DomainException` (`INSUFFICIENT_BALANCE`, `IDEMPOTENCY_CONFLICT`, `CONCURRENCY_CONFLICT`) &rarr; `409 Conflict`.
  - `DomainException` (`TRANSACTION_NOT_REFUNDABLE`, `REFUND_AMOUNT_EXCEEDED`, `CURRENCY_MISMATCH`, domain constraint violation) &rarr; `400 Bad Request`.
  - Unhandled / System `Exception` &rarr; `500 Internal Server Error` with generic safe error message and error code `INTERNAL_SERVER_ERROR`.

---

## Standard Response Envelopes

All API endpoints return consistent, standardized JSON envelopes.

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

Migrations and entity configurations reside inside `PaymentLedger.Repository`. Execute migration commands targeting this project:

Add a new migration:

```powershell
dotnet ef migrations add <MigrationName> --project src/PaymentLedger.Repository --startup-project src/PaymentLedger.Api --output-dir Persistence/Migrations
```

Apply migrations manually:

```powershell
dotnet ef database update --project src/PaymentLedger.Repository --startup-project src/PaymentLedger.Api
```

Remove the latest unapplied migration:

```powershell
dotnet ef migrations remove --project src/PaymentLedger.Repository --startup-project src/PaymentLedger.Api
```

---

## Concurrency & Data Integrity Guarantees

1. **Pessimistic Row Locking (`SELECT ... FOR UPDATE`)**:
   - Financial mutations execute inside an atomic database transaction using row-level locking on the target wallet.
   - Under heavy concurrent debit contention against a low-balance wallet, transactions are strictly serialized:
     - Requests exceeding remaining balance are rejected with HTTP 409 (`INSUFFICIENT_BALANCE`).
     - The wallet balance is guaranteed to never drop below zero.
     - Double-entry ledger entries remain fully balanced.

2. **Thread-Safe Anti-Double-Click Guard**:
   - Centralized memory-throttled anti-double-click protection synchronizes incoming concurrent requests, ensuring only a single thread can execute an identical unkeyed mutation within the 3-second throttle window, returning HTTP 429 (`DUPLICATE_REQUEST`) to parallel duplicates.

3. **Concurrent Idempotency Deduplication**:
   - Multiple parallel requests carrying the same `Idempotency-Key` serialize safely: the initial request executes the transaction while concurrent duplicates either await/receive the cached response or trigger conflict/duplicate protections, guaranteeing zero double-debit.

---

## Testing & Quality Gates

Run all test suites across the solution:

```powershell
dotnet test PaymentLedgerService.sln
```

### Test Suites

- **Unit & Concurrency Tests** (`PaymentLedger.UnitTests` - 179 passed):
  - Invariants, money precision, wallet status state transitions, and ledger double-entry balancing.
  - Direct business service logic (`UserBusinessService`, `WalletBusinessService`, `TransactionBusinessService`) and edge case validations.
  - Multi-threaded concurrency simulation tests verifying `SELECT ... FOR UPDATE` row serialization, zero-negative balance invariants, and idempotency deduplication under parallel execution.
  - Refund lifecycle, partial and full refunds, over-refund guardrails, and transaction reference tracking.
  - Wallet balance reconciliation and double-entry consistency audits.
  - API controller endpoints, pagination, and error response contracts.
  - `CorrelationIdMiddleware` context lifecycle and whitespace sanitization.
  - `GlobalExceptionHandlerMiddleware` error code mapping (400, 404, 409, 429, 500) and response streaming safety.
  - `IdempotencyService` thread synchronization, hashing, and anti-double-click multi-thread isolation.
  ```powershell
  dotnet test tests/PaymentLedger.UnitTests/PaymentLedger.UnitTests.csproj
  ```

- **Architecture Tests** (`PaymentLedger.ArchitectureTests` - 7 passed):
  - NetArchTest rules enforcing clean 3-tier layer isolation (`Controller -> Business -> Repository`):
    - `Api` only depends on `Business` and `Shared` (no direct reference to `Repository`).
    - `Business` only depends on `Repository` and `Shared` (no reference to `Api`).
    - `Repository` only depends on `Shared` (no reference to `Api` or `Business`).
    - `Controllers` do not reference Entity Framework Core or database repositories directly.
    - `Controllers` only depend on `Business` services and interfaces (`IUserBusiness`, `IWalletBusiness`).
    - All middleware classes reside strictly in `PaymentLedger.Api.Middleware`.
    - `IIdempotencyService` implementation resides in `PaymentLedger.Business.Common`.
  ```powershell
  dotnet test tests/PaymentLedger.ArchitectureTests/PaymentLedger.ArchitectureTests.csproj
  ```

- **Integration Tests** (`PaymentLedger.IntegrationTests` - 9 tests):
  - Real PostgreSQL container tests via Testcontainers (`[DockerFact]`).
  - True database transaction concurrency tests verifying `SELECT ... FOR UPDATE` row locking and non-negative balance enforcement under concurrent HTTP debits.
  - Idempotency deduplication integration tests.
  - Database schema migration, idempotent treasury seeding, and health checks.
  ```powershell
  dotnet test tests/PaymentLedger.IntegrationTests/PaymentLedger.IntegrationTests.csproj
  ```
