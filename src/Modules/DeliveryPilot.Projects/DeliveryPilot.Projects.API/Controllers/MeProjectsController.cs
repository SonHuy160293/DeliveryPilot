using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Application.Features.Projects.Queries.ListGuestProjects;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace DeliveryPilot.Projects.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/me/guest-projects")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MeProjectsController(ISender sender) : ApiController(sender)
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string? status = null, [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        Result<ProjectPage> result = await Sender.SendAsync(new ListGuestProjectsQuery(QueryValue("status", status), limit, QueryValue("cursor", cursor)), cancellationToken);
        return Ok(result.Value);
    }

    private string? QueryValue(string name, string? value)
        => Request.Query.TryGetValue(name, out StringValues values) ? values.Count == 1 ? values[0] ?? string.Empty : string.Empty : value;
}
