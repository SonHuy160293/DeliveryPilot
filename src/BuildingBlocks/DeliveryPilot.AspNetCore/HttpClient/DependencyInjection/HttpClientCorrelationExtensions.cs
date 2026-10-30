using DeliveryPilot.AspNetCore.HttpClient.Abstractions;
using DeliveryPilot.AspNetCore.HttpClient.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryPilot.AspNetCore.HttpClient.DependencyInjection;

public static class HttpClientCorrelationExtensions
{
    public static IServiceCollection AddHttpClientCorrelation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Singleton<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>());
        return services;
    }
}
