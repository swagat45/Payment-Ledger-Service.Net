# AGENTS.md — Payment Ledger Service

This guide provides instructions, architectural rules, test commands, and coding conventions for AI agents and developers working on the **Payment Ledger Service** codebase.

---

## 1. Project Overview & Tech Stack

**Payment Ledger Service** is a production-grade, idempotent digital wallet and double-entry financial ledger system with a full-stack architecture:

- **Backend**: .NET 10, ASP.NET Core Web API, C# 13
- **ORM & Database**: Entity Framework Core 10, PostgreSQL 17 (Npgsql EF Core Provider)
- **Frontend**: Angular 17.3, TypeScript 5.4, Tailwind CSS 3.4 (`PaymentLedger.UI/`)
- **Logging & Diagnostics**: Serilog (Console + rolling file logs in `logs/payment-ledger-.log`), ASP.NET Core Health Checks
- **Testing**: xUnit 2.9, FluentAssertions 8.3, Respawn 6.2, Testcontainers 4.12 (PostgreSQL container), NetArchTest.Rules 1.3.2
- **Containerization**: Docker, Docker Compose, Nginx (for UI reverse proxy)

---

## 2. Solution Structure & Layering

The backend enforces a streamlined, direct **3-tier architecture** with zero MediatR/CQRS indirection:

```
PaymentLedgerService/
├── src/
│   ├── PaymentLedger.Api/              # Presentation: Controllers, Middleware, Filters, Program.cs
│   │   ├── Controllers/               # UsersController, WalletsController, TransactionsController, LedgerController
│   │   ├── Middleware/                # CorrelationIdMiddleware, GlobalExceptionHandlerMiddleware
│   │   ├── Filters/                   # ApiResponseCorrelationFilter
│   │   ├── Extensions/                # ServiceCollectionExtensions (DI registration)
│   │   └── Program.cs                 # App bootstrap, Serilog config, pipeline configuration
│   │
│   ├── PaymentLedger.Business/         # Core Domain Logic, Validations, Financial Orchestration
│   │   ├── Services/                  # UserBusinessService, WalletBusinessService, TransactionBusinessService
│   │   ├── Models/                    # Consolidated Per-API Models (UserModels.cs, WalletModels.cs, TransactionModels.cs)
│   │   ├── Common/                    # IdempotencyService, SystemDateTimeProvider
│   │   ├── Exceptions/                # ValidationException, ExceptionToResponseMapper
│   │   └── DependencyInjection.cs     # AddBusiness() DI extension
│   │
│   ├── PaymentLedger.Repository/       # Persistence & Database Access
│   │   ├── Entities/                  # User, Wallet, Transaction, LedgerEntry, IdempotencyRequest, OutboxMessage
│   │   ├── Enums/                     # WalletStatus, TransactionType, TransactionStatus, LedgerEntryType, UserStatus
│   │   ├── ValueObjects/              # Money
│   │   ├── Persistence/               # AppDbContext, Configurations/ (EF Core fluent mappings), Migrations/
│   │   ├── Repositories/              # UserRepository, WalletRepository, TransactionRepository, LedgerRepository, IdempotencyRepository, OutboxRepository
│   │   ├── Abstractions/              # IUnitOfWork, IAppDbContext, repository interfaces, IDateTimeProvider
│   │   ├── Seeding/                   # TreasuryWalletSeeder (seeds Treasury user & multi-currency wallets)
│   │   └── DependencyInjection.cs     # AddRepository() DI extension
│   │
│   └── PaymentLedger.Shared/           # Cross-Cutting Contracts (Independent of all other solution projects)
│       ├── Responses/                 # ApiResponse<T>, ErrorResponse, PagedResponse<T>, IApiResponse
│       ├── Constants/                 # ErrorCodes
│       └── Context/                   # CorrelationContext, ICorrelationIdProvider
│
├── tests/
│   ├── PaymentLedger.UnitTests/         # 204 tests: Unit, domain invariants, business edge cases, concurrency simulations
│   ├── PaymentLedger.ArchitectureTests/ # 7 tests: NetArchTest enforcing strict layer boundaries
│   └── PaymentLedger.IntegrationTests/  # 9 tests: Real PostgreSQL Testcontainers integration & concurrency tests
│
└── PaymentLedger.UI/                   # Angular 17.3 frontend with Tailwind CSS
    └── src/app/
        ├── core/                      # Interceptors, models, services (api.service, user.service, wallet.service)
        ├── features/                  # dashboard, users, wallets (wallet-details, burst/concurrency tester)
        └── shared/                    # Layout and reusable UI components
```

---

## 3. Essential Commands

### 3.1 Building the Solution

```powershell
# Restore dependencies
dotnet restore PaymentLedgerService.sln

# Build entire solution in Debug mode
dotnet build PaymentLedgerService.sln

# Build with Release configuration
dotnet build PaymentLedgerService.sln -c Release
```

### 3.2 Running the Application

#### Option A: Docker Compose (Full Stack - Recommended)
Runs PostgreSQL 17, Web API (port 8080), and Angular UI (port 4200):
```powershell
# Start all services with build
docker compose up --build

# Run in background (detached)
docker compose up -d --build

# Stop all services
docker compose down

# Stop and wipe database volume
docker compose down -v
```

Endpoints:
- **Web API**: `http://localhost:8080`
- **Swagger UI**: `http://localhost:8080/swagger`
- **Health Check**: `http://localhost:8080/health`
- **Angular UI**: `http://localhost:4200`
- **Kafka Web UI**: `http://localhost:8085`
- **Kafka Broker (Host PLAINTEXT)**: `localhost:9092`
- **RabbitMQ (Optional - `--profile rabbitmq`)**: AMQP `localhost:5672`, Management UI `http://localhost:15672`

#### Option B: Local .NET Run (Backend Only)
Requires PostgreSQL running locally (configure connection in `src/PaymentLedger.Api/appsettings.Development.json`):
```powershell
dotnet run --project src/PaymentLedger.Api
```

Endpoints:
- **HTTP**: `http://localhost:5275`
- **HTTPS**: `https://localhost:7155`
- **Swagger UI**: `http://localhost:5275/swagger`
- **Health Check**: `http://localhost:5275/health`

#### Option C: Angular UI Local Run
Inside `PaymentLedger.UI/`:
```powershell
# Install frontend dependencies
npm install

# Start Angular development server (http://localhost:4200)
npm start

# Build frontend production bundle (outputs to dist/payment-ledger-ui)
npm run build
```

---

## 4. Testing & Verification

All automated tests must pass before completing any changes.

```powershell
# Run the entire solution test suite (Unit, Architecture, and Integration tests)
dotnet test PaymentLedgerService.sln
```

### 4.1 Running Individual Test Projects

```powershell
# Unit & Domain Tests (204 tests - fast, in-memory)
dotnet test tests/PaymentLedger.UnitTests/PaymentLedger.UnitTests.csproj

# Architecture Boundary Tests (7 tests - NetArchTest)
dotnet test tests/PaymentLedger.ArchitectureTests/PaymentLedger.ArchitectureTests.csproj

# Integration & Concurrency Tests (9 tests - requires Docker daemon for PostgreSQL Testcontainer)
dotnet test tests/PaymentLedger.IntegrationTests/PaymentLedger.IntegrationTests.csproj
```

### 4.2 Filtering Specific Tests

```powershell
# Run a specific unit test by name
dotnet test tests/PaymentLedger.UnitTests/PaymentLedger.UnitTests.csproj --filter "FullyQualifiedName~DebitWalletAsync"

# Run integration concurrency tests
dotnet test tests/PaymentLedger.IntegrationTests/PaymentLedger.IntegrationTests.csproj --filter "FullyQualifiedName~WalletConcurrencyTests"
```

---

## 5. Database Migrations (EF Core)

Database migrations and entity configurations reside inside `PaymentLedger.Repository`. Always specify both `--project` and `--startup-project`:

```powershell
# Add a new migration
dotnet ef migrations add <MigrationName> `
  --project src/PaymentLedger.Repository `
  --startup-project src/PaymentLedger.Api `
  --output-dir Persistence/Migrations

# Apply pending migrations to the configured database
dotnet ef database update `
  --project src/PaymentLedger.Repository `
  --startup-project src/PaymentLedger.Api

# Remove the latest unapplied migration
dotnet ef migrations remove `
  --project src/PaymentLedger.Repository `
  --startup-project src/PaymentLedger.Api

# Generate SQL script of migrations
dotnet ef migrations script `
  --project src/PaymentLedger.Repository `
  --startup-project src/PaymentLedger.Api `
  --output migrations.sql
```

> [!NOTE]
> On startup, `Program.cs` automatically executes `dbContext.Database.MigrateAsync()` and runs `TreasuryWalletSeeder.SeedAsync()` to ensure migrations and system treasury accounts are created idempotently.

---

## 6. Architecture & Coding Rules

Strict adherence to these rules is required to maintain architectural integrity and keep `LayerDependencyTests` passing:

### 6.1 Layer Dependency Hierarchy
- **`PaymentLedger.Api`** may reference `PaymentLedger.Business` and `PaymentLedger.Shared`. It must **NEVER** reference `PaymentLedger.Repository` (except in `Program.cs` for DI registration `AddRepository()` and initial migration).
- **`PaymentLedger.Business`** may reference `PaymentLedger.Repository` and `PaymentLedger.Shared`. It must **NEVER** reference `PaymentLedger.Api`.
- **`PaymentLedger.Repository`** may reference `PaymentLedger.Shared`. It must **NEVER** reference `PaymentLedger.Business` or `PaymentLedger.Api`.
- **`PaymentLedger.Shared`** is completely decoupled and must **NEVER** reference `Api`, `Business`, or `Repository`.

### 6.2 Controllers & Presentation Layer
- **No Direct DbContext or Repositories**: Controllers must **never** inject `AppDbContext` or repository interfaces. Controllers interact strictly with business service interfaces (`IUserBusinessService`, `IWalletBusinessService`, `ITransactionBusinessService`).
- **Standardized Response Envelopes**: All controller actions return `ApiResponse<T>`, `PagedResponse<T>`, or rely on `GlobalExceptionHandlerMiddleware` to format `ErrorResponse`.
- **Consolidated Per-API Model Files**: Request/Response DTOs belong in consolidated domain files under `PaymentLedger.Business/Models/` (e.g. `UserModels.cs`, `WalletModels.cs`, `TransactionModels.cs`). Avoid creating individual file-per-DTO bloat.
- **Middleware Class Placement**: All middleware classes must reside strictly in the `PaymentLedger.Api.Middleware` namespace.
- **Middleware DI Lifetime**: Conventional middlewares instantiated by ASP.NET Core are singletons. **Never inject scoped services into middleware constructors**. Inject them into `InvokeAsync(HttpContext, IScopedService)` or retrieve them via `context.RequestServices`. Use `[ActivatorUtilitiesConstructor]` if multiple constructors exist.

### 6.3 Financial Invariants & Double-Entry Ledger
- **Strict Double-Entry Bookkeeping**: Every financial transaction must create at least two balanced `LedgerEntry` records (Debit and Credit) such that:
  $$\sum \text{Debits} == \sum \text{Credits} == \text{Transaction.Amount}$$
- **System Treasury Accounts**:
  - **Credit Mutation**: Source is Treasury Wallet; Destination is User Wallet. Entries: `Treasury Debit`, `User Credit`.
  - **Debit Mutation**: Source is User Wallet; Destination is Treasury Wallet. Entries: `User Debit`, `Treasury Credit`.
  - **Refund Mutation**: Source is Treasury Wallet; Destination is User Wallet. Entries: `Treasury Debit`, `User Credit`. Linked via `ReferenceTransactionId`.
- **Pessimistic Row-Level Locking (`SELECT ... FOR UPDATE`)**:
  - All balance-altering operations (debit, credit, refund) must acquire a database row lock using `_walletRepository.GetByIdForUpdateAsync(walletId, ct)` inside an atomic database transaction (`_unitOfWork.BeginTransactionAsync(ct)`).
  - Balance updates and double-entry ledger additions must be saved and committed atomically.
- **Zero-Negative Balance Invariant**:
  - A wallet balance must **never** drop below `0.00`.
  - Checked at the domain model level (`wallet.Debit(amount, ...)` throws `DomainException` if balance insufficient).
  - Enforced at the database level via PostgreSQL check constraint `ck_wallets_balance_non_negative` (`"balance" >= 0`).
- **Post-Lock Idempotency Verification**:
  - Because concurrent duplicate requests with the same `Idempotency-Key` may wait on the row lock while the first request commits, services **must re-check idempotency immediately after acquiring the row lock** (`postLockCheck`). If already processed, immediately roll back the transaction and return the cached `TransactionResponse`.
- **Money Precision**:
  - Always store monetary amounts using `decimal` with precision `numeric(18,2)`. Always round to 2 decimal places (`decimal.Round(amount, 2)`).

### 6.4 Idempotency & Concurrency Protections
- **Idempotency Header & Payload Hash**:
  - Supports `Idempotency-Key` header or body field.
  - Automatically computes SHA-256 hash of payload (`Amount`, `Currency`, `Description`, `Metadata`) to prevent payload tampering.
  - Reusing a key with a different payload throws `DomainException` with `ErrorCodes.IdempotencyConflict` (HTTP 409).
- **Anti-Double-Click Guard**:
  - Unkeyed mutation requests trigger a 3-second memory-cached throttle window (`CheckAntiDoubleClick`). Rapid duplicate clicks within 3 seconds are rejected with `ErrorCodes.DuplicateRequest` (HTTP 429).
- **Deterministic Business Keys**:
  - When clients supply `ReferenceId` (e.g. external order ID), the idempotency service constructs `"{action}:{walletId}:{referenceId}"` to avoid duplicate charges even without UUID keys.

### 6.5 Transaction & Database Resource Management
- **Safe Transaction Disposal in `AppDbContext`**:
  - When committing or rolling back transactions in EF Core, always capture `Database.CurrentTransaction` into a local variable and use `await using (currentTransaction)`:
    ```csharp
    var currentTransaction = Database.CurrentTransaction;
    if (currentTransaction != null)
    {
        await using (currentTransaction)
        {
            await currentTransaction.RollbackAsync(cancellationToken);
        }
    }
    ```
  - *Never* call `Database.CurrentTransaction.DisposeAsync()` after `CommitAsync`/`RollbackAsync`, as EF Core nulls the property upon completion, causing a `NullReferenceException`.

### 6.6 Logging & Diagnostics
- **Logger Abstractions**:
  - Classes in `PaymentLedger.Business` and `PaymentLedger.Repository` must use `Microsoft.Extensions.Logging.ILogger<T>`. They must **NEVER** reference or depend on `Serilog` directly.
- **Correlation ID Tracking**:
  - `CorrelationIdMiddleware` extracts or generates `X-Correlation-ID`.
  - Ambient correlation is tracked via `CorrelationContext.Current`, `ICorrelationIdProvider`, and Serilog's `LogContext`.
  - Always preserve and include `correlationId` in response envelopes.

### 6.7 C# & Code Style Guidelines
- **C# 13 / .NET 10 Idioms**:
  - Use file-scoped namespaces (`namespace PaymentLedger.Business.Services;`).
  - Use records for immutable DTOs and response envelopes.
  - Use collection expressions (`[]` instead of `new List<T>()` or `Array.Empty<T>()`).
  - Use target-typed `new()` where the type is obvious.
  - Enable nullable reference types (`<Nullable>enable</Nullable>`).
- **Cancellation Tokens**:
  - Always accept and forward `CancellationToken ct = default` across all async controller actions, business service methods, and repository operations.
- **Dependency Injection**:
  - Every project defines its own `DependencyInjection.cs` extension method (`AddApiServices`, `AddBusiness`, `AddRepository`).

---

## 7. Error Codes Reference

All known error codes are defined in `PaymentLedger.Shared.Constants.ErrorCodes`:

| Error Code | HTTP Status | Description |
|---|---|---|
| `VALIDATION_FAILED` | 400 Bad Request | Request model validation failed (missing or invalid fields) |
| `CURRENCY_MISMATCH` | 400 Bad Request | Target wallet currency does not match transaction currency |
| `WALLET_NOT_ACTIVE` | 400 Bad Request | Target or treasury wallet is not in Active status |
| `TRANSACTION_NOT_REFUNDABLE` | 400 Bad Request | Transaction cannot be refunded (e.g. not a debit, already refunded) |
| `REFUND_AMOUNT_EXCEEDED` | 400 Bad Request | Requested refund amount exceeds remaining refundable amount |
| `USER_NOT_FOUND` | 404 Not Found | Specified user ID does not exist |
| `WALLET_NOT_FOUND` | 404 Not Found | Specified wallet ID does not exist |
| `TRANSACTION_NOT_FOUND` | 404 Not Found | Specified transaction ID does not exist |
| `INSUFFICIENT_BALANCE` | 409 Conflict | Wallet does not have enough balance for debit operation |
| `IDEMPOTENCY_CONFLICT` | 409 Conflict | Idempotency key reused with a different request payload |
| `CONCURRENCY_CONFLICT` | 409 Conflict | Mutation with this idempotency key is currently processing |
| `USER_ALREADY_EXISTS` | 409 Conflict | User with this email already exists |
| `DUPLICATE_REQUEST` | 429 Too Many Requests | Unkeyed request throttled by anti-double-click guard |
| `INTERNAL_SERVER_ERROR` | 500 Internal Server Error | Unhandled server exception (stack trace redacted) |
