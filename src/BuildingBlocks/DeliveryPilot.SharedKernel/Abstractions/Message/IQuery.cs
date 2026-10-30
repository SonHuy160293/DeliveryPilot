using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.SharedKernel.Abstractions.Message;

public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }
