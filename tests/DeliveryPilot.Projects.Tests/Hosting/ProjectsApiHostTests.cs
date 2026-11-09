using System.Net;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeliveryPilot.Projects.Tests.Hosting;

public sealed class ProjectsApiHostTests : IClassFixture<ProjectsApiHostTests.Factory>
{
    private readonly Factory _factory;

    public ProjectsApiHostTests(Factory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessProbeReturnsHealthy()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void BearerValidationPinsHs256AndUsesIdentitySessionCheck()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        JwtBearerOptions options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal([AuthExtensions.AcceptedSigningAlgorithm], options.TokenValidationParameters.ValidAlgorithms);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.IsType<IdentitySessionValidator>(scope.ServiceProvider.GetRequiredService<IAuthenticatedContextValidator>());
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Minimal hosting reads registration-time configuration before ConfigureAppConfiguration runs.
            foreach ((string key, string? value) in TestConfiguration.Host())
            {
                builder.UseSetting(key, value);
            }
        }
    }
}
