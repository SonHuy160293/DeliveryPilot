using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Security;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.GetDeliveryEligibility;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Route("api/v{version:apiVersion}/integrations/organization-invitation-deliveries")]
[TypeFilter(typeof(InvitationDispatchKeyFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InvitationDeliveryEligibilityController(ISender sender) : ApiController(sender)
{
    [HttpGet("{deliveryMessageId:guid}/eligibility")]
    public async Task<IActionResult> GetAsync(Guid deliveryMessageId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new GetDeliveryEligibilityQuery(deliveryMessageId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
