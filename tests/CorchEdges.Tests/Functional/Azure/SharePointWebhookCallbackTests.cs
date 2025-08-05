using System.Net;
using CorchEdges.Abstractions;
using CorchEdges.Functions.SharePoint;
using CorchEdges.Tests.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using AzureFunctionsHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;
using AzureFunctionsHttpResponseData = Microsoft.Azure.Functions.Worker.Http.HttpResponseData;
using AzureFunctionsHttpTriggerAttribute = Microsoft.Azure.Functions.Worker.HttpTriggerAttribute;

namespace CorchEdges.Tests.Functional.Azure;

[Trait("Category", TestCategories.Integration)]
[Trait("Target", "ReceiveSharePointChangeNotification")]
public class SharePointWebhookCallbackIntegrationTests
{
    private readonly Mock<ISharePointWebhookProcessor> _mockProcessor;
    private readonly ReceiveSharePointChangeNotification _function;

    public SharePointWebhookCallbackIntegrationTests()
    {
        _mockProcessor = new Mock<ISharePointWebhookProcessor>();
        var mockLogger = new Mock<ILogger<ReceiveSharePointChangeNotification>>();
        _function = new ReceiveSharePointChangeNotification(_mockProcessor.Object, mockLogger.Object);
    }

    [Fact]
    [Trait("Scenario", "Handshake")]
    public async Task Run_WhenHandshakeRequest_ReturnsHandshakeResponseWithNullServiceBus()
    {
        // Arrange
        var mockRequest = CreateMockHttpRequest();
        var expectedResponse = CreateMockHttpResponse(HttpStatusCode.OK);
        
        _mockProcessor
            .Setup(x => x.TryHandshake(mockRequest.Object))
            .Returns(expectedResponse.Object);

        // Act
        var result = await _function.Run(mockRequest.Object);

        // Assert
        Assert.Equal(expectedResponse.Object, result.HttpResponse);
        Assert.Null(result.BusMessage);
        
        // Verify processor was called correctly
        _mockProcessor.Verify(x => x.TryHandshake(mockRequest.Object), Times.Once);
        _mockProcessor.Verify(x => x.BuildEnqueueAsync(It.IsAny<AzureFunctionsHttpRequestData>()), Times.Never);
    }

    [Fact]
    [Trait("Scenario", "Notification")]
    public async Task Run_WhenNotificationRequest_ReturnsAcceptedWithServiceBusMessage()
    {
        // Arrange
        var mockRequest = CreateMockHttpRequest();
        var expectedResponse = CreateMockHttpResponse(HttpStatusCode.Accepted);
        var expectedQueueBody = """{"value": [{"resource": "Lists/test-list"}]}""";

        _mockProcessor
            .Setup(x => x.TryHandshake(mockRequest.Object))
            .Returns((AzureFunctionsHttpResponseData?)null);

        _mockProcessor
            .Setup(x => x.BuildEnqueueAsync(mockRequest.Object))
            .ReturnsAsync((expectedResponse.Object, expectedQueueBody));

        // Act
        var result = await _function.Run(mockRequest.Object);

        // Assert
        Assert.Equal(expectedResponse.Object, result.HttpResponse);
        Assert.Equal(expectedQueueBody, result.BusMessage);
        
        // Verify both processor methods were called
        _mockProcessor.Verify(x => x.TryHandshake(mockRequest.Object), Times.Once);
        _mockProcessor.Verify(x => x.BuildEnqueueAsync(mockRequest.Object), Times.Once);
    }

    [Fact]
    [Trait("Scenario", "ErrorHandling")]
    public async Task Run_WhenProcessorReturnsError_ReturnsErrorResponseWithNullServiceBus()
    {
        // Arrange
        var mockRequest = CreateMockHttpRequest();
        var errorResponse = CreateMockHttpResponse(HttpStatusCode.BadRequest);

        _mockProcessor
            .Setup(x => x.TryHandshake(mockRequest.Object))
            .Returns((AzureFunctionsHttpResponseData?)null);

        _mockProcessor
            .Setup(x => x.BuildEnqueueAsync(mockRequest.Object))
            .ReturnsAsync((errorResponse.Object, null));

        // Act
        var result = await _function.Run(mockRequest.Object);

        // Assert
        Assert.Equal(errorResponse.Object, result.HttpResponse);
        Assert.Null(result.BusMessage);
    }

    [Fact]
    [Trait("Architecture", "TypeValidation")]
    public void Azure_Function_Types_AreFromCorrectNamespace()
    {
        // Verify we're using the Azure Functions Worker types, not other HttpRequestData types
        
        var httpRequestDataType = typeof(AzureFunctionsHttpRequestData);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Http.HttpRequestData", httpRequestDataType.FullName);
        
        // The types are actually defined in Microsoft.Azure.Functions.Worker.Core, not Extensions.Http
        Assert.Equal("Microsoft.Azure.Functions.Worker.Core", httpRequestDataType.Assembly.GetName().Name);
        
        var httpResponseDataType = typeof(AzureFunctionsHttpResponseData);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Http.HttpResponseData", httpResponseDataType.FullName);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Core", httpResponseDataType.Assembly.GetName().Name);
    }

    [Fact]
    [Trait("Architecture", "PackageStructure")]
    public void Azure_Function_Package_Structure_IsCorrect()
    {
        // Document the actual Azure Functions Worker package structure
        
        // Core types (HttpRequestData, HttpResponseData) are in Microsoft.Azure.Functions.Worker.Core
        var httpRequestDataType = typeof(AzureFunctionsHttpRequestData);
        var httpResponseDataType = typeof(AzureFunctionsHttpResponseData);
        
        Assert.Equal("Microsoft.Azure.Functions.Worker.Core", httpRequestDataType.Assembly.GetName().Name);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Core", httpResponseDataType.Assembly.GetName().Name);
        
        // HTTP-specific functionality (like HttpTriggerAttribute) comes from Extensions.Http
        var httpTriggerType = typeof(AzureFunctionsHttpTriggerAttribute);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Extensions.Http", httpTriggerType.Assembly.GetName().Name);
        
        // This documents the expected package structure:
        // - Core types: Microsoft.Azure.Functions.Worker.Core
        // - HTTP extensions: Microsoft.Azure.Functions.Worker.Extensions.Http
        // - ServiceBus extensions: Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
    }

    [Fact]
    [Trait("Architecture", "DependencyInjection")]
    public void SharePointWebhookCallback_Constructor_AcceptsIWebhookProcessor()
    {
        // Test that the Azure Function properly accepts the abstraction
        var mockProcessor = new Mock<ISharePointWebhookProcessor>();
        var function = new ReceiveSharePointChangeNotification(mockProcessor.Object, Mock.Of<ILogger<ReceiveSharePointChangeNotification>>());
        
        Assert.NotNull(function);
        // This ensures the dependency injection will work correctly
    }

    private Mock<AzureFunctionsHttpRequestData> CreateMockHttpRequest()
    {
        var mockRequest = new Mock<AzureFunctionsHttpRequestData>(Mock.Of<FunctionContext>());
        mockRequest.Setup(x => x.Url).Returns(new Uri("https://example.com"));
        return mockRequest;
    }

    private Mock<AzureFunctionsHttpResponseData> CreateMockHttpResponse(HttpStatusCode statusCode)
    {
        var mockResponse = new Mock<AzureFunctionsHttpResponseData>(Mock.Of<FunctionContext>());
        mockResponse.Setup(x => x.StatusCode).Returns(statusCode);
        return mockResponse;
    }
}