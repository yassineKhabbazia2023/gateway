using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiGateway.UnitTests.Authorization
{
    public class ClaimsCoherenceTests
    {
        /// <summary>
        /// Cette méthode se base sur les fichiers fournis par le PM afin d'assurer que tout changement dans Ocelot n'impacte pas les droits des utilisateurs.
        /// Si vous rencontrez une erreur, merci de revenir vers votre TL/PM avant de modifier le fichier profile.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CheckClaimsCoherence()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            var jsonConfig = File.ReadAllText(@"..\\..\\..\\..\\..\\src\\Config\\ocelot.json");
            var ocelotConfig = JsonConvert.DeserializeObject<OcelotConfiguration>(jsonConfig);
            var allRoutes = ocelotConfig.GetFlattenedRoutes();
            var files = Directory.GetFiles(@"..\\..\\..\\Authorization\\Profiles");
            foreach (var file in files)
            {
                var profile = JsonConvert.DeserializeObject<Profile>(File.ReadAllText(file));

                var FilteredRouteList = allRoutes.Where(d => d.Claims is null || profile.ProfileClaims.Any(pc => d.Claims.Contains(pc))).Select(route => route.Route);

                // to update profile endpoints easily uncomment this code
                //profile.ProfileEndpoints = FiltredRoutes.ToArray();
                //var routes = JsonConvert.SerializeObject(profile, Newtonsoft.Json.Formatting.Indented);
                //File.WriteAllText(file, routes);
                //return;

                foreach (var route in profile.ProfileEndpoints)
                {
                    Assert.True(FilteredRouteList.Contains(route), $"L'utilisateur avec le profil {profile.ProfileName} n'a plus accès à cette route: {route}.");
                }
            }
        }
    }
}
