using Newtonsoft.Json;

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
        public void CheckClaimsCoherence()
        {
            // 🔍 Trouver la racine du projet
            string projectRoot = FindProjectRoot();

            // 🔗 Construire les chemins des fichiers
            string configFolder = Path.Combine(projectRoot, "src", "Config");
            string profilesPath = Path.Combine(projectRoot, "tests", "ApiGateway.UnitTests", "Authorization", "Profiles");

            // ✅ Lire et fusionner les routes de tous les fichiers ocelot.<domaine>.json
            var ocelotFiles = Directory.GetFiles(configFolder, "ocelot.*.json");
            if (ocelotFiles.Length == 0)
            {
                throw new FileNotFoundException($"❌ Aucun fichier ocelot.*.json trouvé dans : {configFolder}");
            }

            var routes = ocelotFiles
                .Select(file => JsonConvert.DeserializeObject<OcelotConfiguration>(File.ReadAllText(file))
                    ?? throw new InvalidDataException($"❌ Le fichier {file} est malformé ou vide."))
                .Where(config => config.Routes is not null)
                .SelectMany(config => config.Routes)
                .ToList();

            var allRoutes = new OcelotConfiguration { Routes = routes }.GetFlattenedRoutes();

            // ✅ Vérifier l'existence du dossier Profiles
            if (!Directory.Exists(profilesPath))
            {
                throw new DirectoryNotFoundException($"❌ Dossier des profils non trouvé : {profilesPath}");
            }

            var profileFiles = Directory.GetFiles(profilesPath, "*.json");

            if (profileFiles.Length == 0)
            {
                throw new InvalidOperationException("❌ Aucun fichier de profil trouvé dans le dossier des profils.");
            }

            foreach (var file in profileFiles)
            {
                var profile = JsonConvert.DeserializeObject<Profile>(File.ReadAllText(file));

                if (profile == null)
                {
                    throw new InvalidDataException($"❌ Le fichier de profil {file} est malformé ou vide.");
                }

                var filteredRouteList = allRoutes
                    .Where(d => d.Claims is null || profile.ProfileClaims.Any(pc => d.Claims.Contains(pc)))
                    .Select(route => route.Route)
                    .ToList();

                Console.WriteLine($"✅ {filteredRouteList.Count} routes accessibles pour le profil {profile.ProfileName}");

                // 🔄 Générer automatiquement les profils si besoin (à activer manuellement)
                bool regenerateProfiles = false; // ⚠️ Passe à true si tu veux mettre à jour les profils

                if (regenerateProfiles)
                {
                    profile.ProfileEndpoints = filteredRouteList.ToArray();
                    var updatedProfileJson = JsonConvert.SerializeObject(profile, Formatting.Indented);
                    File.WriteAllText(file, updatedProfileJson);
                    continue; // On évite de faire le test après la mise à jour
                }

                // Vérifier que chaque endpoint du profil est bien accessible
                foreach (var route in profile.ProfileEndpoints)
                {
                    Assert.True(filteredRouteList.Contains(route),
                        $"❌ L'utilisateur avec le profil {profile.ProfileName} n'a plus accès à cette route: {route}.");
                }
            }
        }
        private static string FindProjectRoot()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(currentDirectory))
            {
                if (Directory.Exists(Path.Combine(currentDirectory, "src")) &&
                    Directory.Exists(Path.Combine(currentDirectory, "tests")))
                {
                    return currentDirectory;
                }
                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }
            throw new DirectoryNotFoundException("❌ Impossible de trouver le dossier racine contenant 'src' et 'tests'.");
        }
    }
}
