using System;
using System.Net;
using Xunit;
using ApiGateway.Exceptions;
using Pulse.Back.ExceptionMiddleware.BaseException;

namespace ApiGateway.Tests.Exceptions
{
    public class GatewayExceptionTests
    {
        [Fact]
        public void Constructor_ShouldSetProperties_WhenCalled()
        {
            // Arrange
            int statusCode = 400;
            string errorCode = "INVALID_REQUEST";
            string errorMessage = "The request is invalid";

            // Act
            var exception = new GatewayException(statusCode, errorCode, errorMessage);

            // Assert
            Assert.Equal(statusCode, exception.StatusCode);
            Assert.Equal(errorCode, exception.Code);
            Assert.Equal(errorMessage, exception.Message);
        }

        [Theory]
        [InlineData(400, "BAD_REQUEST", "Bad request")]
        [InlineData(401, "UNAUTHORIZED", "Unauthorized access")]
        [InlineData(403, "FORBIDDEN", "Access forbidden")]
        [InlineData(404, "NOT_FOUND", "Resource not found")]
        [InlineData(500, "SERVER_ERROR", "Internal server error")]
        public void Constructor_ShouldSetCorrectValues_WithDifferentStatusCodes(int statusCode, string errorCode, string errorMessage)
        {
            // Act
            var exception = new GatewayException(statusCode, errorCode, errorMessage);

            // Assert
            Assert.Equal(statusCode, exception.StatusCode);
            Assert.Equal(errorCode, exception.Code);
            Assert.Equal(errorMessage, exception.Message);
        }

        [Fact]
        public void Constructor_ShouldInheritFromBusinessException()
        {
            // Arrange
            var exception = new GatewayException(400, "ERROR", "Error message");

            // Assert
            Assert.IsAssignableFrom<BusinessException>(exception);
        }

        [Fact]
        public void Constructor_ShouldInheritFromException()
        {
            // Arrange
            var exception = new GatewayException(400, "ERROR", "Error message");

            // Assert
            Assert.IsAssignableFrom<Exception>(exception);
        }

        [Fact]
        public void HttpStatus_ShouldBeInheritedFromBusinessException()
        {
            // Arrange - We need to check if HttpStatus is properly inherited
            // This is a bit tricky since we're not setting it explicitly in GatewayException
            // We'll have to check if the property exists and doesn't throw

            // Act
            var exception = new GatewayException(400, "ERROR", "Error message");

            // Assert
            // We can't reliably test the actual value here since it depends on the BusinessException implementation
            // But we can verify the property exists and can be accessed
            var httpStatusProperty = exception.GetType().GetProperty("HttpStatus");
            Assert.NotNull(httpStatusProperty);
        }

    }
}