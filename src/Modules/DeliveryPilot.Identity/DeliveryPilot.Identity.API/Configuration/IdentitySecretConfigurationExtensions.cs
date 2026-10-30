using Microsoft.Extensions.Configuration;

namespace DeliveryPilot.Identity.API.Configuration;

public static class IdentitySecretConfigurationExtensions
{
    public static ConfigurationManager AddIdentitySecretFiles(this ConfigurationManager configuration)
    {
        string directory = configuration["DELIVERYPILOT_CONFIG_SECRETS_DIRECTORY"] ?? "/run/secrets/config";
        configuration.AddKeyPerFile(Path.GetFullPath(directory), optional: true, reloadOnChange: false);
        return configuration;
    }
}
