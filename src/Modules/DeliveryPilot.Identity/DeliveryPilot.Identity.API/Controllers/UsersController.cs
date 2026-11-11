using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.Application.Features.Users.Queries.GetUser;
using DeliveryPilot.Identity.Application.Features.Users.Queries.LookupUsers;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

public sealed class UsersController(ISender sender) : IntegrationApiController(sender)
{
    [HttpPost("lookup")]
    [RequestSizeLimit(16384)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<UserLookupResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserLookupResult>> LookupAsync(UserLookupRequest request, CancellationToken cancellationToken)
    {
        Result<UserLookupResult> result = await Sender.SendAsync(new LookupUsersQuery(request.UserIds), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserIntegrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserIntegrationDto>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        Result<UserIntegrationDto> result = await Sender.SendAsync(new GetUserQuery(userId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
