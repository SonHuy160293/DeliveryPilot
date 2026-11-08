using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.OwnershipNominations;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.AcceptNomination;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.CancelNomination;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.DeclineNomination;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.NominateOwner;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListOwnershipNominations;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/ownership-nominations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationOwnershipController(ISender sender) : ApiController(sender)
{
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> NominateAsync(Guid organizationId, NominateOwnerRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.SendAsync(new NominateOwnerCommand(organizationId, request.MembershipId, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : HandleFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, [FromQuery] string status = "pending", [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        string suppliedStatus = Request.Query.TryGetValue("status", out var statuses) ? statuses.ToString() : status;
        string? suppliedCursor = Request.Query.TryGetValue("cursor", out var cursors) ? cursors.ToString() : cursor;
        var result = await Sender.SendAsync(new ListOwnershipNominationsQuery(organizationId, suppliedStatus, limit, suppliedCursor), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("{nominationId:guid}/accept")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> AcceptAsync(Guid organizationId, Guid nominationId, AcceptNominationRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.SendAsync(new AcceptNominationCommand(organizationId, nominationId, request.ExpectedVersion, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("{nominationId:guid}/decline")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> DeclineAsync(Guid organizationId, Guid nominationId, DeclineNominationRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.SendAsync(new DeclineNominationCommand(organizationId, nominationId, request.ExpectedVersion, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("{nominationId:guid}/cancel")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CancelAsync(Guid organizationId, Guid nominationId, CancelNominationRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.SendAsync(new CancelNominationCommand(organizationId, nominationId, request.ExpectedVersion, request.Reason, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
