namespace ApiGateway.UnitTests.Authorization
{
    internal class Profile
    {
        public string ProfileName { get; set; }
        public IList<string> ProfileClaims { get; set; }
        public IList<string> ProfileEndpoints { get; set; }
    }
    internal class ProfilesObject
    {
        public IList<Profile> Profiles { get; set; }
    }

}
