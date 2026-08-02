# Secretary — architecture

Greenfield rebuild of the Appointment platform. Same product (sekretar.az), new structure built for
**N modules × N voice providers × N telephony transports** from day one.

The *what* and *why* of each decision is in [`decisions.md`](./decisions.md). This document is the
*how* — the shapes, the seams, and the code layout.

Status: **plan.** Nothing built.

---

## 1. The three axes

| axis | decides | varies per | abstraction |
|---|---|---|---|
| **Module** | what the agent can do — tools, instructions, entities | tenant | `IAgentModule` |
| **Voice option** | which model answers, over which protocol | call | `IRealtimeSession` |
| **Transport** | where the audio comes from | deployment | `ITelephonyGateway` |

No axis may name another. A module never mentions OpenAI; a provider never mentions appointments;
a gateway never mentions either. All they share is audio frames and tool schemas.

---

## 2. Transport — one seam, browser, phone and WhatsApp

```
browser mic ─────────── PCM16 24k ──┐
Twilio · PSTN ───────── μ-law 8k ───┼──▶ ITelephonyGateway ──AudioFrame──▶ IRealtimeSession ──▶ provider
Twilio · WhatsApp ───── Opus (tbc) ─┘              ▲
                                             resample / transcode
```

Two levels: the gateway handles **signalling**, the channel it produces handles **media**.

```csharp
interface ITelephonyGateway {                       // signalling
    string Key { get; }                             // "browser" | "twilio"
    GatewayCapabilities Capabilities { get; }       // Outbound? Dtmf? Transfer?
    IAsyncEnumerable<ICallChannel> ListenAsync(CancellationToken ct);
    Task<ICallChannel> PlaceCallAsync(string destination, CancellationToken ct);
}

interface ICallChannel : IAsyncDisposable {         // one call: media + control
    CallInfo Info { get; }                          // callerId, dialledNumber, direction, channel
    AudioFormat Format { get; }                     // the gateway declares; the session adapts
    IAsyncEnumerable<AudioFrame> ReadAsync(CancellationToken ct);
    ValueTask WriteAsync(AudioFrame frame, CancellationToken ct);
    ValueTask HangupAsync(CancellationToken ct);
}
```

Format conversion lives in `Secretary.Voice/Audio`, **once**, not per provider.

| gateway | channel | when | hosting requirement |
|---|---|---|---|
| `browser` | — | demo, dev, the Call page | any |
| `twilio` | PSTN | production (step 6) | PaaS — outbound websocket + HTTPS webhook |
| `twilio` | WhatsApp | step 8 | same gateway, same hosting |

**WhatsApp Business Calling is a channel, not a gateway** (decision E8). It routes through Twilio
Programmable Voice, so it reuses the same webhook + TwiML + `<Connect><Stream>` path; `CallInfo`
reports the channel. Two things make it strategically interesting: it needs no +994 DID or carrier
interconnect, and it delivers Opus wideband rather than 8 kHz G.711 — which directly helps
Azerbaijani recognition. Both are unconfirmed pending K8–K10.

Self-hosted SIP is **dropped** (decision E3). If it ever returns it is another implementation of
the same interface, not a rework.

### Known rough edges

- **Capability gaps.** `PlaceCallAsync` is meaningless for the browser; DTMF and transfer exist on
  Twilio only. Expressed as `Capabilities` flags the caller checks, not runtime exceptions.
- **Gateways aren't self-contained.** Browser needs a WebSocket endpoint, Twilio an HTTPS webhook —
  both require ASP.NET routing, so each gateway contributes an `IEndpointRouteBuilder` extension.
- **`ListenAsync` is a mild fiction for the browser** — calls arrive on the HTTP pipeline, not a
  loop we own. The endpoint writes into a `Channel<T>` the enumerable drains.
- **Pacing.** Telephony RTP is a strict 20 ms cadence; websocket audio arrives in bursts. The
  jitter/pacing buffer belongs in the shared layer.

---

## 3. Voice options — named, registered, not enumerated

The stored value is a **stable string key**, so renaming a label or bumping a model version is
never a migration.

```csharp
public sealed record VoiceOption(
    string Key,                  // "openai.realtime.gpt-realtime"  — persisted on Call
    string DisplayName,
    string ProviderKey,          // "openai" | "google"
    string Model,
    AudioFormat PreferredFormat,
    RateCard Pricing);
```

```
openai.realtime.gpt-realtime          production baseline
openai.realtime.gpt-realtime-mini     ~3x cheaper, quality unproven
google.live.gemini-live               ~4x cheaper, Azerbaijani unverified
```

Providers register their own options in their own `DependencyInjection`, so `Secretary.Voice` never
lists them.

### One abstraction over OpenAI Realtime and Gemini Live

Both are: open websocket → send session config → stream audio in → receive audio out and tool calls
→ reply to tool calls → receive usage. Identical lifecycle, different envelopes.

```csharp
interface IRealtimeSession : IAsyncDisposable {
    ValueTask StartAsync(RealtimeSessionConfig config, CancellationToken ct);
    ValueTask SendAudioAsync(AudioFrame frame, CancellationToken ct);
    IAsyncEnumerable<RealtimeEvent> EventsAsync(CancellationToken ct);   // AudioOut, ToolCall, TurnEnd, Usage, Error
    ValueTask SendToolResultAsync(string callId, string json, CancellationToken ct);
}
```

Two genuine per-provider differences, both isolated:

1. **Tool schema** — OpenAI realtime JSON vs Gemini `functionDeclarations`. One
   `IToolSchemaTranslator` per provider, fed the same neutral `AIFunction` list.
2. **Usage reporting** — different shapes and billing units. One `IUsageTranslator` per provider
   emitting a common `ModelTokenSpend`.

Barge-in and interruption semantics also differ and are the most likely place the abstraction
leaks. That is why Gemini lands at step 4, not step 7.

### Gemini backend selection

```json
"Google": {
  "Backend": "AiStudio",          // | "Enterprise"
  "ApiKey": "…",                  // AI Studio — user-secrets
  "Project": "…",                 // Enterprise
  "Location": "europe-west3",
  "Model": "…"                    // model ids can differ between backends
}
```

One factory reads `Backend` and builds the right `Google.GenAI` `Client`. Everything downstream —
`Live`, `ConnectAsync()`, the session code — is identical. Setting both `enterprise` and `vertexAI`
conflictingly throws, so the factory picks exactly one.

---

## 4. Agents — the contract between the module and provider axes

```csharp
record AgentBriefing(
    string Instructions,                     // fully rendered: prose + tenant context
    IReadOnlyList<AIFunction> Tools,         // provider-neutral
    string? OpeningLine);

interface IAgentModule {                     // no provider anywhere
    string Key { get; }                                  // "appointment"
    IReadOnlyList<string> ToolGroups { get; }
    Task<AgentBriefing> BuildBriefingAsync(AgentContext ctx, CancellationToken ct);
}

interface IRealtimeProvider {                // no module anywhere
    string ProviderKey { get; }
    Task<IRealtimeSession> ConnectAsync(VoiceOption option, AgentBriefing briefing, CancellationToken ct);
}
```

Composition root:

```csharp
var module   = _modules.Resolve(tenant.ModuleKey);
var option   = _voiceCatalog.Resolve(tenant.VoiceOptionKey);
var briefing = await module.BuildBriefingAsync(ctx, ct);
var session  = await _providers[option.ProviderKey].ConnectAsync(option, briefing, ct);
```

Adding a module touches no provider code. Adding a provider touches no module code.

### Tool groups

```csharp
interface IAgentToolGroup {
    string Name { get; }              // "clients" | "call-control" | "appointments"
    ToolScope Scope { get; }          // Shared | Module
    IEnumerable<AIFunction> Build(AgentContext ctx);
}
```

`clients`, `call-control`, `escalation` are **Shared** — Feedback names them without redefining
them. A module declares group names; the factory resolves and concatenates.

### Instructions — one complete file per (module × provider)

```
Modules/Appointment/Instructions/
  openai.md
  gemini.md
```

No shared base, no deltas (decision F3). Start as identical copies, optimise one at a time.

`InstructionLoader.Load(module, providerKey)` resolves the file; `BuildBriefingAsync` renders tenant
context into it. **Prose is duplicated; data injection is not** — the services list, provider names,
working hours and Baku date are shared code.

**Missing combination = startup failure.** Validate every reachable (module × voice option) pair at
boot and refuse to start, the same way a missing rate card does. A silent fallback would run Gemini
on OpenAI-tuned instructions and poison the comparison.

---

## 5. Solution layout

```
Secretary.sln
backend/src/
  Secretary.Domain/
    Common/                 Tenant, Account, Client, Call, TenantModule, Escalation
    Modules/Appointment/    Appointment, Provider, ServiceOffering, ProviderServiceOffering
  Secretary.Application/
    Common/                 auth, tenancy, dispatch, ICurrentTenant, IModuleAccess
    Modules/Appointment/    handlers, DTOs, validators
  Secretary.Infrastructure/
    Common/                 DbContext, migrations, query filters, repositories
    Modules/Appointment/    module configs + repositories
  Secretary.Api/
    Common/                 auth, error handling, /api/me, admin endpoints
    Modules/Appointment/    /api/appointment/* endpoints
    Voice/                  call hub, gateway webhooks

  Secretary.Voice/                     no vendor SDKs
    Abstractions/  IRealtimeSession, IRealtimeSessionFactory, ITelephonyGateway, ICallChannel,
                   IToolSchemaTranslator, IUsageTranslator, AudioFrame, AudioFormat,
                   RealtimeEvent, ModelTokenSpend
    Registry/      VoiceOption, VoiceOptionCatalog, RateCard
    Audio/         PcmAudio, resampler, μ-law codec, energy VAD, pacing buffer
    Session/       CallSession (provider-agnostic orchestration), CallMetrics
    Text/          AzerbaijaniTextNormalizer, numbers, abbreviations

  Secretary.Voice.OpenAi/              OpenAiRealtimeSession + schema + usage translators
  Secretary.Voice.Google/              GeminiLiveSession   + schema + usage translators

  Secretary.Telephony.Browser/         websocket transport for the demo
  Secretary.Telephony.Twilio/          Media Streams gateway

  Secretary.Agents/                    no vendor SDKs
    Abstractions/  IAgentModule, IAgentToolGroup, ToolScope, AgentContext, AgentBriefing
    Tools/Common/  ClientTools, CallControlTools, EscalationTools
    Modules/Appointment/  AppointmentTools, ServiceCatalogTools, Instructions/{openai,gemini}.md
    Directory/     tenant service catalog + provider directory caches

backend/tests/                         mirrors src, one test project per src project
frontend/                              module manifest registry — see decisions.md §B
```

`Secretary.Api` is the only project referencing the provider and telephony projects. If
`Secretary.Agents` or `Secretary.Voice` ever gains a vendor SDK reference, the layering has broken.

---

## 6. Persistence — PostgreSQL, clean start

**PostgreSQL** (decision C5), and the database **starts empty** (decision C6) — no data is carried
over from the Appointment system, and the first migration is generated from the new model rather
than inherited.

Practical notes:

- `Npgsql.EntityFrameworkCore.PostgreSQL`, plus `Npgsql.NodaTime` if NodaTime stays in the stack.
- **Decide snake_case now.** `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`) is
  idiomatic for Postgres and avoids quoted PascalCase identifiers everywhere. Cheap now, tedious
  later.
- **`timestamptz` for everything temporal.** Npgsql maps `DateTime` with `Kind` strictly — decide
  UTC-everywhere at the boundary and convert to Baku time only for display and for the agent.
- **`citext`** for email, so uniqueness is case-insensitive without `LOWER()` indexes.
- Integer surrogate keys, as in the current system.

Hosting: **Azure Database for PostgreSQL Flexible Server**, B1ms, Germany West Central — roughly
$15/month including storage. Postgres also reopens cheaper options (Neon has a Frankfurt region and
a usable free tier) if the demo phase should cost nothing; see decisions.md §H.

---

## 7. Tenant + module authorization

Enforced in three places, deliberately redundant:

1. **`TenantModule` table** — `(Id, TenantId, Module, Status, EnabledAt, DisabledAt)`, unique on
   `(TenantId, Module)`. Source of truth, toggled by platform admin.
2. **Endpoint policy** — every `/api/{module}/*` route carries `RequireModule("appointment")`. A
   tenant without the module gets 403, not an empty list.
3. **Query filter** — module entities filter on tenant.

⚠️ **Platform admins have `TenantId == null`.** A naive `HasQueryFilter(x => x.TenantId == current)`
on `TenantModule` returns nothing for the one account that administers it. Needs the null-admin
escape from the start.

Resolution is **per-request cached, not a JWT claim**, so enabling a module takes effect on the
tenant's next request rather than their next login.

**No authentication on calls.** A caller is anonymous. Tenant identity is passed into the
`CallSession` at construction, never read ambiently from the current user — which also makes the
session testable without a fake HTTP context.

---

## 8. Build order

| | |
|---|---|
| 1 | Solution skeleton, Domain + Infrastructure `Common/`, auth, tenancy, `TenantModule`, first Postgres migration |
| 2 | Appointment module end to end — entities, endpoints, tools — no voice yet |
| 3 | `Secretary.Voice` + `Telephony.Browser` + `Voice.OpenAi` → a working browser call |
| 4 | `Voice.Google` → Gemini Live on the same seam. **First real test of the abstraction.** |
| 5 | Frontend: module registry, `/app` resolver, Appointment pages |
| 6 | `Telephony.Twilio` → real phone call, same code path as the demo |
| 7 | Second module (Feedback) → first real test of tool reuse |
| 8 | WhatsApp Business Calling channel — reuses step 6's gateway. May move ahead of step 6 if WhatsApp availability resolves before the carrier interconnect does. |

Step 4 is deliberately early. If `IRealtimeSession` can't absorb Gemini Live cleanly, that must
surface before three more things are built on top of it.

---

## 9. Open questions

Tracked in [`decisions.md`](./decisions.md) §K. The two that matter soonest:

- **Gemini's Azerbaijani quality** — unverified, and Azerbaijani is what killed the chained
  pipelines. One throwaway spike answers it and should happen before step 4 commits.
- **Gemini paid-tier concurrency** — the free tier allows 3 concurrent sessions, which makes any
  load test meaningless.
