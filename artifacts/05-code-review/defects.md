# Event Ledger — Review Defects

**Sources:** REV-01 (code quality) + REV-02 (OWASP security)  
**Date:** 2026-06-16

## Summary

| Source | Critical | Major | Minor | Info |
|--------|----------|-------|-------|------|
| Code (REV-01) | 0 | 4 | 5 | 6 |
| Security (REV-02) | 0 | 0 | 6 | 4 |

**T-18c remediation required:** Yes — **4 Major** code defects (REV-01-M01..M04). Security has no Major/Critical.

---

## Code defects (REV-01)

| ID | Severity | Location | Description | Owner | Status |
|----|----------|----------|-------------|-------|--------|
| REV-01-M01 | **Major** | `EventGateway/Services/EventService.cs` | Concurrent duplicate `eventId` POST can throw `DbUpdateException` → 500 | DEV-01 | **Fixed** |
| REV-01-M02 | **Major** | `EventGateway/Program.cs` | No global ProblemDetails exception middleware | DEV-01 | **Fixed** |
| REV-01-M03 | **Major** | `tests/` | Missing integration, Polly, trace tests (NFR-15–18) | QA-01..04 | **Fixed** (qa-wave-2) |
| REV-01-M04 | **Major** | `*.csproj` | EF Core version mismatch 8.0.17 vs 8.0.11 | DEV-03 | **Fixed** |
| REV-01-m01 | Minor | `EventEndpoints.cs` | No request body size limit | DEV-01 | Open |
| REV-01-m02 | Minor | `EventService.cs` | Loose metadata deserialization type | DEV-01 | Open |

---

## Security defects (REV-02)

| ID | Severity | OWASP | CWE | Location | Description | Owner | Status |
|----|----------|-------|-----|----------|-------------|-------|--------|
| SEC-001 | Minor | A05 | CWE-16 | `appsettings.json` (both) | `AllowedHosts: "*"` | DEV-03 | **Fixed** |
| SEC-002 | Minor | A05 | CWE-209 | `AccountServiceClient.cs` | Exception message may leak in 503 detail | DEV-01 | **Fixed** |
| SEC-003 | Minor | A03 | CWE-400 | `EventRequest.Metadata` | Unbounded metadata / body size | DEV-01 | **Fixed** (64KB limit) |
| SEC-004 | Minor | A03 | CWE-20 | `AccountEndpoints.cs` | No `accountId` length validation | DEV-02 | **Fixed** |
| SEC-006 | Minor | A09 | — | `EventGateway/Program.cs` | Missing explicit exception middleware | DEV-01 | **Fixed** |
| SEC-007 | Info | A05 | — | Both services | No security headers middleware | DEV-03 | Open |
| SEC-008 | Info | A04 | — | Gateway | No rate limiting | — | Accepted |
| SEC-009 | Info | A06 | — | `*.csproj` | Package vulnerability scan not in CI | DEV-03 | Open |
| SEC-010 | Info | A01 | — | System | No API auth (handout scope) | — | Risk Accepted |

---

## Remediation plan (T-18c)

| Priority | IDs | Agent | Action |
|----------|-----|-------|--------|
| P0 | REV-01-M01, REV-01-M02, SEC-006 | DEV-01 | Idempotency race + global exception handler |
| P1 | REV-01-M04, SEC-001 | DEV-03 | Align EF versions; tighten AllowedHosts for Docker |
| P2 | SEC-002, SEC-003 | DEV-01 | Sanitize 503; body size limit |
| P3 | SEC-004 | DEV-02 | Route param length validation |
| QA phase | REV-01-M03 | QA-01..04 | Integration, resiliency, trace tests |

---

## Sign-off

- [ ] Developer approved review-wave-1
- [ ] T-18c remediation complete
- [ ] Re-review spot-check (if needed)
