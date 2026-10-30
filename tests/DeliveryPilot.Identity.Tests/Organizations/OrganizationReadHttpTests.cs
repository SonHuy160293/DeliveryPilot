using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyMembership;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyOrganizations;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetOrganizationMember;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.ListOrganizationMembers;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationReadHttpTests
{
    [Fact]
    public async Task ResponsesKeepJsonEnvelopeRolesCursorNoStoreAndFailures()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddSingleton<ISender, TestSender>();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
            .AddApplicationPart(typeof(OrganizationMembersController).Assembly);
        await using var app = builder.Build();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        Guid organizationId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        using var unauthenticated = await client.GetAsync("/api/v1/me/organizations");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-User", "yes");
        using var workspaceResponse = await client.GetAsync("/api/v1/me/organizations");
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
        Assert.True(workspaceResponse.Headers.CacheControl?.NoStore);
        using var workspaceJson = JsonDocument.Parse(await workspaceResponse.Content.ReadAsStringAsync());
        JsonElement workspacePage = workspaceJson.RootElement.GetProperty("data");
        Assert.Equal("workspace-cursor", workspacePage.GetProperty("nextCursor").GetString());
        Assert.Equal("owner", workspacePage.GetProperty("items")[0].GetProperty("roles")[0].GetProperty("code").GetString());

        foreach (string path in new[] { $"/api/v1/organizations/{organizationId}/members/me", $"/api/v1/organizations/{organizationId}/members/{memberId}" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("reader@example.test", json.RootElement.GetProperty("data").GetProperty("primaryEmail").GetString());
            Assert.Equal("member", json.RootElement.GetProperty("data").GetProperty("roles")[0].GetProperty("code").GetString());
        }

        using var membersResponse = await client.GetAsync($"/api/v1/organizations/{organizationId}/members");
        Assert.Equal(HttpStatusCode.OK, membersResponse.StatusCode);
        using var membersJson = JsonDocument.Parse(await membersResponse.Content.ReadAsStringAsync());
        Assert.Equal("member-cursor", membersJson.RootElement.GetProperty("data").GetProperty("nextCursor").GetString());

        using var failure = await client.GetAsync($"/api/v1/organizations/{organizationId}/members?status=fail");
        Assert.Equal(HttpStatusCode.BadRequest, failure.StatusCode);
        using var failureJson = JsonDocument.Parse(await failure.Content.ReadAsStringAsync());
        Assert.Equal("Read.Failed", failureJson.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private sealed class TestSender : ISender
    {
        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var role = new OrganizationRoleResponse(Guid.NewGuid(), "member", "Member");
            var member = new OrganizationMemberResponse(Guid.NewGuid(), Guid.NewGuid(), "Reader", "reader@example.test", "active", [role], DateTimeOffset.UnixEpoch, 1);
            object result = request switch
            {
                GetMyOrganizationsQuery => Result.Success(new OrganizationPageResponse<OrganizationWorkspaceResponse>([new(Guid.NewGuid(), "Workspace", [new(Guid.NewGuid(), "owner", "Owner")], Guid.NewGuid(), 1, 1)], "workspace-cursor")),
                GetMyMembershipQuery or GetOrganizationMemberQuery => Result.Success(member),
                ListOrganizationMembersQuery { Status: "fail" } => Result.Failure<OrganizationPageResponse<OrganizationMemberResponse>>(new Error("Read.Failed", "Read failed.")),
                ListOrganizationMembersQuery => Result.Success(new OrganizationPageResponse<OrganizationMemberResponse>([member], "member-cursor")),
                _ => throw new InvalidOperationException("Unexpected query."),
            };
            return Task.FromResult((TResponse)result);
        }
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-User"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
