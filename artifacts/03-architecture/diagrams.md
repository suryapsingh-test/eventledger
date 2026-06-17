# Event Ledger — Architecture Diagrams

**Task:** T-04 · **Agent:** ARCH-01  
**Format:** Mermaid

---

## 1. System context

```mermaid
C4Context
  title System Context — Event Ledger

  Person(client, "Upstream Client", "Submits transaction events")
  System(gateway, "Event Gateway", "Public API — validate, idempotency, audit")
  System(account, "Account Service", "Internal — balances and transactions")

  Rel(client, gateway, "POST/GET /events", "HTTP/JSON")
  Rel(gateway, account, "POST /accounts/{id}/transactions", "HTTP/JSON")
```

---

## 2. Container diagram

```mermaid
flowchart TB
  subgraph Client["External"]
    C[Upstream Systems]
  end

  subgraph Host["Docker Host"]
    subgraph GW["Event Gateway :8080"]
      GAPI[ASP.NET Core API]
      GDB[(SQLite gateway.db)]
      GAPI --> GDB
    end

    subgraph ACC["Account Service :8081 internal"]
      AAPI[ASP.NET Core API]
      ADB[(SQLite account.db)]
      AAPI --> ADB
    end
  end

  C -->|POST/GET /events| GAPI
  GAPI -->|POST /transactions + traceparent| AAPI
```

---

## 3. Sequence — happy path POST /events

```mermaid
sequenceDiagram
  autonumber
  participant Client
  participant Gateway
  participant GatewayDB as Gateway SQLite
  participant Account
  participant AccountDB as Account SQLite

  Client->>Gateway: POST /events (eventId=evt-001)
  Gateway->>Gateway: Validate payload
  Gateway->>GatewayDB: SELECT by eventId
  GatewayDB-->>Gateway: not found

  Gateway->>Account: POST /accounts/acct-123/transactions
  Note over Gateway,Account: traceparent header propagated

  Account->>AccountDB: BEGIN; upsert Account; INSERT Transaction
  AccountDB-->>Account: OK
  Account-->>Gateway: 201 TransactionResponse

  Gateway->>GatewayDB: INSERT EventRecord
  Gateway-->>Client: 201 EventResponse
```

---

## 4. Sequence — idempotent duplicate

```mermaid
sequenceDiagram
  participant Client
  participant Gateway
  participant GatewayDB as Gateway SQLite
  participant Account

  Client->>Gateway: POST /events (eventId=evt-001, duplicate)
  Gateway->>GatewayDB: SELECT by eventId
  GatewayDB-->>Gateway: EventRecord exists
  Gateway-->>Client: 200 EventResponse (Idempotency-Replay: true)

  Note over Gateway,Account: Account NOT called
```

---

## 5. Sequence — Account Service unavailable

```mermaid
sequenceDiagram
  participant Client
  participant Gateway
  participant GatewayDB as Gateway SQLite
  participant Account

  Client->>Gateway: POST /events (new eventId)
  Gateway->>GatewayDB: SELECT by eventId
  GatewayDB-->>Gateway: not found

  Gateway->>Account: POST /transactions
  Account--xGateway: timeout / connection refused

  Note over Gateway: Polly circuit breaker / timeout
  Gateway-->>Client: 503 ProblemDetails

  Note over GatewayDB: No event persisted
```

---

## 6. Sequence — GET events while Account down

```mermaid
sequenceDiagram
  participant Client
  participant Gateway
  participant GatewayDB as Gateway SQLite
  participant Account

  Note over Account: Account Service stopped

  Client->>Gateway: GET /events?account=acct-123
  Gateway->>GatewayDB: SELECT ORDER BY eventTimestamp
  GatewayDB-->>Gateway: rows
  Gateway-->>Client: 200 EventResponse[]

  Note over Gateway,Account: No Account call
```

---

## 7. Component diagram — Event Gateway

```mermaid
flowchart LR
  subgraph EventGateway
    EP[Endpoints]
    VAL[EventValidator]
    IDEM[IdempotencyService]
    AUD[EventAuditStore]
    CL[AccountServiceClient]
    EP --> VAL
    EP --> IDEM
    EP --> CL
    EP --> AUD
    IDEM --> AUD
    CL -->|HttpClient + Polly| EXT[Account Service]
  end
```

---

## 8. Deployment — Docker Compose

```mermaid
flowchart TB
  subgraph compose["docker-compose"]
    direction TB
    GW[event-gateway<br/>ports: 8080:8080]
    ACC[account-service<br/>expose: 8081]
    GW -->|depends_on healthy| ACC
  end

  User[Developer / Client] -->|localhost:8080| GW
  GW -.->|internal network| ACC
```
