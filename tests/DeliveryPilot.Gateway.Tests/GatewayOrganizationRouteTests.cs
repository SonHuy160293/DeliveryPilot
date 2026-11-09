using System.Net;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeliveryPilot.Gateway.Tests;

public sealed partial class GatewayHttpTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task OrganizationCorsPreflightAllowsPublishedMethodsAndIdempotencyHeader(string method)
    {
        await using var rig = await Rig.StartAsync();
        string path = "/identity/api/v1/organizations/019a0000-0000-7000-8000-000000000001/roles";
        if (method == "PUT")
        {
            path += "/019a0000-0000-7000-8000-000000000002";
        }

        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,idempotency-key,content-type");
        using HttpResponseMessage response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(method, string.Join(',', response.Headers.GetValues("Access-Control-Allow-Methods")), StringComparison.Ordinal);
        Assert.Contains("idempotency-key", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, rig.BackendCalls);
    }

    public static IEnumerable<object[]> OrganizationRoutes()
    {
        const string org = "/organizations/{organizationId:guid}";
        yield return ["POST", "/organizations"];
        yield return ["GET", "/me/organizations"];
        yield return ["GET", org + "/permissions"];
        yield return ["GET", org + "/roles"];
        yield return ["POST", org + "/roles"];
        yield return ["PUT", org + "/roles/{roleId:guid}"];
        yield return ["POST", org + "/roles/{roleId:guid}/retire"];
        yield return ["GET", org + "/members"];
        yield return ["GET", org + "/members/me"];
        yield return ["GET", org + "/members/{membershipId:guid}"];
        yield return ["POST", org + "/members/{membershipId:guid}/remove"];
        yield return ["POST", org + "/members/{membershipId:guid}/demote-owner"];
        yield return ["POST", org + "/members/{membershipId:guid}/roles/{roleId:guid}/grant"];
        yield return ["POST", org + "/members/{membershipId:guid}/roles/{roleId:guid}/revoke"];
        yield return ["POST", org + "/invitations"];
        yield return ["GET", org + "/invitations"];
        yield return ["GET", org + "/invitations/{invitationId:guid}"];
        yield return ["POST", org + "/invitations/{invitationId:guid}/resend"];
        yield return ["POST", org + "/invitations/{invitationId:guid}/reissue"];
        yield return ["POST", org + "/invitations/{invitationId:guid}/cancel"];
        yield return ["POST", "/organization-invitations/{invitationId:guid}/preview"];
        yield return ["POST", "/organization-invitations/{invitationId:guid}/accept"];
        yield return ["POST", "/organization-invitations/{invitationId:guid}/decline"];
        yield return ["POST", org + "/ownership-nominations"];
        yield return ["GET", org + "/ownership-nominations"];
        yield return ["POST", org + "/ownership-nominations/{nominationId:guid}/accept"];
        yield return ["POST", org + "/ownership-nominations/{nominationId:guid}/decline"];
        yield return ["POST", org + "/ownership-nominations/{nominationId:guid}/cancel"];
        yield return ["GET", "/me/ownership-nominations"];
        yield return ["GET", org + "/audit-events"];
    }

    [Theory]
    [MemberData(nameof(OrganizationRoutes))]
    public async Task EveryOrganizationRouteForwardsOnlyItsExactMethodAndPreservesPrivateBytes(string method, string pattern)
    {
        await using var rig = await Rig.StartAsync();
        string internalPath = "/api/v1" + System.Text.RegularExpressions.Regex.Replace(pattern, @"\{\w+:guid\}", "019a0000-0000-7000-8000-000000000001");
        string path = "/identity" + internalPath;
        const string query = "?limit=2&cursor=a%2Bb%2Fc&from=2026-11-05T00%3A00%3A00Z";
        const string body = "{\"reason\":\"Boundary test\",\"proof\":\"private-proof-marker\"}";
        string key = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(new HttpMethod(method), path + query) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Authorization", "Bearer private-token-marker");
        request.Headers.Add("Idempotency-Key", key);
        using HttpResponseMessage response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        JsonObject json = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(internalPath, json["path"] !.GetValue<string>());
        Assert.Equal(query, json["query"] !.GetValue<string>());
        Assert.Equal(body, json["body"] !.GetValue<string>());
        Assert.Equal(key, json["idempotencyKey"] !.GetValue<string>());
        Assert.Equal("Bearer private-token-marker", json["authorization"] !.GetValue<string>());
        using HttpResponseMessage denied = await rig.Client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
        Assert.Equal(1, rig.BackendCalls);
        string logs = string.Join('\n', rig.Logs.Events.Select(item => item.RenderMessage(CultureInfo.InvariantCulture) + string.Join(',', item.Properties)));
        Assert.DoesNotContain("private-proof-marker", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("private-token-marker", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductionOrganizationAllowlistMatchesTheReviewedThirtyMethodPathPairs()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Gateway/DeliveryPilot.Gateway/proxy.json"));
        JsonObject routes = JsonNode.Parse(await File.ReadAllTextAsync(path))!["ReverseProxy"] !["Routes"] !.AsObject();
        Assert.Equal(33, routes.Count);
        var expected = OrganizationRoutes().Select(item => item[0] + " /identity/api/v1" + item[1]).ToHashSet(StringComparer.Ordinal);
        var actual = routes.Where(item => item.Key is not("identity-register" or "identity-login" or "notification-resend")).Select(item =>
        {
            JsonNode route = item.Value!;
            Assert.Equal("identity", route["ClusterId"] !.GetValue<string>());
            Assert.Equal("ConfiguredCors", route["CorsPolicy"] !.GetValue<string>());
            Assert.Equal("00:00:30", route["Timeout"] !.GetValue<string>());
            Assert.Equal(16384, route["MaxRequestBodySize"] !.GetValue<int>());
            Assert.Equal("/identity", route["Transforms"] ![0] !["PathRemovePrefix"] !.GetValue<string>());
            Assert.Single(route["Match"] !["Methods"] !.AsArray());
            return route["Match"] !["Methods"] ![0] !.GetValue<string>() + " " + route["Match"] !["Path"] !.GetValue<string>();
        }).ToHashSet(StringComparer.Ordinal);
        Assert.True(expected.SetEquals(actual));
    }

    [Theory]
    [InlineData("/identity/api/v1/integrations/organization-invitation-deliveries/019a0000-0000-7000-8000-000000000001/eligibility")]
    [InlineData("/identity/api/v1/integrations/users/019a0000-0000-7000-8000-000000000001")]
    [InlineData("/identity/api/v1/integrations/authority/session-checks")]
    [InlineData("/identity/api/v1/integrations/authority/organization-checks")]
    [InlineData("/identity/api/v1/organizations/019a0000-0000-7000-8000-000000000001/unknown")]
    public async Task PrivateAndUnknownOrganizationPathsRemainUnpublished(string path)
    {
        await using var rig = await Rig.StartAsync();
        foreach (HttpMethod method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using HttpResponseMessage response = await rig.Client.SendAsync(new HttpRequestMessage(method, path));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Equal(0, rig.BackendCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrganizationPayloadLimitsApplyToKnownAndStreamingLengths(bool chunked)
    {
        await using var rig = await Rig.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/identity/api/v1/organizations");
        request.Content = chunked ? new ChunkedContent(16385) : new ByteArrayContent(new byte[16385]);
        using HttpResponseMessage response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task PrivateCachingAndRetryHeadersSurviveUpstreamErrorsAndGatewayFailures()
    {
        const string path = "/identity/api/v1/organizations/019a0000-0000-7000-8000-000000000001/audit-events";
        await using (var rig = await Rig.StartAsync())
        {
            rig.Respond = context =>
            {
                context.Response.Headers.CacheControl = "public,max-age=86400";
                context.Response.Headers.RetryAfter = "37";
                context.Response.StatusCode = 429;
                return context.Response.WriteAsync("{\"safe\":true}");
            };
            using HttpResponseMessage response = await rig.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(TimeSpan.FromSeconds(37), response.Headers.RetryAfter!.Delta);
            Assert.Equal("{\"safe\":true}", await response.Content.ReadAsStringAsync());
        }

        await using var unavailable = await Rig.StartAsync(config => config["ReverseProxy"] !["Clusters"] !["identity"] !["Destinations"] !["identity-api"] !["Address"] = "http://127.0.0.1:1/");
        using HttpResponseMessage failure = await unavailable.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
        Assert.True(failure.Headers.CacheControl?.NoStore);
    }
}
