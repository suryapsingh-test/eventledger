# Event Ledger — Edge Cases

**Task:** T-03 · **Agent:** BA-01  
**Purpose:** Scenarios beyond happy path for architecture, implementation, and QA design.

---

## 1. Idempotency and duplicates

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-01** | Exact duplicate: same `eventId`, identical body re-submitted | Return original response; no balance change; single stored event | AC-03 |
| **EC-02** | Same `eventId`, **different** body (e.g., different `amount`) | Treat as duplicate by `eventId` — return original, reject conflicting replay (recommended); architect must document choice | QA-01 |
| **EC-03** | Concurrent duplicate submissions (two parallel POSTs with same `eventId`) | Only one transaction applied; no double balance change; deterministic response | QA-03 |
| **EC-04** | Duplicate arrives after Account Service temporarily failed on first attempt | If first attempt did not persist success, retry may succeed once; if first succeeded, duplicate is idempotent | QA-04 |

---

## 2. Out-of-order arrival

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-05** | DEBIT arrives before CREDIT (by `eventTimestamp`) but CREDIT submitted first | Balance reflects chronological order by `eventTimestamp`, not arrival order | AC-08 |
| **EC-06** | Multiple events same `eventTimestamp` | Stable ordering tie-break (e.g., `eventId` or arrival order) — architect to define; listing order consistent | QA-01 |
| **EC-07** | Event with `eventTimestamp` far in the future | Accepted if valid ISO 8601; appears last in chronological listing | QA-01 |
| **EC-08** | Event with `eventTimestamp` before existing events | Balance recalculated correctly; listing inserts at correct position | AC-08 |

---

## 3. Validation boundaries

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-09** | `amount` with many decimal places (e.g., 0.001) | Accept if > 0; precision rules documented (decimal vs double) | QA-01 |
| **EC-10** | Very large `amount` | Accept within numeric limits; no silent overflow | QA-02 |
| **EC-11** | Empty string `eventId` or `accountId` | 400 validation error | AC-04 |
| **EC-12** | Malformed `eventTimestamp` (not ISO 8601) | 400 validation error | QA-01 |
| **EC-13** | Missing `Content-Type: application/json` or invalid JSON body | 400 with meaningful error | QA-01 |
| **EC-14** | `metadata` null vs omitted vs empty object | All acceptable; no validation failure | QA-01 |
| **EC-15** | Unknown JSON fields in payload | Ignored or rejected — architect to document (recommend ignore) | QA-01 |

---

## 4. Account and balance

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-16** | DEBIT exceeding current balance | Handout silent — recommend allow negative balance OR reject with 422; architect must decide and document | QA-02 |
| **EC-17** | First transaction on new `accountId` | Account auto-created or 404 — architect to define; balance starts at 0 before first CREDIT | QA-02 |
| **EC-18** | Balance query for unknown account | 404 or zero balance — document contract | QA-02 |
| **EC-19** | Many transactions on one account | Balance remains correct; list/query performance acceptable for take-home scope | QA-03 |

---

## 5. Account Service failures

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-20** | Account Service down on POST | 503 (or documented) within timeout; no orphan event if policy is persist-after-success | AC-13 |
| **EC-21** | Account Service slow (exceeds timeout) | Timeout triggers; client receives error; no hang | AC-18 |
| **EC-22** | Account Service returns 500 | Gateway maps to appropriate client error; resiliency policy counts failure | QA-04 |
| **EC-23** | Account Service recovers after circuit open | Circuit half-open / recovery — requests succeed again; document behavior | QA-04 |
| **EC-24** | Intermittent failures (flaky downstream) | Retry policy (if used) respects backoff limits; no infinite retry | QA-04 |
| **EC-25** | GET events works while POST fails (Account down) | Read path unaffected | AC-14 |

---

## 6. Tracing and observability

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-26** | Client sends incoming trace/correlation header | Gateway may honor or replace — document; downstream must log consistent ID | AC-17 |
| **EC-27** | Failed POST still logs trace ID on Gateway | Logs present for troubleshooting even on failure | AC-16 |
| **EC-28** | Health check when database unavailable | `/health` returns unhealthy status (503) | AC-12 |
| **EC-29** | Metric increments on validation failure vs downstream failure | Custom metric distinguishes or aggregates — either acceptable if documented | AC-19 |

---

## 7. Security and abuse (handout + OWASP prep)

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-30** | Oversized JSON payload | Reject with 413 or 400; no crash | REV-02 / QA-01 |
| **EC-31** | SQL/script injection in string fields | Stored safely; no injection — parameterized queries | REV-02 |
| **EC-32** | Direct call to Account Service from external client | Not required to block in take-home if network-isolated in Docker; document trust boundary | ARCH |

---

## 8. Operational

| ID | Scenario | Expected behavior | QA ref |
|----|----------|-------------------|--------|
| **EC-33** | Gateway restarted; Account running | Previously stored events still readable; balance in Account DB intact | QA-03 |
| **EC-34** | Account restarted; Gateway running | Gateway events intact; balance rebuilt from Account DB transactions | QA-03 |
| **EC-35** | Both services fresh start | Empty state; first event creates account/balance as designed | QA-03 |

---

## 9. Decisions required before implementation

| ID | Question | Owner |
|----|----------|-------|
| **DEC-01** | Same `eventId`, conflicting payload — reject or return original? | ARCH-01 |
| **DEC-02** | Insufficient funds on DEBIT — allow negative or reject? | ARCH-01 + developer approval |
| **DEC-03** | Auto-create account on first transaction? | ARCH-01 |
| **DEC-04** | Duplicate HTTP status code (200 vs 409) | ARCH-01 |
| **DEC-05** | Persist Gateway event before or after Account success | ARCH-01 (sprint plan recommends after success) |
