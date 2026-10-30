namespace DeliveryPilot.SharedKernel.Abstractions.Message;

public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent { }
