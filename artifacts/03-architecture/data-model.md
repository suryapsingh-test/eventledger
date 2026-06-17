# Event Ledger — Data Model

**Task:** T-04 · **Agent:** ARCH-01  
**ORM:** EF Core 8 + SQLite (separate database per service)

---

## Event Gateway database (`gateway.db`)

### Entity: EventRecord

Stores successfully applied events (persist-after-success, DEC-05).

| Column | Type | Notes |
|--------|------|-------|
| `EventId` | `TEXT` PK | Business id from client |
| `AccountId` | `TEXT` NOT NULL | Indexed |
| `Type` | `TEXT` NOT NULL | `CREDIT` \| `DEBIT` |
| `Amount` | `TEXT` NOT NULL | Decimal stored as string for precision |
| `Currency` | `TEXT` NOT NULL | |
| `EventTimestamp` | `TEXT` NOT NULL | ISO 8601 |
| `MetadataJson` | `TEXT` NULL | Serialized metadata object |
| `PayloadJson` | `TEXT` NOT NULL | Full original request |
| `PayloadHash` | `TEXT` NOT NULL | SHA-256 for conflict detection |
| `ReceivedAt` | `TEXT` NOT NULL | UTC |
| `ProcessedAt` | `TEXT` NOT NULL | UTC when Account confirmed |
| `Status` | `TEXT` NOT NULL | `Applied` |
| `TraceId` | `TEXT` NULL | Correlation |

**Indexes:**

| Name | Columns | Purpose |
|------|---------|---------|
| `PK_EventRecord` | `EventId` | Primary key + idempotency |
| `IX_EventRecord_AccountId_EventTimestamp` | `AccountId`, `EventTimestamp`, `EventId` | List by account, ordered |

**Idempotency:** `EventId` unique constraint. Lookup before downstream call.

---

### EF Core configuration (sketch)

```csharp
entity.HasKey(e => e.EventId);
entity.HasIndex(e => new { e.AccountId, e.EventTimestamp, e.EventId });
entity.Property(e => e.Amount).HasConversion(
    v => v.ToString("F2"),
    v => decimal.Parse(v));
```

---

## Account Service database (`account.db`)

### Entity: Account

| Column | Type | Notes |
|--------|------|-------|
| `AccountId` | `TEXT` PK | |
| `CreatedAt` | `TEXT` NOT NULL | UTC |
| `Currency` | `TEXT` NOT NULL | Set from first transaction |

**Indexes:** Primary key only.

---

### Entity: Transaction

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `INTEGER` PK AUTO | Surrogate key |
| `EventId` | `TEXT` NOT NULL | **Unique** — global idempotency |
| `AccountId` | `TEXT` NOT NULL | FK → Account |
| `Type` | `TEXT` NOT NULL | `CREDIT` \| `DEBIT` |
| `Amount` | `TEXT` NOT NULL | Decimal as string |
| `Currency` | `TEXT` NOT NULL | |
| `EventTimestamp` | `TEXT` NOT NULL | Business time |
| `AppliedAt` | `TEXT` NOT NULL | Processing time |

**Indexes:**

| Name | Columns | Purpose |
|------|---------|---------|
| `IX_Transaction_EventId` | `EventId` UNIQUE | Idempotency |
| `IX_Transaction_AccountId_EventTimestamp` | `AccountId`, `EventTimestamp` | History queries |

**Relationships:**

```
Account 1 ── * Transaction
```

---

### Balance computation

**No cached balance column** in v1 — computed on read:

```sql
SELECT
  COALESCE(SUM(CASE WHEN Type = 'CREDIT' THEN Amount ELSE 0 END), 0)
  - COALESCE(SUM(CASE WHEN Type = 'DEBIT' THEN Amount ELSE 0 END), 0)
FROM Transactions
WHERE AccountId = @accountId
```

Optional optimization (out of scope): materialized `Balance` updated in same transaction as insert — not required for take-home.

**Out-of-order:** Insert order irrelevant; sum is commutative.

**Negative balance:** Allowed (DEC-02). DEBIT always inserts if `eventId` is new.

---

## Idempotency keys summary

| Service | Key | Scope | On duplicate |
|---------|-----|-------|--------------|
| Gateway | `EventId` | Global | Return existing `EventRecord` |
| Account | `EventId` | Global | Return existing `Transaction` |

Global `EventId` uniqueness matches handout ("unique identifier for the event").

---

## Migration strategy

| Environment | Approach |
|-------------|----------|
| Development | `context.Database.EnsureCreated()` or EF migrations at startup |
| Docker | Migrations applied on container start |
| Tests | In-memory SQLite or temp file per test class |

---

## Data volume assumptions (take-home)

| Assumption | Value |
|------------|-------|
| Accounts | < 10,000 |
| Events per account | < 100,000 |
| Payload size | < 16 KB |

No partitioning or archival required.
