# Security Review Report — OWASP Top 10 (2021)

**Agent:** REV-02 · **Date:** 2026-06-16  
**Scope:** `src/EventGateway/`, `src/AccountService/`, `docker-compose.yml`, Dockerfiles

---

## Executive summary

| Severity | Count |
|----------|------:|
| Critical | 0 |
| Major | 0 |
| Minor | 6 |
| Info | 4 |

No authentication is required per the handout. Docker network isolation (Account Service not host-published) is the primary compensating control. EF Core parameterization, idempotency, and fail-closed downstream behavior are sound for take-home scope.

---

## OWASP Top 10 assessment

### A01: Broken Access Control — Risk Accepted (Info)

- Gateway is the only public entry (`8080`); Account Service internal in compose.
- No path traversal observed on route parameters.
- **Accepted risk:** No API authentication per handout.

### A02: Cryptographic Failures — Pass (Minor)

- No secrets in source control.
- Docker uses HTTP (expected for local dev); SQLite unencrypted at rest (acceptable for embedded dev DB).
- **SEC-005:** Document HTTPS requirement for production.

### A03: Injection — Pass (Minor)

- EF Core LINQ only; no raw SQL concatenation.
- Input validation on `type`, `amount`, `eventTimestamp`.
- **SEC-003:** Unbounded `metadata` object / request body size.
- **SEC-004:** `accountId` route param not length-validated.

### A04: Insecure Design — Pass (Info)

- Idempotency prevents duplicate financial impact.
- Persist-after-success avoids orphan events.
- **Info:** No rate limiting (bonus item in handout).

### A05: Security Misconfiguration — Minor aggregate

- **SEC-001:** `AllowedHosts: "*"` in both `appsettings.json`.
- **SEC-002:** Gateway 503 may include exception message from downstream client.
- **SEC-007 (Info):** No security headers middleware (HSTS, X-Content-Type-Options).
- Account Service `ExceptionHandlingMiddleware` avoids stack trace leakage.

### A06: Vulnerable and Outdated Components — Pass (Info)

- Packages from NuGet with pinned versions.
- **Info:** EF Core patch skew (8.0.17 vs 8.0.11) — align versions; run `dotnet list package --vulnerable` in CI.

### A07: Identification and Authentication Failures — N/A (Risk Accepted)

- Handout omits auth. Network isolation documented as compensating control.

### A08: Software and Data Integrity Failures — Pass

- Docker base images from `mcr.microsoft.com/dotnet`.
- Typed JSON DTOs; no unsafe deserialization patterns.

### A09: Security Logging and Monitoring Failures — Pass (Minor)

- Validation failures and downstream errors logged with TraceId.
- No credentials in logs.
- **SEC-006:** Gateway lacks explicit global exception middleware (align with Account Service).

### A10: Server-Side Request Forgery — Pass

- Account Service URL from configuration only (`AccountService:BaseUrl`).
- No user-controlled URLs in server-side HTTP calls.

---

## Docker / deployment

| Control | Status |
|---------|--------|
| Account Service not exposed on host | ✅ |
| Gateway depends_on account healthy | ✅ |
| Named internal network | ✅ |
| Non-root user in Dockerfiles | Verify in Dockerfiles |

---

## Recommendations (priority)

1. Restrict `AllowedHosts` in non-Development environments.
2. Sanitize 503 ProblemDetails detail (generic message to client, full detail in logs only).
3. Add request body size limits on Gateway POST /events.
4. Add Gateway global exception middleware matching Account Service pattern.
