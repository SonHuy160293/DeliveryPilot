using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.SharedKernel.Cqrs;

public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
