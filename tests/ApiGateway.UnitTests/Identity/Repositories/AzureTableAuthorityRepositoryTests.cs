using ApiGateway.Identity.Adapters;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Models;
using ApiGateway.Identity.Options;
using ApiGateway.Identity.Repositories;
using Azure.Data.Tables;
using Azure;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ApiGateway.UnitTests.Identity.Repositories
{
    public class AzureTableAuthorityRepositoryTests
    {
        private readonly Mock<ITableClientAdapter> _authoritiesClientMock;
        private readonly Mock<ITableClientAdapter> _audienceClientMock;
        private readonly Mock<ITableClientAdapter> _issuersClientMock;
        private readonly Mock<ITableClientAdapter> _signInKeysClientMock;
        private readonly Mock<ITableClientFactory> _tableClientFactoryMock;
        private readonly Mock<IOptions<AzureTableAuthorityRepositoryOptions>> _optionsMock;
        private readonly AzureTableAuthorityRepository _repository;

        public AzureTableAuthorityRepositoryTests()
        {
            _authoritiesClientMock = new Mock<ITableClientAdapter>();
            _audienceClientMock = new Mock<ITableClientAdapter>();
            _issuersClientMock = new Mock<ITableClientAdapter>();
            _signInKeysClientMock = new Mock<ITableClientAdapter>();
            _tableClientFactoryMock = new Mock<ITableClientFactory>();

            _optionsMock = new Mock<IOptions<AzureTableAuthorityRepositoryOptions>>();
            _optionsMock.SetupGet(m => m.Value)
                .Returns(new AzureTableAuthorityRepositoryOptions
                {
                    IsvcAzureStorageUri = "http://storage.uri",
                    IsvcAzureStorageKey = "dummyKey",
                    IsvcAzureStorageName = "myStorage"
                });

            _tableClientFactoryMock
                .Setup(factory => factory.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TableSharedKeyCredential>()))
                .Returns<string, string, TableSharedKeyCredential>((uri, tableName, credential) =>
                {
                    return tableName switch
                    {
                        "Authorities" => _authoritiesClientMock.Object,
                        "Audience" => _audienceClientMock.Object,
                        "Issuers" => _issuersClientMock.Object,
                        "SignInKeys" => _signInKeysClientMock.Object,
                        _ => throw new InvalidOperationException("Unknown table name")
                    };
                });

            _repository = new AzureTableAuthorityRepository(_optionsMock.Object, _tableClientFactoryMock.Object);
        }

        [Fact]
        public async Task FindAuthorities_ReturnsAuthorities_WhenDataExists()
        {
            // Arrange
            var entities = new List<TableEntity>
            {
                new TableEntity("Authority", "1") { { "Name", "Auth1" }, { "Url", "http://auth1.com" }, { "ValidateIssuer", true } }
            };

            var audiences = new List<TableEntity>
            {
                new TableEntity("ValidAudience","1"){ { "Id", new Guid()},{"Name","Aud1"},}
            };

            var issuers = new List<TableEntity>
            {
                new TableEntity("ValidIssuer","1"){ { "Id", new Guid()},{"Name","Aud1"}, {"RoleName", "Role1"},{ "CanBeSystemAccount",true } }
            };

            var keys = new List<TableEntity>
            {
            };

            _authoritiesClientMock
                .Setup(client => client.Query(It.IsAny<string>()))
                .Returns(ToAsyncPageable(entities));

            _audienceClientMock
                .Setup(client => client.Query(It.IsAny<string>()))
                .Returns(ToAsyncPageable(audiences));

            _issuersClientMock
                .Setup(client => client.Query(It.IsAny<string>()))
                .Returns(ToAsyncPageable(issuers));

            _signInKeysClientMock
                .Setup(client => client.Query(It.IsAny<string>()))
                .Returns(ToAsyncPageable(keys));

            // Act
            var result = await _repository.FindAuthorities();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Auth1", result[0].Name);
        }

        [Fact]
        public async Task FindAuthorities_ReturnsEmptyList_WhenNoDataExists()
        {
            // Arrange
            _authoritiesClientMock
                .Setup(client => client.Query(It.IsAny<string>()))
                .Returns(ToAsyncPageable(new List<TableEntity>()));

            // Act
            var result = await _repository.FindAuthorities();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task FindAuthorities_HandlesException_AndReturnsEmptyList()
        {
            // Arrange
            _authoritiesClientMock
                .Setup(client => client.Query(It.IsAny<string>()))
                .Throws(new RequestFailedException("Simulated failure"));

          
            // Act & Assert
            var exception = await Assert.ThrowsAsync<RequestFailedException>(() => _repository.FindAuthorities());

            // Verify the exception message or any other relevant details if needed
            Assert.Equal("Simulated failure", exception.Message);
        }

        private static AsyncPageable<TableEntity> ToAsyncPageable(IEnumerable<TableEntity> entities)
        {
            return new MockAsyncPageable<TableEntity>(entities);
        }

        private class MockAsyncPageable<T> : AsyncPageable<T>
        {
            private readonly IEnumerable<T> _items;

            public MockAsyncPageable(IEnumerable<T> items)
            {
                _items = items;
            }

            public override async IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
            {
                yield return Page<T>.FromValues(_items.ToList(), null, null);
                await Task.CompletedTask;
            }
        }
    }
}
