using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ApiGateway.UnitTests.Permissions
{
    public class OcelotConfigurationTests
    {
        private static readonly string ConfigFolder = Path.Combine(FindProjectRoot(), "src", "Config");

        private static readonly IReadOnlyDictionary<string, List<JObject>> RoutesByFile = Directory
            .GetFiles(ConfigFolder, "ocelot.*.json")
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToDictionary(
                file => Path.GetFileName(file),
                file => JObject.Parse(File.ReadAllText(file))["Routes"]?.ToObject<List<JObject>>() ?? new List<JObject>());

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

        [Fact]
        public void EnsureConfigurationFilesAreFound()
        {
            Assert.NotEmpty(RoutesByFile);
            Assert.NotEmpty(RoutesByFile.SelectMany(f => f.Value));
        }

        [Fact]
        public void EnsureSpecificRoutesAreBeforeGenericOnes()
        {
            var errors = new List<string>();

            foreach (var (fileName, routes) in RoutesByFile)
            {
                var orderedRoutes = routes
                    .Select(route => route["UpstreamPathTemplate"]?.ToString())
                    .Where(path => !string.IsNullOrEmpty(path))
                    .ToList();

                var orderedRoutesCount = orderedRoutes.Count;

                for (int i = 0; i < orderedRoutesCount; i++)
                {
                    if (orderedRoutes[i].Contains("{everything}"))
                    {
                        var genericRoute = orderedRoutes[i];

                        for (int j = orderedRoutesCount - 1; j > i; j--)
                        {
                            if (orderedRoutes[j].StartsWith(genericRoute.Replace("{everything}", "")))
                            {
                                errors.Add($"❌ La route générique '{genericRoute}' est définie avant sa version spécifique '{orderedRoutes[j]}' dans {fileName}.");
                            }
                        }
                    }
                }
            }

            if (errors.Any())
            {
                Assert.Fail($"❌ {errors.Count} incohérences trouvées :\n" + string.Join("\n", errors));
            }
        }

        /// <summary>
        /// Chaque route doit être déclarée dans le fichier de son domaine :
        /// le premier segment upstream après /gtw/ détermine le fichier ocelot.&lt;domaine&gt;.json.
        /// Exemple : /gtw/prospect/api/... doit être dans ocelot.prospect.json.
        /// </summary>
        [Fact]
        public void EnsureRoutesAreDeclaredInTheirDomainFile()
        {
            var errors = new List<string>();

            foreach (var (fileName, routes) in RoutesByFile)
            {
                var expectedDomain = Regex.Match(fileName, @"^ocelot\.(.+)\.json$").Groups[1].Value;

                foreach (var upstream in routes.Select(route => route["UpstreamPathTemplate"]?.ToString()))
                {
                    var match = Regex.Match(upstream ?? string.Empty, @"^/gtw/(?<domain>[^/{?]+)[/?]", RegexOptions.IgnoreCase);
                    if (!match.Success)
                    {
                        errors.Add($"❌ La route '{upstream}' ({fileName}) ne respecte pas le format d'upstream '/gtw/<domaine>/...'.");
                    }
                    else if (!string.Equals(match.Groups["domain"].Value, expectedDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"❌ La route '{upstream}' (domaine '{match.Groups["domain"].Value.ToLowerInvariant()}') est déclarée dans {fileName} au lieu de ocelot.{match.Groups["domain"].Value.ToLowerInvariant()}.json.");
                    }
                }
            }

            if (errors.Any())
            {
                Assert.Fail($"❌ {errors.Count} incohérences trouvées :\n" + string.Join("\n", errors));
            }
        }

        [Fact]
        public void EnsureGlobalConfigurationIsDefinedExactlyOnce()
        {
            var filesWithGlobal = Directory
                .GetFiles(ConfigFolder, "ocelot.*.json")
                .Where(file => JObject.Parse(File.ReadAllText(file))["GlobalConfiguration"] is not null)
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(filesWithGlobal.Count == 1 && filesWithGlobal[0] == "ocelot.global.json",
                $"❌ GlobalConfiguration doit être définie uniquement dans ocelot.global.json. Trouvée dans : [{string.Join(", ", filesWithGlobal)}]");
        }

        /// <summary>
        /// Ensures Prospect onboarding routes are protected by the role handler in addition to the feature flag handler.
        /// </summary>
        [Fact]
        public void EnsureProspectOnboardingRoutesUseRoleHandler()
        {
            var routes = RoutesByFile["ocelot.prospect.json"];
            var onboardingRoutes = routes
                .Where(route => route["UpstreamPathTemplate"]?.ToString().StartsWith(
                    "/gtw/prospect/api/onboarding/",
                    StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            Assert.NotEmpty(onboardingRoutes);

            var unsecuredRoutes = onboardingRoutes
                .Where(route =>
                {
                    var handlers = route["DelegatingHandlers"]?.Select(handler => handler.ToString()).ToList() ?? [];
                    return !handlers.Contains("ProspectExperienceHandler") || !handlers.Contains("RoleHandler");
                })
                .Select(route => route["UpstreamPathTemplate"]?.ToString())
                .ToList();

            Assert.True(unsecuredRoutes.Count == 0,
                $"❌ Les routes Prospect onboarding doivent utiliser ProspectExperienceHandler et RoleHandler : [{string.Join(", ", unsecuredRoutes)}]");
        }
    }
}
