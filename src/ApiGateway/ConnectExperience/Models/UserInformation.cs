using ApiGateway.Contact.Models;
using ApiGateway.Models;
using Newtonsoft.Json;

namespace ApiGateway.ConnectExperience.Models
{
    public class UserInformation
    {
        [JsonProperty("contact")]
        public required ContactMeViewModel Contact;

        [JsonProperty("permissions")]
        public List<string>? Permissions;

        [JsonProperty("favoriteEntities")]
        public List<FavoriteAccount>? FavoriteEntities;
    }
}
