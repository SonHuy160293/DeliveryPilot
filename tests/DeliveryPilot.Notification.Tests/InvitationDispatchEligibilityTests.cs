using System.Net;
using DeliveryPilot.Notification.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeliveryPilot.Notification.Tests;

public sealed class InvitationDispatchEligibilityTests
{
    [Fact]
    public async Task ClientFailsClosedForInvalidOrUnavailableAuthority()
    {
        string path = Path.Combine(Path.GetTempPath(), $"invitation-dispatch-{Guid.NewGuid():N}.key");
        await File.WriteAllTextAsync(path, "separate-test-dispatch-key");
        try
        {
            var handler = new StubHandler();
            using var http = new HttpClient(handler);
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["InvitationDispatch:IdentityOrigin"] = "http://127.0.0.1:9999/", ["InvitationDispatch:ApiKeyFile"] = path }).Build();
            var client = new InvitationDispatchEligibility(http, config);
            handler.Body = "{\"data\":{\"allowed\":true,\"expiresAt\":null}}";
            Assert.Null(await client.IsAllowedAsync(Guid.CreateVersion7(), CancellationToken.None));
            handler.Body = "{\"data\":{\"allowed\":false,\"expiresAt\":null}}";
            Assert.False(await client.IsAllowedAsync(Guid.CreateVersion7(), CancellationToken.None));
            handler.Body = "{\"data\":{\"allowed\":true,\"expiresAt\":\"2099-01-01T00:00:00+00:00\"}}";
            Assert.True(await client.IsAllowedAsync(Guid.CreateVersion7(), CancellationToken.None));
            Assert.Equal("separate-test-dispatch-key", handler.Header);
            handler.Status = HttpStatusCode.ServiceUnavailable;
            Assert.Null(await client.IsAllowedAsync(Guid.CreateVersion7(), CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public string Body { get; set; } = "{}";
        public string? Header { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Header = request.Headers.GetValues("X-Invitation-Dispatch-Key").Single();
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
        }
    }
}
