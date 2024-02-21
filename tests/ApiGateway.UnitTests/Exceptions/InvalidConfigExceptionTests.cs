using ApiGateway.Exceptions;

namespace ApiGateway.UnitTests.Exceptions;

public class InvalidConfigExceptionTests
{
    [Theory]
    [InlineData("DatabaseConnectionString")]
    public void MissingConfigMessage_ValidConfigName_ReturnsExpectedMessage(string configName)
    {
        // Arrange
        var expectedMessage = $"The configuration '{configName}' is required but was not found.";

        // Act
        var actualMessage = InvalidConfigException.MissingConfigMessage(configName);

        // Assert
        actualMessage.Should().Be(expectedMessage);
    }

    [Theory]
    [InlineData("ApiEndpoint")]
    public void IncorrectValueMessage_ValidConfigName_ReturnsExpectedMessage(string configName)
    {
        // Arrange
        var expectedMessage = $"The value provided for '{configName}' is incorrect or not supported.";

        // Act
        var actualMessage = InvalidConfigException.IncorrectValueMessage(configName);

        // Assert
        actualMessage.Should().Be(expectedMessage);
    }
    [Fact]
    public void Constructor_NoParameters_SetsDefaultMessage()
    {
        // Act
        var exception = new InvalidConfigException();

        // Assert
        exception.Message.Should().Be(InvalidConfigException.DefaultMessage);
    }
    [Theory]
    [InlineData("Custom error message.")]
    public void Constructor_CustomMessage_SetsCustomMessage(string customMessage)
    {
        // Act
        var exception = new InvalidConfigException(customMessage);

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
        var exception = new InvalidConfigException(customMessage, innerException);

        // Assert
        exception.Message.Should().Be(customMessage);
        exception.InnerException.Should().Be(innerException);
    }



}