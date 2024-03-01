using ApiGateway.Exceptions;
using Microsoft.Extensions.Configuration;
using ApiGateway.Extensions;

namespace ApiGateway.UnitTests.Extensions
{
    public class FileHelperTests
    {
        [Fact]
        public void GetOcelotConfigFullPathName_ConfigurationPathNotSet_ThrowsNullReferenceException()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c[It.IsAny<string>()]).Returns<string>(null!); 

            // Act & Assert
            Assert.Throws<InvalidConfigException>(() => FileHelper.GetOcelotConfigFullPathName(configMock.Object));
        }

        [Fact]
        public void GetOcelotConfigFullPathName_ValidConfigurationPath_ReturnsExpectedFullPath()
        {
            // Arrange
            var fixture = new Fixture();
            var expectedPath = fixture.Create<string>();
            var expectedFileName = "ocelot.json"; 
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c[It.IsAny<string>()]).Returns(expectedPath);

            // Act
            var fullPath = FileHelper.GetOcelotConfigFullPathName(configMock.Object);

            // Assert
            fullPath.Should().Be(Path.Combine(expectedPath, expectedFileName));
        }

        [Fact]
        public void GetLiteDbDir_ValidConfigurationPath_ReturnsExpectedDirectoryPath()
        {
            // Arrange
            var fixture = new Fixture();
            var expectedPath = fixture.Create<string>();
            var expectedDbName = "mocks.db";
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c[It.IsAny<string>()]).Returns(expectedPath);

            // Act
            var dbDir = FileHelper.GetLiteDbDir(configMock.Object);

            // Assert
            dbDir.Should().Be(Path.Combine(expectedPath, expectedDbName));
        }

        [Fact]
        public void GetLiteDbDir_ConfigurationPathNotSet_ThrowsNullReferenceException()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c[It.IsAny<string>()]).Returns<string>(null!);

            // Act & Assert
            Assert.Throws<InvalidConfigException>(() => FileHelper.GetLiteDbDir(configMock.Object));
        }
        
    }
}
