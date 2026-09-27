# Payment Ledger Service — Architecture & Design

This document details the architectural design, financial ledger principles, concurrency controls, and idempotency guarantees of the **Payment Ledger Service**.

---

## 1. System Architecture Overview

The system follows a streamlined, direct **3-tier architecture** with zero CQRS/MediatR indirection:

```mermaid
flowchart TD
    subgraph Client["Client Tier"]
        UI["Angular 17 SPA<br/>(Tailwind CSS)"]
        API_Client["External HTTP / API Clients"]
    end

    subgraph Presentation["Presentation Tier (PaymentLedger.Api)"]
        Nginx["Nginx Reverse Proxy (:4200)"]
        Gateway["ASP.NET Core Web API (:8080)"]
        Middleware["Middlewares:<br/>• CorrelationIdMiddleware<br/>• GlobalExceptionHandlerMiddleware<br/>• Serilog Logging<br/>• CORS Policy"]
        Controllers["Controllers:<br/>• UsersController<br/>• WalletsController<br/>• TransactionsController<br/>• LedgerController"]
    end

    subgraph Business["Business Tier (PaymentLedger.Business)"]
        BServices["Business Services:<br/>• UserBusinessService<br/>• WalletBusinessService<br/>• TransactionBusinessService"]
        IdemEngine["Idempotency Engine:<br/>• IdempotencyService<br/>• SHA-256 Request Fingerprinting<br/>• Memory Anti-Double-Click Guard"]
        Models["Consolidated Models:<br/>• UserModels.cs<br/>• WalletModels.cs<br/>• TransactionModels.cs"]
    end

    subgraph Persistence["Data & Persistence Tier (PaymentLedger.Repository)"]
        UnitOfWork["Unit of Work & DbContext:<br/>• AppDbContext<br/>• IUnitOfWork"]
        Repos["Repositories:<br/>• UserRepository<br/>• WalletRepository (Row Locking)<br/>• TransactionRepository<br/>• LedgerRepository<br/>• IdempotencyRepository"]
        Entities["Database Entities:<br/>• User<br/>• Wallet<br/>• Transaction<br/>• LedgerEntry<br/>• IdempotencyRequest"]
    end

    subgraph Database["Database Tier"]
        Postgres[("PostgreSQL 17 Database<br/>• Pessimistic Row Locks<br/>• CHECK balance >= 0")]
    end

    UI --> Nginx
    Nginx --> Gateway
    API_Client --> Gateway
    Gateway --> Middleware
    Middleware --> Controllers
    Controllers --> BServices
    BServices --> IdemEngine
    BServices --> UnitOfWork
    BServices --> Repos
    Repos --> Entities
    UnitOfWork --> Postgres
    Repos --> Postgres
```

---

## 2. Double-Entry Ledger Bookkeeping Architecture

Every financial movement creates at least two immutable, balanced `LedgerEntry` records such that:

$$\sum \text{Debits} = \sum \text{Credits} = \text{Transaction.Amount}$$

All money entries are tracked strictly against System Treasury accounts.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Client / Merchant
    participant API as WalletsController
    participant Service as WalletBusinessService
    participant Idem as IdempotencyService
    participant Repo as WalletRepository
    participant DB as PostgreSQL (AppDbContext)

    Note over Client,DB: Flow 1: Credit Wallet (Deposit from Treasury)
    Client->>API: POST /api/v1/wallets/{id}/credit (Amount, Currency, Idempotency-Key)
    API->>Service: CreditWalletAsync(walletId, req, key)
    Service->>Idem: CheckAsync(key, hash)
    Idem-->>Service: None (Fresh request)
    Service->>DB: BeginTransactionAsync()
    Service->>Repo: GetByIdForUpdateAsync(walletId) [SELECT ... FOR UPDATE]
    Repo-->>Service: targetWallet (Locked)
    Service->>Repo: GetTreasuryWalletAsync(currency)
    Repo-->>Service: treasuryWallet
    Service->>Service: targetWallet.Credit(amount)
    Service->>DB: AddAsync(Transaction [Posted])
    Service->>DB: AddRangeAsync([Treasury Debit Entry, User Credit Entry])
    Service->>Idem: SaveAsync(key, response, 200)
    Service->>DB: SaveChangesAsync() & CommitTransactionAsync()
    Service-->>API: TransactionResponse (Balance Updated)
    API-->>Client: 200 OK (ApiResponse<TransactionResponse>)

    Note over Client,DB: Flow 2: Debit Wallet (Withdrawal / Purchase with Pessimistic Lock)
    Client->>API: POST /api/v1/wallets/{id}/debit (Amount, Currency, Idempotency-Key)
    API->>Service: DebitWalletAsync(walletId, req, key)
    Service->>Idem: CheckAsync(key, hash)
    Idem-->>Service: None
    Service->>DB: BeginTransactionAsync()
    Service->>Repo: GetByIdForUpdateAsync(walletId) [SELECT ... FOR UPDATE]
    Repo-->>Service: targetWallet (Locked)
    Service->>Idem: PostLockCheckAsync(key, hash)
    Idem-->>Service: None (Not processed concurrently)
    alt Balance >= Amount
        Service->>Service: targetWallet.Debit(amount)
        Service->>DB: AddAsync(Transaction [Posted])
        Service->>DB: AddRangeAsync([User Debit Entry, Treasury Credit Entry])
        Service->>Idem: SaveAsync(key, response, 200)
        Service->>DB: SaveChangesAsync() & CommitTransactionAsync()
        Service-->>API: TransactionResponse
        API-->>Client: 200 OK
    else Insufficient Balance
        Service->>DB: RollbackTransactionAsync()
        Service-->>API: throw DomainException(INSUFFICIENT_BALANCE)
        API-->>Client: 409 Conflict (ErrorResponse)
    end
```

---

## 3. Concurrency Protection & Pessimistic Row Locking

When multiple parallel requests target the same wallet simultaneously (e.g. rapid card swipes, race condition exploits):

```mermaid
sequenceDiagram
    autonumber
    participant ReqA as Request A ($60 Debit)
    participant ReqB as Request B ($60 Debit)
    participant DB as PostgreSQL (Initial Balance = $100.00)

    Note over ReqA,ReqB: Both requests arrive at the exact same millisecond
    ReqA->>DB: BEGIN TX; SELECT * FROM wallets WHERE id = ... FOR UPDATE;
    Note over DB: Lock granted to Request A
    ReqB->>DB: BEGIN TX; SELECT * FROM wallets WHERE id = ... FOR UPDATE;
    Note over DB: Request B blocked waiting on Row Lock

    Note over ReqA,DB: Request A executes under lock
    ReqA->>DB: Balance ($100) >= $60 -> Update balance to $40
    ReqA->>DB: Insert Ledger Entries ($60 Debit, $60 Credit)
    ReqA->>DB: COMMIT TX;
    Note over DB: Lock released! Request A completes (HTTP 200 OK)

    Note over ReqB,DB: Lock now acquired by Request B
    DB-->>ReqB: Returns updated wallet row (Balance = $40.00)
    ReqB->>ReqB: Balance ($40.00) < $60.00 -> Fails Invariant Check!
    ReqB->>DB: ROLLBACK TX;
    Note over ReqB: Request B rejected with INSUFFICIENT_BALANCE (HTTP 409)
```

### Invariants Enforced
1. **Zero-Negative Balance**: `wallet.Balance >= 0.00` guaranteed by both domain validation and database check constraint:
   ```sql
   CONSTRAINT ck_wallets_balance_non_negative CHECK (balance >= 0)
   ```
2. **Strict Serialization**: All balance mutations are serialized via `GetByIdForUpdateAsync(walletId)`.
3. **Double-Entry Equality**: `sum(debits) - sum(credits) == 0` verified continuously.

---

## 4. Idempotency Lifecycle & Tamper Detection

The service features an automated, centralized idempotency engine:

```mermaid
flowchart TD
    Start["Incoming Request<br/>(POST /credit, /debit, /refund)"] --> KeyRes{"Idempotency-Key<br/>Supplied in Header?"}
    KeyRes -- Yes --> UseHeader["Use Header Key"]
    KeyRes -- No --> RefCheck{"ReferenceId<br/>Supplied in Body?"}
    RefCheck -- Yes --> MakeDetKey["Generate Deterministic Key:<br/>{action}:{walletId}:{referenceId}"]
    RefCheck -- No --> AutoKey["Anti-Double-Click Guard<br/>SHA-256 Memory Throttling (3s Window)"]

    AutoKey --> IsDuplicate{"Duplicate Click<br/>within 3s?"}
    IsDuplicate -- Yes --> RejectThrottled["Return HTTP 429<br/>(DUPLICATE_REQUEST)"]
    IsDuplicate -- No --> GenerateUUID["Generate auto:UUID"]

    UseHeader --> HashPayload["Compute SHA-256 Payload Fingerprint"]
    MakeDetKey --> HashPayload
    GenerateUUID --> HashPayload

    HashPayload --> CheckRepo["Check Idempotency Table<br/>(key + endpoint)"]
    CheckRepo --> Exists{"Key Already Exists?"}

    Exists -- Yes --> CheckHash{"Request Hash<br/>Matches?"}
    CheckHash -- No --> ConflictTamper["Throw IDEMPOTENCY_CONFLICT<br/>(HTTP 409)"]
    CheckHash -- Yes --> CheckStatus{"Status in DB?"}
    CheckStatus -- Completed --> ReturnCached["Return Cached TransactionResponse<br/>(HTTP 200 OK, Zero Re-execution)"]
    CheckStatus -- Processing --> ConflictConcurrency["Throw CONCURRENCY_CONFLICT<br/>(HTTP 409)"]

    Exists -- No --> BeginTx["Begin Atomic Database Transaction"]
    BeginTx --> RowLock["Acquire Row Lock (SELECT ... FOR UPDATE)"]
    RowLock --> PostLockCheck{"Post-Lock Recheck:<br/>Completed while waiting?"}
    PostLockCheck -- Yes --> RollbackCached["Rollback TX & Return Cached Response"]
    PostLockCheck -- No --> ExecuteMutation["Execute Mutation & Insert Double-Entry Ledger"]
    ExecuteMutation --> SaveIdem["Save Idempotency Record (Status: Completed)"]
    SaveIdem --> CommitTx["Commit Transaction & Return Response"]
```

---

## 5. Database Schema & Entity Relationships

```mermaid
erDiagram
    USERS ||--o{ WALLETS : owns
    USERS {
        uuid id PK
        string full_name
        string email UK
        string status
        timestamptz created_at
        timestamptz updated_at
    }

    WALLETS ||--o{ TRANSACTIONS : source_wallet
    WALLETS ||--o{ TRANSACTIONS : destination_wallet
    WALLETS ||--o{ LEDGER_ENTRIES : holds
    WALLETS {
        uuid id PK
        uuid user_id FK
        string currency
        numeric balance "CHECK balance >= 0"
        string status
        timestamptz created_at
        timestamptz updated_at
    }

    TRANSACTIONS ||--o{ LEDGER_ENTRIES : contains
    TRANSACTIONS ||--o{ TRANSACTIONS : refunds
    TRANSACTIONS {
        uuid id PK
        string transaction_code UK
        string type
        string status
        uuid source_wallet_id FK
        uuid destination_wallet_id FK
        numeric amount
        string currency
        string idempotency_key UK
        uuid reference_transaction_id FK
        string description
        string metadata
        numeric balance_after_transaction
        timestamptz created_at
        timestamptz updated_at
    }

    LEDGER_ENTRIES {
        bigint id PK
        uuid transaction_id FK
        uuid wallet_id FK
        string entry_type "Debit or Credit"
        numeric amount
        string currency
        numeric balance_after_transaction
        timestamptz created_at
    }

    IDEMPOTENCY_REQUESTS {
        uuid id PK
        string idempotency_key UK
        string endpoint
        string request_hash
        string status "Processing or Completed"
        int status_code
        string response_payload
        timestamptz expires_at
        timestamptz created_at
    }
```
