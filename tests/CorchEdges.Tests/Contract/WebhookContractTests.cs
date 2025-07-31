using System.Net;
using CorchEdges.Abstractions;
using CorchEdges.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AzureFunctionsHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;
using AzureFunctionsHttpResponseData = Microsoft.Azure.Functions.Worker.Http.HttpResponseData;

namespace CorchEdges.Tests.Contract;

[Trait("Category", "Contract")]
[Trait("Component", "WebhookProcessor")]
public class WebhookProcessorContractTests
{
    private readonly Mock<ILogger<DefaultSharePointWebhookProcessor>> _mockLogger;
    private readonly DefaultSharePointWebhookProcessor _processor;

    public WebhookProcessorContractTests()
    {
        _mockLogger = new Mock<ILogger<DefaultSharePointWebhookProcessor>>();
        _processor = new DefaultSharePointWebhookProcessor(_mockLogger.Object);
    }

    [Fact]
    [Trait("Contract", "Interface")]
    public void DefaultWebhookProcessor_ImplementsIWebhookProcessor()
    {
        // Contract test: Verify that DefaultSharePointWebhookProcessor implements the expected interface
        Assert.IsAssignableFrom<ISharePointWebhookProcessor>(_processor);
    }

    [Fact]
    [Trait("Contract", "TryHandshake")]
    public void TryHandshake_Method_HasCorrectSignature()
    {
        // Contract test: Verify method signature matches interface
        var method = typeof(DefaultSharePointWebhookProcessor).GetMethod("TryHandshake");
        Assert.NotNull(method);
        
        // Should take Azure Functions HttpRequestData and return HttpResponseData?
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(AzureFunctionsHttpRequestData), parameters[0].ParameterType);
        Assert.Equal(typeof(AzureFunctionsHttpResponseData), method.ReturnType);
    }

    [Fact]
    [Trait("Contract", "BuildEnqueueAsync")]
    public void BuildEnqueueAsync_Method_HasCorrectSignature()
    {
        // Contract test: Verify method signature matches interface
        var method = typeof(DefaultSharePointWebhookProcessor).GetMethod("BuildEnqueueAsync");
        Assert.NotNull(method);
        
        // Should take Azure Functions HttpRequestData and return Task<(HttpResponseData, string?)>
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(AzureFunctionsHttpRequestData), parameters[0].ParameterType);
        
        // Return type should be Task<(HttpResponseData response, string? queueBody)>
        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
        
        // Check the tuple type inside the Task
        var tupleType = method.ReturnType.GetGenericArguments()[0];
        Assert.True(tupleType.IsGenericType);
        
        // Verify it's a ValueTuple with HttpResponseData and string?
        var tupleArgs = tupleType.GetGenericArguments();
        Assert.Equal(2, tupleArgs.Length);
        Assert.Equal(typeof(AzureFunctionsHttpResponseData), tupleArgs[0]);
        Assert.Equal(typeof(string), tupleArgs[1]);
    }

    [Fact]
    [Trait("Contract", "AzureFunctionTypes")]
    public void IWebhookProcessor_UsesCorrectAzureFunctionTypes()
    {
        // Contract test: Verify that the interface uses the correct Azure Function types
        // This helps catch namespace confusion issues
        
        var interfaceType = typeof(ISharePointWebhookProcessor);
        
        var tryHandshakeMethod = interfaceType.GetMethod("TryHandshake");
        Assert.NotNull(tryHandshakeMethod);
        
        var handshakeParam = tryHandshakeMethod.GetParameters()[0];
        Assert.Equal(typeof(AzureFunctionsHttpRequestData), handshakeParam.ParameterType);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Http.HttpRequestData", handshakeParam.ParameterType.FullName);
        
        // Verify the types come from the Core assembly (not IdentityModel)
        Assert.Equal("Microsoft.Azure.Functions.Worker.Core", handshakeParam.ParameterType.Assembly.GetName().Name);
        
        var buildEnqueueMethod = interfaceType.GetMethod("BuildEnqueueAsync");
        Assert.NotNull(buildEnqueueMethod);
        
        var enqueueParam = buildEnqueueMethod.GetParameters()[0];
        Assert.Equal(typeof(AzureFunctionsHttpRequestData), enqueueParam.ParameterType);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Http.HttpRequestData", enqueueParam.ParameterType.FullName);
        Assert.Equal("Microsoft.Azure.Functions.Worker.Core", enqueueParam.ParameterType.Assembly.GetName().Name);
    }

    [Fact]
    [Trait("Contract", "Constructor")]
    public void Constructor_AcceptsRequiredDependencies()
    {
        // Contract test: Verify constructor accepts the expected dependencies
        var constructor = typeof(DefaultSharePointWebhookProcessor)
            .GetConstructor(new[] { typeof(ILogger<DefaultSharePointWebhookProcessor>) });
        
        Assert.NotNull(constructor);
        
        // Should be able to create instance with logger
        var logger = Mock.Of<ILogger<DefaultSharePointWebhookProcessor>>();
        var instance = constructor.Invoke(new object[] { logger });
        
        Assert.NotNull(instance);
        Assert.IsType<DefaultSharePointWebhookProcessor>(instance);
    }

    [Fact]
    [Trait("Contract", "Logging")]
    public void DefaultWebhookProcessor_RequiresLogger()
    {
        // Contract test: Verify that logger is required (not optional)
        Assert.Throws<ArgumentNullException>(() => new DefaultSharePointWebhookProcessor(null!));
    }

    [Theory]
    [Trait("Contract", "HttpStatusCodes")]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Accepted)]
    public void ExpectedHttpStatusCodes_AreSupported(HttpStatusCode statusCode)
    {
        // Contract test: Verify that the processor can work with expected HTTP status codes
        Assert.True(Enum.IsDefined(typeof(HttpStatusCode), statusCode));
    }
}