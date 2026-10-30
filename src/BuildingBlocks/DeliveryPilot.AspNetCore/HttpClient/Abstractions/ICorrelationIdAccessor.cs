namespace DeliveryPilot.AspNetCore.HttpClient.Abstractions;

public interface ICorrelationIdAccessor
{
    Guid? GetCorrelationId();
}
