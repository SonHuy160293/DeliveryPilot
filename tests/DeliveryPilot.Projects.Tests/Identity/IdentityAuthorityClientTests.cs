using System.Net;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Projects.Infrastructure.DependencyInjection;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace DeliveryPilot.Projects.Tests.Identity;

public sealed class IdentityAuthorityClientTests
{
    [Fact]
    public async Task SessionCheckUsesInternalRouteIntegrationKeyAndEnvelope()
    {
        using var handler = new RecordingHandler("""{"success":true,"data":{"active":true}}""");
        await using ServiceProvider provider = BuildProvider(handler);
        var client = provider.GetRequiredService<IIdentityAuthorityClient>();

        var result = await client.CheckSessionAsync(new SessionCheckRequest(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.Active);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri(TestConfiguration.IdentityBaseUrl + IdentityIntegration.SessionChecksEndpoint), handler.RequestUri);
        Assert.Equal(TestConfiguration.IntegrationApiKey, handler.IntegrationKey);
    }

    [Fact]
    public async Task ClientErrorBecomesFailedResult()
    {
        using var handler = new RecordingHandler("{}", HttpStatusCode.BadRequest);
        await using ServiceProvider provider = BuildProvider(handler);

        var result = await provider.GetRequiredService<IIdentityAuthorityClient>()
            .CheckOrganizationAuthorityAsync(new OrganizationAuthorityCheckRequest(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(new Uri(TestConfiguration.IdentityBaseUrl + IdentityIntegration.OrganizationChecksEndpoint), handler.RequestUri);
    }

    [Fact]
    public async Task UserLookupUsesInternalPostIntegrationKeyAndEnvelope()
    {
        Guid id = Guid.NewGuid();
        using var handler = new RecordingHandler($$$"""{"success":true,"data":{"users":[{"id":"{{{id}}}","displayName":"Ada","avatarUrl":null,"status":"active"}]}}""");
        await using ServiceProvider provider = BuildProvider(handler);
        IntegrationResult<UserLookupResult> result = await provider.GetRequiredService<IIdentityAuthorityClient>().LookupUsersAsync(new UserLookupRequest([id]), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", Assert.Single(result.Data!.Users).DisplayName);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri(TestConfiguration.IdentityBaseUrl + "api/v1/integrations/users/lookup"), handler.RequestUri);
        Assert.Equal(TestConfiguration.IntegrationApiKey, handler.IntegrationKey);
        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(id, body.RootElement.GetProperty("userIds")[0].GetGuid());
        Assert.Single(body.RootElement.EnumerateObject());
    }

    private static ServiceProvider BuildProvider(RecordingHandler handler)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(TestConfiguration.Host()).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityIntegration(configuration);
        services.Configure<HttpClientFactoryOptions>(
            IdentityIntegration.ClientName,
            options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));
        return services.BuildServiceProvider();
    }

    private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? IntegrationKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            IntegrationKey = request.Headers.TryGetValues("X-Integration-Key", out var values) ? values.Single() : null;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
