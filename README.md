# 🏦 Payment Ledger Service

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![PostgreSQL 17](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Redis 7](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![Apache Kafka](https://img.shields.io/badge/Apache_Kafka-KRaft_Mode-231F20?logo=apachekafka&logoColor=white)](https://kafka.apache.org/)
[![Angular 17](https://img.shields.io/badge/Angular-17.3-DD0031?logo=angular&logoColor=white)](https://angular.io/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-3.4-38B2AC?logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)
[![Tests Passing](https://img.shields.io/badge/Tests-232_Passed-success?logo=checkmarx&logoColor=white)](tests/)
[![Architecture Boundary](https://img.shields.io/badge/NetArchTest-100%25_Compliant-blueviolet)](tests/PaymentLedger.ArchitectureTests/)

A production-grade, highly concurrent digital wallet and double-entry financial ledger service designed for zero financial discrepancies, sub-millisecond idempotency resolution, and guaranteed event streaming across distributed microservices.

> 📖 **Architecture Diagrams & Deep-Dive**: Full Mermaid sequence diagrams, High-Level Design (HLD), Low-Level Design (LLD), and Database ER schemas are documented in **[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)**.
> 
> 🌐 **API Request Samples**: Comprehensive cURL requests and payload definitions are documented in **[`docs/API_SAMPLES.md`](docs/API_SAMPLES.md)**.

---

## 📑 Table of Contents

- [1. Executive System Overview](#1-executive-system-overview)
- [2. Core Engineering Guarantees](#2-core-engineering-guarantees)
- [3. Architecture & Ecosystem Overview](#3-architecture--ecosystem-overview)
- [4. Solution Structure & Layer Boundaries](#4-solution-structure--layer-boundaries)
- [5. Quick Start & Deployment](#5-quick-start--deployment)
- [6. API Reference Matrix](#6-api-reference-matrix)
- [7. Quality Gates & Test Verification](#7-quality-gates--test-verification)
- [8. Error Catalog & Resilience](#8-error-catalog--resilience)

---

## 1. Executive System Overview

| Capability | Mechanism | Production Guarantee |
|---|---|---|
| **Bookkeeping Core** | Pure Double-Entry Bookkeeping | $\sum \text{Debits} \equiv \sum \text{Credits} \equiv \text{Txn Amount}$. Balanced ledger entry pairs on every mutation. |
| **Concurrency Guard** | PostgreSQL `SELECT ... FOR UPDATE` | Strict row serialization during balance changes. Zero negative balance under high contention. |
| **Idempotency Engine** | Hybrid Redis (`SETNX`) + PostgreSQL Fingerprinting | Fast-path `<1ms` response replay; SHA-256 payload tampering detection (`HTTP 409`). |
| **Dual-Write Safety** | Transactional Outbox Pattern | Domain events staged atomically within the mutation transaction. Zero phantom events. |
| **Outbox Polling** | Background Worker + `SKIP LOCKED` | Parallel worker nodes claim disjoint batches without lock contention or deadlocks. |
| **Message Broker** | Apache Kafka (KRaft mode) | Partition-ordered delivery keyed by `ReferenceId` for downstream ERP reconciliation. |
| **Downstream Consumers**| Inventory API (`EVA.StockManagement`) | Automated Purchase Order (`PO`) closing and Goods Received Note (`GRN`) accounts payable matching. |
| **Architecture Rules** | Automated NetArchTest Validation | 3-tier separation strictly verified at test-time. Zero repository leakage into presentation. |

---

## 2. Core Engineering Guarantees

### 1. Mathematical Double-Entry Balancing
Every balance-altering transaction generates at least two immutable `LedgerEntry` records (Debit and Credit) such that:
$$\sum \text{Debits} \equiv \sum \text{Credits} \equiv \text{Transaction.Amount}$$
- **Credit Flow**: System Treasury Debit $\rightarrow$ User Wallet Credit.
- **Debit Flow**: User Wallet Debit $\rightarrow$ System Treasury Credit.
- **Refund Flow**: System Treasury Debit $\rightarrow$ User Wallet Credit (traceably linked via `ReferenceTransactionId`).

### 2. Pessimistic Row-Level Locking (`SELECT ... FOR UPDATE`)
All balance-altering operations (debit, credit, refund) acquire an exclusive database row lock inside an atomic transaction (`_unitOfWork.BeginTransactionAsync`). Under heavy concurrent contention, mutations serialize deterministically, preventing race condition overdrafts.

### 3. Engine-Level Zero-Negative Balance Guard
In addition to domain-layer validation, PostgreSQL enforces an engine-level check constraint:
```sql
CONSTRAINT ck_wallets_balance_non_negative CHECK (balance >= 0)
```
Even if application-level checks were bypassed, the storage engine rejects any operation that would result in a negative balance.

### 4. Distributed Multi-Tier Idempotency
- **Redis Fast-Path**: Keyed transactions are cached in Redis (`TTL 24h`) to serve replays in `<1ms`.
- **Anti-Double-Click Guard**: Unkeyed mutations trigger a 3-second memory/Redis throttle (`SETNX`), rejecting rapid duplicate clicks with `HTTP 429 Too Many Requests`.
- **SHA-256 Payload Fingerprinting**: Computes a cryptographic digest of `Amount`, `Currency`, `Description`, and `Metadata`. Reusing an idempotency key with altered parameters throws `HTTP 409 Idempotency Conflict`.
- **Post-Lock Idempotency Verification**: Re-checks the idempotency record immediately after acquiring the database lock to intercept duplicate requests that queued while the first request was committing.

---

## 3. Architecture & Ecosystem Overview

```
[ Clients / B2B Services ] ──> [ Nginx (:4200) ] ──> [ Web API (:8080) ]
                                                              │
                                            ┌─────────────────┴─────────────────┐
                                            ▼                                   ▼
                                  [ Redis 7 Cache ]                  [ PostgreSQL 17 ACID ]
                                (Fast-Path Idempotency)             (Row Locks & Double-Entry)
                                                                                │
                                                                                ▼
                                                                     [ outbox_messages ]
                                                                                │
                                                            (SELECT ... FOR UPDATE SKIP LOCKED)
                                                                                │
                                                                                ▼
                                                                  [ OutboxProcessorWorker ]
                                                                                │
                                                                                ▼
                                                                 [ Apache Kafka Topic ]
                                                                (payment-ledger.events)
                                                                                │
                                                                                ▼
                                                                     [ Inventory API ]
                                                               (PO & GRN / AP Reconciliation)
```

> Detailed HLD, LLD sequence pipelines, and database ER models: **[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)**.

---

## 4. Solution Structure & Layer Boundaries

```
PaymentLedgerService/
├── docs/                               # Comprehensive Architecture & API Documentation
│   ├── ARCHITECTURE.md                 # Full HLD, LLD Sequence, and Database ERD Diagrams
│   └── API_SAMPLES.md                  # Comprehensive cURL requests and payload definitions
│
├── src/
│   ├── PaymentLedger.Api/              # Presentation Tier (Controllers, Middlewares, Hosted Workers)
│   ├── PaymentLedger.Business/         # Core Domain Tier (Orchestration, Invariants, Kafka Publisher)
│   ├── PaymentLedger.Repository/       # Data Access Tier (EF Core 10, Row Locking, Outbox, Seeding)
│   └── PaymentLedger.Shared/           # Cross-Cutting Tier (Contracts, Options, ErrorCodes, Envelopes)
│
├── tests/
│   ├── PaymentLedger.UnitTests/         # 216 tests: Invariants, idempotency, Kafka publisher, outbox
│   ├── PaymentLedger.ArchitectureTests/ # 7 tests: NetArchTest enforcing strict layer boundaries
│   └── PaymentLedger.IntegrationTests/  # 9 tests: Real PostgreSQL Testcontainers concurrency tests
│
└── PaymentLedger.UI/                   # Angular 17.3 + Tailwind CSS Dashboard SPA
```

> **Strict Dependency Rules**:
> `Api` $\rightarrow$ references `Business` and `Shared` (no direct `Repository` references).
> `Business` $\rightarrow$ references `Repository` and `Shared` (no `Api` references).
> `Repository` $\rightarrow$ references `Shared` (no `Business` or `Api` references).
> `Shared` $\rightarrow$ completely decoupled from all solution layers.

---

## 5. Quick Start & Deployment

### Option A: Docker Compose (Full Stack - Recommended)

Runs PostgreSQL 17, Redis 7, Apache Kafka (KRaft), Kafka UI, Web API (port 8080), and Angular UI (port 4200):

```powershell
# Build and launch all services in background
docker compose up -d --build

# View container logs
docker compose logs -f

# Stop all services
docker compose down
```

| Service | Host Port | Target URL | Description |
|---|---|---|---|
| **Angular UI** | `4200` | `http://localhost:4200` | Web dashboard & concurrency burst tester |
| **Web API Gateway** | `8080` | `http://localhost:8080` | ASP.NET Core REST API |
| **Swagger UI** | `8080` | `http://localhost:8080/swagger` | Interactive OpenAPI documentation |
| **Kafka Web UI** | `8085` | `http://localhost:8085` | Kafka topic inspector & consumer group monitor |
| **Kafka Broker** | `9092` | `localhost:9092` | PLAINTEXT external host listener |
| **PostgreSQL** | `5432` | `localhost:5432` | Primary ledger database |
| **Redis** | `6379` | `localhost:6379` | Distributed lock & fast-path cache |
| **RabbitMQ (Optional)** | `15672` | `http://localhost:15672` | Active via `docker compose --profile rabbitmq up` |

### Option B: Local .NET CLI Backend Run

```powershell
# Run backend against a local PostgreSQL instance
dotnet run --project src/PaymentLedger.Api
```

---

## 6. API Reference Matrix

All mutating endpoints enforce optional or mandatory `Idempotency-Key` headers and correlation tracking via `X-Correlation-ID`.

| Category | Method | Endpoint | Description | Idempotent |
|---|---|---|---|:---:|
| **Auth** | `POST` | `/api/v1/auth/token` | Issues signed HMAC-SHA256 JWT tokens with custom scopes | N/A |
| **Users** | `POST` | `/api/v1/users` | Creates user identity with unique email validation | No |
| | `GET` | `/api/v1/users` | Paginated user search query with filtering | No |
| | `GET` | `/api/v1/users/{id}` | Retrieves user profile metadata | No |
| **Wallets** | `POST` | `/api/v1/wallets` | Opens currency-specific wallet for an active user | Yes |
| | `GET` | `/api/v1/wallets/{id}` | Queries wallet balance and active lifecycle state | No |
| | `PATCH` | `/api/v1/wallets/{id}/block` | Blocks wallet from accepting financial mutations | Yes |
| | `PATCH` | `/api/v1/wallets/{id}/close` | Closes empty wallet (enforces balance == 0 invariant) | Yes |
| **Mutations** | `POST` | `/api/v1/wallets/{id}/credit` | Credits funds from Treasury with balanced double entries | **Yes** |
| | `POST` | `/api/v1/wallets/{id}/debit` | Debits wallet with `SELECT ... FOR UPDATE` row locking | **Yes** |
| | `GET` | `/api/v1/wallets/{id}/transactions` | Server-side paginated transaction ledger history | No |
| **Refunds** | `POST` | `/api/v1/transactions/{id}/refund` | Processes full/partial refund linked to original debit | **Yes** |
| **Audit** | `GET` | `/api/v1/ledger/verify` | System-wide mathematical reconciliation audit | No |
| | `GET` | `/api/v1/wallets/{id}/verify` | Cryptographic balance vs ledger entry verification | No |

### Sample Mutation Request

```bash
curl -X POST "http://localhost:8080/api/v1/wallets/{walletId}/debit" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: PO-2026-001-PAYMENT" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{
    "amount": 1500.00,
    "currency": "USD",
    "referenceId": "PO-2026-001",
    "description": "Advance payment for procurement order"
  }'
```

---

## 7. Quality Gates & Test Verification

The test suite enforces full verification across 3 dedicated test projects:

```powershell
# Run the complete test suite across all projects
dotnet test PaymentLedgerService.sln
```

```
Total Tests: 232  |  Passed: 232  |  Failed: 0  |  Skipped: 0
```

| Test Project | Tests | Scope & Coverage | Execution Time |
|---|:---:|---|:---:|
| **`PaymentLedger.UnitTests`** | **216** | Double-entry balancing, pessimistic locking, Redis resilience, SHA-256 fingerprinting, Kafka publisher, refund limits, outbox dispatcher | `~450ms` |
| **`PaymentLedger.ArchitectureTests`** | **7** | NetArchTest rules: layer independence, zero repository access from controllers, middleware isolation | `~300ms` |
| **`PaymentLedger.IntegrationTests`** | **9** | Real PostgreSQL Testcontainers concurrency tests: 10 parallel debits against low balance, duplicate key replay, treasury seeding | `~12s` |

---

## 8. Error Catalog & Resilience

All uncaught exceptions are trapped by [`GlobalExceptionHandlerMiddleware`](src/PaymentLedger.Api/Middleware/GlobalExceptionHandlerMiddleware.cs) and sanitized into consistent, traceable error envelopes:

```json
{
  "success": false,
  "errorCode": "INSUFFICIENT_BALANCE",
  "message": "Wallet does not have sufficient funds for this operation.",
  "correlationId": "0HN1234567890:00000001",
  "timestamp": "2026-10-25T14:30:00.000Z"
}
```

| Error Code | HTTP Status | Root Cause & Handled Condition |
|---|:---:|---|
| `VALIDATION_FAILED` | `400` | Input parameters missing, negative amounts, or malformed payloads |
| `CURRENCY_MISMATCH` | `400` | Transaction currency does not match wallet currency |
| `WALLET_NOT_ACTIVE` | `400` | Target wallet is blocked or closed |
| `TRANSACTION_NOT_REFUNDABLE`| `400` | Transaction is not a settled debit or has already been fully refunded |
| `REFUND_AMOUNT_EXCEEDED` | `400` | Requested refund exceeds the original transaction's unrefunded balance |
| `UNAUTHORIZED` / `INVALID_TOKEN` | `401` | Missing, expired, or cryptographically invalid JWT token |
| `FORBIDDEN` | `403` | User does not own the wallet and lacks administrative scopes |
| `USER_NOT_FOUND` / `WALLET_NOT_FOUND` | `404` | Entity does not exist for the provided unique identifier |
| `INSUFFICIENT_BALANCE` | `409` | Target wallet balance is insufficient to satisfy the requested debit |
| `IDEMPOTENCY_CONFLICT` | `409` | Same idempotency key submitted with an altered payload |
| `CONCURRENCY_CONFLICT` | `409` | Identical keyed mutation is already being executed by another thread |
| `DUPLICATE_REQUEST` | `429` | Rapid unkeyed mutation intercepted by the anti-double-click guard |
| `INTERNAL_SERVER_ERROR` | `500` | Unhandled runtime exception (stack trace redacted from client response) |

---

<p align="center">
  <b>Built with architectural precision for high-scale, fault-tolerant financial ledger systems.</b>
</p>
