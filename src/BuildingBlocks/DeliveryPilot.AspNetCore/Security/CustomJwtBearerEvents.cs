using System.Security.Claims;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DeliveryPilot.AspNetCore.Security;

public sealed class CustomJwtBearerEvents : JwtBearerEvents
{
    private readonly ILogger<CustomJwtBearerEvents> _logger;

    public CustomJwtBearerEvents(ILogger<CustomJwtBearerEvents> logger)
    {
        _logger = logger;
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        if (context.Exception is SecurityTokenExpiredException or SecurityTokenInvalidLifetimeException)
        {
            context.Response.Headers["IS-TOKEN-EXPIRED"] = "True";
        }

        return Task.CompletedTask;
    }

    public override async Task Challenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
            "Request.Unauthorized",
            "Authentication is required.",
            TraceId: context.HttpContext.TraceIdentifier)));
    }

    public override async Task Forbidden(ForbiddenContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
            "Request.Forbidden",
            "Access is forbidden.",
            TraceId: context.HttpContext.TraceIdentifier)));
    }

    /// <summary>
    /// Admission boundary for protected requests: after signature, issuer, audience, algorithm and
    /// lifetime validation, require nonempty user/session GUIDs and an uncached active-context check.
    /// A missing validator or any validator failure rejects the request (fail closed).
    /// </summary>
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!TryReadGuid(context.Principal, out Guid userId, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier))
        {
            context.Fail("Missing user identifier.");
            return;
        }

        if (!TryReadGuid(context.Principal, out Guid sessionId, CustomClaims.SessionId))
        {
            context.Fail("Missing session identifier.");
            return;
        }

        var validator = context.HttpContext.RequestServices.GetService<IAuthenticatedContextValidator>();
        if (validator is null)
        {
            context.Fail("Authenticated context validation is unavailable.");
            return;
        }

        try
        {
            if (!await validator.IsActiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
            {
                context.Fail("Authenticated context is no longer active.");
            }
        }
        catch (Exception exception)
        {
            // Never include the exception message: provider errors can contain connection details.
            _logger.LogWarning("Authenticated context validation failed ({ExceptionType}).", exception.GetType().Name);
            context.Fail("Authenticated context validation is unavailable.");
        }
    }

    private static bool TryReadGuid(ClaimsPrincipal? principal, out Guid value, params string[] claimTypes)
    {
        value = Guid.Empty;
        if (principal is null)
        {
            return false;
        }

        foreach (string claimType in claimTypes)
        {
            string? raw = principal.FindFirstValue(claimType);
            if (raw is not null)
            {
                return Guid.TryParse(raw, out value) && value != Guid.Empty;
            }
        }

        return false;
    }
}
