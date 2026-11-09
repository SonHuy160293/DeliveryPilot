using System.Text.Json;
using DeliveryPilot.Projects.API.Middleware;
using Microsoft.AspNetCore.Http;

namespace DeliveryPilot.Projects.Tests.Hosting;

public sealed class RequestBodyLimitTests
{
    [Fact]
    public async Task OversizedBodyReturnsSafeErrorEnvelopeWithTrace()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "size-test" };
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var middleware = new RequestBodyLimitMiddleware(_ => throw new BadHttpRequestException("private server diagnostic", StatusCodes.Status413PayloadTooLarge));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        body.Position = 0;
        using JsonDocument response = await JsonDocument.ParseAsync(body);
        JsonElement error = response.RootElement.GetProperty("error");
        Assert.Equal("Request.PayloadTooLarge", error.GetProperty("code").GetString());
        Assert.Equal("size-test", error.GetProperty("traceId").GetString());
        Assert.DoesNotContain("private server diagnostic", response.RootElement.GetRawText());
    }

    [Fact]
    public async Task OtherTransportFailuresReachSharedExceptionHandling()
    {
        var exception = new BadHttpRequestException("bad request", StatusCodes.Status400BadRequest);
        var middleware = new RequestBodyLimitMiddleware(_ => throw exception);

        BadHttpRequestException actual = await Assert.ThrowsAsync<BadHttpRequestException>(() => middleware.InvokeAsync(new DefaultHttpContext()));

        Assert.Same(exception, actual);
    }
}
