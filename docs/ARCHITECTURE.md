# 🏗️ Payment Ledger Service — Architecture & Design Blueprint

This document contains the complete High-Level Design (HLD) and Low-Level Design (LLD) specifications, concurrency controls, domain entity models, and event-driven integration flows for the **Payment Ledger Service**.

---

## 📑 Table of Contents

- [1. High-Level Design (HLD): Macro Topology](#1-high-level-design-hld-macro-topology)
- [2. Low-Level Design (LLD): Financial Mutation Pipeline](#2-low-level-design-lld-financial-mutation-pipeline)
- [3. Low-Level Design (LLD): Database Schema & Entity Relationships](#3-low-level-design-lld-database-schema--entity-relationships)
- [4. Transactional Outbox & Event-Driven Streaming](#4-transactional-outbox--event-driven-streaming)
- [5. Concurrency Protection & Pessimistic Row Locking](#5-concurrency-protection--pessimistic-row-locking)
- [6. Multi-Tier Idempotency Lifecycle & Tamper Detection](#6-multi-tier-idempotency-lifecycle--tamper-detection)

---

## 1. High-Level Design (HLD): Macro Topology

The macro-system topology spans user-facing clients, an API presentation gateway, transactional core domain, distributed caching, PostgreSQL relational storage, and Kafka-driven integration with downstream ERP microservices:

```mermaid
graph TB
    subgraph Clients["1. Client Tier"]
        UI["Angular 17 Web App<br/>(Tailwind CSS Dashboard :4200)"]
        B2B["External / B2B Services<br/>(REST API Clients)"]
    end

    subgraph Presentation["2. Presentation Tier (PaymentLedger.Api)"]
        Nginx["Nginx Reverse Proxy (:4200)"]
        Gateway["ASP.NET Core 10 Web API (:8080)<br/>• JWT Bearer Auth & Policy Scopes<br/>• CorrelationId Middleware (X-Correlation-ID)<br/>• Global Exception Redaction Middleware"]
        Controllers["API Controllers<br/>• Users • Wallets • Transactions • Ledger"]
    end

    subgraph Business["3. Domain Orchestration Tier (PaymentLedger.Business)"]
        BServices["Business Services<br/>• WalletBusinessService<br/>• TransactionBusinessService<br/>• UserBusinessService"]
        IdemEngine["Distributed Idempotency Engine<br/>• SHA-256 Payload Hasher<br/>• Post-Lock Recheck Guard"]
        OutboxWorker["OutboxProcessorBackgroundService<br/>(Worker Polling :5s)"]
        Publisher["KafkaIntegrationEventPublisher<br/>(Confluent.Kafka)"]
    end

    subgraph Caching["4. Caching & Fast-Path Tier"]
        Redis[("Redis 7 Cluster<br/>• 3-Second Anti-Double-Click Lock<br/>• 24-Hour Idempotent Response Cache")]
    end

    subgraph Storage["5. Relational Persistence Tier (PaymentLedger.Repository)"]
        Postgres[("PostgreSQL 17 Database<br/>• wallets (Row Locked via SELECT ... FOR UPDATE)<br/>• transactions & ledger_entries<br/>• outbox_messages (SELECT ... FOR UPDATE SKIP LOCKED)<br/>• Check Constraint: ck_wallets_balance_non_negative")]
    end

    subgraph Messaging["6. Message Broker Infrastructure"]
        Kafka["Apache Kafka (KRaft Mode :9092)<br/>Topic: payment-ledger.events<br/>Partition Key: ReferenceId"]
        KafkaUI["Kafka Management UI (:8085)"]
    end

    subgraph Consumers["7. Downstream Consumer Ecosystem (Inventory API)"]
        InvWorker["PaymentIntegrationConsumerBackgroundService<br/>(EVA.StockManagement.API)"]
        POConsumer["Purchase Order Reconciler<br/>(PO Advance & Final Settlement)"]
        GRNConsumer["Purchase Voucher / GRN Reconciler<br/>(Accounts Payable Invoice Settlement)"]
        VendorRecon["Vendor Account Reconciler<br/>(Continuous Statement Reconciliation)"]
    end

    UI --> Nginx
    Nginx --> Gateway
    B2B --> Gateway
    Gateway --> Controllers
    Controllers --> BServices
    BServices <--> IdemEngine
    IdemEngine <--> Redis
    BServices --> Postgres
    Postgres -.-> OutboxWorker
    OutboxWorker --> Publisher
    Publisher -->|"TransactionPostedIntegrationEvent"| Kafka
    Kafka -.-> KafkaUI
    Kafka --> InvWorker
    InvWorker --> POConsumer & GRNConsumer & VendorRecon
```

---

## 2. Low-Level Design (LLD): Financial Mutation Pipeline

The sequence below illustrates the end-to-end execution of a balance mutation (`Debit`), detailing the multi-stage concurrency controls, row locking, double-entry ledger insertion, outbox staging, and cache updates:

```mermaid
sequenceDiagram
    autonumber
    participant Client as Client Application
    participant Api as WalletsController
    participant Idem as IdempotencyService
    participant Redis as Redis Cache
    participant Biz as WalletBusinessService
    participant DB as PostgreSQL (AppDbContext)
    participant Outbox as outbox_messages

    Client->>Api: POST /api/v1/wallets/{id}/debit (Idempotency-Key, Payload)
    Api->>Idem: CheckPreLockAsync(key, payloadHash)
    Idem->>Redis: GET idemp:{key}
    alt Redis Fast-Path Cache Hit
        Redis-->>Idem: Cached TransactionResponse
        Idem-->>Api: Cached Result (<1ms)
        Api-->>Client: 200 OK (Served from Cache)
    else Redis Cache Miss
        Idem->>Redis: SETNX idemp:lock:{key} (TTL 3s)
        Note over Redis: Prevents concurrent rapid double-clicks
        Idem->>DB: Query existing IdempotencyRequest by Key
        alt Key Reused with Different Payload
            DB-->>Idem: Mismatched RequestHash
            Idem-->>Api: Throw IDEMPOTENCY_CONFLICT
            Api-->>Client: 409 Conflict
        end
    end

    Api->>Biz: DebitWalletAsync(walletId, request)
    Biz->>DB: BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;
    Biz->>DB: SELECT * FROM wallets WHERE id = @id FOR UPDATE;
    Note over DB: Row lock acquired! Sibling workers queue safely.

    Biz->>Idem: PostLockCheckAsync(key)
    Note over Biz,DB: Verifies predecessor didn't commit while waiting for lock

    Biz->>Biz: Verify Invariants: (Balance >= Amount) && (Status == Active)
    alt Insufficient Balance
        Biz->>DB: ROLLBACK TRANSACTION;
        Biz-->>Api: Throw INSUFFICIENT_BALANCE
        Api-->>Client: 409 Conflict
    end

    Biz->>DB: UPDATE wallets SET balance = balance - @amount, updated_at = @now;
    Biz->>DB: INSERT INTO transactions (id, type, amount, status = 'Posted', ...);
    Biz->>DB: INSERT INTO ledger_entries (Debit User, Credit Treasury);
    Biz->>DB: INSERT INTO outbox_messages (EventType = 'TransactionPostedEvent', ...);
    Biz->>DB: INSERT INTO idempotency_requests (key, payload_hash, response_json);
    Biz->>DB: COMMIT TRANSACTION;
    Note over DB: Lock released! Data and Outbox committed atomically.

    Biz->>Redis: SET idemp:{key} = Response (TTL 24h)
    Biz-->>Api: TransactionResponse
    Api-->>Client: 200 OK (balanceAfterTransaction, transactionCode)
```

---

## 3. Low-Level Design (LLD): Database Schema & Entity Relationships

The relational schema strictly enforces double-entry bookkeeping, transactional outbox storage, and cryptographic idempotency tracking:

```mermaid
erDiagram
    USERS ||--o{ WALLETS : owns
    WALLETS ||--o{ TRANSACTIONS : source_wallet
    WALLETS ||--o{ TRANSACTIONS : destination_wallet
    TRANSACTIONS ||--|{ LEDGER_ENTRIES : contains
    TRANSACTIONS ||--o| TRANSACTIONS : refunds
    WALLETS ||--o{ IDEMPOTENCY_REQUESTS : tracks
    OUTBOX_MESSAGES }|--|| TRANSACTIONS : stages_events_for

    USERS {
        uuid id PK
        varchar email UK "Indexed"
        varchar full_name
        varchar status "Active, Blocked"
        timestamp created_at
    }

    WALLETS {
        uuid id PK
        uuid user_id FK "Indexed"
        varchar currency "USD, INR, EUR"
        numeric balance "CHECK (balance >= 0)"
        varchar status "Active, Blocked, Closed"
        bigint xmin "PostgreSQL row concurrency token"
        timestamp created_at
    }

    TRANSACTIONS {
        uuid id PK
        varchar transaction_code UK "TXN-DB-YYYYMMDD-..."
        uuid source_wallet_id FK
        uuid destination_wallet_id FK
        numeric amount "Precision numeric(18,2)"
        varchar currency
        varchar type "Debit, Credit, Refund"
        varchar status "Pending, Posted, Failed"
        varchar reference_id "PO-2026-001, PV-2026-001"
        uuid reference_transaction_id FK "Linked Refund"
        timestamp created_at
    }

    LEDGER_ENTRIES {
        uuid id PK
        uuid transaction_id FK
        uuid wallet_id FK
        varchar entry_type "Debit, Credit"
        numeric amount "numeric(18,2)"
        timestamp created_at
    }

    IDEMPOTENCY_REQUESTS {
        uuid id PK
        varchar idempotency_key UK
        varchar request_hash "SHA-256 fingerprint"
        jsonb response_payload
        int status_code
        timestamp expires_at
    }

    OUTBOX_MESSAGES {
        uuid id PK
        varchar type "TransactionPostedEvent"
        jsonb payload "Serialized event payload"
        int retry_count
        varchar error "Failure diagnostics"
        timestamp processed_on_utc "Null until dispatched"
        timestamp occurred_on_utc
    }
```

---

## 4. Transactional Outbox & Event-Driven Streaming

```mermaid
flowchart LR
    subgraph PaymentLedgerService["Payment Ledger Service (.NET 10)"]
        direction TB
        Tx["Atomic DB Transaction"] -->|"Commit"| OutboxTable[("outbox_messages")]
        OutboxTable -.->|"SELECT ... FOR UPDATE<br/>SKIP LOCKED"| Dispatcher["OutboxProcessorBackgroundService<br/>(Every 5 Seconds)"]
        Dispatcher --> Publisher["KafkaIntegrationEventPublisher"]
    end

    subgraph KafkaBroker["Message Broker (Kafka / KRaft)"]
        Publisher -->|"Topic: payment-ledger.events<br/>Key: ReferenceId"| Kafka["Kafka Topic Partition"]
    end

    subgraph InventoryService["Inventory API (EVA.StockManagement)"]
        direction TB
        Kafka --> Consumer["PaymentIntegrationConsumerBackgroundService"]
        Consumer -->|"ReferenceId: PO-xxx"| PO["Purchase Orders<br/>• Reconcile Advance<br/>• Status: PartiallyClosed / Closed"]
        Consumer -->|"ReferenceId: PV-xxx"| GRN["Purchase Vouchers (GRN)<br/>• Accounts Payable Invoice Settled<br/>• Reconcile VendorClientAccount"]
    end
```

### Outbox Pattern Guarantees
1. **Zero Dual-Write Anomalies**: Events are committed to `outbox_messages` in the same database transaction as ledger modifications.
2. **Lock-Free Concurrency (`SKIP LOCKED`)**: Worker nodes concurrently poll unprocessed records without contention:
   ```sql
   SELECT * FROM outbox_messages
   WHERE processed_on_utc IS NULL AND retry_count < @maxRetries
   ORDER BY occurred_on_utc ASC
   LIMIT @batchSize
   FOR UPDATE SKIP LOCKED;
   ```
3. **Partition-Keyed Ordering**: All events carry `ReferenceId` as the Kafka message key, ensuring strict sequential processing of related transactions on the same partition.

---

## 5. Concurrency Protection & Pessimistic Row Locking

When multiple concurrent requests target the same wallet (e.g. parallel debits, race condition attempts):

```mermaid
sequenceDiagram
    autonumber
    participant ReqA as Request A ($60 Debit)
    participant ReqB as Request B ($60 Debit)
    participant DB as PostgreSQL (Initial Balance = $100.00)

    Note over ReqA,ReqB: Both arrive concurrently at the exact same millisecond
    ReqA->>DB: BEGIN TX; SELECT * FROM wallets WHERE id = ... FOR UPDATE;
    Note over DB: Lock granted to Request A
    ReqB->>DB: BEGIN TX; SELECT * FROM wallets WHERE id = ... FOR UPDATE;
    Note over DB: Request B blocked waiting on Row Lock

    Note over ReqA,DB: Request A executes safely under lock
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

---

## 6. Multi-Tier Idempotency Lifecycle & Tamper Detection

```mermaid
flowchart TD
    Start["Incoming Request<br/>(POST /credit, /debit, /refund)"] --> KeyRes{"Idempotency-Key<br/>Supplied in Header?"}
    KeyRes -- Yes --> UseHeader["Use Header Key"]
    KeyRes -- No --> RefCheck{"ReferenceId<br/>Supplied in Body?"}
    RefCheck -- Yes --> MakeDetKey["Generate Deterministic Key:<br/>{action}:{walletId}:{referenceId}"]
    RefCheck -- No --> AutoKey["Anti-Double-Click Guard<br/>Redis SETNX (3s Window)"]

    AutoKey --> IsDuplicate{"Duplicate Click<br/>within 3s?"}
    IsDuplicate -- Yes --> RejectThrottled["Return HTTP 429<br/>(DUPLICATE_REQUEST)"]
    IsDuplicate -- No --> GenerateUUID["Generate auto:UUID"]

    UseHeader --> HashPayload["Compute SHA-256 Payload Fingerprint"]
    MakeDetKey --> HashPayload
    GenerateUUID --> HashPayload

    HashPayload --> FastPath{"Check Redis Fast-Path Cache"}
    FastPath -- Hit --> ReturnCachedFast["Return Cached Response (<1ms)"]
    FastPath -- Miss --> CheckRepo["Check Idempotency Table in DB"]
    
    CheckRepo --> Exists{"Key Already Exists in DB?"}
    Exists -- Yes --> CheckHash{"Request Hash Matches?"}
    CheckHash -- No --> ConflictTamper["Throw IDEMPOTENCY_CONFLICT (HTTP 409)"]
    CheckHash -- Yes --> ReturnCachedDB["Return Cached Response & Update Redis"]

    Exists -- No --> BeginTx["Begin Database Transaction"]
    BeginTx --> RowLock["Acquire Row Lock (SELECT ... FOR UPDATE)"]
    RowLock --> PostLockCheck{"Post-Lock Recheck:<br/>Completed while waiting?"}
    PostLockCheck -- Yes --> RollbackCached["Rollback TX & Return Cached Response"]
    PostLockCheck -- No --> ExecuteMutation["Execute Mutation & Insert Ledger Entries"]
    ExecuteMutation --> SaveOutbox["Stage Outbox Message"]
    SaveOutbox --> SaveIdem["Save Idempotency Record (Completed)"]
    SaveIdem --> CommitTx["Commit Transaction & Cache in Redis (TTL 24h)"]
```
