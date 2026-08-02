# Secretary — decision record

Every top-level decision taken during design, with the reasoning and what was rejected.
Companion documents: [`telephony-carrier-checklist.md`](./telephony-carrier-checklist.md).

**Nothing is built yet.** This is the agreed starting point.

---

## A. Product and repository

| # | decision | why |
|---|---|---|
| A1 | **Greenfield rebuild** in a new repo (`memoz1981/secretary`). The Appointment repo is not refactored. | Two structural changes (modules, providers) touch every file. Rebuilding is cheaper than migrating, and the old system keeps running. |
| A2 | **Multi-module platform.** A tenant is granted one or more modules by the platform admin. | The landing page advertises six modules; the backend knows only appointments. |
| A3 | **Appointment module first**, built as one module among many — never as "the app". | The current codebase's core problem is that one module wears the whole solution's name. |
| A4 | Product name stays **sekretar.az**; Lamiya stays the agent persona. | Carried over unchanged. |

## B. Front end

| # | decision | why |
|---|---|---|
| B1 | **Per-module route namespaces** — `/appointments/*`, `/feedback/*`. | Makes a module guard one check on a subtree, and makes lazy-loading trivial. |
| B2 | **No two modules share a view.** Each module has its own pages. | A tenant runs one module 99.99% of the time; cross-module views serve almost nobody and complicate everyone. |
| B3 | **`/app` is the single resolver** — one module redirects straight through, 2+ renders a picker styled like the landing page. Login always navigates to `/app`, never to a module. | One place makes the decision, so refresh, deep links and post-logout all behave the same. |
| B4 | **Only `/admin` is tenant-level.** Everything else lives inside a module. | Business details and staff accounts are one fact; a per-module copy is three places to change one phone number. |
| B5 | **Separate pages, shared data.** `Clients` and `Calls` remain single tenant-level tables (with a module discriminator on `Calls`); each module shows a filtered view. | Duplicating a customer record per module is a data bug, not module separation. Nothing mixed is ever displayed. |
| B6 | **Module manifest registry.** Each module declares key, prefix, home, nav, icon, routes, translation keys in one file. | Adding a module is one folder plus one line — nothing in `App.tsx`, nothing in a shared nav file. |
| B7 | UI stays **trilingual** (az default, ru, en) with the `Record<Language, string>` discipline. | A missing translation must stay a build error. |

**Rejected:** cross-module call log, unified client page, a persistent module switcher in the shell (revisit only if multi-module tenants become common).

## C. Backend structure

| # | decision | why |
|---|---|---|
| C1 | **Module folders inside each layer** — `Infrastructure/Modules/Appointment/` — not a project per module. | Compiler-enforced boundaries earn their keep when several people can violate them. This is a solo codebase. Folders promote to projects mechanically later. |
| C2 | **`Secretary.*` namespaces.** | `AiAppointment.Modules.Feedback` names the solution after one module. |
| C3 | `Secretary.Voice` holds the provider-agnostic voice layer; **no vendor SDK** in `Secretary.Voice` or `Secretary.Agents`. | This is the test that the layering is real. Today's `Agents` project references OpenAI. |
| C4 | `Secretary.Api` is the only composition root referencing provider and gateway projects. | |
| C5 | **PostgreSQL is the target, SQL Server runs locally until the production deploy.** Both EF providers are referenced; `Infrastructure/DependencyInjection.cs` picks. | Greenfield was the only cheap moment to commit to Postgres: managed SQL Server is expensive everywhere except Azure, which pinned hosting to one vendor, while Postgres is cheap on every host. Staying on SQL Server for now keeps local development on a stack that is already installed and working, and the switch is deliberately kept to one line. |
| C6 | **Start from an empty migration.** No data is carried over from the Appointment system; the first migration is generated from the new model. | New project. Inheriting the old schema would import the very structure this rebuild exists to replace, including its one-way int-PK migration history. |

**Switching at deploy** is `UseSqlServer` → `UseNpgsql`, plus `Hangfire.SqlServer` →
`Hangfire.PostgreSql`. A PostgreSQL `InitialCreate` is already generated and parked at
`Common/Persistence/Migrations.Postgres`, excluded from the build via `<Compile Remove>` so its
PostgreSQL-specific SQL can never be applied to SQL Server by accident.

Nothing in the data layer is provider-specific: no column types are hardcoded (nine properties
that pinned `datetime2(7)` were cleared), and index filters double-quote their identifiers —
the one form SQL Server, PostgreSQL and SQLite all accept. Unquoted, PostgreSQL folds the name
to lowercase and cannot match EF's PascalCase columns.

Postgres conventions still to settle at the switch, all cheap then and tedious later:
**snake_case** (`EFCore.NamingConventions`), **`timestamptz` with UTC at the boundary** and Baku
time only for display and the agent, **`citext`** for email so uniqueness is case-insensitive,
and `Npgsql.NodaTime` if NodaTime stays in the stack.

Projects: `Domain`, `Application`, `Infrastructure`, `Api`, `Voice`, `Voice.OpenAi`, `Voice.Google`, `Telephony.Browser`, `Telephony.Twilio`, `Agents` — plus mirrored test projects.

## D. Voice architecture

| # | decision | why |
|---|---|---|
| D1 | **Chained STT → text → TTS is dropped entirely.** Realtime speech-to-speech only. | Called "a poor decision" by the owner. Slower, worse at Azerbaijani, and the cost saving didn't justify it. |
| D2 | **Azure Speech is dropped.** | Its only purpose was Azure Communication Services — see E1. Realtime models leave no seam for external STT/TTS. |
| D3 | **Named string voice options**, not a `V1`/`V2` int enum. Key persisted on `Call`. | A model rename must never be a database migration. |
| D4 | **OpenAI Realtime and Gemini Live both, from the start.** | Gemini is ~4× cheaper; the comparison is only meaningful if both are first-class. |
| D5 | **One `IRealtimeSession` abstraction covers both.** Per-provider `IToolSchemaTranslator` and `IUsageTranslator` isolate the differences. | Both are audio-in/audio-out websockets with tool calls and usage events — identical lifecycle, different envelopes. |
| D6 | **`Google.GenAI`** (official `googleapis/dotnet-genai`) for both Gemini text and Live. | It has a `Live` class with `ConnectAsync()`, and supports both backends. No community package needed. |
| D7 | **AI Studio for demo, Enterprise Agent Platform (ex-Vertex) for prod**, selected by one config flag. | The `Client` constructor takes `enterprise` / `apiKey` / `credential` / `project` / `location`. Everything downstream is identical. |

Initial catalogue:

```
openai.realtime.gpt-realtime          production baseline
openai.realtime.gpt-realtime-mini     ~3x cheaper, quality unproven
google.live.gemini-live               ~4x cheaper, Azerbaijani unverified
```

## E. Telephony

| # | decision | why |
|---|---|---|
| E1 | **Azure Communication Services is out.** | ACS does not offer phone numbers in Azerbaijan at all. This was the sole reason Azure was being considered, and it was never going to work. |
| E2 | **Media-gateway seam, not provider-native SIP.** The call terminates at our infrastructure; audio frames go to whichever provider. | OpenAI's native SIP routes carrier→OpenAI, so our server never sees the media — Gemini could then never answer that number. It forecloses the exact flexibility we want. |
| E3 | **Self-hosted SIP is dropped.** | 2–4 weeks to harden, plus permanent operational burden and toll-fraud exposure, to save ~$0.012/min. Bad trade at any realistic volume. |
| E4 | **Two gateway implementations: Browser (now) and Twilio Media Streams (production).** | |
| E5 | **Two-level abstraction:** `ITelephonyGateway` (signalling, listen/place) hands out `ICallChannel` (per-call media and control). Capability flags, not exceptions, for unsupported operations. | Every transport considered — browser, Twilio, WhatsApp, and self-hosted SIP before it was dropped — splits cleanly into signalling and media. |
| E6 | **Twilio BYOC Trunking** keeps the +994 number with the local carrier — no porting. | Generally available; carrier points a trunk at a Twilio termination SIP domain. |
| E7 | **No authentication on calls.** A caller is anonymous. Tenant identity is *passed into* the call session, never read ambiently from a JWT. | Identifying which tenant's agent answers is routing, not security. Passing it in also makes the session testable. |
| E8 | **WhatsApp Business Calling is a planned channel, not a new gateway.** It routes through Twilio Programmable Voice, so it reuses `Telephony.Twilio` with `CallInfo` reporting a WhatsApp channel rather than a phone line. Build after step 6. | Generally available on Twilio. The `ITelephonyGateway` seam absorbs it at near-zero cost. |

**Why WhatsApp matters more than convenience.** Two strategic advantages:

1. **It bypasses K4 entirely.** No +994 DID, no carrier interconnect, no BYOC. WhatsApp could reach
   production *before* PSTN, with the phone line following later.
2. **Opus wideband instead of 8 kHz G.711.** It skips the PSTN, and narrowband measurably degrades
   speech recognition. Azerbaijani recognition quality is the constraint that has shaped this whole
   project, so this is a real quality lever, not a marginal one.

Combined with WhatsApp's penetration in Azerbaijan, it is a stronger channel than it first appears.

**Caveats, all real:**

- **Inbound requires the customer to already have the business in WhatsApp.** Walk-up customers dial
  the number on the door or Google Maps. So WhatsApp is complementary to PSTN — though plausibly
  *primary* for a business already running its customer comms there, which is common locally.
- **Outbound requires explicit user permission**, which directly complicates Reminders, Feedback and
  Surveys — all outbound modules. Understand the permission flow before planning those.
- **Availability varies by BSP and market**, and business-initiated calling is restricted in some
  countries.
- **WABA setup and Meta business verification** are required — process friction, not engineering.
- **Meta policy dependency.** They set the terms and can change them.

## F. Agents

| # | decision | why |
|---|---|---|
| F1 | **`AgentBriefing` is the single contract between the module axis and the provider axis** — rendered instructions + provider-neutral `AIFunction` tools. | A module never names a provider; a provider never names a module. |
| F2 | **Tool groups registered by name**, scoped `Shared` or `Module`. `clients`, `call-control`, `escalation` are shared. | Reuse across modules without redefinition. |
| F3 | **Separate, complete instruction file per (module × provider).** No shared base, no deltas. Start as identical copies, then optimise one at a time. | Prompt regressions are silent and probabilistic — there is no compiler or test to catch a shared edit breaking the other provider. Duplication is cheaper than coupling here. |
| F4 | **Fail loudly on a missing (module × provider) instruction file** — validate at startup, refuse to boot. | A silent fallback would run Gemini on OpenAI-tuned instructions and poison the comparison. Same discipline as the existing rate-card check. |
| F5 | **Call classification is module-specific.** Store module key + a module-owned outcome, not one global enum. | Today's enum is appointment-flavoured; Feedback has entirely different outcomes. |
| F6 | Data injection (services, providers, hours, Baku date) stays **shared code**, only prose is duplicated. | A bug in rendered context is deterministic and testable — fixing it twice is waste. |

Layout: `Modules/Appointment/Instructions/{openai.md, gemini.md}`.

## G. Authorization

| # | decision | why |
|---|---|---|
| G1 | **`TenantModule` table** `(Id, TenantId, Module, Status, EnabledAt, DisabledAt)`, unique on `(TenantId, Module)`. Platform admin toggles it. | Source of truth for module access. |
| G2 | Enforced in **three places**: the table, an endpoint policy on `/api/{module}/*`, and the query filter. | A tenant without the module gets 403, not an empty list. |
| G3 | **Per-request cached, not a JWT claim.** | An admin enabling a module takes effect on the next request, not the next login. |
| G4 | ⚠️ **Platform admins have `TenantId == null`** — the query filter needs the null-admin escape from day one. | Otherwise the one account that administers `TenantModule` can't see it. |

## H. Hosting

| # | decision | why |
|---|---|---|
| H1 | **Azure, Germany West Central (Frankfurt).** App Service B1 + Azure Database for PostgreSQL Flexible Server B1ms, **~$28/month**. | Twilio's nearest edge is Frankfurt and the media path is carrier→Twilio→app, so the app belongs near Frankfurt, not near Baku. ⚠️ Note the original justification for Azure was that Azure SQL is the only cheap managed SQL Server — **C5 voided that**. Azure remains a reasonable choice on familiarity and single-vendor simplicity, but it is no longer the forced one. |
| H2 | **Static frontend on a CDN free tier** (Cloudflare Pages / Azure Static Web Apps). | Don't pay to serve static files. |
| H3 | ⚠️ **Never scale to zero.** Minimum one always-on instance. | Long-lived websockets to both Twilio and the model — scale-to-zero drops live calls. Rules out consumption-plan Functions and Lambda entirely. |
| H4 | ⚠️ **Configure websocket idle timeout explicitly.** App Service defaults to 230 s. | Otherwise a call dies just under four minutes and looks like a model bug. |

Alternatives, now all viable on Postgres: **Neon** Frankfurt (usable free tier — worth taking for
the demo phase at $0), **Fly.io** `fra` ~$15/mo, **Hetzner** ~$5/mo but high ops, **GCP Cloud Run**
europe-west3 ~$35/mo. AWS was ~$55/mo only because of the RDS SQL Server licence, so it is now
competitive too.

## I. Reference costs

Per million tokens:

| model | audio in | audio out |
|---|---|---|
| gpt-realtime-2.1 | $32 | $64 |
| gpt-realtime-2.1-mini | $10 | $20 |
| Gemini 2.5 Flash native audio | $3 | $12 |

Realistic per-minute (context is re-billed each turn, roughly doubling naive math):

| | $/min | 3-min call, incl. Twilio |
|---|---|---|
| OpenAI Realtime | ~$0.10 | ~$0.34 |
| OpenAI Realtime mini | ~$0.03 | ~$0.13 |
| Gemini Live | ~$0.025 | ~$0.11 |
| Twilio voice + Media Streams | ~$0.012 | |

The measured figure from the existing system — **~$0.10/min, ~$0.15 per call** — is the trustworthy anchor.

**Gemini Live limits:** ~10 min websocket lifetime, 15 min audio session, 128k context, **3 concurrent sessions on free tier**. Session resumption handles reconnect; handles valid 2 h.

## J. Build order

1. Solution skeleton — Domain + Infrastructure `Common/`, auth, tenancy, `TenantModule`
2. Appointment module end to end — entities, endpoints, tools — no voice
3. `Secretary.Voice` + `Telephony.Browser` + `Voice.OpenAi` → a working browser call
4. `Voice.Google` → Gemini Live on the same seam — **first real test of the abstraction**
5. Frontend — module registry, `/app` resolver, Appointment pages
6. `Telephony.Twilio` → real phone call on the same code path
7. Second module (Feedback) → first real test of tool reuse
8. WhatsApp Business Calling channel — reuses step 6's gateway (may move earlier if K10 resolves before K4)

Step 4 is deliberately early: if `IRealtimeSession` can't absorb Gemini Live cleanly, that must surface before three more things are built on top of it.

## K. Open questions

| | question | blocks |
|---|---|---|
| K1 | ~~SQL Server or Postgres?~~ ✅ **Resolved — Postgres. See C5.** | — |
| K2 | ~~Gemini's Azerbaijani quality~~ ✅ **Answered by a real call, 2026-08-02: it works, and the quality is better than OpenAI's.** That was the question the whole two-provider design existed to settle. Tuning items below. | — |
| K3 | **Gemini paid-tier concurrency** — free tier is 3 sessions. Need the paid number before load testing means anything. | production |
| K4 | **+994 carrier interconnect** — see the carrier checklist. | step 6 only |
| K5 | **Voice option per tenant, or per tenant × module?** Per-tenant now; outbound campaigns will eventually want a cheaper model than inbound booking. | later |
| K6 | ~~Migrate Appointment production data, or start clean?~~ ✅ **Resolved — start clean, empty migration. See C6.** | — |
| K7 | AI Studio paid quotas — if too thin, prod is forced to Enterprise sooner than planned. | production |
| K8 | **Does Media Streams work on WhatsApp Business Calling?** Same crux question as BYOC — the whole channel depends on it. | WhatsApp channel |
| K9 | **What audio format arrives from WhatsApp via Media Streams?** If Twilio transcodes Opus down to μ-law 8 kHz, the quality advantage in E8 disappears. If wideband passes through, we need to handle it — and want to. | WhatsApp channel |
| K10 | **Is WhatsApp Business Calling available for Azerbaijan**, inbound and outbound? | WhatsApp channel |

⚠️ **K8–K10 belong in the same Twilio conversation as K4.** The answers could reorder the path to
production — if WhatsApp is available and the carrier interconnect is slow, WhatsApp ships first.

## M. Gemini tuning — found on the first real call

Gemini Live works, and on Azerbaijani it sounds better than OpenAI. Three things need fixing,
all deferred to their own change so the provider itself could land clean.

| | symptom | where to look |
|---|---|---|
| M1 | **Speaks too fast** — words run together and stop being clear, though the voice itself is better than OpenAI's. | Try the instruction first, it costs nothing. If prompting won't hold the pace, check whether `SpeechConfig` exposes a rate, and try other prebuilt voices — `Aoede` was chosen without evidence. |
| M2 | **A pause around provider selection**, just before or just after; the exact moment wasn't captured. | Almost certainly the tool round-trip. Reproduce against the session log, which records every tool call and result, and check whether the follow-up was swallowed or fired twice. Likely the same root cause as M3. |
| M3 | **No goodbye.** The call sat silent for 5–10 seconds, then hung up. | **Probable cause, check this first:** the orchestrator handles `EndCall` by sending the tool output, then a dictated goodbye turn, and hangs up on the *next* `ResponseFinished`. But Gemini continues a turn by itself once a tool result lands — so that auto-continuation almost certainly produces the next `ResponseFinished`, and the hang-up fires on it before the goodbye has generated. `GeminiLiveSession.StartResponseAsync` deliberately does not swallow a dictated turn, but nothing stops the auto-turn racing it. |

A fourth item surfaced from the same call log, and it was the serious one:

| | symptom | cause |
|---|---|---|
| M4 | **A booking failed mid-call** with "A second operation was started on this context instance", after the caller's number was not recognised. | **Gemini asks for several tools at once** — its `toolCall` message carries a *list* of function calls — and the orchestrator runs each off the event loop without awaiting. Two then hit the one request-scoped `DbContext` simultaneously, and EF Core is not thread-safe. OpenAI sends function calls one at a time, which is why this never showed. |

### All four fixed, 2026-08-02

- **M1 — not fixed, deliberately.** A pacing instruction was written and then reverted: on more
  listening the speed is good, and slowing the agent down would cost the thing that makes Gemini
  feel better than OpenAI. Revisit only if real callers struggle. Worth knowing for that day:
  `SpeechConfig` exposes no rate, so pace can only be asked for in words or changed by trying a
  different prebuilt voice.
- **M2 / M3** — `IRealtimeSession.ContinuesTurnAfterToolResult`. The orchestrator now knows which
  providers answer on their own, skips its own follow-up for those, and on the hang-up path sends
  the farewell wording *with* the tool result so the automatic turn speaks it. Previously the
  automatic turn raced the dictated goodbye, won, and the hang-up fired on its completion — five
  seconds of silence instead of a farewell. The earlier one-shot suppression flag inside the
  Gemini session is gone: it stopped a double reply but could not order two turns.
- **M4** — `RealtimeToolInvoker` runs one tool at a time. A gate rather than a `DbContext` per
  tool, because the toolset is resolved once per call; serial execution costs nothing measurable
  while the caller is already hearing audio.
- **Phone numbers are canonicalised** on write and on lookup (`PhoneNumberNormalizer`), which is
  why the caller was not recognised: numbers arrive as speech and were stored verbatim, leaving
  two "Mehdi" rows — `0535353535` and `055 250 58 32`.

⚠️ **Existing rows were not back-filled.** Clients created before this keep their as-spoken
number and will not match a normalised lookup. Fine for the demo data; a real deployment needs a
one-off migration, and duplicates merged before any unique index goes on the column.

## L. Document map

| file | holds |
|---|---|
| `decisions.md` | this file — every decision, its reasoning, and what was rejected |
| `architecture.md` | the *how* — interface shapes, seams, solution layout, build order |
| `telephony-carrier-checklist.md` | what to ask the Azerbaijani carrier and Twilio before production |

The earlier `docs/architecture.md` has been folded into `design/architecture.md` and the `docs/` folder removed — it predated decisions **E3** (self-hosted SIP dropped), **F3** (per-provider instruction copies), **C5** (Postgres) and **C6** (clean start).
