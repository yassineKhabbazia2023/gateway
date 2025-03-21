using Newtonsoft.Json.Linq;

namespace ApiGateway.UnitTests.Permissions
{
    public class OcelotConfigurationTests
    {
        private static readonly string JsonPath = Path.Combine(FindProjectRoot(), "src", "Config", "ocelot.json");

        private static readonly JObject OcelotConfig = JObject.Parse(File.ReadAllText(JsonPath));
        private static readonly List<JObject> Routes = OcelotConfig["Routes"]?.ToObject<List<JObject>>() ?? new List<JObject>();

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
        public void EnsureSpecificRoutesAreBeforeGenericOnes()
        {
            var orderedRoutes = Routes
                .Select(route => route["UpstreamPathTemplate"]?.ToString())
                .Where(path => !string.IsNullOrEmpty(path))
                .ToList();

            var errors = new List<string>();
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
                            errors.Add($"❌ La route générique '{genericRoute}' est définie avant sa version spécifique '{orderedRoutes[j]}' dans ocelot.json.");
                        }
                    }
                }
            }

            if (errors.Any())
            {
                Assert.Fail($"❌ {errors.Count} incohérences trouvées dans ocelot.json :\n" + string.Join("\n", errors));
            }
        }
    }
}
