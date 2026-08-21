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

- **M1 — the general slowdown was rejected, a narrow fix took its place.** A blanket pacing
  instruction was written and reverted: the speed is good, and slowing the agent down would cost
  the thing that makes Gemini feel better than OpenAI.

  What is actually wrong is narrower. The agent said **"otuz otuz"** for 09:30 — the hour clipped
  off — and then said "doqquz otuz" correctly when asked to repeat. It knows the value; it
  swallows the first word at speed. So `PhoneAgent.md` gained a section covering times, prices
  and phone numbers only: say both words of a time in full, never read a leading zero, read a
  phone number in pairs. The rest of a sentence still moves at a normal pace.

  Worth knowing if it recurs: `SpeechConfig` exposes no rate, so the remaining levers are wording
  and a different prebuilt voice.
- **M2 / M3** — `IRealtimeSession.ContinuesTurnAfterToolResult`. The orchestrator knows which
  providers answer on their own and skips its own follow-up for those. The one-shot suppression
  flag inside the Gemini session is gone: it stopped a double reply but could not order two turns.

  **The first attempt at M3 was wrong and the second call proved it.** The theory was that the
  automatic turn raced the dictated goodbye and won. The real fault was one link earlier:
  **Gemini does not complete a turn while a tool call is outstanding**, and the orchestrator
  deferred `EndCall`'s result until response-done. The completion waited on the result, the
  result waited on the completion, and the call sat silent until something interrupted it — the
  log shows `status=Cancelled, reason=interrupted` after 10.9 seconds. OpenAI emits
  `response.done` with a function call still unanswered, which is why deferring works there and
  only there.

  `EndCall` is now answered the moment it is asked, with the farewell wording riding along on the
  result, and the hang-up check no longer hangs off the deferred id.

- **M2 — the pause was Gemini's end-of-turn detection, left on defaults.** The log ruled out
  everything else: tools answered in 55–133 ms and first audio arrived within a second of the
  turn opening. The wait was Gemini deciding the caller had finished — worst after a one-word
  answer like "xeyr", where there is little speech to be confident about. `RealtimeInputConfig`
  now sets `EndOfSpeechSensitivity = High` and a configurable `SilenceDurationMs`, default 500.

  Start sensitivity is deliberately left alone: making Gemini keener to hear speech *begin* would
  also make it keener to mistake its own voice returning through the speakers for the caller,
  which is the echo problem the OpenAI session fights with far-field noise reduction.

  `GeminiLive:EndOfSpeechSilenceMs` is configuration rather than a constant because it is a real
  trade-off with no right answer from a desk — too long feels slow, too short talks over someone
  drawing breath. Tune it from real calls.
- **M4** — `RealtimeToolInvoker` runs one tool at a time. A gate rather than a `DbContext` per
  tool, because the toolset is resolved once per call; serial execution costs nothing measurable
  while the caller is already hearing audio.
- **Phone numbers are canonicalised** on write and on lookup (`PhoneNumberNormalizer`), which is
  why the caller was not recognised: numbers arrive as speech and were stored verbatim, leaving
  two "Mehdi" rows — `0535353535` and `050 111 22 33`.

⚠️ **Existing rows were not back-filled.** Clients created before this keep their as-spoken
number and will not match a normalised lookup. Fine for the demo data; a real deployment needs a
one-off migration, and duplicates merged before any unique index goes on the column.

### The verdict after a dozen calls

**Gemini feels markedly more human than OpenAI** — the owner's judgement after tuning both, and
the thing worth remembering when the numbers below are argued about. It still answers a very
short question more slowly, and it is preferred anyway. That is the two-provider bet paying off:
the question was never which is cheaper, it was whether the cheaper one is good enough, and it
turned out to be better.

### Still open, deliberately not in the tuning change

| | what | why it was left |
|---|---|---|
| N1 | **Short answers still take ~3 s.** Down from 7.3 s, but a one-word "xeyr" still lags a sentence. | Everything measurable has been ruled out: thinking tokens (`thoughts` empty), our tools (55–133 ms), our relay (`first audio after 0 ms`), end-of-speech silence, turn pacing. What remains is Gemini's own speech detection, and it reports neither interim transcripts nor voice-activity signals, so it cannot be seen into. **The remaining lever is to stop using its VAD**: `AutomaticActivityDetection.Disabled` plus explicit `ActivityStart`/`ActivityEnd` driven by our own microphone detector, which fires within ~100 ms. That needs proper hangover smoothing first — the current detector chops continuous speech into fragments — so it is its own change. |
| N2 | **Caller transcription is off**, which is what took the delay from ~4.5 s to ~3 s. | It cost the `Caller said:` line that makes a call reviewable, and on the OpenAI side pinning the caller's words as Azerbaijani text is what stops the model drifting into English after a barge-in. `GeminiLive:TranscribeCaller` restores it. Revisit once N1 removes the reason for having turned it off. |
| N3 | **Business hours are hardcoded 09:00–21:00**, so the agent offered 20:30 today. | Availability is behaving as written: a 30-minute service starting 20:30 finishes exactly at close. Whether the real hours are shorter, and whether the last start should be pulled back so appointments finish *before* close rather than at it, are business questions. Per-tenant business hours were already deferred; this belongs there. |
| N4 | **Pronunciation rules exist only for Gemini.** | Correct for now — "otuz otuz" and "albilerem" were only ever heard from Gemini. If OpenAI shows the same habits, the rule gets written into its own file then, from its own evidence. |

## O. Modules — decided while building them

Section G planned the module system; this is what building it changed. (IDs start at O to avoid
colliding with §M's N1–N4.)

| # | decision | why |
|---|---|---|
| O1 | **Modules are a hardcoded enum**, numbered explicitly, not rows in a table. | A module is code — pages, tools, a schema. A row for one that nothing can render is a lie the admin screen would have to tell. `TenantModule` grants them; it does not define them. The front-end registry is deliberately shorter than the enum, and the admin screen shows a granted-but-unbuilt module disabled rather than pretending. |
| O2 | **A schema per module** — `app` for Appointment, `inf` later, `dbo` for `Tenants`, `Accounts`, `TenantModules`. | Separation becomes structural instead of conventional: no discriminator column, no filter to forget. `ALTER SCHEMA ... TRANSFER` made the move data-preserving. |
| O3 | **`Clients`, `Calls`, `Escalations` and the dashboard belong to the module, not the tenant** — reversing the original assumption. | "A business has one customer list" sounded obviously right and was wrong: an appointment line and an information line take calls from different people. One shared list would show each module the other's callers. **Administration is the only genuinely shared screen** — one company, one set of staff accounts. |
| O4 | The same person contacting two modules is **two rows in two tables**, and a call that touches both will need a rule about which schema records it. | Accepted consequence of O3, not an oversight. Nothing to decide until a second module actually ships. |
| O5 | **The voice orchestrator checks the module itself**, on top of the endpoint policy. | The agent calls application services in-process. A telephony bridge will hand it a call that never passed an HTTP endpoint, so an endpoint policy alone is not a boundary. |
| O6 | **Modules declare their own overlays**, mounted only when held. | The escalation toast was global, so every signed-in user opened a SignalR connection to the appointment hub — including tenants the hub now rejects. Keeping the decision in the registry means `App.tsx` still names no module. |
| O7 | ⚠️ **The picker lives at exactly `/app`.** | `"/appointments/calendar".startsWith("/app")` is true, which silently classified the entire Appointment module as the picker and stripped its sidebar. Prefix-matching a route that is a prefix of another route is the trap; equality is both the fix and the accurate description. |

## L. Document map

| file | holds |
|---|---|
| `decisions.md` | this file — every decision, its reasoning, and what was rejected |
| `architecture.md` | the *how* — interface shapes, seams, solution layout, build order |
| `telephony-carrier-checklist.md` | what to ask the Azerbaijani carrier and Twilio before production |

The earlier `docs/architecture.md` has been folded into `design/architecture.md` and the `docs/` folder removed — it predated decisions **E3** (self-hosted SIP dropped), **F3** (per-provider instruction copies), **C5** (Postgres) and **C6** (clean start).
