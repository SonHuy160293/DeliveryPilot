using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.SharedKernel.Abstractions.Message;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand { }

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse> { }
