using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.API.Contracts.Engagements;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementMilestone;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneDate;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneStatus;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementMilestone;
using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Projects.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/engagements/{engagementId:guid}/milestones")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EngagementMilestonesController(ISender sender) : ApiController(sender)
{
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> AddAsync(Guid organizationId, Guid engagementId, AddMilestoneRequest request, CancellationToken cancellationToken)
    {
        var command = new AddEngagementMilestoneCommand(organizationId, engagementId, request.Name, request.Description, request.TargetDate);
        Result<MilestoneResult> result = await Sender.SendAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{milestoneId:guid}")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> EditAsync(Guid organizationId, Guid engagementId, Guid milestoneId, EditMilestoneRequest request, CancellationToken cancellationToken)
    {
        var command = new EditEngagementMilestoneCommand(organizationId, engagementId, milestoneId, request.ExpectedVersion, request.Name, request.Description);
        Result<MilestoneResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{milestoneId:guid}/change-date")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ChangeDateAsync(Guid organizationId, Guid engagementId, Guid milestoneId, ChangeMilestoneDateRequest request, CancellationToken cancellationToken)
    {
        var command = new ChangeMilestoneDateCommand(organizationId, engagementId, milestoneId, request.ExpectedVersion, request.TargetDate, request.Reason);
        Result<MilestoneResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{milestoneId:guid}/change-status")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ChangeStatusAsync(Guid organizationId, Guid engagementId, Guid milestoneId, ChangeMilestoneStatusRequest request, CancellationToken cancellationToken)
    {
        var command = new ChangeMilestoneStatusCommand(organizationId, engagementId, milestoneId, request.ExpectedVersion, request.Status, request.Reason);
        Result<MilestoneResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }
}
