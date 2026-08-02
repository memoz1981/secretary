using System.Net;
using Secretary.Domain.Exceptions;
using FluentValidation;

namespace Secretary.Api.Middleware;

/// <summary>Maps domain-level exceptions to specific HTTP status codes instead of letting
/// them surface as a generic 500, so a bad request and a genuine server fault are
/// distinguishable to the client.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var (statusCode, message) = Map(exception);
            if (statusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            // A WebSocket-upgraded request (e.g. /voice/live-call, /hubs/escalations) has
            // already sent its response the moment the upgrade handshake completed — trying
            // to set a status code or write a JSON body after that throws
            // "StatusCode cannot be set because the response has already started" and masks
            // whatever the real exception was. Nothing meaningful can be written back at that
            // point; the error above is already logged.
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.StatusCode = (int)statusCode;
            await context.Response.WriteAsJsonAsync(new { error = message });
        }
    }

    private static (HttpStatusCode StatusCode, string Message) Map(Exception exception) => exception switch
    {
        NotFoundException e => (HttpStatusCode.NotFound, e.Message),
        InvalidCredentialsException e => (HttpStatusCode.Unauthorized, e.Message),
        TenantInactiveException e => (HttpStatusCode.Forbidden, e.Message),
        TenantMismatchException e => (HttpStatusCode.Forbidden, e.Message),
        SchedulingConflictException e => (HttpStatusCode.Conflict, e.Message),
        InvalidStateTransitionException e => (HttpStatusCode.Conflict, e.Message),
        EmailAlreadyInUseException e => (HttpStatusCode.Conflict, e.Message),
        DuplicateClientPhoneNumberException e => (HttpStatusCode.Conflict, e.Message),
        ClientBlackListedException e => (HttpStatusCode.Conflict, e.Message),
        ValidationException e => (HttpStatusCode.BadRequest, string.Join(" ", e.Errors.Select(x => x.ErrorMessage))),
        DomainException e => (HttpStatusCode.BadRequest, e.Message),
        ArgumentException e => (HttpStatusCode.BadRequest, e.Message),
        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
    };
}
