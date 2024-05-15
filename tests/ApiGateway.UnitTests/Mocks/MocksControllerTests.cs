using System.Net;
using System.Text;
using ApiGateway.Mocks;
using ApiGateway.Mocks.Models;
using IdentityModel.OidcClient;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace ApiGateway.UnitTests.Mocks;

public class MocksControllerTests
{
    [Fact]
    public async void GetMockIndex_WhenCalled_ReturnsOkResultWithMockIndexes()
    {
        // Arrange
        var fixture = new Fixture();
        var mockIndexes = fixture.CreateMany<MockIndexDoc>()
            .ToList();
        var mockDatabase = new Mock<ILiteDatabase>();
        var mockCollection = new Mock<ILiteCollection<MockIndexDoc>>();

        mockCollection.Setup(x => x.FindAll())
            .Returns(mockIndexes);
        mockDatabase.Setup(db => db.GetCollection<MockIndexDoc>(It.IsAny<string>(),
                BsonAutoId.ObjectId))
            .Returns(mockCollection.Object);

        var featureManager = new Mock<IFeatureManager>(MockBehavior.Strict);
        featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);

        var controller = new MocksController(mockDatabase.Object, featureManager.Object);

        // Act
        var result = await controller.GetMockIndex();

        // Assert
        result.Should()
            .BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.Value.Should()
            .BeEquivalentTo(mockIndexes);
    }


    [Fact]
    public async void GetMockIndex_WhenFeatureGate_False_ReturnsForbidden()
    {
        // Arrange
        var fixture = new Fixture();
        var mockIndexes = fixture.CreateMany<MockIndexDoc>()
            .ToList();
        var mockDatabase = new Mock<ILiteDatabase>();
        var mockCollection = new Mock<ILiteCollection<MockIndexDoc>>();
        var featureManager = new Mock<IFeatureManager>(MockBehavior.Strict);
        featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(false);

        var controller = new MocksController(mockDatabase.Object, featureManager.Object);

        // Act
        var result = await controller.GetMockIndex() as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ModifyMockResponse_FileIsNotJson_ReturnsBadRequest()
    {
        // Arrange
        var mockDatabase = new Mock<ILiteDatabase>();

        var featureManager = new Mock<IFeatureManager>(MockBehavior.Strict);
        featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);

        var controller = new MocksController(mockDatabase.Object, featureManager.Object);
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(_ => _.FileName)
            .Returns("invalid.txt"); // Ensure file extension is not .json

        var request = new MockIndexRequest
        {
            DownstreamUri = "http://test.com",
            HttpVerb = "GET"
        };
        var fileUpdate = new MockEntryFileUpdate
        {
            File = mockFile.Object
        };

        // Act
        var result = await controller.ModifyMockResponse(request,
            fileUpdate);

        // Assert
        result.Should()
            .BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult?.Value.Should()
            .Be("Only JSON files are accepted.");
    }

    [Fact]
    public async Task ModifyMockResponse_FileContentIsNotValidJson_ReturnsBadRequest()
    {
        // Arrange
        var mockDatabase = new Mock<ILiteDatabase>();

        var featureManager = new Mock<IFeatureManager>(MockBehavior.Strict);
        featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);

        var controller = new MocksController(mockDatabase.Object, featureManager.Object);
        var invalidJsonContent = "Invalid JSON";
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName)
            .Returns("response.json");
        mockFile.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(invalidJsonContent)));

        var request = new MockIndexRequest
        {
            DownstreamUri = "http://example.com",
            HttpVerb = "GET"
        };
        var fileUpdate = new MockEntryFileUpdate
        {
            File = mockFile.Object
        };

        // Act
        var result = await controller.ModifyMockResponse(request,
            fileUpdate);

        // Assert
        result.Should()
            .BeOfType<BadRequestObjectResult>();
        (result as BadRequestObjectResult)?.Value.Should()
            .Be("The file content is not valid JSON.");
    }

    [Fact]
    public async Task ModifyMockResponse_NewMockIndexDoc_InsertsDocSuccessfully()
    {
        // Arrange
        var mockDatabase = new Mock<ILiteDatabase>();
        var mockCollection = new Mock<ILiteCollection<MockIndexDoc>>();

        mockCollection.Setup(x => x.FindById(It.IsAny<BsonValue>()))
            .Returns(value: null!);
        mockCollection.Setup(x => x.Insert(It.IsAny<MockIndexDoc>()))
            .Verifiable();
        mockDatabase.Setup(db => db.GetCollection<MockIndexDoc>(It.IsAny<string>(),
                BsonAutoId.ObjectId))
            .Returns(mockCollection.Object);

        var featureManager = new Mock<IFeatureManager>(MockBehavior.Strict);
        featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);

        var controller = new MocksController(mockDatabase.Object, featureManager.Object);
        var mockFile = new Mock<IFormFile>();

        mockFile.Setup(f => f.FileName)
            .Returns("valid.json");
        mockFile.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream("{}"u8.ToArray()));

        var request = new MockIndexRequest
        {
            DownstreamUri = "http://test.com",
            HttpVerb = "GET"
        };
        var fileUpdate = new MockEntryFileUpdate
        {
            File = mockFile.Object
        };

        // Act
        var result = await controller.ModifyMockResponse(request,
            fileUpdate);

        // Assert
        mockCollection.Verify(x => x.Insert(It.IsAny<MockIndexDoc>()),
            Times.Once);
        result.Should()
            .BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.Value.Should()
            .Be("Mock response updated successfully.");
    }

    [Fact]
    public async Task ModifyMockResponse_ExistingMockIndexDoc_UpdatesDocSuccessfully()
    {
        // Arrange
        var mockDatabase = new Mock<ILiteDatabase>();
        var mockCollection = new Mock<ILiteCollection<MockIndexDoc>>();
        var existingMockIndexDoc = new MockIndexDoc
        {
            DownstreamUri = "http://test.com",
            HttpVerb = "GET",
            JsonContent = "{}",
            Id = "someId"
        };

        mockCollection.Setup(x => x.FindById(It.IsAny<BsonValue>()))
            .Returns(existingMockIndexDoc);
        mockCollection.Setup(x => x.Update(It.IsAny<MockIndexDoc>()))
            .Verifiable();
        mockDatabase.Setup(db => db.GetCollection<MockIndexDoc>(It.IsAny<string>(),
                BsonAutoId.ObjectId))
            .Returns(mockCollection.Object);

        var featureManager = new Mock<IFeatureManager>(MockBehavior.Strict);
        featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);

        var controller = new MocksController(mockDatabase.Object, featureManager.Object);
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName)
            .Returns("valid.json");
        mockFile.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{}")));

        var request = new MockIndexRequest
        {
            DownstreamUri = "http://test.com",
            HttpVerb = "GET"
        };
        var fileUpdate = new MockEntryFileUpdate
        {
            File = mockFile.Object
        };

        // Act
        var result = await controller.ModifyMockResponse(request,
            fileUpdate);

        // Assert
        mockCollection.Verify(x => x.Update(It.IsAny<MockIndexDoc>()),
            Times.Once);
        result.Should()
            .BeOfType<OkObjectResult>();
        ((result as OkObjectResult)!).Value.Should()
            .Be("Mock response updated successfully.");
    }

    [Fact]
    public async Task ModifyMockResponse_WhenFeatureGate_False_ReturnsForbidden()
    {
        // Arrange
        var mockDatabase = new Mock<ILiteDatabase>();

        var featureManager = new Mock<IFeatureManager>(MockBehavior.Strict);
        featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(false);

        var controller = new MocksController(mockDatabase.Object, featureManager.Object);
        var mockFile = new Mock<IFormFile>();

        var request = new MockIndexRequest
        {
            DownstreamUri = "http://example.com",
            HttpVerb = "GET"
        };
        var fileUpdate = new MockEntryFileUpdate
        {
            File = mockFile.Object
        };

        // Act
        var result = await controller.ModifyMockResponse(request,
            fileUpdate) as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }
}