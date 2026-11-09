using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.API.Contracts.Engagements;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementContact;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementContact;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementContact;
using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Projects.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/engagements/{engagementId:guid}/contacts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EngagementContactsController(ISender sender) : ApiController(sender)
{
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> AddAsync(Guid organizationId, Guid engagementId, AddContactRequest request, CancellationToken cancellationToken)
    {
        var command = new AddEngagementContactCommand(organizationId, engagementId, request.Name, request.Email, request.ContactRole);
        Result<ContactResult> result = await Sender.SendAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{contactId:guid}")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> EditAsync(Guid organizationId, Guid engagementId, Guid contactId, EditContactRequest request, CancellationToken cancellationToken)
    {
        var command = new EditEngagementContactCommand(organizationId, engagementId, contactId, request.ExpectedVersion, request.Name, request.Email, request.ContactRole);
        Result<ContactResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{contactId:guid}/remove")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> RemoveAsync(Guid organizationId, Guid engagementId, Guid contactId, RemoveContactRequest request, CancellationToken cancellationToken)
    {
        var command = new RemoveEngagementContactCommand(organizationId, engagementId, contactId, request.ExpectedVersion, request.Reason);
        Result<ContactResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }
}
