using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.API.Contracts.Projects;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;
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
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CreateAsync(Guid organizationId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        Guid key = Guid.Empty;
        if (Request.Headers.TryGetValue("Idempotency-Key", out StringValues keys) && keys.Count == 1 && Guid.TryParse(keys[0], out Guid parsedKey)) { key = parsedKey; }
        Result<CreateProjectResult> result = await Sender.SendAsync(new CreateProjectCommand(organizationId, key, request.Name, request.Purpose, request.Kind, request.EngagementId), cancellationToken);
        return StatusCode(result.Value.IsReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created, result.Value.Snapshot);
    }
}
