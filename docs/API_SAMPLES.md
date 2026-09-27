# Payment Ledger Service — Sample API Requests & Responses

This guide provides concrete, production-ready cURL commands and representative JSON request and response payloads for all endpoints in the **Payment Ledger Service**.

---

## 1. Global Conventions & Headers

All mutating requests (`POST`, `PUT`, `PATCH`, `DELETE`) and informational requests (`GET`) adhere to standardized HTTP headers:

| Header | Description | Required? | Example |
|---|---|---|---|
| `Content-Type` | MIME payload type | Required for POST/PUT | `application/json` |
| `X-Correlation-ID` | Distributed tracing correlation identifier | Optional (auto-generated if omitted) | `trace-8f6a9b4c-92b1` |
| `Idempotency-Key` | Mutation deduplication identifier | Recommended for financial mutations | `debit-usr-789-order-456` |

Default Base URLs:
- **Docker Compose**: `http://localhost:8080`
- **Local .NET CLI**: `http://localhost:5275`

---

## 2. User Management

### 2.1 Create User
Creates a customer profile with unique email validation.

```bash
curl -X POST "http://localhost:8080/api/v1/users" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: req-user-create-001" \
  -d '{
    "fullName": "Alice Johnson",
    "email": "alice.johnson@example.com"
  }'
```

**Response (`201 Created`):**
```json
{
  "success": true,
  "message": "User created successfully.",
  "data": {
    "id": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
    "userId": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
    "fullName": "Alice Johnson",
    "email": "alice.johnson@example.com",
    "status": "Active",
    "createdAt": "2026-09-27T12:00:00.000Z",
    "updatedAt": "2026-09-27T12:00:00.000Z"
  },
  "correlationId": "req-user-create-001"
}
```

---

### 2.2 List Users (Server-Side Paginated)
Lists users with optional name/email query filtering.

```bash
curl -X GET "http://localhost:8080/api/v1/users?pageNumber=1&pageSize=10&searchText=Alice" \
  -H "X-Correlation-ID: req-user-list-001"
```

**Response (`200 OK`):**
```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1,
  "success": true,
  "message": "Users retrieved successfully.",
  "data": [
    {
      "id": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
      "userId": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
      "fullName": "Alice Johnson",
      "email": "alice.johnson@example.com",
      "status": "Active",
      "createdAt": "2026-09-27T12:00:00.000Z",
      "updatedAt": "2026-09-27T12:00:00.000Z"
    }
  ],
  "items": [
    {
      "id": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
      "userId": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
      "fullName": "Alice Johnson",
      "email": "alice.johnson@example.com",
      "status": "Active",
      "createdAt": "2026-09-27T12:00:00.000Z",
      "updatedAt": "2026-09-27T12:00:00.000Z"
    }
  ],
  "correlationId": "req-user-list-001"
}
```

---

### 2.3 Get User by ID

```bash
curl -X GET "http://localhost:8080/api/v1/users/e812d8a4-0466-4f4f-b672-98e6ad79a611"
```

---

### 2.4 Update User

```bash
curl -X PUT "http://localhost:8080/api/v1/users/e812d8a4-0466-4f4f-b672-98e6ad79a611" \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Alice J. Smith",
    "email": "alice.smith@example.com"
  }'
```

---

### 2.5 Delete User

```bash
curl -X DELETE "http://localhost:8080/api/v1/users/e812d8a4-0466-4f4f-b672-98e6ad79a611"
```

---

## 3. Wallet Management

### 3.1 Create Wallet
Initializes an active multi-currency wallet with an initial balance of `0.00`.

```bash
curl -X POST "http://localhost:8080/api/v1/wallets" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: req-wallet-create-001" \
  -d '{
    "userId": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
    "currency": "USD"
  }'
```

**Response (`201 Created`):**
```json
{
  "success": true,
  "message": "Wallet created successfully.",
  "data": {
    "walletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
    "currency": "USD",
    "balance": 0.00,
    "status": "Active",
    "createdAt": "2026-09-27T12:05:00.000Z",
    "updatedAt": "2026-09-27T12:05:00.000Z"
  },
  "correlationId": "req-wallet-create-001"
}
```

---

### 3.2 Get Wallet Balance

```bash
curl -X GET "http://localhost:8080/api/v1/wallets/3fa85f64-5717-4562-b3fc-2c963f66afa6/balance"
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Balance retrieved successfully.",
  "data": {
    "walletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
    "currency": "USD",
    "availableBalance": 0.00,
    "status": "Active",
    "lastUpdatedAt": "2026-09-27T12:05:00.000Z"
  },
  "correlationId": "trace-auto-generated"
}
```

---

### 3.3 List Wallets for User

```bash
curl -X GET "http://localhost:8080/api/v1/wallets?userId=e812d8a4-0466-4f4f-b672-98e6ad79a611"
```

---

### 3.4 Block and Unblock Wallet

**Block Wallet:**
```bash
curl -X PATCH "http://localhost:8080/api/v1/wallets/3fa85f64-5717-4562-b3fc-2c963f66afa6/block"
```

**Unblock Wallet:**
```bash
curl -X PATCH "http://localhost:8080/api/v1/wallets/3fa85f64-5717-4562-b3fc-2c963f66afa6/unblock"
```

---

## 4. Financial Transactions & Idempotency

### 4.1 Credit Wallet (Deposit from Treasury)
Transfers funds from the System Treasury wallet into the destination user wallet, generating balanced double-entry ledger entries.

```bash
curl -X POST "http://localhost:8080/api/v1/wallets/3fa85f64-5717-4562-b3fc-2c963f66afa6/credit" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: credit-usr-alice-topup-001" \
  -H "X-Correlation-ID: corr-credit-alice-01" \
  -d '{
    "amount": 250.00,
    "currency": "USD",
    "description": "Initial account deposit",
    "metadata": "{\"source\":\"stripe\",\"chargeId\":\"ch_3N1abcxyz\"}"
  }'
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Wallet credited successfully.",
  "data": {
    "transactionId": "b47c9e01-2a1d-488f-9a02-23f461879c7b",
    "transactionCode": "TXN-CR-20260927-B47C9E01",
    "type": "Credit",
    "status": "Posted",
    "sourceWalletId": "11111111-1111-1111-1111-111111111111",
    "destinationWalletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "amount": 250.00,
    "currency": "USD",
    "idempotencyKey": "credit-usr-alice-topup-001",
    "description": "Initial account deposit",
    "metadata": "{\"source\":\"stripe\",\"chargeId\":\"ch_3N1abcxyz\"}",
    "balanceAfterTransaction": 250.00,
    "createdAt": "2026-09-27T12:10:00.000Z"
  },
  "correlationId": "corr-credit-alice-01"
}
```

---

### 4.2 Debit Wallet (Withdrawal / Purchase with Pessimistic Locking)
Debits user funds to the Treasury wallet using `SELECT ... FOR UPDATE` row locking.

```bash
curl -X POST "http://localhost:8080/api/v1/wallets/3fa85f64-5717-4562-b3fc-2c963f66afa6/debit" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: debit-order-checkout-9912" \
  -H "X-Correlation-ID: corr-debit-checkout-01" \
  -d '{
    "amount": 75.25,
    "currency": "USD",
    "referenceId": "order-checkout-9912",
    "description": "Purchase of Wireless Headphones",
    "metadata": "{\"itemCount\":1,\"sku\":\"WH-1000XM5\"}"
  }'
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Wallet debited successfully.",
  "data": {
    "transactionId": "d821fa91-66c2-4bbf-a29e-1903eef580d2",
    "transactionCode": "TXN-DB-20260927-D821FA91",
    "type": "Debit",
    "status": "Posted",
    "sourceWalletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "destinationWalletId": "11111111-1111-1111-1111-111111111111",
    "amount": 75.25,
    "currency": "USD",
    "idempotencyKey": "debit-order-checkout-9912",
    "description": "Purchase of Wireless Headphones",
    "metadata": "{\"itemCount\":1,\"sku\":\"WH-1000XM5\"}",
    "balanceAfterTransaction": 174.75,
    "createdAt": "2026-09-27T12:12:00.000Z"
  },
  "correlationId": "corr-debit-checkout-01"
}
```

---

### 4.3 Idempotency Replay (Zero Double-Spend)
Replaying the exact same request with the same `Idempotency-Key` returns the original cached response without re-executing or modifying the wallet balance.

```bash
curl -X POST "http://localhost:8080/api/v1/wallets/3fa85f64-5717-4562-b3fc-2c963f66afa6/debit" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: debit-order-checkout-9912" \
  -d '{
    "amount": 75.25,
    "currency": "USD",
    "referenceId": "order-checkout-9912",
    "description": "Purchase of Wireless Headphones",
    "metadata": "{\"itemCount\":1,\"sku\":\"WH-1000XM5\"}"
  }'
```

**Response (`200 OK - Cached`):** Returns identical transaction `d821fa91-66c2-4bbf-a29e-1903eef580d2`. The balance remains `$174.75`.

---

### 4.4 Anti-Double-Click Throttling
Rapid duplicate unkeyed requests within 3 seconds are throttled by the memory guard.

**Response (`429 Too Many Requests`):**
```json
{
  "success": false,
  "message": "Duplicate request detected. Please wait before retrying.",
  "data": null,
  "errors": [
    {
      "code": "DUPLICATE_REQUEST",
      "field": null,
      "message": "Duplicate request detected. Please wait before retrying."
    }
  ],
  "correlationId": "trace-auto-generated"
}
```

---

### 4.5 Insufficient Balance Conflict
When attempting to debit more than the current available balance.

**Response (`409 Conflict`):**
```json
{
  "success": false,
  "message": "Insufficient balance. Available: 174.75 USD, Requested: 500.00 USD.",
  "data": null,
  "errors": [
    {
      "code": "INSUFFICIENT_BALANCE",
      "field": "Amount",
      "message": "Insufficient balance. Available: 174.75 USD, Requested: 500.00 USD."
    }
  ],
  "correlationId": "trace-auto-generated"
}
```

---

## 5. Refunds & Transaction Auditability

### 5.1 Refund Debit Transaction
Executes a partial or full refund against an existing debit transaction, returning funds to the user wallet.

```bash
curl -X POST "http://localhost:8080/api/v1/transactions/d821fa91-66c2-4bbf-a29e-1903eef580d2/refund" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: refund-order-9912-full" \
  -d '{
    "amount": 75.25,
    "currency": "USD",
    "reason": "Customer returned headphones unopened",
    "metadata": "{\"rmaId\":\"RMA-7749\"}"
  }'
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Transaction refunded successfully.",
  "data": {
    "transactionId": "f9a12c8b-5e4d-498c-8547-66a9d187cf29",
    "transactionCode": "TXN-RF-20260927-F9A12C8B",
    "type": "Refund",
    "status": "Posted",
    "sourceWalletId": "11111111-1111-1111-1111-111111111111",
    "destinationWalletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "amount": 75.25,
    "currency": "USD",
    "idempotencyKey": "refund-order-9912-full",
    "description": "Customer returned headphones unopened",
    "metadata": "{\"rmaId\":\"RMA-7749\"}",
    "balanceAfterTransaction": 250.00,
    "createdAt": "2026-09-27T12:15:00.000Z"
  },
  "correlationId": "trace-auto-generated"
}
```

---

### 5.2 Transaction Details with Complete Ledger Audit Entries

```bash
curl -X GET "http://localhost:8080/api/v1/transactions/d821fa91-66c2-4bbf-a29e-1903eef580d2"
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Transaction details retrieved successfully.",
  "data": {
    "transactionId": "d821fa91-66c2-4bbf-a29e-1903eef580d2",
    "transactionCode": "TXN-DB-20260927-D821FA91",
    "type": "Debit",
    "status": "Posted",
    "sourceWalletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "destinationWalletId": "11111111-1111-1111-1111-111111111111",
    "amount": 75.25,
    "currency": "USD",
    "idempotencyKey": "debit-order-checkout-9912",
    "referenceTransactionId": null,
    "description": "Purchase of Wireless Headphones",
    "metadata": "{\"itemCount\":1,\"sku\":\"WH-1000XM5\"}",
    "balanceAfterTransaction": 174.75,
    "createdAt": "2026-09-27T12:12:00.000Z",
    "updatedAt": "2026-09-27T12:12:00.000Z",
    "totalRefundedAmount": 75.25,
    "remainingRefundableAmount": 0.00,
    "ledgerEntries": [
      {
        "id": 101,
        "transactionId": "d821fa91-66c2-4bbf-a29e-1903eef580d2",
        "walletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "entryType": "Debit",
        "amount": 75.25,
        "currency": "USD",
        "balanceAfterTransaction": 174.75,
        "createdAt": "2026-09-27T12:12:00.000Z"
      },
      {
        "id": 102,
        "transactionId": "d821fa91-66c2-4bbf-a29e-1903eef580d2",
        "walletId": "11111111-1111-1111-1111-111111111111",
        "entryType": "Credit",
        "amount": 75.25,
        "currency": "USD",
        "balanceAfterTransaction": 1075.25,
        "createdAt": "2026-09-27T12:12:00.000Z"
      }
    ],
    "refunds": [
      {
        "transactionId": "f9a12c8b-5e4d-498c-8547-66a9d187cf29",
        "transactionCode": "TXN-RF-20260927-F9A12C8B",
        "amount": 75.25,
        "currency": "USD",
        "status": "Posted",
        "createdAt": "2026-09-27T12:15:00.000Z"
      }
    ]
  },
  "correlationId": "trace-auto-generated"
}
```

---

### 5.3 Per-Transaction Double-Entry Balance Audit

```bash
curl -X GET "http://localhost:8080/api/v1/transactions/d821fa91-66c2-4bbf-a29e-1903eef580d2/verify"
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Transaction consistency verification completed.",
  "data": {
    "transactionId": "d821fa91-66c2-4bbf-a29e-1903eef580d2",
    "transactionCode": "TXN-DB-20260927-D821FA91",
    "transactionAmount": 75.25,
    "totalDebits": 75.25,
    "totalCredits": 75.25,
    "isBalanced": true,
    "message": "Transaction is fully balanced: debits and credits equal 75.25 USD.",
    "auditedAt": "2026-09-27T12:16:00.000Z"
  },
  "correlationId": "trace-auto-generated"
}
```

---

### 5.4 Per-Wallet Balance Reconciliation Audit
Verifies that current wallet balance equals total credits minus total debits across the immutable ledger.

```bash
curl -X GET "http://localhost:8080/api/v1/wallets/3fa85f64-5717-4562-b3fc-2c963f66afa6/verify"
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Wallet consistency verification completed.",
  "data": {
    "walletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "e812d8a4-0466-4f4f-b672-98e6ad79a611",
    "currency": "USD",
    "currentBalance": 250.00,
    "calculatedLedgerBalance": 250.00,
    "discrepancy": 0.00,
    "isConsistent": true,
    "auditedAt": "2026-09-27T12:16:30.000Z"
  },
  "correlationId": "trace-auto-generated"
}
```

---

### 5.5 System-Wide Global Ledger Consistency Audit
Runs complete cryptographic audit verifying zero discrepancy across all wallets, all transactions, and all ledger entry debits and credits.

```bash
curl -X POST "http://localhost:8080/api/v1/ledger/verify"
```

**Response (`200 OK`):**
```json
{
  "success": true,
  "message": "Ledger consistency verification completed.",
  "data": {
    "isConsistent": true,
    "totalWalletsAudited": 4,
    "totalTransactionsAudited": 3,
    "totalLedgerEntriesAudited": 6,
    "transactionDiscrepancies": [],
    "walletDiscrepancies": [],
    "auditedAt": "2026-09-27T12:17:00.000Z"
  },
  "correlationId": "trace-auto-generated"
}
```

---

## 6. Health & Diagnostics

```bash
curl -i "http://localhost:8080/health"
```

**Response (`200 OK`):**
```
HTTP/1.1 200 OK
Content-Type: text/plain
Healthy
```
