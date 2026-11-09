using DeliveryPilot.SharedKernel.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryPilot.Projects.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Projects CQRS handlers and the shared pipeline (validation, logging, performance,
    /// tracing, transaction, handler).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
        => services
            .AddApplicationCqrs()
            .AddApplicationPipelineBehaviors();

    public static IServiceCollection AddApplicationCqrs(this IServiceCollection services)
        => services.AddSelfMadeCqrs(cfg =>
            cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
}
