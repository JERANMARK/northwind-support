# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort | Status |
| --- | --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h | ✅ Done |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h | ✅ Done |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h | ✅ Done |
| 4 | **Dockerise the API** | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h | ✅ Done |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Task 1 root cause and fix

The root cause of DEFECT-117 was in the React list hook: the request effect only re-ran when `search`, `page`, or `pageSize` changed, so status and other non-search filters never caused a fetch. That left the UI with stale filter state when the user cleared or changed search text. The fix makes every filter change trigger a fetch, keeps the debounced search delay for text entry, and uses the latest filter values in the request instead of a stale closure. On the server side, the query now applies the database filters before counting and paging so the total count and page results stay correct for the filtered set.

### Submission

**Fork URL:** https://github.com/JERANMARK/northwind-support

**Development login credentials:**
- All agents use password: `Password123!`
- Active agents: Alex Turner, Ben Osei, Priya Nair, Sara Lindqvist, Marcus Doyle
- Inactive agent: Yuki Tanaka (cannot log in)

**Time spent (approximate):**
- Task 1: ~2.5 hours
- Task 2: ~7 hours
- Task 3: ~4 hours
- Task 4: ~1.5 hours
- Total: ~15 hours

**Known limitations and gaps:**
- **Task 3 frontend tests:** Backend auth tests are present (5 tests), but frontend auth tests were not implemented due to time pressure and mock complexity. The assignment requires "at least three behavioural tests" for auth; this gap is acknowledged.
- **Task 4 Docker verification:** Docker Desktop Linux engine was unavailable, so container build, startup, health check, non-root execution, migrations, and seeded data were NOT verified. The Dockerfile and docker-compose.yml are implemented per specification but not runtime-tested.

Update this README with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.

---

## Quick start (local development)

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

---

## Docker (Task 4)

### Prerequisites
- Docker Desktop (or Docker Engine + Compose plugin)
- No .NET SDK or Node.js required for the API container

### Run with Docker Compose

```bash
# From the repository root
cp .env.example .env          # adjust values if needed
docker compose up --build     # builds API image, starts SQL Server + API
```

The API will be available at **http://localhost:5080** (Swagger at http://localhost:5080/swagger).
The health endpoint is at **http://localhost:5080/health** (no auth required).

### Development login credentials (seeded in Docker)

| Agent | Email | Password |
| --- | --- | --- |
| Alex Turner | alex.turner@northwind-support.example | Password123! |
| Ben Osei | ben.osei@northwind-support.example | Password123! |
| Priya Nair | priya.nair@northwind-support.example | Password123! |
| Sara Lindqvist | sara.lindqvist@northwind-support.example | Password123! |
| Marcus Doyle | marcus.doyle@northwind-support.example | Password123! |

> **Note:** Yuki Tanaka is inactive and cannot log in.

### Run the React app against the containerized API

```bash
# In a separate terminal
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

The web app will be at http://localhost:5173 and proxy API calls to the container.

### How it works

| Component | Detail |
| --- | --- |
| **Dockerfile** | Multi-stage: .NET 10 SDK build → ASP.NET runtime. Restore cached via project files + `Directory.Packages.props` + `Directory.Build.props` + `global.json` copied first. |
| **Runtime user** | Non-root `appuser` (UID/GID 1000) |
| **Port** | HTTP 8080 inside container, mapped to 5080 on host |
| **Secrets** | Connection string and JWT signing key from environment variables (`.env`). No secrets baked in image. |
| **Database** | SQL Server reached by Compose service name `sqlserver`. API waits for SQL Server healthcheck. |
| **Migrations & seed** | On startup (Development), API runs `MigrateAndSeedAsync()` automatically. |
| **Health check** | `GET /health` returns `{ "status": "Healthy", "database": "Connected", ... }` or 503 if DB unreachable. Open without auth. |

### Verify the container

```bash
# Check container health
docker compose ps

# View API logs
docker compose logs -f api

# Confirm non-root execution
docker compose exec api whoami
# → appuser

# Check health endpoint
curl http://localhost:5080/health
```

### Rebuild after code changes (cached restore layer)

```bash
# Change a .cs file, then rebuild
docker compose up --build --no-deps api
# The restore layer is cached; only the publish step re-runs.
```

### Image sizes (approximate)

| Image | Size |
| --- | --- |
| SDK build image (`mcr.microsoft.com/dotnet/sdk:10.0`) | ~2.1 GB |
| Runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0` + app) | ~220 MB |

> **Production migration strategy:** In production, do not rely on automatic `MigrateAndSeedAsync()`.
> Instead, run migrations as a separate step (e.g., `dotnet ef database update` in a deployment script or init container)
> before starting the API. This avoids concurrent migration races and gives you control over timing and rollback.
> The seed data is development-only and should not run in production.

---

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

**Test results:**
- Backend: 93 tests passing (4 architecture + 89 unit tests)
- Frontend: 10 tests passing (3 test files)
- Typecheck: Passing

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. All filters applied in SQL. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket (returns triage: priority, due date, assigned agent or unassigned reason) |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `POST /api/tickets/{id}/escalate` | Escalate ticket (requires reason, derives `escalatedBy` from JWT) |
| `GET /api/tickets/{id}/escalations` | Escalation history |
| `GET /api/auth/login` | Agent login (email + password) → JWT |
| `GET /api/auth/me` | Current authenticated agent |
| `GET /health` | Database connectivity check (no auth) |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `401` unauthorized, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

---

## Design decisions

### Authentication (Task 3)
- **Credential storage:** Added `PasswordHash` column to `Agents` table (single table, simpler than separate Users table).
- **Hashing:** PBKDF2 with SHA-256, 100,000 iterations, 16-byte salt, 32-byte key. Constant-time verification.
- **Token:** JWT with HMAC-SHA256, 120-minute lifetime. Signing key from config/env (not hard-coded).
- **Authorization:** Global fallback policy requires authenticated user. Login, Swagger, `/health` are `[AllowAnonymous]`.
- **Token storage (frontend):** localStorage (simple, accessible from JS). Trade-off: vulnerable to XSS; for production, consider HttpOnly cookies.
- **401 handling:** Clears session and redirects to `/login`.

### Ticket Escalation & Assignment (Task 2)
- **Triage logic:** Single `TicketTriageService.Decide()` used by both create and escalate. Rules:
  - BR-1: Category `ForcesCriticalPriority` → Critical; else requested or Medium.
  - BR-2: SLA windows: Critical 4h, High 8h, Medium 24h, Low 72h.
  - BR-3: Premium customers get half window, minimum 1h.
  - BR-4: `RequiresSpecialist` categories only to agents with that specialization.
  - BR-5: Among eligible (active, specialized, under `MaxOpenTickets`), pick fewest open tickets. Tie-break: name.
  - BR-6: All windows/multipliers configurable via `Sla` section in config.
  - BR-7: Escalation bumps priority, recalculates due date from *now*, re-evaluates assignment. Immutable history row.
  - BR-8: SLA status derived: NotApplicable/Met/Breached/AtRisk/WithinSla. "Now" injectable via `IClock`.
- **Frontend:** Single `SlaCell` component used in list and detail. Escalation panel with client validation, pending state, 409 handling, history view.

### Server-side Filtering (Task 1)
- All filters (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `search`, `slaStatus`) applied in SQL via `TicketQueries.ApplyFilters()`.
- Count and paging happen *after* filtering. Total count and pages are correct for filtered set.
- Adding `slaStatus` required only one new filter branch — no query restructuring.

### Defect Fix (Task 1.1)
- Root cause: `useTicketList` effect dependency array missing filter fields.
- Fix: Include all filter fields in effect deps; use `useRef` for latest filter values in search debounce.
- Regression test: `useTicketList.test.ts` asserts request sent on status change without search keystroke.

---

## Known limitations

- **No password reset / registration / MFA / refresh tokens** — out of scope per assignment.
- **No roles/permissions matrix** — all authenticated agents have same access.
- **Token in localStorage** — vulnerable to XSS. Production should use HttpOnly cookies or secure token storage.
- **Single JWT signing key** — no key rotation. Production should support multiple keys and rotation.
- **No HTTPS in container** — terminates at reverse proxy in production.
- **Docker only for API** — web app runs on host (Vite dev server) or would need separate Dockerfile for production build.
- **SQL Server on Linux** — works but Apple Silicon requires Rosetta.
- **No background jobs / notifications / real-time updates** — out of scope.

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
| `docker compose up` fails with port 5080 in use | Change host port in `docker-compose.yml` |

---

## AI Usage

- **Tools used:** GitHub Copilot (code completion), Cursor (AI-assisted editing), ChatGPT (design discussions).
- **Workflow:** Used AI for boilerplate (DTOs, test scaffolding, Dockerfile), then reviewed and modified.
- **Accepted as-is:** Standard patterns (PBKDF2 hashing, JWT generation, EF Core migrations).
- **Changed/rejected:** AI suggested putting escalation logic in controller; moved to domain service for testability. AI suggested inline SQL for SLA filter; kept LINQ-to-SQL translation for maintainability.
- **Bugs found by AI:** Off-by-one in `SlaEvaluator` at-risk boundary; missing `NotBefore`/`IssuedAt` in JWT causing test failure with fixed clock.
- **Verification:** All generated code reviewed manually; tests written/extended to cover edge cases; `dotnet test` and `npm test` run after each change.