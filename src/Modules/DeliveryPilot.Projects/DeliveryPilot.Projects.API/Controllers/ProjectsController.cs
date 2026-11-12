using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.API.Contracts.Projects;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.GetProject;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.ListMemberProjects;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProjectParticipants;
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
