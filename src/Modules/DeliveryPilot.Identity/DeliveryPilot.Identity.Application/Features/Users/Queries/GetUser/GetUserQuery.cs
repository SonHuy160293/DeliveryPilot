using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Users.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserIntegrationDto>;
