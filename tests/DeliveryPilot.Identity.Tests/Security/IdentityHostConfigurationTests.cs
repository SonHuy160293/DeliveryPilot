using System.Net;
using System.Text;
using DeliveryPilot.Identity.API.Configuration;
using DeliveryPilot.Identity.API.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class IdentityHostConfigurationTests
{
    [Fact]
    public void SecretFilesOverrideJsonAndEnvironmentWithoutLosingOtherSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), "deliverypilot-config-" + Guid.NewGuid().ToString("N"));
        string environmentPrefix = "DELIVERYPILOT_CONFIG_TEST_" + Guid.NewGuid().ToString("N") + "_";
        string[] keys = ["Jwt__SecretKey", "IntegrationApi__ApiKeys__0"];
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string key in keys)
            {
                Environment.SetEnvironmentVariable(environmentPrefix + key, "environment-value");
                File.WriteAllText(Path.Combine(directory, key), "file-value");
            }

            using var json = new MemoryStream(Encoding.UTF8.GetBytes("""{"Jwt":{"SecretKey":"json-value","Issuer":"issuer-from-json"}}"""));
            using var configuration = new ConfigurationManager();
            configuration.AddJsonStream(json);
            configuration.AddEnvironmentVariables(environmentPrefix);
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["DELIVERYPILOT_CONFIG_SECRETS_DIRECTORY"] = directory });
            configuration.AddIdentitySecretFiles();

            foreach (string key in keys)
            {
                Assert.Equal("file-value", configuration[key.Replace("__", ":", StringComparison.Ordinal)]);
                Assert.Equal("environment-value", Environment.GetEnvironmentVariable(environmentPrefix + key));
            }

            Assert.Equal("issuer-from-json", configuration["Jwt:Issuer"]);
        }
        finally
        {
            foreach (string key in keys)
            {
                Environment.SetEnvironmentVariable(environmentPrefix + key, null);
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingOptionalSecretDirectoryPreservesConfiguredValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "deliverypilot-no-config-" + Guid.NewGuid().ToString("N"));
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DELIVERYPILOT_CONFIG_SECRETS_DIRECTORY"] = directory,
            ["Jwt:SecretKey"] = "existing-value",
        });
        configuration.AddIdentitySecretFiles();
        Assert.Equal("existing-value", configuration["Jwt:SecretKey"]);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("Production", HttpStatusCode.TemporaryRedirect)]
    [InlineData("Development", HttpStatusCode.Accepted)]
    public async Task HealthProbesUseHttpWhileProductionBusinessRequestsRequireHttps(string environment, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddIdentityHttpsRedirection(builder.Configuration);
        await using var app = builder.Build();
        app.UseIdentityHttpsRedirection();
        app.MapGet("/health/live", () => Results.Ok());
        app.MapGet("/health/ready", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
        app.MapGet("/api/v1/integrations/users/test", () => Results.Accepted());
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        using var lookup = await client.GetAsync("/api/v1/integrations/users/test");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Null(ready.Headers.Location);
        Assert.Equal(expected, lookup.StatusCode);
        if (environment == Environments.Production)
        {
            Assert.Equal("https", lookup.Headers.Location!.Scheme);
        }
    }
}
