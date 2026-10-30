using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.SharedKernel.Abstractions.Message;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse> { }
