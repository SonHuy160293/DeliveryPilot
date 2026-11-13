using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.API.Contracts.Projects;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;
using DeliveryPilot.Projects.Application.Features.Projects.Commands;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeProjectEngagement;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeSprintLength;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProjectStakeholder;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectStakeholder;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.RetireProjectStakeholder;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectBrief;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.GetProject;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.ListMemberProjects;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProjectParticipants;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.GetProjectBrief;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProductGoalHistory;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProjectStakeholders;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace DeliveryPilot.Projects.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/projects")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProjectsController(ISender sender) : ApiController(sender)
{
    [HttpGet("{projectId:guid}/stakeholders")]
    public async Task<IActionResult> StakeholdersAsync(Guid organizationId, Guid projectId, [FromQuery] bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<ProjectStakeholderView>> result = await Sender.SendAsync(new ListProjectStakeholdersQuery(organizationId, projectId, includeRetired), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{projectId:guid}/stakeholders")]
    [RequestSizeLimit(32768)]
    public async Task<IActionResult> AddStakeholderAsync(Guid organizationId, Guid projectId, ProjectStakeholderRequest request, CancellationToken cancellationToken)
    {
        Result<ProjectStakeholderView> result = await Sender.SendAsync(new CreateProjectStakeholderCommand(organizationId, projectId, request.ToValues()), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{projectId:guid}/stakeholders/{stakeholderId:guid}")]
    [RequestSizeLimit(32768)]
    public async Task<IActionResult> UpdateStakeholderAsync(Guid organizationId, Guid projectId, Guid stakeholderId, ProjectStakeholderRequest request, CancellationToken cancellationToken)
    {
        Result<ProjectStakeholderView> result = await Sender.SendAsync(new UpdateProjectStakeholderCommand(organizationId, projectId, stakeholderId, request.ExpectedVersion, request.ToValues()), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{projectId:guid}/stakeholders/{stakeholderId:guid}/retire")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> RetireStakeholderAsync(Guid organizationId, Guid projectId, Guid stakeholderId, RetireProjectStakeholderRequest request, CancellationToken cancellationToken)
    {
        Result<ProjectStakeholderView> result = await Sender.SendAsync(new RetireProjectStakeholderCommand(organizationId, projectId, stakeholderId, request.ExpectedVersion), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{projectId:guid}/sprint-length")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ChangeSprintLengthAsync(Guid organizationId, Guid projectId, ChangeSprintLengthRequest request, CancellationToken cancellationToken)
    {
        Result<ProjectSettingsResult> result = await Sender.SendAsync(new ChangeSprintLengthCommand(organizationId, projectId, request.ExpectedVersion, request.SprintLengthWeeks), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{projectId:guid}/engagement")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> ChangeEngagementAsync(Guid organizationId, Guid projectId, ChangeProjectEngagementRequest request, CancellationToken cancellationToken)
    {
        Result<ProjectSettingsResult> result = await Sender.SendAsync(new ChangeProjectEngagementCommand(organizationId, projectId, request.ExpectedVersion, request.Kind, request.EngagementId, request.Reason), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{projectId:guid}/brief")]
    public async Task<IActionResult> GetBriefAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        Result<ProjectBriefView> result = await Sender.SendAsync(new GetProjectBriefQuery(organizationId, projectId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{projectId:guid}/brief/product-goal-history")]
    public async Task<IActionResult> ProductGoalHistoryAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ProductGoalHistoryEntry>> result = await Sender.SendAsync(new ListProductGoalHistoryQuery(organizationId, projectId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{projectId:guid}/brief")]
    [RequestSizeLimit(196608)]
    public async Task<IActionResult> UpdateBriefAsync(Guid organizationId, Guid projectId, UpdateProjectBriefRequest request, CancellationToken cancellationToken)
    {
        Result<ProjectBriefView> result = await Sender.SendAsync(new UpdateProjectBriefCommand(organizationId, projectId, request.ExpectedVersion, request.ToValues()), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, [FromQuery] string? status = null, [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        Result<ProjectPage> result = await Sender.SendAsync(new ListMemberProjectsQuery(organizationId, QueryValue("status", status), limit, QueryValue("cursor", cursor)), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        Result<ProjectDetailView> result = await Sender.SendAsync(new GetProjectQuery(organizationId, projectId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{projectId:guid}/participants")]
    public async Task<IActionResult> ParticipantsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ProjectParticipantView>> result = await Sender.SendAsync(new ListProjectParticipantsQuery(organizationId, projectId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CreateAsync(Guid organizationId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        Guid key = Guid.Empty;
        if (Request.Headers.TryGetValue("Idempotency-Key", out StringValues keys) && keys.Count == 1 && Guid.TryParse(keys[0], out Guid parsedKey)) { key = parsedKey; }
        Result<CreateProjectResult> result = await Sender.SendAsync(new CreateProjectCommand(organizationId, key, request.Name, request.Purpose, request.Kind, request.EngagementId), cancellationToken);
        return StatusCode(result.Value.IsReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created, result.Value.Snapshot);
    }

    private string? QueryValue(string name, string? value)
        => Request.Query.TryGetValue(name, out StringValues values) ? values.Count == 1 ? values[0] ?? string.Empty : string.Empty : value;
}
