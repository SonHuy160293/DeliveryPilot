using DeliveryPilot.SharedKernel.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryPilot.Notification.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services) => services.AddSelfMadeCqrs(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly)).AddApplicationPipelineBehaviors();
}
