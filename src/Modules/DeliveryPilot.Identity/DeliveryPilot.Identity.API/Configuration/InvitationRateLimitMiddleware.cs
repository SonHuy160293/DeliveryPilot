using System.Globalization;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DeliveryPilot.Identity.API.Configuration;

public sealed class InvitationRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        Type? controller = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.AsType();
        bool protectedEndpoint = controller == typeof(OrganizationInvitationsController) || controller == typeof(InvitationResponsesController) ||
            controller == typeof(OrganizationMembersController) || controller == typeof(OrganizationOwnershipController) ||
            controller == typeof(MyOwnershipNominationsController) || controller == typeof(OrganizationAuditController) || controller == typeof(OrganizationRolesController);
        if (protectedEndpoint)
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        try
        {
            await next(context);
        }
        catch (InvitationRateLimitException exception) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
                exception.Code,
                exception.Message,
                TraceId: context.TraceIdentifier)));
        }
        catch (BadHttpRequestException exception) when (protectedEndpoint && !context.Response.HasStarted && exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
                "Request.TooLarge", "Request body is too large.", TraceId: context.TraceIdentifier)));
        }
    }
}
