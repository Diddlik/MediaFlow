using System.Text.Json;
using MediaFlow.Application.Abstractions;
using MediaFlow.Application.Services;
using MediaFlow.Core.Domain;
using MediaFlow.Web.Background;
using MQTTnet;

namespace MediaFlow.Web.Integrations;

public sealed class MqttIntegrationWorker(
    EventControlService eventControl,
    IRuntimeSettingsStore runtimeSettings,
    AutomationStatus automationStatus,
    IConfiguration configuration,
    ILogger<MqttIntegrationWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("MediaFlow:Mqtt:Enabled", false))
        {
            logger.LogInformation("MQTT integration is disabled");
            return;
        }

        var host = configuration["MediaFlow:Mqtt:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("MQTT integration is enabled but MediaFlow:Mqtt:Host is empty");
            return;
        }

        var port = Math.Clamp(configuration.GetValue("MediaFlow:Mqtt:Port", 1883), 1, 65535);
        var clientId = configuration["MediaFlow:Mqtt:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) clientId = "mediaflow-" + Environment.MachineName.ToLowerInvariant();

        var baseTopic = NormalizeBaseTopic(configuration["MediaFlow:Mqtt:BaseTopic"] ?? "mediaflow");
        var commandTopic = $"{baseTopic}/events/command";
        var eventStateTopic = $"{baseTopic}/events/state";
        var statusTopic = $"{baseTopic}/status";

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += async args =>
        {
            if (!string.Equals(args.ApplicationMessage.Topic, commandTopic, StringComparison.Ordinal)) return;

            await _commandGate.WaitAsync(stoppingToken);
            try
            {
                await HandleCommandAsync(
                    client,
                    args.ApplicationMessage.ConvertPayloadToString(),
                    eventStateTopic,
                    statusTopic,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MQTT command handling failed");
                await PublishAsync(client, eventStateTopic, new
                {
                    ok = false,
                    error = ex.Message,
                    at = DateTimeOffset.UtcNow
                }, retain: false, CancellationToken.None);
            }
            finally
            {
                _commandGate.Release();
            }
        };

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(host, port);

        var username = configuration["MediaFlow:Mqtt:Username"];
        var password = configuration["MediaFlow:Mqtt:Password"];
        if (!string.IsNullOrWhiteSpace(username))
            optionsBuilder.WithCredentials(username, password ?? string.Empty);

        if (configuration.GetValue("MediaFlow:Mqtt:UseTls", false))
            optionsBuilder.WithTlsOptions(_ => { });

        var options = optionsBuilder.Build();
        var reconnectSeconds = Math.Clamp(configuration.GetValue("MediaFlow:Mqtt:ReconnectSeconds", 5), 1, 300);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(options, stoppingToken);
                    var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(commandTopic)
                        .Build();
                    await client.SubscribeAsync(subscribeOptions, stoppingToken);
                    logger.LogInformation("MQTT connected to {Host}:{Port}; subscribed to {Topic}", host, port, commandTopic);
                    await PublishStatusAsync(client, statusTopic, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(reconnectSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MQTT connection failed; retrying in {Seconds}s", reconnectSeconds);
                await Task.Delay(TimeSpan.FromSeconds(reconnectSeconds), stoppingToken);
            }
        }

        if (client.IsConnected)
        {
            try { await client.DisconnectAsync(); }
            catch (Exception ex) { logger.LogDebug(ex, "MQTT disconnect failed during shutdown"); }
        }
    }

    private async Task HandleCommandAsync(
        dynamic client,
        string payload,
        string eventStateTopic,
        string statusTopic,
        CancellationToken cancellationToken)
    {
        MqttEventCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<MqttEventCommand>(payload, _jsonOptions);
        }
        catch (JsonException ex)
        {
            await PublishAsync(client, eventStateTopic, new
            {
                ok = false,
                error = "Invalid JSON: " + ex.Message,
                at = DateTimeOffset.UtcNow
            }, false, cancellationToken);
            return;
        }

        if (command is null || string.IsNullOrWhiteSpace(command.Action))
        {
            await PublishAsync(client, eventStateTopic, new
            {
                ok = false,
                error = "Command action is required.",
                at = DateTimeOffset.UtcNow
            }, false, cancellationToken);
            return;
        }

        var action = command.Action.Trim().ToLowerInvariant();
        EventControlResult result;

        switch (action)
        {
            case "start":
                if (command.EventId is null)
                {
                    await PublishInvalidAsync(client, eventStateTopic, action, "eventId is required.", cancellationToken);
                    return;
                }
                result = await eventControl.StartAsync(command.EventId.Value, cancellationToken);
                break;

            case "stop":
                if (command.EventId is null)
                {
                    await PublishInvalidAsync(client, eventStateTopic, action, "eventId is required.", cancellationToken);
                    return;
                }
                result = await eventControl.StopAsync(command.EventId.Value, cancellationToken);
                break;

            case "quick-start":
                if (string.IsNullOrWhiteSpace(command.Name) || command.SourceGroupId is null || command.DestinationShareId is null)
                {
                    await PublishInvalidAsync(
                        client,
                        eventStateTopic,
                        action,
                        "name, sourceGroupId and destinationShareId are required.",
                        cancellationToken);
                    return;
                }

                if (!TryEnum(command.OperationMode, OperationMode.SafeMove, out OperationMode operationMode) ||
                    !TryEnum(command.ConflictStrategy, ConflictStrategy.AppendSourceName, out ConflictStrategy conflictStrategy) ||
                    !TryEnum(command.DuplicateStrategy, DuplicateStrategy.SafeMoveToExisting, out DuplicateStrategy duplicateStrategy))
                {
                    await PublishInvalidAsync(client, eventStateTopic, action, "One or more strategy values are invalid.", cancellationToken);
                    return;
                }

                result = await eventControl.QuickStartAsync(new QuickStartEventCommand(
                    command.Name,
                    command.SourceGroupId.Value,
                    command.DestinationShareId.Value,
                    command.Type ?? "Vacation",
                    command.DestinationFolderTemplate ?? "{event.name}",
                    operationMode,
                    conflictStrategy,
                    duplicateStrategy), cancellationToken);
                break;

            case "quick-stop":
                if (string.IsNullOrWhiteSpace(command.Name))
                {
                    await PublishInvalidAsync(client, eventStateTopic, action, "name is required.", cancellationToken);
                    return;
                }
                result = await eventControl.QuickStopAsync(command.Name, cancellationToken);
                break;

            default:
                await PublishInvalidAsync(client, eventStateTopic, action, "Unknown action.", cancellationToken);
                return;
        }

        await PublishAsync(client, eventStateTopic, new
        {
            ok = result.Status is EventControlStatus.Success or EventControlStatus.Created,
            action,
            status = result.Status.ToString(),
            error = result.Error,
            @event = result.Event,
            at = DateTimeOffset.UtcNow
        }, false, cancellationToken);

        await PublishStatusAsync(client, statusTopic, cancellationToken);
    }

    private async Task PublishStatusAsync(dynamic client, string topic, CancellationToken cancellationToken)
    {
        var settings = await runtimeSettings.GetAsync(cancellationToken);
        await PublishAsync(client, topic, new
        {
            service = "MediaFlow",
            online = true,
            mode = settings.DryRun ? "dry-run" : "live",
            settings.AutomationEnabled,
            automation = automationStatus.Snapshot(),
            at = DateTimeOffset.UtcNow
        }, retain: true, cancellationToken);
    }

    private Task PublishInvalidAsync(
        dynamic client,
        string topic,
        string action,
        string error,
        CancellationToken cancellationToken) =>
        PublishAsync(client, topic, new
        {
            ok = false,
            action,
            status = EventControlStatus.Invalid.ToString(),
            error,
            at = DateTimeOffset.UtcNow
        }, false, cancellationToken);

    private async Task PublishAsync(
        dynamic client,
        string topic,
        object payload,
        bool retain,
        CancellationToken cancellationToken)
    {
        var builder = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(payload, _jsonOptions));
        if (retain) builder.WithRetainFlag();
        await client.PublishAsync(builder.Build(), cancellationToken);
    }

    private static bool TryEnum<T>(string? value, T defaultValue, out T result) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = defaultValue;
            return true;
        }
        return Enum.TryParse(value, ignoreCase: true, out result);
    }

    private static string NormalizeBaseTopic(string topic) =>
        string.IsNullOrWhiteSpace(topic) ? "mediaflow" : topic.Trim().Trim('/');
}

public sealed record MqttEventCommand(
    string Action,
    Guid? EventId = null,
    string? Name = null,
    Guid? SourceGroupId = null,
    Guid? DestinationShareId = null,
    string? Type = null,
    string? DestinationFolderTemplate = null,
    string? OperationMode = null,
    string? ConflictStrategy = null,
    string? DuplicateStrategy = null);
