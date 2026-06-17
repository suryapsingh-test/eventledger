# Event Ledger — API Contracts

**Task:** T-04 · **Agent:** ARCH-01  
**Shared DTOs:** `src/EventLedger.Contracts/` (DEV-03)

---

## Conventions

| Item | Value |
|------|-------|
| Content-Type | `application/json` |
| Date/time | ISO 8601 UTC (`2026-05-15T14:02:11Z`) |
| Errors | RFC 7807 `application/problem+json` |
| Trace propagation | W3C `traceparent` header (Gateway → Account) |
| Idempotency replay | Response header `Idempotency-Replay: true` on duplicate `eventId` |

---

## Event Gateway API (public)

Base URL: `http://localhost:8080`

### POST /events

Submit a transaction event.

**Request body:**

```json
{
  "eventId": "evt-001",
  "accountId": "acct-123",
  "type": "CREDIT",
  "amount": 150.00,
  "currency": "USD",
  "eventTimestamp": "2026-05-15T14:02:11Z",
  "metadata": {
    "source": "mainframe-batch",
    "batchId": "B-9042"
  }
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `eventId` | string | Yes | Non-empty, max 128 |
| `accountId` | string | Yes | Non-empty, max 64 |
| `type` | string | Yes | `CREDIT` \| `DEBIT` |
| `amount` | number | Yes | > 0, max 2 decimal places |
| `currency` | string | Yes | Non-empty, max 8 |
| `eventTimestamp` | string | Yes | Valid ISO 8601 |
| `metadata` | object | No | JSON object |

**Responses:**

| Status | When | Body |
|--------|------|------|
| **201 Created** | First successful apply | `EventResponse` |
| **200 OK** | Duplicate `eventId` | `EventResponse` + `Idempotency-Replay: true` |
| **400 Bad Request** | Validation failure | `ProblemDetails` |
| **503 Service Unavailable** | Account down / circuit open / timeout | `ProblemDetails` |

**EventResponse:**

```json
{
  "eventId": "evt-001",
  "accountId": "acct-123",
  "type": "CREDIT",
  "amount": 150.00,
  "currency": "USD",
  "eventTimestamp": "2026-05-15T14:02:11Z",
  "metadata": { "source": "mainframe-batch" },
  "receivedAt": "2026-06-16T14:00:01Z",
  "status": "Applied"
}
```

---

### GET /events/{eventId}

Retrieve a single event by `eventId`.

**Path parameter:** `eventId` — business event identifier (not internal surrogate key).

| Status | Body |
|--------|------|
| **200 OK** | `EventResponse` |
| **404 Not Found** | `ProblemDetails` |

---

### GET /events?account={accountId}

List events for an account.

**Query:** `account` (required) — `accountId` value.

| Status | Body |
|--------|------|
| **200 OK** | `EventResponse[]` ordered by `eventTimestamp` ASC, `eventId` ASC |
| **400 Bad Request** | Missing `account` query param |

**Example:**

```json
[
  {
    "eventId": "evt-001",
    "accountId": "acct-123",
    "type": "CREDIT",
    "amount": 100.00,
    "currency": "USD",
    "eventTimestamp": "2026-05-15T10:00:00Z",
    "receivedAt": "2026-06-16T14:00:00Z",
    "status": "Applied"
  }
]
```

---

### GET /health

| Status | Body |
|--------|------|
| **200 OK** | `HealthResponse` (healthy) |
| **503 Service Unavailable** | `HealthResponse` (unhealthy) |

```json
{
  "status": "Healthy",
  "service": "EventGateway",
  "checks": {
    "database": "Healthy"
  },
  "timestamp": "2026-06-16T14:00:00Z"
}
```

---

## Account Service API (internal)

Base URL: `http://account-service:8081` (Docker) / `http://localhost:8081` (local dev)

### POST /accounts/{accountId}/transactions

Apply a transaction. Called by Gateway only.

**Path:** `accountId`

**Request body:**

```json
{
  "eventId": "evt-001",
  "type": "CREDIT",
  "amount": 150.00,
  "currency": "USD",
  "eventTimestamp": "2026-05-15T14:02:11Z"
}
```

| Field | Type | Required |
|-------|------|----------|
| `eventId` | string | Yes |
| `type` | string | Yes (`CREDIT` \| `DEBIT`) |
| `amount` | number | Yes (> 0) |
| `currency` | string | Yes |
| `eventTimestamp` | string | Yes |

**Responses:**

| Status | When | Body |
|--------|------|------|
| **201 Created** | New transaction applied | `TransactionResponse` |
| **200 OK** | Duplicate `eventId` | `TransactionResponse` + `Idempotency-Replay: true` |
| **400 Bad Request** | Validation failure | `ProblemDetails` |
| **500 Internal Server Error** | Unexpected failure | `ProblemDetails` |

**TransactionResponse:**

```json
{
  "eventId": "evt-001",
  "accountId": "acct-123",
  "type": "CREDIT",
  "amount": 150.00,
  "currency": "USD",
  "eventTimestamp": "2026-05-15T14:02:11Z",
  "appliedAt": "2026-06-16T14:00:01Z",
  "balanceAfter": 150.00
}
```

**Side effect:** Creates `Account` row if `accountId` does not exist (DEC-03).

---

### GET /accounts/{accountId}/balance

| Status | Body |
|--------|------|
| **200 OK** | `BalanceResponse` |
| **404 Not Found** | Account never created |

```json
{
  "accountId": "acct-123",
  "currency": "USD",
  "balance": 120.00,
  "asOf": "2026-06-16T14:00:00Z"
}
```

---

### GET /accounts/{accountId}

Account details with recent transactions.

| Status | Body |
|--------|------|
| **200 OK** | `AccountDetailResponse` |
| **404 Not Found** | Account not found |

```json
{
  "accountId": "acct-123",
  "createdAt": "2026-06-16T13:00:00Z",
  "balance": 120.00,
  "currency": "USD",
  "recentTransactions": [
    {
      "eventId": "evt-002",
      "type": "DEBIT",
      "amount": 30.00,
      "eventTimestamp": "2026-05-15T11:00:00Z",
      "appliedAt": "2026-06-16T14:00:01Z"
    }
  ]
}
```

`recentTransactions`: last 20 by `eventTimestamp DESC`.

---

### GET /health

Same shape as Gateway `HealthResponse` with `"service": "AccountService"`.

---

## ProblemDetails (errors)

```json
{
  "type": "https://eventledger/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "amount must be greater than 0",
  "traceId": "abc123"
}
```

**503 example (downstream unavailable):**

```json
{
  "type": "https://eventledger/errors/service-unavailable",
  "title": "Account Service unavailable",
  "status": 503,
  "detail": "Unable to apply transaction. Circuit breaker is open.",
  "traceId": "abc123"
}
```

---

## Internal Gateway → Account mapping

Gateway transforms `POST /events` body to Account transaction request:

| Event field | Transaction field |
|-------------|-------------------|
| `eventId` | `eventId` |
| `type` | `type` |
| `amount` | `amount` |
| `currency` | `currency` |
| `eventTimestamp` | `eventTimestamp` |
| `accountId` | path `{accountId}` |

`metadata` is **not** forwarded to Account Service (Gateway-only audit field).
