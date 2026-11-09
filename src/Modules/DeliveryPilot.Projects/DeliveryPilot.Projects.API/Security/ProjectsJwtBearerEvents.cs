using DeliveryPilot.AspNetCore.Security;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace DeliveryPilot.Projects.API.Security;

/// <summary>Preserves shared bearer admission while distinguishing an unavailable Identity dependency.</summary>
public sealed class ProjectsJwtBearerEvents(CustomJwtBearerEvents shared, IAuthenticatedContextValidator validator) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        await shared.TokenValidated(context);
        if (validator is IdentitySessionValidator { IsUnavailable: true })
        {
            throw new ServiceUnavailableException("Identity.Unavailable", "Session validity could not be confirmed.");
        }
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context) => shared.AuthenticationFailed(context);
    public override Task Challenge(JwtBearerChallengeContext context) => shared.Challenge(context);
    public override Task Forbidden(ForbiddenContext context) => shared.Forbidden(context);
}
