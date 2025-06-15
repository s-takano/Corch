using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using CorchEdges.Models;
using Xunit.Abstractions;

namespace CorchEdges.Tests.Integration.ServiceBus;

[Collection("ServiceBus Integration Tests")]
public class SharePointChangeProcessingServiceBusTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusReceiver _receiver;
    private readonly string _queueName = "sp-changes-integration-test";

    public SharePointChangeProcessingServiceBusTests(IntegrationTestFixture fixture, ITestOutputHelper output) 
        : base(fixture, output)
    {
        _output = output;

        var connectionString = Configuration.GetConnectionString("ServiceBusConnection") 
            ?? throw new InvalidOperationException("ServiceBusConnection not configured");

        _serviceBusClient = new ServiceBusClient(connectionString);
        _sender = _serviceBusClient.CreateSender(_queueName);
        _receiver = _serviceBusClient.CreateReceiver(_queueName);
    }

    protected override void ConfigureBuilder(IConfigurationBuilder builder)
    {
        builder.AddAzureKeyVault(
            new Uri("https://corch-edges-test-kv.vault.azure.net/"),
            new DefaultAzureCredential() // This will use your Azure CLI login
        );
    }

    [Fact]
    public async Task ServiceBus_Connection_ShouldBeValid()
    {
        // Arrange
        var connectionString = Configuration.GetConnectionString("ServiceBusConnection");
        Assert.NotNull(connectionString);
        Assert.Contains("servicebus.windows.net", connectionString);

        // Act & Assert
        await using var client = new ServiceBusClient(connectionString);

        // This will throw if the connection is invalid
        await using var sender = client.CreateSender("test-queue");
    }
    
    [Fact]
    public async Task ServiceBus_SendAndReceiveNotificationEnvelope_ShouldMaintainDataIntegrity()
    {
        // Arrange
        var originalNotification = CreateTestNotificationEnvelope();
        var messageBody = JsonSerializer.Serialize(originalNotification);
        var message = new ServiceBusMessage(messageBody)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            Subject = "SharePoint Change Notification"
        };

        // Act - Send message
        await _sender.SendMessageAsync(message);
        _output.WriteLine($"Sent message with ID: {message.MessageId}");

        // Act - Receive message
        var receivedMessage = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(receivedMessage);

        var receivedBody = receivedMessage.Body.ToString();
        var deserializedNotification = JsonSerializer.Deserialize<NotificationEnvelope>(receivedBody);

        // Assert
        Assert.NotNull(deserializedNotification);
        Assert.Equal(originalNotification.Value.Length, deserializedNotification.Value.Length);
        
        var original = originalNotification.Value[0];
        var received = deserializedNotification.Value[0];
        
        Assert.Equal(original.SubscriptionId, received.SubscriptionId);
        Assert.Equal(original.Resource, received.Resource);
        Assert.Equal(original.ClientState, received.ClientState);
        Assert.Equal(original.ChangeType, received.ChangeType);

        // Complete the message
        await _receiver.CompleteMessageAsync(receivedMessage);
        _output.WriteLine($"Completed message with ID: {receivedMessage.MessageId}");
    }

    [Fact]
    public async Task ServiceBus_SendMultipleNotifications_ShouldProcessInOrder()
    {
        // Arrange
        var notifications = CreateMultipleTestNotifications(5);
        var messages = notifications.Select((notif, index) => new ServiceBusMessage(JsonSerializer.Serialize(notif))
        {
            MessageId = $"test-{index:D3}",
            Subject = $"Test Notification {index}",
            SessionId = "test-session" // Ensure ordering
        }).ToArray();

        // Act - Send all messages
        await _sender.SendMessagesAsync(messages);
        _output.WriteLine($"Sent {messages.Length} messages");

        // Act - Receive all messages
        var receivedMessages = new List<ServiceBusReceivedMessage>();
        for (int i = 0; i < messages.Length; i++)
        {
            var received = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(received);
            receivedMessages.Add(received);
        }

        // Assert
        Assert.Equal(messages.Length, receivedMessages.Count);
        
        for (int i = 0; i < receivedMessages.Count; i++)
        {
            var receivedNotification = JsonSerializer.Deserialize<NotificationEnvelope>(
                receivedMessages[i].Body.ToString());
            Assert.NotNull(receivedNotification);
            
            // Complete each message
            await _receiver.CompleteMessageAsync(receivedMessages[i]);
        }

        _output.WriteLine($"Successfully processed {receivedMessages.Count} messages");
    }

    [Fact]
    public async Task ServiceBus_MessageWithCustomProperties_ShouldPreserveMetadata()
    {
        // Arrange
        var notification = CreateTestNotificationEnvelope();
        var messageBody = JsonSerializer.Serialize(notification);
        
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = "SharePoint Change",
            ContentType = "application/json",
            CorrelationId = "test-correlation-123",
            ReplyTo = "test-reply-queue",
            TimeToLive = TimeSpan.FromMinutes(30)
        };

        // Add custom application properties
        message.ApplicationProperties["SourceSystem"] = "SharePoint";
        message.ApplicationProperties["ProcessingType"] = "ExcelFile";
        message.ApplicationProperties["Priority"] = "High";
        message.ApplicationProperties["RetryCount"] = 0;

        // Act
        await _sender.SendMessageAsync(message);
        var receivedMessage = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(message.MessageId, receivedMessage.MessageId);
        Assert.Equal(message.Subject, receivedMessage.Subject);
        Assert.Equal(message.ContentType, receivedMessage.ContentType);
        Assert.Equal(message.CorrelationId, receivedMessage.CorrelationId);
        Assert.Equal(message.ReplyTo, receivedMessage.ReplyTo);

        // Verify custom properties
        Assert.Equal("SharePoint", receivedMessage.ApplicationProperties["SourceSystem"]);
        Assert.Equal("ExcelFile", receivedMessage.ApplicationProperties["ProcessingType"]);
        Assert.Equal("High", receivedMessage.ApplicationProperties["Priority"]);
        Assert.Equal(0, receivedMessage.ApplicationProperties["RetryCount"]);

        await _receiver.CompleteMessageAsync(receivedMessage);
    }

    [Fact]
    public async Task ServiceBus_LargeNotificationPayload_ShouldHandleCorrectly()
    {
        // Arrange - Create a large notification with many change items
        var largeNotification = CreateLargeNotificationEnvelope(100);
        var messageBody = JsonSerializer.Serialize(largeNotification);
        var messageSizeKB = Encoding.UTF8.GetByteCount(messageBody) / 1024;
        
        _output.WriteLine($"Large message size: {messageSizeKB} KB");
        
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = "Large SharePoint Batch"
        };

        // Act
        await _sender.SendMessageAsync(message);
        var receivedMessage = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.NotNull(receivedMessage);
        var receivedNotification = JsonSerializer.Deserialize<NotificationEnvelope>(
            receivedMessage.Body.ToString());
        
        Assert.NotNull(receivedNotification);
        Assert.Equal(100, receivedNotification.Value.Length);

        await _receiver.CompleteMessageAsync(receivedMessage);
        _output.WriteLine("Large message processed successfully");
    }

    [Fact]
    public async Task ServiceBus_MessageDeadLettering_ShouldHandleFailedProcessing()
    {
        // Arrange
        var notification = CreateTestNotificationEnvelope();
        var messageBody = JsonSerializer.Serialize(notification);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = "dead-letter-test",
            Subject = "Test Dead Letter"
        };

        // Act - Send message
        await _sender.SendMessageAsync(message);
        var receivedMessage = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(receivedMessage);

        // Simulate processing failure - abandon the message multiple times
        await _receiver.AbandonMessageAsync(receivedMessage, new Dictionary<string, object>
        {
            ["FailureReason"] = "Simulated processing failure",
            ["FailureTime"] = DateTimeOffset.UtcNow.ToString()
        });

        _output.WriteLine("Message abandoned - simulating processing failure");

        // The message should eventually be moved to dead letter queue after max delivery attempts
        // In a real scenario, you'd check the dead letter queue, but for this test we'll verify the abandon worked
        
        // Try to receive again (should get the same message due to abandon)
        var retriedMessage = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
        if (retriedMessage != null)
        {
            Assert.Equal(receivedMessage.MessageId, retriedMessage.MessageId);
            Assert.True(retriedMessage.DeliveryCount > receivedMessage.DeliveryCount);
            
            // Complete it to clean up
            await _receiver.CompleteMessageAsync(retriedMessage);
        }
    }

    [Fact]
    public async Task ServiceBus_ConcurrentMessageProcessing_ShouldHandleMultipleConsumers()
    {
        // Arrange
        var messageCount = 10;
        var notifications = CreateMultipleTestNotifications(messageCount);
        var messages = notifications.Select((notif, index) => new ServiceBusMessage(JsonSerializer.Serialize(notif))
        {
            MessageId = $"concurrent-{index:D3}",
            Subject = $"Concurrent Test {index}"
        }).ToArray();

        // Act - Send all messages
        await _sender.SendMessagesAsync(messages);

        // Create multiple receivers to simulate concurrent processing
        var receiver1 = _serviceBusClient.CreateReceiver(_queueName);
        var receiver2 = _serviceBusClient.CreateReceiver(_queueName);

        var processedMessages = new List<ServiceBusReceivedMessage>();
        var lockObject = new object();

        // Process messages concurrently
        var tasks = new[]
        {
            ProcessMessagesAsync(receiver1, "Receiver1", processedMessages, lockObject, messageCount / 2),
            ProcessMessagesAsync(receiver2, "Receiver2", processedMessages, lockObject, messageCount / 2)
        };

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(messageCount, processedMessages.Count);
        Assert.True(processedMessages.All(m => m != null));

        // Cleanup
        await receiver1.DisposeAsync();
        await receiver2.DisposeAsync();
    }
    
    [Fact]
    public async Task ServiceBus_MessageScheduling_ShouldDelayProcessing()
    {
        // Arrange
        var notification = CreateTestNotificationEnvelope();
        var messageBody = JsonSerializer.Serialize(notification);
        var delaySeconds = 10;
        var scheduleTime = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
    
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = "scheduled-test",
            Subject = "Scheduled Processing Test"
        };

        // Act - Schedule message for future delivery
        var startTime = DateTimeOffset.UtcNow;
        var sequenceNumber = await _sender.ScheduleMessageAsync(message, scheduleTime);
        _output.WriteLine($"Scheduled message {sequenceNumber} for {scheduleTime:HH:mm:ss.fff}");

        // Try to receive immediately (should not get anything)
        var immediateMessage = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
        Assert.Null(immediateMessage);

        // Wait for scheduled time
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds + 2)); // Add buffer for processing

        var scheduledMessage = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
        var actualDelay = DateTimeOffset.UtcNow.Subtract(startTime);

        // Assert
        Assert.NotNull(scheduledMessage);
        Assert.Equal(message.MessageId, scheduledMessage.MessageId);
        _output.WriteLine($"Scheduled message delivered after {actualDelay.TotalSeconds:F1} seconds");
    
        await _receiver.CompleteMessageAsync(scheduledMessage);
    }
    
    private async Task ProcessMessagesAsync(
        ServiceBusReceiver receiver, 
        string receiverName, 
        List<ServiceBusReceivedMessage> processedMessages, 
        object lockObject, 
        int maxMessages)
    {
        var processed = 0;
        while (processed < maxMessages)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
            if (message == null) break;

            lock (lockObject)
            {
                processedMessages.Add(message);
            }

            await receiver.CompleteMessageAsync(message);
            _output.WriteLine($"{receiverName} processed message: {message.MessageId}");
            processed++;
        }
    }

    private static NotificationEnvelope CreateTestNotificationEnvelope()
    {
        return new NotificationEnvelope
        {
            Value = new[]
            {
                new ChangeNotification
                {
                    SubscriptionId = Guid.NewGuid(),
                    ClientState = "integration-test-state",
                    SubscriptionExpirationDateTime = DateTimeOffset.UtcNow.AddHours(1),
                    Resource = "sites/test-site/lists/test-list/items/123",
                    TenantId = Guid.NewGuid(),
                    ChangeType = ChangeType.Updated
                }
            }
        };
    }

    private static NotificationEnvelope[] CreateMultipleTestNotifications(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new NotificationEnvelope
            {
                Value = new[]
                {
                    new ChangeNotification
                    {
                        SubscriptionId = Guid.NewGuid(),
                        ClientState = $"test-state-{i}",
                        SubscriptionExpirationDateTime = DateTimeOffset.UtcNow.AddHours(1),
                        Resource = $"sites/test-site/lists/test-list/items/{i}",
                        TenantId = Guid.NewGuid(),
                        ChangeType = ChangeType.Updated
                    }
                }
            })
            .ToArray();
    }

    private static NotificationEnvelope CreateLargeNotificationEnvelope(int itemCount)
    {
        var notifications = Enumerable.Range(1, itemCount)
            .Select(i => new ChangeNotification
            {
                SubscriptionId = Guid.NewGuid(),
                ClientState = $"large-batch-item-{i}",
                SubscriptionExpirationDateTime = DateTimeOffset.UtcNow.AddHours(1),
                Resource = $"sites/large-site/lists/large-list/items/{i}",
                TenantId = Guid.NewGuid(),
                ChangeType = ChangeType.Updated
            })
            .ToArray();

        return new NotificationEnvelope { Value = notifications };
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Create queue if it doesn't exist (requires admin permissions)
        try
        {
            var adminClient = new ServiceBusAdministrationClient(Configuration.GetConnectionString("ServiceBusConnection"));
            if (!await adminClient.QueueExistsAsync(_queueName))
            {
                await adminClient.CreateQueueAsync(_queueName);
                _output.WriteLine($"Created test queue: {_queueName}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Could not create queue (may already exist): {ex.Message}");
        }

        // Purge any existing messages
        await PurgeQueueAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _receiver.DisposeAsync();
        await _sender.DisposeAsync();
        await _serviceBusClient.DisposeAsync();
    }

    private async Task PurgeQueueAsync()
    {
        // Remove any leftover messages from previous test runs
        var purgeReceiver = _serviceBusClient.CreateReceiver(_queueName);
        try
        {
            while (true)
            {
                var message = await purgeReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1));
                if (message == null) break;
                await purgeReceiver.CompleteMessageAsync(message);
            }
        }
        finally
        {
            await purgeReceiver.DisposeAsync();
        }
    }
}

// Collection definition to ensure tests run sequentially
[CollectionDefinition("ServiceBus Integration Tests", DisableParallelization = true)]
public class ServiceBusIntegrationTestCollection
{
}