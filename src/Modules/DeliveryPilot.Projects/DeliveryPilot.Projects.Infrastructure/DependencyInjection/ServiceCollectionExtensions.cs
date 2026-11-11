using DeliveryPilot.AspNetCore.HttpClient.DependencyInjection;
using DeliveryPilot.EntityFrameworkCore.Interceptors;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.IssueEngagementLinkCode;
using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries;
using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using DeliveryPilot.SharedKernel.Abstractions.Persistence;
using DeliveryPilot.SharedKernel.DependencyInjection;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryPilot.Projects.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers everything the Projects API needs from Infrastructure.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddPersistence(configuration)
            .AddUnitOfWork()
            .AddIdentityIntegration(configuration)
            .AddInfrastructureCqrs();

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetApplicationConnectionString(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<ProjectsDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddScoped<IEngagementCreationStore, EngagementCreationStore>();
        services.AddScoped<IEngagementMutationStore, EngagementMutationStore>();
        services.AddScoped<IEngagementReadStore, EngagementReadStore>();
        services.AddScoped<IClientLinkStore, ClientLinkStore>();
        services.AddOptions<EngagementLinkAttemptOptions>()
            .Bind(configuration.GetSection("EngagementLinks:Attempts"))
            .Validate(options => options.MaxFailures > 0 && options.Window > TimeSpan.Zero && options.Window <= TimeSpan.FromDays(1), "A positive attempt limit and a window of at most one day are required.")
            .ValidateOnStart();
        services.AddScoped<ILinkAttemptLimiter>(provider => new LinkAttemptLimiter(connectionString, provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EngagementLinkAttemptOptions>>()));
        services.AddSingleton<IEngagementLinkCodeGenerator, EngagementLinkCodeGenerator>();

        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        => services.AddScoped<IUnitOfWork, ProjectsUnitOfWork>();

    /// <summary>
    /// Registers the internal Identity authority client (integration API key, never through the Gateway),
    /// the per-request session validator used by bearer authentication, and workspace authority.
    /// </summary>
    public static IServiceCollection AddIdentityIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationHttpClient<IIdentityAuthorityClient, IdentityAuthorityClient>(
            IdentityIntegration.ClientName,
            configuration.GetSection(IdentityIntegration.ConfigurationSection));
        services.AddScoped<IAuthenticatedContextValidator, IdentitySessionValidator>();
        services.AddScoped<IWorkspaceAuthority, IdentityWorkspaceAuthority>();
        return services;
    }

    public static IServiceCollection AddInfrastructureCqrs(this IServiceCollection services)
        => services.AddSelfMadeCqrs(typeof(ServiceCollectionExtensions).Assembly);

    private static string GetApplicationConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ApplicationDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'ApplicationDb' is missing.");
        }

        if (configuration["Database:PasswordFile"] is { Length: > 0 } passwordFile)
        {
            var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
            {
                Password = File.ReadAllText(passwordFile).Trim(),
            };
            return parsed.ConnectionString;
        }

        return connectionString;
    }
}
