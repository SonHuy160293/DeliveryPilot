using System.Text;
using DeliveryPilot.AspNetCore.Options;
using DeliveryPilot.AspNetCore.Security;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DeliveryPilot.AspNetCore.DependencyInjection;

public static class AuthExtensions
{
    /// <summary>The only accepted JWT signing algorithm. Issuers must sign with the same algorithm.</summary>
    public const string AcceptedSigningAlgorithm = SecurityAlgorithms.HmacSha256;

    public static IServiceCollection AddCurrentUserProvider(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();

        return services;
    }

    /// <summary>
    /// Configures bearer validation with the same rules in every environment: pinned HS256,
    /// required issuer, audience, signature, <c>exp</c> and zero clock skew. A token is valid strictly
    /// before its <c>exp</c> instant according to the registered <see cref="TimeProvider"/>.
    /// After cryptographic validation, <see cref="CustomJwtBearerEvents"/> requires nonempty
    /// <c>sub</c>/<c>sid</c> GUIDs and an active context from the registered
    /// <see cref="IAuthenticatedContextValidator"/>; without one, every token is rejected.
    /// </summary>
    public static IServiceCollection AddConfiguredAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(option => !string.IsNullOrWhiteSpace(option.SecretKey), "JWT secret key is required.")
            .Validate(option => Encoding.UTF8.GetByteCount(option.SecretKey) >= 32, "JWT secret key must be at least 32 bytes.")
            .Validate(option => !string.IsNullOrWhiteSpace(option.Issuer), "JWT issuer is required.")
            .Validate(option => !string.IsNullOrWhiteSpace(option.Audience), "JWT audience is required.")
            .Validate(option => option.AccessTokenExpirationMinutes > 0, "JWT access token expiration must be greater than zero.")
            .ValidateOnStart();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT options are not properly configured.");

        services.TryAddSingleton(TimeProvider.System);
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidAlgorithms = [AcceptedSigningAlgorithm],
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = string.IsNullOrEmpty(jwtOptions.SecretKey)
                        ? null
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.Response.Headers["IS-TOKEN-EXPIRED"] = "True";
                        }

                        return Task.CompletedTask;
                    }
                };

                options.EventsType = typeof(CustomJwtBearerEvents);
            });

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TimeProvider>((options, timeProvider) =>
            {
                options.TimeProvider = timeProvider;
                options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
                {
                    DateTime now = timeProvider.GetUtcNow().UtcDateTime;
                    return expires.HasValue
                        && now < expires.Value.ToUniversalTime()
                        && (!notBefore.HasValue || notBefore.Value.ToUniversalTime() <= now);
                };
            });

        services.AddScoped<CustomJwtBearerEvents>();

        return services;
    }

    public static IServiceCollection AddConfiguredAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        services
            .AddOptions<IntegrationApiOptions>()
            .BindConfiguration(IntegrationApiOptions.SectionName)
            .Validate(option => !string.IsNullOrWhiteSpace(option.HeaderName), "Integration API header name is required.")
            .Validate(option => option.ApiKeys.Length > 0, "At least one integration API key is required.")
            .Validate(option => option.ApiKeys.All(key => !string.IsNullOrWhiteSpace(key) && key.Length <= 4096), "Integration API keys must be non-empty and no longer than 4096 characters.")
            .ValidateOnStart();

        services.AddScoped<IntegrationApiKeyFilter>();

        return services;
    }
}
