using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckSession;

public sealed record CheckSessionQuery(Guid UserId, Guid SessionId) : IQuery<SessionCheckResult>;
