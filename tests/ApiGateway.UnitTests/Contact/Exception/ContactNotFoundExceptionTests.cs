using ApiGateway.Contact.Exceptions;

namespace ApiGateway.UnitTests.Contact.Exception
{
    public class ContactNotFoundExceptionTests
    {
        [Fact]
        public void ContactNotFoundException_Constructor()
        {
            // Act
            var exception = new ContactNotFoundException();

            // Assert
            exception.Message.Should().Be(ContactNotFoundException.DefaultMessage);
        }

        [Theory]
        [InlineData("Custom error message.")]
        public void ContactNotFoundException_CustomMessage_SetsCustomMessage(string customMessage)
        {
            // Act
            var exception = new ContactNotFoundException(customMessage);

            // Assert
            exception.Message.Should().Be(customMessage);
        }

        [Theory]
        [InlineData("Custom error message.")]
        public void Constructor_ContactNotFoundException_SetsCustomMessageAndInnerException(string customMessage)
        {
            // Arrange
            var innerException = new System.Exception("Inner exception message");

            // Act
            var exception = new ContactNotFoundException(customMessage, innerException);

            // Assert
            exception.Message.Should().Be(customMessage);
            exception.InnerException.Should().Be(innerException);
        }
    }
}
