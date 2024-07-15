using ApiGateway.Exceptions;

namespace ApiGateway.UnitTests.Exceptions;
public  class GatewayExceptionTests
{
    [Fact]
    public void Constructor_NoParameters_SetsDefaultMessage()
    {
        // Act
        var exception = new GatewayException();

        // Assert
        exception.Message.Should().Be(GatewayException.DefaultMessage);
    }
    [Theory]
    [InlineData("Custom error message.")]
    public void Constructor_CustomMessage_SetsCustomMessage(string customMessage)
    {
        // Act
        var exception = new GatewayException(customMessage);

        // Assert
        exception.Message.Should().Be(customMessage);
    }
    [Theory]
    [InlineData("Custom error message.")]
    public void Constructor_CustomMessageAndInnerException_SetsCustomMessageAndInnerException(string customMessage)
    {
        // Arrange
        var innerException = new Exception("Inner exception message");

        // Act
        var exception = new GatewayException(customMessage, innerException);

        // Assert
        exception.Message.Should().Be(customMessage);
        exception.InnerException.Should().Be(innerException);
    }
}
