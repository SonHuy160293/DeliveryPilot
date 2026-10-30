using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.InvitationResponses;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.AcceptInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.DeclineInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.PreviewInvitation;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organization-invitations/{invitationId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InvitationResponsesController(ISender sender) : ApiController(sender)
{
    [HttpPost("preview")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> PreviewAsync(Guid invitationId, PreviewInvitationRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.SendAsync(new PreviewInvitationQuery(invitationId, request.Proof), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("accept")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> AcceptAsync(Guid invitationId, AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.SendAsync(new AcceptInvitationCommand(invitationId, request.Proof, request.ExpectedVersion, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("decline")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> DeclineAsync(Guid invitationId, DeclineInvitationRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.SendAsync(new DeclineInvitationCommand(invitationId, request.Proof, request.ExpectedVersion, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
