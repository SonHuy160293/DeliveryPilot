using System.Net;
using DeliveryPilot.Projects.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DeliveryPilot.Projects.Tests.Hosting;

public sealed class ProjectsSecretFilesTests
{
    [Fact]
    public async Task ApiStartsWithKeysProvidedOnlyBySecretFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "deliverypilot-projects-secret-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Dictionary<string, string?> configuration = TestConfiguration.Host();
        string[] keys = ["Jwt:SecretKey", "Integrations:Identity:ApiKey", "IntegrationApi:ApiKeys:0"];
        try
        {
            foreach (string key in keys)
            {
                await File.WriteAllTextAsync(Path.Combine(directory, key.Replace(":", "__", StringComparison.Ordinal)), configuration[key]);
                configuration.Remove(key);
            }

            configuration["DELIVERYPILOT_CONFIG_SECRETS_DIRECTORY"] = directory;
            using var factory = new SecretFactory(configuration);
            using HttpClient client = factory.CreateClient();
            using HttpResponseMessage response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            foreach (string key in keys)
            {
                File.Delete(Path.Combine(directory, key.Replace(":", "__", StringComparison.Ordinal)));
            }

            Directory.Delete(directory);
        }
    }

    private sealed class SecretFactory(Dictionary<string, string?> configuration) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach ((string key, string? value) in configuration)
            {
                builder.UseSetting(key, value);
            }
        }
    }
}
