using DeliveryPilot.EntityFrameworkCore.Interceptors;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations;
using DeliveryPilot.Identity.Infrastructure.Auditing;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.SharedKernel.Abstractions.Persistence;
using DeliveryPilot.SharedKernel.DependencyInjection;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryPilot.Identity.Infrastructure.DepedencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddPersistence(configuration)
            .AddRepositories()
            .AddUnitOfWork()
            .AddAuthSessionCore(configuration)
            .AddInfrastructureCqrs();

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetApplicationConnectionString(configuration);

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddSingleton<SqlConnectionFactory>();

        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddOptions<InvitationIssuanceOptions>().Bind(configuration.GetSection(InvitationIssuanceOptions.SectionName));
        services.AddOptions<InvitationDeliveryProducerOptions>().Bind(configuration.GetSection(InvitationDeliveryProducerOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordCredentialRepository, PasswordCredentialRepository>();
        services.AddScoped<IIdentityAuditWriter, IdentityAuditWriter>();
        services.AddScoped<IOrganizationAuthorization, OrganizationAuthorizationService>();
        services.AddScoped<IOrganizationMutationGuard, OrganizationMutationGuard>();
        services.AddScoped<IOrganizationCreationStore, OrganizationCreationStore>();
        services.AddScoped<IOrganizationReadStore, OrganizationReadStore>();
        services.AddScoped<IOrganizationRoleStore, OrganizationRoleStore>();
        services.AddScoped<IInvitationIssuanceGovernor, InvitationIssuanceGovernor>();
        services.AddScoped<IInvitationStore, InvitationStore>();
        services.AddScoped<IInvitationDeliveryEligibilityStore, InvitationDeliveryEligibilityStore>();
        services.AddScoped<IInvitationDeliveryStatusStore, InvitationDeliveryStatusStore>();
        services.AddScoped<IInvitationProofService, InvitationProofService>();
        services.AddScoped<IInvitationDeliveryStager, InvitationDeliveryStager>();
        services.AddScoped<InvitationIssuanceService>();
        services.AddScoped<IInvitationRecipientStore, InvitationRecipientStore>();
        services.AddScoped<InvitationResponseService>();
        services.AddScoped<DeliveryPilot.Identity.Application.Abstractions.Messaging.IEmailDeliveryReceiptStore, DeliveryPilot.Identity.Infrastructure.Messaging.EmailDeliveryReceiptStore>();
        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        => services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();

    /// <summary>
    /// Registers Identity session issuance, refresh rotation, revocation and the per-request
    /// authenticated-context validator. Token settings bind the shared <c>Jwt</c> section and are
    /// validated at startup; missing lifetimes or signing material fail the host.
    /// </summary>
    public static IServiceCollection AddAuthSessions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthSessionCore(configuration);
        services
            .AddOptions<AuthTokenOptions>()
            .Bind(configuration.GetSection(AuthTokenOptions.SectionName))
            .Validate(option => option.HasValidSecretKey(), $"JWT secret key must contain at least {AuthTokenOptions.MinimumSecretKeyBytes} bytes.")
            .Validate(option => !string.IsNullOrWhiteSpace(option.Issuer), "JWT issuer is required.")
            .Validate(option => !string.IsNullOrWhiteSpace(option.Audience), "JWT audience is required.")
            .Validate(option => option.AccessTokenExpirationMinutes > 0, "JWT access token expiration must be greater than zero.")
            .Validate(option => option.RefreshTokenExpirationDays > 0, "Refresh token expiration must be greater than zero.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddInfrastructureCqrs(this IServiceCollection services)
        => services
            .AddSelfMadeCqrs(typeof(ServiceCollectionExtensions).Assembly);

    private static IServiceCollection AddAuthSessionCore(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthTokenOptions>()
            .Bind(configuration.GetSection(AuthTokenOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<JwtAccessTokenIssuer>();
        services.TryAddScoped<IAuthSessionService, AuthSessionService>();
        services.TryAddScoped<IAuthenticatedContextValidator, AuthenticatedContextValidator>();
        services.TryAddScoped<IOrganizationAuthorization, OrganizationAuthorizationService>();
        services.TryAddScoped<IOrganizationMutationGuard, OrganizationMutationGuard>();
        return services;
    }

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
