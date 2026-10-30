using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.OrganizationInvitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.CancelInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.IssueInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ReissueInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ResendInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.GetInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.ListInvitations;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/invitations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationInvitationsController(ISender sender) : ApiController(sender)
{
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> IssueAsync(Guid organizationId, IssueInvitationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new IssueInvitationCommand(organizationId, request.RecipientEmail, request.RoleIds, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : HandleFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, [FromQuery] string status = "pending", [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        // MVC normally converts empty query strings to null; preserve supplied values for validation.
        string? suppliedCursor = Request.Query.TryGetValue("cursor", out var cursorValues) ? cursorValues.ToString() : cursor;
        string suppliedStatus = Request.Query.TryGetValue("status", out var statusValues) ? statusValues.ToString() : status;
        var result = await Sender.SendAsync(new ListInvitationsQuery(organizationId, suppliedStatus, limit, suppliedCursor), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpGet("{invitationId:guid}")]
    public async Task<IActionResult> GetAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new GetInvitationQuery(organizationId, invitationId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("{invitationId:guid}/resend")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ResendAsync(Guid organizationId, Guid invitationId, ResendInvitationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new ResendInvitationCommand(organizationId, invitationId, request.ExpectedVersion, request.Reason, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("{invitationId:guid}/reissue")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ReissueAsync(Guid organizationId, Guid invitationId, ReissueInvitationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new ReissueInvitationCommand(organizationId, invitationId, request.ExpectedVersion, request.RoleIds, request.Reason, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("{invitationId:guid}/cancel")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CancelAsync(Guid organizationId, Guid invitationId, CancelInvitationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new CancelInvitationCommand(organizationId, invitationId, request.ExpectedVersion, request.Reason, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
