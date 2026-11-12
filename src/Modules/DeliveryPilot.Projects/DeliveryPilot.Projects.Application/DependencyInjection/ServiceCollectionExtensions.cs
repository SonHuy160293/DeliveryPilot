using DeliveryPilot.SharedKernel.DependencyInjection;
using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries;
using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.Projects.Application.Features.Projects.Access;
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
            .AddScoped<EngagementMutationService>()
            .AddScoped<EngagementReadAuthorization>()
            .AddScoped<ClientLinkAuthorization>()
            .AddScoped<EngagementLinkConfirmationService>()
            .AddScoped<ProjectAccessEvaluator>()
            .AddApplicationCqrs()
            .AddApplicationPipelineBehaviors();

    public static IServiceCollection AddApplicationCqrs(this IServiceCollection services)
        => services.AddSelfMadeCqrs(cfg =>
            cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
}
