using System.Globalization;
using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.API.Middleware;

public sealed class EngagementLinkRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (EngagementLinkRateLimitException exception) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(exception.Code, exception.Message, TraceId: context.TraceIdentifier)));
        }
    }
}
