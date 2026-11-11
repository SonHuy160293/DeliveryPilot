using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace DeliveryPilot.Gateway.Tests;

public sealed partial class GatewayHttpTests
{
    public static IEnumerable<object[]> ProjectsRoutes()
    {
        const string collection = "/organizations/{organizationId:guid}/engagements";
        const string item = collection + "/{engagementId:guid}";
        const string links = "/organizations/{organizationId:guid}/engagement-links";
        yield return ["GET", collection];
        yield return ["POST", collection];
        yield return ["GET", item];
        yield return ["PUT", item];
        yield return ["GET", item + "/history"];
        yield return ["PUT", item + "/contract-value"];
        yield return ["POST", item + "/close"];
        yield return ["POST", item + "/reopen"];
        yield return ["POST", item + "/link-codes"];
        yield return ["POST", item + "/client-link/remove"];
        yield return ["POST", item + "/milestones"];
        yield return ["PUT", item + "/milestones/{milestoneId:guid}"];
        yield return ["POST", item + "/milestones/{milestoneId:guid}/change-date"];
        yield return ["POST", item + "/milestones/{milestoneId:guid}/change-status"];
        yield return ["POST", item + "/contacts"];
        yield return ["PUT", item + "/contacts/{contactId:guid}"];
        yield return ["POST", item + "/contacts/{contactId:guid}/remove"];
        yield return ["GET", links];
        yield return ["POST", links + "/preview"];
        yield return ["POST", links + "/confirm"];
        yield return ["POST", links + "/{engagementId:guid}/remove"];
    }

    [Fact]
    public async Task ProductionProjectsAllowlistMatchesReviewedMethodsPathsAndTransportSettings()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Gateway/DeliveryPilot.Gateway/proxy.json"));
        JsonNode config = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        JsonObject routes = config["ReverseProxy"]!["Routes"]!.AsObject();
        var expected = ProjectsRoutes().Select(x => x[0] + " /projects/api/v1" + x[1]).ToHashSet(StringComparer.Ordinal);
        var actual = routes.Where(x => x.Value!["ClusterId"]!.GetValue<string>() == "projects").Select(x =>
        {
            JsonNode route = x.Value!;
            Assert.Equal("ConfiguredCors", route["CorsPolicy"]!.GetValue<string>());
            Assert.Equal("00:00:30", route["Timeout"]!.GetValue<string>());
            Assert.Equal(16384, route["MaxRequestBodySize"]!.GetValue<int>());
            Assert.Equal("/projects", route["Transforms"]![0]!["PathRemovePrefix"]!.GetValue<string>());
            Assert.Equal("Set", route["Transforms"]![1]!["X-Forwarded"]!.GetValue<string>());
            Assert.Equal("Forwarded", route["Transforms"]![2]!["RequestHeaderRemove"]!.GetValue<string>());
            Assert.Single(route["Match"]!["Methods"]!.AsArray());
            return route["Match"]!["Methods"]![0]!.GetValue<string>() + " " + route["Match"]!["Path"]!.GetValue<string>();
        }).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(21, actual.Count);
        Assert.True(expected.SetEquals(actual));
        Assert.Equal("00:00:30", config["ReverseProxy"]!["Clusters"]!["projects"]!["HttpRequest"]!["ActivityTimeout"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(ProjectsRoutes))]
    public async Task EveryProjectsRouteForwardsExactBytesAndRejectsOtherMethods(string method, string pattern)
    {
        await using var rig = await Rig.StartAsync();
        string internalPath = "/api/v1" + Regex.Replace(pattern, @"\{\w+:guid\}", "019a0000-0000-7000-8000-000000000001");
        string path = "/projects" + internalPath;
        const string body = "{\"code\":\"private-link-proof\"}";
        const string query = "?limit=2&cursor=a%2Bb%2Fc";
        string key = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(new HttpMethod(method), path + query) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Authorization", "Bearer private-session-marker");
        request.Headers.Add("Idempotency-Key", key);
        using HttpResponseMessage response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        JsonObject actual = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(internalPath, actual["path"]!.GetValue<string>());
        Assert.Equal(body, actual["body"]!.GetValue<string>());
        Assert.Equal(query, actual["query"]!.GetValue<string>());
        Assert.Equal(key, actual["idempotencyKey"]!.GetValue<string>());
        Assert.Equal("Bearer private-session-marker", actual["authorization"]!.GetValue<string>());
        using HttpResponseMessage wrongMethod = await rig.Client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.True(wrongMethod.Headers.CacheControl?.NoStore);
        Assert.Equal(1, rig.BackendCalls);
        string logs = string.Join('\n', rig.Logs.Events.Select(x => x.RenderMessage(CultureInfo.InvariantCulture) + string.Join(',', x.Properties)));
        Assert.DoesNotContain("private-link-proof", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("private-session-marker", logs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/projects/api/v1/organizations/not-guid/engagements")]
    [InlineData("/projects/api/v1/organizations/019a0000-0000-7000-8000-000000000001/engagements/not-guid")]
    [InlineData("/projects/api/v1/organizations/019a0000-0000-7000-8000-000000000001/engagements/unknown")]
    [InlineData("/projects/api/v1/organizations/019a0000-0000-7000-8000-000000000001/projects")]
    [InlineData("/projects/api/v1/integrations/users/lookup")]
    public async Task UnknownProjectsPathsAndInvalidIdentifiersDoNotReachBackend(string path)
    {
        await using var rig = await Rig.StartAsync();
        using HttpResponseMessage response = await rig.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, rig.BackendCalls);
        if (path.Contains("/organizations/", StringComparison.Ordinal))
        {
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task ProjectsPreflightAllowsConfiguredMethodsAndIdempotencyHeader(string method)
    {
        await using var rig = await Rig.StartAsync();
        string path = "/projects/api/v1/organizations/019a0000-0000-7000-8000-000000000001/engagements";
        if (method == "PUT")
        {
            path += "/019a0000-0000-7000-8000-000000000002";
        }

        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,idempotency-key");
        using HttpResponseMessage response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("idempotency-key", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, rig.BackendCalls);
    }
}
