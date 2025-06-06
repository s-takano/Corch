using Moq;
using Xunit;

namespace CorchEdges.Tests;

public class MoqVerificationTest
{
    [Fact]
    public void Moq_ShouldWork_BasicTest()
    {
        // Arrange
        var mock = new Mock<ITestInterface>();
        mock.Setup(x => x.GetValue()).Returns("test");

        // Act
        var result = mock.Object.GetValue();

        // Assert
        Assert.Equal("test", result);
        mock.Verify(x => x.GetValue(), Times.Once);
    }

    public interface ITestInterface
    {
        string GetValue();
    }
} 