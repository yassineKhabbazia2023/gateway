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
                    IsvcAzureStorageName = "myStorage",
                    ManagedIdentityClientId = "1",

                });

            _tableClientFactoryMock
                .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((uri, table, creds) =>
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
    }
}
