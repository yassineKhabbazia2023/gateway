using ApiGateway.Identity.Adapters;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Models;
using ApiGateway.Identity.Options;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;

namespace ApiGateway.Identity.Repositories
{
    public class AzureTableAuthorityRepository : IAuthorityRepository
    {
        private readonly ITableClientAdapter _authoritiesClient;
        private readonly ITableClientAdapter _audienceClient;
        private readonly ITableClientAdapter _issuersClient;
        private readonly ITableClientAdapter _signInKeysClient;
        private readonly ITableClientFactory _tableClientFactory;
        private readonly IOptions<AzureTableAuthorityRepositoryOptions> _options;

        public AzureTableAuthorityRepository(IOptions<AzureTableAuthorityRepositoryOptions> options, ITableClientFactory tableClientFactory)
        {
            _tableClientFactory = tableClientFactory;
            _options = options;
            _authoritiesClient = CreateTableClient("Authorities");
            _audienceClient = CreateTableClient("Audience");
            _issuersClient = CreateTableClient("Issuers");
            _signInKeysClient = CreateTableClient("SignInKeys");
        }

        private ITableClientAdapter CreateTableClient(string tableName)
        {
            return _tableClientFactory.Create(_options.Value.IsvcAzureStorageUri, tableName, new TableSharedKeyCredential(_options.Value.IsvcAzureStorageName, _options.Value.IsvcAzureStorageKey));
        }

        public async Task<IReadOnlyList<AuthorityJson>> FindAuthorities(bool? enabled = true)
        {
            var authorityEntities = _authoritiesClient.Query(filter: $"PartitionKey eq 'Authority'");

            var authorities = new List<AuthorityJson>();

            await foreach (var authorityEntity in authorityEntities)
            {
                var authority = await CreateAuthorityJson(authorityEntity);
                authorities.Add(authority);
            }

            return authorities.AsReadOnly();
        }

        private async Task<AuthorityJson> CreateAuthorityJson(TableEntity authorityEntity)
        {
            var authorityId = new Guid();
            var authorityName = authorityEntity.GetString("Name");
            var authorityUrl = authorityEntity.GetString("Url");
            var authorityValidateIssuer = authorityEntity.GetBoolean("ValidateIssuer") ?? false;
            var authorityJsonWebKeyFetchUrl = authorityEntity.GetString("JsonWebKeyFetchUrl");
            var authorityNameClaimType = authorityEntity.GetString("NameClaimType");

            var validAudiences = await GetValidAudiences(authorityEntity.RowKey);
            var validIssuers = await GetValidIssuers(authorityEntity.RowKey);
            var signInKeys = await GetSignInKeys(authorityEntity.RowKey);

            return new AuthorityJson(
                authorityId,
                authorityName,
                authorityUrl,
                authorityValidateIssuer,
                authorityJsonWebKeyFetchUrl,
                authorityNameClaimType,
                new ReadOnlyCollection<SigningKey>(signInKeys),
                new ReadOnlyCollection<ValidAudience>(validAudiences),
                new ReadOnlyCollection<ValidIssuer>(validIssuers)
            );
        }

        private async Task<List<ValidAudience>> GetValidAudiences(string authorityRowKey)
        {
            var audienceEntitiesPage = _audienceClient.Query(filter: $"PartitionKey eq 'ValidAudience' and AuthorityRowKey eq '{authorityRowKey}'");

            var audienceEntities = new List<TableEntity>();

            await foreach (var audienceEntity in audienceEntitiesPage)
            {
                audienceEntities.Add(audienceEntity);
            }

            return audienceEntities.Select(a => new ValidAudience(new Guid(), a.GetString("Name"))).ToList();
        }

        private async Task<List<ValidIssuer>> GetValidIssuers(string authorityRowKey)
        {
            var issuerEntitiesPage = _issuersClient.Query(filter: $"PartitionKey eq 'ValidIssuer' and AuthorityRowKey eq '{authorityRowKey}'");

            var issuersEntities = new List<TableEntity>();

            await foreach (var issueEntity in issuerEntitiesPage)
            {
                issuersEntities.Add(issueEntity);
            }

            return issuersEntities.Select(i => new ValidIssuer(new Guid(), i.GetString("Name"), i.GetString("RoleName"), i.GetBoolean("CanBeSystemAccount") ?? false)).ToList();
        }

        private async Task<List<SigningKey>> GetSignInKeys(string authorityRowKey)
        {
            try
            {
                var signInKeyEntitiesPage = _signInKeysClient.Query(filter: $"PartitionKey eq 'SignInKey' and AuthorityRowKey eq '{authorityRowKey}'");

                var signInKeyEntities = new List<TableEntity>();

                await foreach (var signInKeyEntity in signInKeyEntitiesPage)
                {
                    signInKeyEntities.Add(signInKeyEntity);
                }

                return signInKeyEntities.Select(s => new SigningKey(new Guid(), s.GetString("JsonWebKey"))).ToList();
            }
            catch (RequestFailedException)
            {
                // Return an empty list if no sign-in keys are found
                return new List<SigningKey>();
            }
        }
    }
}
