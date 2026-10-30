using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.SharedKernel.Abstractions.Message;

public interface ICommand : IRequest<Result> { }

public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }
