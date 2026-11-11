using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.API.Contracts.Engagements;
using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.PreviewEngagementLink;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.ConfirmEngagementLink;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Projects.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/engagement-links")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EngagementLinksController(ISender sender) : ApiController(sender)
{
    [HttpPost("preview")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> PreviewAsync(Guid organizationId, EngagementLinkCodeRequest request, CancellationToken cancellationToken)
    {
        Result<EngagementLinkPreview> result = await Sender.SendAsync(new PreviewEngagementLinkCommand(organizationId, request.Code), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("confirm")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ConfirmAsync(Guid organizationId, EngagementLinkCodeRequest request, CancellationToken cancellationToken)
    {
        Result<EngagementLinkPreview> result = await Sender.SendAsync(new ConfirmEngagementLinkCommand(organizationId, request.Code), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<LinkedEngagement>> result = await Sender.SendAsync(new ListLinkedEngagementsQuery(organizationId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{engagementId:guid}/remove")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> RemoveAsync(Guid organizationId, Guid engagementId, RemoveClientLinkRequest request, CancellationToken cancellationToken)
    {
        Result<LinkedEngagementRemoval> result = await Sender.SendAsync(new RemoveClientLinkCommand(organizationId, engagementId, request.Reason), cancellationToken);
        return Ok(result.Value);
    }
}
