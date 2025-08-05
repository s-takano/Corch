using System.Net;
using System.Text;
using CorchEdges.Services;
using CorchEdges.Tests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CorchEdges.Tests.Unit.Core;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "WebhookProcessor")]
public class DefaultSharePointWebhookProcessorTests
{
    private readonly Mock<ILogger<DefaultSharePointWebhookProcessor>> _mockLogger;
    private readonly DefaultSharePointWebhookProcessor _processor;
    private readonly TestFunctionContext _functionContext;

    public DefaultSharePointWebhookProcessorTests()
    {
        _mockLogger = new Mock<ILogger<DefaultSharePointWebhookProcessor>>();
        _processor = new DefaultSharePointWebhookProcessor(_mockLogger.Object);
        _functionContext = new TestFunctionContext();
    }

    #region TryHandshake Tests - Focus on Query String Logic

    [Theory]
    [Trait("Method", "TryHandshake")]
    [InlineData("validationtoken", "test-token-123")]
    [InlineData("validationToken", "another-token-456")]
    public void TryHandshake_WithValidationToken_ReturnsOkResponse(string paramName, string tokenValue)
    {
        // Arrange
        string url = $"https://example.com?{paramName}={tokenValue}";
        var mockRequest = (HttpRequestData)CreateTestRequest(null, url);

        // Act
        var result = _processor.TryHandshake(mockRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        
        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Validation handshake responded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Method", "TryHandshake")]
    public void TryHandshake_WithoutValidationToken_ReturnsNull()
    {
        // Arrange
        var mockRequest = (HttpRequestData)CreateTestRequest(null, "https://example.com?other=param");

        // Act
        var result = _processor.TryHandshake(mockRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [Trait("Method", "TryHandshake")]
    public void TryHandshake_WithEmptyQueryString_ReturnsNull()
    {
        // Arrange
        var mockRequest = (HttpRequestData)CreateTestRequest(null, "https://example.com");

        // Act
        var result = _processor.TryHandshake(mockRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [Trait("Method", "TryHandshake")]
    public void TryHandshake_WithMultipleQueryParams_ExtractsCorrectToken()
    {
        // Arrange
        var expectedToken = "correct-token";
        string url = $"https://example.com?foo=bar&validationtoken={expectedToken}&baz=qux";
        var mockRequest = (HttpRequestData)CreateTestRequest(null, url);

        // Act
        var result = _processor.TryHandshake(mockRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    #endregion

    #region BuildEnqueueAsync Tests - Now testable with TestHttpResponseData

    [Fact]
    [Trait("Method", "BuildEnqueueAsync")]
    public async Task BuildEnqueueAsync_WithValidBody_ReturnsAcceptedAndQueueBody()
    {
        // Arrange
        var testBody = "valid notification body";
        var mockRequest = CreateTestRequest(testBody);

        // Act
        var (response, queueBody) = await _processor.BuildEnqueueAsync(mockRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(testBody, queueBody);

        // Verify response content
        if (response is TestHttpResponseData testResponse)
        {
            var content = testResponse.GetWrittenContent();
            Assert.Equal("Queued.", content);
        }
    }

    [Fact]
    [Trait("Method", "BuildEnqueueAsync")]
    public async Task BuildEnqueueAsync_WithEmptyBody_ReturnsBadRequestAndNull()
    {
        // Arrange
        var mockRequest = CreateTestRequest("");

        // Act
        var (response, queueBody) = await _processor.BuildEnqueueAsync(mockRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(queueBody);

        // Verify response content
        if (response is TestHttpResponseData testResponse)
        {
            var content = testResponse.GetWrittenContent();
            Assert.Equal("Empty body", content);
        }
    }

    [Fact]
    [Trait("Method", "BuildEnqueueAsync")]
    public async Task BuildEnqueueAsync_WithNullBody_ReturnsBadRequestAndNull()
    {
        // Arrange
        var mockRequest = CreateTestRequest(null);

        // Act
        var (response, queueBody) = await _processor.BuildEnqueueAsync(mockRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(queueBody);
    }

    #endregion

    #region Query String Parsing Logic Tests (Extracted Logic)

    [Theory]
    [Trait("Logic", "QueryStringParsing")]
    [InlineData("https://example.com?validationtoken=abc123", true, "abc123")]
    [InlineData("https://example.com?validationToken=def456", true, "def456")]
    [InlineData("https://example.com?other=param", false, null)]
    [InlineData("https://example.com", false, null)]
    [InlineData("https://example.com?validationtoken=", true, "")]
    public void QueryStringValidationTokenExtraction_VariousScenarios_WorksCorrectly(
        string url, bool shouldHaveToken, string? expectedToken)
    {
        // Arrange
        var uri = new Uri(url);
        var query = QueryHelpers.ParseQuery(uri.Query);

        // Act
        bool hasValidationToken = query.TryGetValue("validationtoken", out var token1) || 
                                 query.TryGetValue("validationToken", out token1);

        // Assert
        Assert.Equal(shouldHaveToken, hasValidationToken);
        if (shouldHaveToken)
        {
            Assert.Equal(expectedToken, token1.ToString());
        }
    }

    #endregion

    #region Helper Methods

    private HttpRequestData CreateTestRequest(string? body, string? url = "https://example.com")
    {
        return new TestHttpRequestData(_functionContext, url!, body);
    }

    #endregion
}