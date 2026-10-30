using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Notification.Tests;

public sealed class DeploymentSmokeTests
{
    [DeploymentFact]
    public async Task DeployedWorkersDeliverEncryptedRequestAndDeduplicateReplay()
    {
        var configuration = DeploymentConfiguration.Load();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        CancellationToken cancellationToken = deadline.Token;
        Guid messageId = Guid.CreateVersion7();
        string recipient = "deployment-" + messageId.ToString("N") + "@example.test";
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string keyId = configuration.Get("DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID", "local-v1");
        string publicKey = await File.ReadAllTextAsync(configuration.SecretPath("notification", "keys", keyId + ".public.pem"), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var request = VerificationEmailEnvelopeFactory.Create(
            messageId, Guid.CreateVersion7(), null, recipient, "Deployment <smoke>", "en", token,
            now, now.AddHours(1), keyId, publicKey);
        string envelope = JsonSerializer.Serialize(request);
        Assert.False(envelope.Contains(recipient, StringComparison.Ordinal));
        Assert.False(envelope.Contains(token, StringComparison.Ordinal));

        await using (var identity = configuration.Identity())
        {
            await new IdentityUnitOfWork(identity).ExecuteInTransactionAsync(async ct =>
            {
                await new IdentityEmailOutbox(identity).StageAsync(request, ct);
                return true;
            }, cancellationToken);
        }

        await WaitUntilAsync(async () =>
        {
            await using var identity = configuration.Identity();
            await using var notification = configuration.Notification();
            return await notification.EmailDeliveries.AnyAsync(x => x.Id == messageId && x.Status == "ProviderAccepted", cancellationToken)
                && await identity.Set<EmailDeliveryReceipt>().AnyAsync(x => x.DeliveryMessageId == messageId, cancellationToken)
                && await identity.Set<OutboxMessage>().AnyAsync(x => x.MessageId == messageId && x.Status == OutboxStatus.Published, cancellationToken);
        }, cancellationToken);

        using var mailpit = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:" + configuration.Port("DELIVERYPILOT_MAILPIT_HTTP_PORT").ToString(CultureInfo.InvariantCulture) + "/"),
            Timeout = TimeSpan.FromSeconds(5),
        };
        string? mailId = null;
        await WaitUntilAsync(async () =>
        {
            string[] matching = await FindMailAsync(mailpit, recipient, cancellationToken);
            Assert.InRange(matching.Length, 0, 1);
            mailId = matching.SingleOrDefault();
            return mailId is not null;
        }, cancellationToken);
        using (var content = JsonDocument.Parse(await mailpit.GetStringAsync("api/v1/message/" + mailId, cancellationToken)))
        {
            string html = content.RootElement.GetProperty("HTML").GetString()!;
            string text = content.RootElement.GetProperty("Text").GetString()!;
            Assert.True(html.Contains("Verify email", StringComparison.Ordinal));
            Assert.True(html.Contains("&lt;smoke&gt;", StringComparison.Ordinal));
            Assert.True(text.Contains("/verify-email#token=", StringComparison.Ordinal));
        }

        await using (var identity = configuration.Identity())
        {
            await new IdentityUnitOfWork(identity).ExecuteInTransactionAsync(async ct =>
            {
                var row = await identity.Set<OutboxMessage>().SingleAsync(x => x.MessageId == messageId, ct);
                row.RequeuePublished(DateTimeOffset.UtcNow);
                return true;
            }, cancellationToken);
        }

        // A second receipt proves the real consumer processed the replay. No test
        // handler or in-process worker is involved in this deployment verification.
        await WaitUntilAsync(async () =>
        {
            await using var notification = configuration.Notification();
            return await notification.Set<OutboxMessage>().CountAsync(x => x.CausationId == messageId && x.Status == OutboxStatus.Published, cancellationToken) >= 2;
        }, cancellationToken);
        await using (var notification = configuration.Notification())
        {
            Assert.Equal(1, await notification.EmailDeliveries.CountAsync(x => x.Id == messageId, cancellationToken));
            Assert.Equal(1, await notification.Set<InboxMessage>().CountAsync(x => x.MessageId == messageId, cancellationToken));
            Assert.Equal(1, await notification.EmailDeliveryAttempts.CountAsync(x => x.DeliveryId == messageId, cancellationToken));
        }

        await using (var identity = configuration.Identity())
        {
            Assert.Equal(1, await identity.Set<EmailDeliveryReceipt>().CountAsync(x => x.DeliveryMessageId == messageId, cancellationToken));
        }

        Assert.Single(await FindMailAsync(mailpit, recipient, cancellationToken));
    }

    [DeploymentFact]
    public async Task DeployedRuntimeRolesCannotChangeSchemasOrAccessAnotherModule()
    {
        var configuration = DeploymentConfiguration.Load();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        CancellationToken cancellationToken = deadline.Token;
        foreach (var role in new[]
        {
            (Module: "identity", User: "identity_api", Password: "api-password.secret"),
            (Module: "identity", User: "identity_worker", Password: "worker-password.secret"),
            (Module: "notification", User: "notification_runtime", Password: "runtime-password.secret"),
        })
        {
            await using var connection = new NpgsqlConnection(configuration.Connection(role.Module, role.User, role.Password));
            await connection.OpenAsync(cancellationToken);
            await using (var privileges = new NpgsqlCommand("SELECT rolsuper OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname = current_user", connection))
            {
                Assert.False((bool)(await privileges.ExecuteScalarAsync(cancellationToken))!);
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var create = new NpgsqlCommand("CREATE SCHEMA \"DeploymentDenied_" + Guid.NewGuid().ToString("N") + "\"", connection, transaction);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync(cancellationToken));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
            await transaction.RollbackAsync(cancellationToken);
        }

        await using (var worker = new NpgsqlConnection(configuration.Connection("identity", "identity_worker", "worker-password.secret")))
        {
            await worker.OpenAsync(cancellationToken);
            await using var read = new NpgsqlCommand("SELECT 1 FROM shared.\"Users\" LIMIT 0", worker);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => read.ExecuteScalarAsync(cancellationToken));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        foreach (var attempt in new[]
        {
            (Target: "notification", User: "identity_api", Owner: "identity", Password: "api-password.secret"),
            (Target: "identity", User: "notification_runtime", Owner: "notification", Password: "runtime-password.secret"),
        })
        {
            string password = await File.ReadAllTextAsync(configuration.SecretPath(attempt.Owner, attempt.Password), cancellationToken);
            var builder = configuration.ConnectionBuilder(attempt.Target, attempt.User, password.Trim());
            await using var otherModule = new NpgsqlConnection(builder.ConnectionString);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => otherModule.OpenAsync(cancellationToken));
            Assert.Contains(denied.SqlState, new[] { PostgresErrorCodes.InvalidPassword, PostgresErrorCodes.InvalidAuthorizationSpecification, PostgresErrorCodes.InsufficientPrivilege });
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        while (!await condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    private static async Task<string[]> FindMailAsync(HttpClient client, string recipient, CancellationToken cancellationToken)
    {
        using var list = JsonDocument.Parse(await client.GetStringAsync("api/v1/messages?limit=200", cancellationToken));
        return list.RootElement.GetProperty("messages").EnumerateArray()
            .Where(message => message.GetProperty("To").EnumerateArray().Any(to => to.GetProperty("Address").GetString() == recipient))
            .Select(message => message.GetProperty("ID").GetString()!)
            .ToArray();
    }

    private sealed class DeploymentFactAttribute : FactAttribute
    {
        public DeploymentFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELIVERYPILOT_DEPLOYMENT_TEST_CONFIG")))
            {
                Skip = "Set DELIVERYPILOT_DEPLOYMENT_TEST_CONFIG to an isolated Development/Mailpit deployment env file.";
            }
        }
    }

    private sealed class DeploymentConfiguration(Dictionary<string, string> values, string secretsDirectory)
    {
        public string Get(string key, string? fallback = null)
            => values.TryGetValue(key, out string? value) ? value : fallback ?? throw new InvalidOperationException("Missing deployment setting " + key + ".");

        public int Port(string key) => int.Parse(Get(key), CultureInfo.InvariantCulture);

        public string SecretPath(params string[] parts) => Path.Combine([secretsDirectory, .. parts]);

        public NpgsqlConnectionStringBuilder ConnectionBuilder(string module, string user, string password)
            => new()
            {
                Host = "127.0.0.1",
                Port = Port(module == "identity" ? "DELIVERYPILOT_IDENTITY_POSTGRES_PORT" : "DELIVERYPILOT_NOTIFICATION_POSTGRES_PORT"),
                Database = module == "identity" ? "DeliveryPilotIdentity" : "DeliveryPilotNotification",
                Username = user,
                Password = password,
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 5,
                IncludeErrorDetail = false,
                LogParameters = false,
            };

        public string Connection(string module, string user, string passwordFile)
            => ConnectionBuilder(module, user, File.ReadAllText(SecretPath(module, passwordFile)).Trim()).ConnectionString;

        public IdentityDbContext Identity()
            => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(Connection("identity", "identity_api", "api-password.secret")).Options);

        public NotificationDbContext Notification()
            => new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(Connection("notification", "notification_runtime", "runtime-password.secret")).Options);

        public static DeploymentConfiguration Load()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeliveryPilot.slnx")))
            {
                directory = directory.Parent;
            }

            string root = directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
            string config = Environment.GetEnvironmentVariable("DELIVERYPILOT_DEPLOYMENT_TEST_CONFIG")!;
            if (!Path.IsPathRooted(config))
            {
                config = Path.Combine(root, config);
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadLines(config))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var match = Regex.Match(line, "^([A-Z][A-Z0-9_]*)=(.*)$", RegexOptions.CultureInvariant);
                if (!match.Success || !values.TryAdd(match.Groups[1].Value, match.Groups[2].Value))
                {
                    throw new InvalidOperationException("Deployment test configuration must contain unique literal KEY=value entries.");
                }
            }

            if (values.GetValueOrDefault("DELIVERYPILOT_ENVIRONMENT") != "Development" || values.GetValueOrDefault("DELIVERYPILOT_EMAIL_MODE") != "Mailpit")
            {
                throw new InvalidOperationException("Deployment smoke tests require an isolated Development deployment using Mailpit.");
            }

            string secrets = values["DELIVERYPILOT_SECRETS_DIR"];
            return new DeploymentConfiguration(values, Path.IsPathRooted(secrets) ? secrets : Path.Combine(root, secrets));
        }
    }
}
