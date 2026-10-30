using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.SharedKernel.Cqrs;

public interface IPublisher
{
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
