using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.API.Contracts.Engagements;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.IssueEngagementLinkCode;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.CloseEngagement;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.ReopenEngagement;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementClientLink;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.SetEngagementContractValue;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.UpdateEngagementDetails;
using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries.GetEngagement;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries.ListEngagementHistory;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries.ListEngagements;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace DeliveryPilot.Projects.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/engagements")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EngagementsController(ISender sender) : ApiController(sender)
{
    [HttpPut("{engagementId:guid}")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> UpdateAsync(Guid organizationId, Guid engagementId, UpdateEngagementRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateEngagementDetailsCommand(
            organizationId,
            engagementId,
            request.ExpectedVersion,
            request.ClientDisplayName,
            request.AgreementReference,
            request.AgreementSignedOn,
            request.ClientLegalName,
            request.ClientCountryCode,
            request.EngagementName,
            request.DocumentUrl,
            request.Reason);
        Result<UpdatedEngagementDetails> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{engagementId:guid}/client-link/remove")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> RemoveClientLinkAsync(Guid organizationId, Guid engagementId, RemoveEngagementClientLinkRequest request, CancellationToken cancellationToken)
    {
        var command = new RemoveEngagementClientLinkCommand(organizationId, engagementId, request.ExpectedVersion, request.Reason);
        Result<EngagementStateResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{engagementId:guid}/link-codes")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> IssueLinkCodeAsync(Guid organizationId, Guid engagementId, IssueEngagementLinkCodeRequest request, CancellationToken cancellationToken)
    {
        var command = new IssueEngagementLinkCodeCommand(organizationId, engagementId, request.ExpectedVersion);
        Result<IssuedEngagementLinkCode> result = await Sender.SendAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, [FromQuery] string? status = null, [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        var query = new ListEngagementsQuery(organizationId, QueryValue("status", status), limit, QueryValue("cursor", cursor));
        Result<EngagementPage<EngagementListEntry>> result = await Sender.SendAsync(query, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{engagementId:guid}")]
    public async Task<IActionResult> GetAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        var query = new GetEngagementQuery(organizationId, engagementId);
        Result<EngagementView> result = await Sender.SendAsync(query, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{engagementId:guid}/history")]
    public async Task<IActionResult> HistoryAsync(Guid organizationId, Guid engagementId, [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        var query = new ListEngagementHistoryQuery(organizationId, engagementId, limit, QueryValue("cursor", cursor));
        Result<EngagementPage<EngagementHistoryEntry>> result = await Sender.SendAsync(query, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{engagementId:guid}/close")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CloseAsync(Guid organizationId, Guid engagementId, CloseEngagementRequest request, CancellationToken cancellationToken)
    {
        var command = new CloseEngagementCommand(organizationId, engagementId, request.ExpectedVersion, request.Reason);
        Result<EngagementStateResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{engagementId:guid}/reopen")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ReopenAsync(Guid organizationId, Guid engagementId, ReopenEngagementRequest request, CancellationToken cancellationToken)
    {
        var command = new ReopenEngagementCommand(organizationId, engagementId, request.ExpectedVersion, request.Reason);
        Result<EngagementStateResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{engagementId:guid}/contract-value")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> SetContractValueAsync(Guid organizationId, Guid engagementId, SetEngagementContractValueRequest request, CancellationToken cancellationToken)
    {
        var command = new SetEngagementContractValueCommand(organizationId, engagementId, request.ExpectedVersion, request.ContractAmount, request.ContractCurrency, request.PlannedEffortPersonDays, request.Reason);
        Result<EngagementContractValueResult> result = await Sender.SendAsync(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CreateAsync(Guid organizationId, CreateEngagementRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Guid key = Guid.Empty;
        if (Request.Headers.TryGetValue("Idempotency-Key", out StringValues keys) && keys.Count == 1 && Guid.TryParse(keys[0], out Guid parsedKey))
        {
            key = parsedKey;
        }

        var command = new CreateEngagementCommand(
            organizationId,
            key,
            request.ClientDisplayName,
            request.AgreementReference,
            request.AgreementSignedOn,
            request.ClientLegalName,
            request.ClientCountryCode,
            request.EngagementName,
            request.DocumentUrl);
        Result<CreateEngagementResult> result = await Sender.SendAsync(command, cancellationToken);
        return StatusCode(result.Value.IsReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created, result.Value.Snapshot);
    }

    private string? QueryValue(string name, string? boundValue)
    {
        if (!Request.Query.TryGetValue(name, out StringValues values))
        {
            return boundValue;
        }

        return values.Count == 1 ? values[0] ?? string.Empty : string.Empty;
    }
}
