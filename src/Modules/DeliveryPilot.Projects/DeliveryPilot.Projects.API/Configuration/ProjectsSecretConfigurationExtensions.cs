using Microsoft.Extensions.Configuration;

namespace DeliveryPilot.Projects.API.Configuration;

public static class ProjectsSecretConfigurationExtensions
{
    public static ConfigurationManager AddProjectsSecretFiles(this ConfigurationManager configuration)
    {
        string directory = configuration["DELIVERYPILOT_CONFIG_SECRETS_DIRECTORY"] ?? "/run/secrets/config";
        configuration.AddKeyPerFile(Path.GetFullPath(directory), optional: true, reloadOnChange: false);
        return configuration;
    }
}
