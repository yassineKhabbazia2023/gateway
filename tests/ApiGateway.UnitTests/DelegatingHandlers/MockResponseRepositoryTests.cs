using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Mocks.Models;
using LiteDB;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class MockResponseRepositoryTests
{
    private readonly IFixture _fixture = new Fixture();
    private readonly Mock<ILiteDatabase> _liteDatabaseMock = new();
    private readonly Mock<ILiteCollection<MockIndexDoc>> _mockResponsesCollectionMock = new();

    public MockResponseRepositoryTests()
    {
        _liteDatabaseMock.Setup(db => db.GetCollection<MockIndexDoc>(It.IsAny<string>(),
                BsonAutoId.ObjectId))
            .Returns(_mockResponsesCollectionMock.Object);
    }

    [Fact]
    public void GetJsonContent_RouteKeyExists_ReturnsTrueAndJsonContent()
    {
        // Arrange
        var routeKey = _fixture.Create<string>();
        var expectedMockIndexDoc = _fixture.Build<MockIndexDoc>()
            .With(doc => doc.Id,
                routeKey)
            .With(doc => doc.JsonContent,
                _fixture.Create<string>())
            .Create();

        _mockResponsesCollectionMock.Setup(m => m.FindOne(It.IsAny<Expression<Func<MockIndexDoc, bool>>>()))
            .Returns(expectedMockIndexDoc);

        var repository = new MockResponseRepository(_liteDatabaseMock.Object);

        // Act
        var (exists, jsonContent) = repository.GetJsonContent(routeKey);

        // Assert
        exists.Should()
            .BeTrue();
        jsonContent.Should()
            .Be(expectedMockIndexDoc.JsonContent);
    }

    [Fact]
    public void GetJsonContent_RouteKeyDoesNotExist_ReturnsFalseAndNull()
    {
        // Arrange
        var routeKey = _fixture.Create<string>();

        _mockResponsesCollectionMock.Setup(m => m.FindOne(It.IsAny<Expression<Func<MockIndexDoc, bool>>>()))
            .Returns((MockIndexDoc)null!);

        var repository = new MockResponseRepository(_liteDatabaseMock.Object);

        // Act
        var (exists, jsonContent) = repository.GetJsonContent(routeKey);

        // Assert
        exists.Should()
            .BeFalse();
        jsonContent.Should()
            .BeNull();
    }
}