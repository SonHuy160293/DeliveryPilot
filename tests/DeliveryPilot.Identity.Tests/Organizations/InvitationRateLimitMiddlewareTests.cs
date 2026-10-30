using System.Text.Json;
using DeliveryPilot.Identity.API.Configuration;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class InvitationRateLimitMiddlewareTests
{
    [Fact]
    public async Task ReturnsRateLimitEnvelopeAndRetryAfter()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Response.Body = new MemoryStream();
        var middleware = new InvitationRateLimitMiddleware(_ => throw new InvitationRateLimitException(42));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("42", context.Response.Headers.RetryAfter.ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        context.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Invitation.RateLimited", body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("trace-123", body.RootElement.GetProperty("error").GetProperty("traceId").GetString());
    }
}
