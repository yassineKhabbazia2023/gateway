using ApiGateway.Identity.Adapters;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Options;
using ApiGateway.Identity.Repositories;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

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
                .Returns(new AzureTableAuthorityRepositoryOptions()
                {
                    IsvcAzureStorageUri = "http://storage.uri",
                    IsvcAzureStorageKey = "hRgzOwq2iimMAEMGh5tDEQ==",
                    IsvcAzureStorageName = "myStorage"
                });

            _tableClientFactoryMock
                .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TableSharedKeyCredential>()))
                .Returns<string, string, TableSharedKeyCredential>((uri, table, creds) =>
                {
                    return table switch
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
        public async Task FindAuthorities_ShouldReturnListOfAuthorities_AndVerifyCreateMethodCall()
        {
            // Arrange
            var authorityEntities = new List<TableEntity>
            {
                new TableEntity("Authority", "1")
                {
                    { "Name", "Authority1" },
                    { "Url", "https://authority1.example.com" },
                    { "ValidateIssuer", true },
                    { "JsonWebKeyFetchUrl", "https://authority1.example.com/jwks" },
                    { "NameClaimType", "name" }
                },
                new TableEntity("Authority", "2")
                {
                    { "Name", "Authority2" },
                    { "Url", "https://authority2.example.com" },
                    { "ValidateIssuer", false },
                    { "JsonWebKeyFetchUrl", "https://authority2.example.com/jwks" },
                    { "NameClaimType", "name" }
                }
            };

            var pageAuthorityEntities = Page<TableEntity>.FromValues(authorityEntities, continuationToken: null, new Mock<Response>().Object);
            var asyncPagableAuthorityEntities = AsyncPageable<TableEntity>.FromPages(new[] { pageAuthorityEntities });

            var audienceEntities = new List<TableEntity>
            {
                new TableEntity("ValidAudience", "1") { { "Name", "Audience1" }, { "AuthorityRowKey", "1" } },
                new TableEntity("ValidAudience", "2") { { "Name", "Audience2" }, { "AuthorityRowKey", "2" } }
            };

            var pageAudienceEntities = Page<TableEntity>.FromValues(audienceEntities, continuationToken: null, new Mock<Response>().Object);
            var asyncPagableAudienceEntities = AsyncPageable<TableEntity>.FromPages(new[] { pageAudienceEntities });

            var issuerEntities = new List<TableEntity>
            {
                new TableEntity("ValidIssuer", "1") { { "Name", "Issuer1" }, { "AuthorityRowKey", "1" }, { "RoleName", "Role1" }, { "CanBeSystemAccount", true } },
                new TableEntity("ValidIssuer", "2") { { "Name", "Issuer2" }, { "AuthorityRowKey", "2" }, { "RoleName", "Role2" }, { "CanBeSystemAccount", false } }
            };

            var pageIssuerEntities = Page<TableEntity>.FromValues(issuerEntities, continuationToken: null, new Mock<Response>().Object);
            var asyncPagableIssuerEntities = AsyncPageable<TableEntity>.FromPages(new[] { pageIssuerEntities });

            var signInKeyEntities = new List<TableEntity>
            {
                new TableEntity("SignInKey", "1") { { "JsonWebKey", "dummyKey1" }, { "AuthorityRowKey", "1" } },
                new TableEntity("SignInKey", "2") { { "JsonWebKey", "dummyKey2" }, { "AuthorityRowKey", "2" } }
            };

            var pageSignInKeyEntities = Page<TableEntity>.FromValues(signInKeyEntities, continuationToken: null, new Mock<Response>().Object);
            var asyncPagableSignInKeyEntities = AsyncPageable<TableEntity>.FromPages(new[] { pageSignInKeyEntities });

            _authoritiesClientMock.Setup(x => x.Query(It.IsAny<string>()))
                .Returns(asyncPagableAuthorityEntities);
            _audienceClientMock.Setup(x => x.Query(It.IsAny<string>())).Returns(asyncPagableAudienceEntities);
            _issuersClientMock.Setup(x => x.Query(It.IsAny<string>())).Returns(asyncPagableIssuerEntities);
            _signInKeysClientMock.Setup(x => x.Query(It.IsAny<string>())).Returns(asyncPagableSignInKeyEntities);

            // Act
            var result = await _repository.FindAuthorities();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, a => a.Name == "Authority1");
            Assert.Contains(result, a => a.Name == "Authority2");

            _tableClientFactoryMock.Verify(f => f.Create(
                It.Is<string>(uri => uri == "http://storage.uri"),
                It.Is<string>(tableName => tableName == "Authorities"),
                It.Is<TableSharedKeyCredential>(cred => cred.AccountName == "myStorage")), Times.Once);

            _tableClientFactoryMock.Verify(f => f.Create(
                It.Is<string>(uri => uri == "http://storage.uri"),
                It.Is<string>(tableName => tableName == "Audience"),
                It.Is<TableSharedKeyCredential>(cred => cred.AccountName == "myStorage")), Times.Once);

            _tableClientFactoryMock.Verify(f => f.Create(
                It.Is<string>(uri => uri == "http://storage.uri"),
                It.Is<string>(tableName => tableName == "Issuers"),
                It.Is<TableSharedKeyCredential>(cred => cred.AccountName == "myStorage")), Times.Once);

            _tableClientFactoryMock.Verify(f => f.Create(
                It.Is<string>(uri => uri == "http://storage.uri"),
                It.Is<string>(tableName => tableName == "SignInKeys"),
                It.Is<TableSharedKeyCredential>(cred => cred.AccountName == "myStorage")), Times.Once);

            _authoritiesClientMock.Verify(a => a.Query(It.Is<string>(q => q.Contains("Authority"))));
            _audienceClientMock.Verify(a => a.Query(It.Is<string>(q => q.Contains("ValidAudience"))));
            _issuersClientMock.Verify(a => a.Query(It.Is<string>(q => q.Contains("ValidIssuer"))));
            _signInKeysClientMock.Verify(a => a.Query(It.Is<string>(q => q.Contains("SignInKey"))));
        }
    }
}
