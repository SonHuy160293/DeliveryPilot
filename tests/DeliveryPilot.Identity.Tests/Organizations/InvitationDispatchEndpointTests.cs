using System.Net;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class InvitationDispatchEndpointTests
{
    [Fact]
    public async Task DedicatedKeyIsRequiredAndUnknownDeliveryIsIndistinguishable()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["InvitationDispatch:ApiKey"] = "dedicated-dispatch-test-key" });
        builder.Services.AddApplicationCqrs();
        builder.Services.AddScoped<IInvitationDeliveryEligibilityStore, DenyStore>();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>()).AddApplicationPart(typeof(InvitationDeliveryEligibilityController).Assembly);
        await using var app = builder.Build();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        string route = $"/api/v1/integrations/organization-invitation-deliveries/{Guid.CreateVersion7()}/eligibility";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Integration-Key", "dedicated-dispatch-test-key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Invitation-Dispatch-Key", "incorrect");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Invitation-Dispatch-Key");
        client.DefaultRequestHeaders.Add("X-Invitation-Dispatch-Key", "dedicated-dispatch-test-key");
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("data").GetProperty("allowed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").GetProperty("expiresAt").ValueKind);
    }

    private sealed class DenyStore : IInvitationDeliveryEligibilityStore
    {
        public Task<InvitationDeliveryEligibility> GetAsync(Guid deliveryId, CancellationToken cancellationToken)
            => Task.FromResult(new InvitationDeliveryEligibility(false, null));
    }
}
