using System.Text;
using Secretary.Agents;
using Secretary.Agents.Realtime;
using Secretary.Api.Auth;
using Secretary.Api.BackgroundServices;
using Secretary.Api.Hubs;
using Secretary.Api.Middleware;
using Secretary.Api.ModelBinding;
using Secretary.Api.Serialization;
using Secretary.Api.Scheduling;
using Secretary.Api.Startup;
using Secretary.Api.Voice;
using Secretary.Application;
using Secretary.Application.Abstractions;
using Secretary.Application.Pricing;
using Secretary.Domain.Enums;
using Secretary.Infrastructure;
using Secretary.Voice.Google;
using Secretary.Voice.OpenAi;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// The React frontend runs on a different origin (Vite dev server, or a separately hosted
// production build) — without this, every browser request from it is blocked before it even
// reaches a controller. Origins come from configuration, not hardcoded, so each environment
// (local dev, staging, production) lists its own frontend URL(s).
const string FrontendCorsPolicy = "FrontendCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// Local dev opt-out (appsettings.Development.json): accept any origin so localhost vs
// 127.0.0.1 vs a LAN address never blocks testing. AllowAnyOrigin() can't be combined with
// AllowCredentials() (which SignalR's /negotiate needs), so "any" is expressed via
// SetIsOriginAllowed instead. Production keeps the explicit origin list.
var allowAnyOrigin = builder.Configuration.GetValue<bool>("Cors:AllowAnyOrigin");
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        if (allowAnyOrigin)
        {
            policy.SetIsOriginAllowed(_ => true);
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader()
            .AllowAnyMethod()
            // SignalR's client sends its /negotiate request with credentials included;
            // without this, the browser rejects the preflight response outright (an empty
            // Access-Control-Allow-Credentials header) before the hub connection ever starts.
            .AllowCredentials();
    });
});

builder.Services.AddControllers(options =>
    {
        options.ModelBinderProviders.Insert(0, new InstantModelBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new InstantJsonConverter());
        // Without this, System.Text.Json serializes every enum (AccountRole, TenantStatus,
        // AppointmentStatus, CallOutcome, ...) as its raw integer ordinal, not its name — but
        // the frontend's types.ts and every role/status comparison in the app expects string
        // values like "PlatformAdmin", "Active", "Confirmed". This was never caught because
        // nothing before real deployment ever exercised real JSON serialization end-to-end
        // (Application.Tests are pure unit tests against services; Api.Tests never hit a
        // running host until now).
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
// After AddApplicationServices() so AddAgents()'s IServiceCatalogChangeNotifier implementation
// wins over Application's no-op default (see Secretary.Application.DependencyInjection).
builder.Services.AddAgents(builder.Configuration);

// Realtime providers. Each registers its own session factory; the resolver picks by pipeline,
// so adding one never edits a switch statement.
builder.Services.AddOpenAiRealtime(builder.Configuration);
builder.Services.AddGeminiLive(builder.Configuration);

// What every call costs is tracked, not estimated — see backend/README.md, "What a call costs".
builder.Services.Configure<ModelPricingOptions>(builder.Configuration.GetSection(ModelPricingOptions.SectionName));
ModelPricingGuard.EnsureConfiguredModelIsPriced(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantProvider, HttpCurrentTenantProvider>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddSignalR();
builder.Services.AddHostedService<EscalationTimeoutService>();

// Flow E's 9am reminder job — a calendar-scheduled recurring job is exactly what Hangfire is
// for, rather than a hand-rolled BackgroundService with its own polling/timing logic (unlike
// EscalationTimeoutService above, which genuinely needs a short poll interval, not a
// once-a-day schedule). Reuses the same database as the rest of the app.
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<ReminderSchedulerJob>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep short claim names ("sub", "role") as-is instead of ASP.NET Core's default
        // remapping to long ClaimTypes URIs, so Api code that reads "sub" directly
        // (EscalationsController) matches what JwtTokenGenerator actually writes.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!)),
            RoleClaimType = AppClaimTypes.Role,
        };

        // SignalR (and the raw /voice WebSocket endpoint below) send the JWT as a query
        // string access_token, not an Authorization header — a browser/console WebSocket
        // client can't set custom headers on the upgrade request.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/voice")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });
// Roles say who the caller is; module policies say what their tenant bought. Both apply.
builder.Services.AddAuthorization(options => options.AddModulePolicies());
builder.Services.AddScoped<IAuthorizationHandler, ModuleAuthorizationHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Every module that can answer a phone needs a file for every dialable provider. A missing one
// otherwise surfaces as silence on the first call that picks that combination, not as a startup
// failure. Resolved from the registry rather than a list kept here, so adding a module cannot
// quietly skip the check — which costs a startup scope, the same way the seeder below does.
using (var instructionScope = app.Services.CreateScope())
{
    InstructionFileGuard.EnsureEveryDialablePipelineHasInstructions(
        instructionScope.ServiceProvider.GetRequiredService<AgentModuleRegistry>()
            .All.Select(m => m.InstructionName));
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Dev-only visibility into scheduled/executed jobs — no auth wired up for it, so it
    // must never be exposed outside Development.
    app.UseHangfireDashboard("/hangfire");
}

// A freshly migrated database has no account and no way to create one — see DevDataSeeder.
// Development only, and only when the Accounts table is empty.
await DevDataSeeder.SeedAsync(app.Services, app.Environment, app.Logger);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<EscalationHub>("/hubs/escalations");

// Bridges the browser's mic/speakers (the Call page — see frontend/src/lib/liveVoiceCall.ts)
// to OpenAI's Realtime API, in place of the originally discussed Twilio telephony integration.
// Agent-role JWT only, same as every other Agent-scoped endpoint — this "is" a call from that
// tenant's AI agent's perspective.
// GET and CONNECT, not MapGet: over HTTP/2 browsers open WebSockets with an extended
// CONNECT request (RFC 8441), not a GET upgrade — Chrome reuses its existing HTTP/2
// connection to Kestrel, and a GET-only route silently never matches that handshake.
// HTTP/1.1 clients (IIS-hosted deployments, any non-browser caller) still use GET.
app.MapMethods("/voice/live-call", new[] { HttpMethods.Get, HttpMethods.Connect }, async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    // ?pipeline= chooses which architecture answers. One endpoint rather than several, because
    // everything around the call — auth, transport, the Call Log write — is identical and only
    // the thing in the middle differs. See VoicePipelineCatalog.
    var requested = VoicePipelineCatalog.Find(context.Request.Query["pipeline"]);
    if (requested is null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync(
            "Unknown pipeline. Expected one of: "
            + string.Join(", ", VoicePipelineCatalog.All.Select(entry => entry.Pipeline)));
        return;
    }

    // ?module= says which line was dialled. One line answers as one module, so this is the
    // stand-in for the inbound number a telephony bridge will one day resolve to a tenant and a
    // module. Defaults to Appointment: the demo Call page predates modules entirely.
    var module = Module.Appointment;
    var requestedModule = context.Request.Query["module"].ToString();
    if (!string.IsNullOrWhiteSpace(requestedModule) && !Enum.TryParse(requestedModule, ignoreCase: true, out module))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync(
            "Unknown module. Expected one of: " + string.Join(", ", Enum.GetNames<Module>()));
        return;
    }

    // Checked before the socket is accepted, so a tenant without the module never gets one. It
    // cannot be an authorization policy like every other module endpoint, because the module is
    // a query parameter and a policy is fixed at registration. The orchestrator checks again
    // once it holds a socket, which is not redundant: a telephony bridge will hand it a call
    // that never passed through here at all.
    var tenantModules = context.RequestServices.GetRequiredService<ICurrentTenantModules>();
    if (!await tenantModules.HasAsync(module, context.RequestAborted))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();

    var orchestrator = context.RequestServices.GetRequiredService<LiveVoiceCallOrchestrator>();
    await orchestrator.RunAsync(socket, module, requested.Pipeline, requested.RealtimeModel, context.RequestAborted);
    // Owner is allowed alongside Agent so the web UI's demo Call page can dial with the
    // tenant's own login instead of shipping the Agent credentials to the browser.
}).RequireAuthorization(policy => policy.RequireRole("Agent", "Owner"));

/// The legend behind the Call page's pipeline picker, so the labels and the options cannot
/// drift from what the server will actually dial.
app.MapGet("/voice/pipelines", () => VoicePipelineCatalog.All.Select(entry => new
{
    pipeline = entry.Pipeline.ToString(),
    label = entry.Label,
    realtimeModel = entry.RealtimeModel,
    enabled = entry.Enabled,
})).RequireAuthorization(policy => policy.RequireRole("Agent", "Owner"));

// Flow E: "At 9:00 AM (business's local timezone)" — Asia/Baku, since every tenant currently
// runs on that one fixed timezone (Stage 1, confirmed). Revisit the single hardcoded zone if
// multi-timezone tenants are ever supported.
//
// Registering this talks to Hangfire's SQL Server storage immediately, synchronously, before
// the app starts serving requests — a connection problem here (bad connection string, an
// unreachable/misconfigured DB) must not crash the whole API over a background-job feature.
// Log and continue instead of letting an unhandled exception here take down every endpoint.
try
{
    RecurringJob.AddOrUpdate<ReminderSchedulerJob>(
        "daily-appointment-reminders",
        job => job.RunAsync(CancellationToken.None),
        "0 9 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku") });
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to register the daily reminder recurring job — the API will still start, but Flow E's reminders won't run until this is fixed.");
}

app.Run();

public partial class Program
{
}
