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

        private static readonly IReadOnlyDictionary<string, List<JObject>> AggregatesByFile = Directory
            .GetFiles(ConfigFolder, "ocelot.*.json")
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToDictionary(
                file => Path.GetFileName(file),
                file => JObject.Parse(File.ReadAllText(file))["Aggregates"]?.ToObject<List<JObject>>() ?? new List<JObject>());

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

        /// <summary>
        /// Ocelot valide au démarrage que chaque RouteKey d'un agrégat correspond à une route existante
        /// (<c>AllRoutesForAggregateExist</c>). Une clé orpheline fait échouer la validation de configuration
        /// et empêche le démarrage de la gateway entière, pas seulement de l'endpoint agrégé.
        /// Les fichiers étant concaténés par Merge-OcelotConfig.ps1, la résolution se fait tous fichiers confondus.
        /// </summary>
        [Fact]
        public void EnsureAggregateRouteKeysReferenceExistingRoutes()
        {
            var declaredRouteKeys = RoutesByFile
                .SelectMany(file => file.Value)
                .Select(route => route["Key"]?.ToString())
                .Where(key => !string.IsNullOrEmpty(key))
                .ToHashSet(StringComparer.Ordinal);

            var aggregates = AggregatesByFile
                .SelectMany(file => file.Value.Select(aggregate => (FileName: file.Key, Aggregate: aggregate)))
                .ToList();

            Assert.NotEmpty(aggregates);

            var errors = new List<string>();

            foreach (var (fileName, aggregate) in aggregates)
            {
                var upstream = aggregate["UpstreamPathTemplate"]?.ToString();
                var routeKeys = aggregate["RouteKeys"]?.Select(key => key.ToString()).ToList() ?? [];

                if (routeKeys.Count == 0)
                {
                    errors.Add($"❌ L'agrégat '{upstream}' ({fileName}) ne déclare aucun RouteKeys.");
                    continue;
                }

                foreach (var missingKey in routeKeys.Where(key => !declaredRouteKeys.Contains(key)))
                {
                    errors.Add($"❌ L'agrégat '{upstream}' ({fileName}) référence le RouteKey '{missingKey}' qui ne correspond à aucune route déclarée (propriété 'Key').");
                }
            }

            if (errors.Any())
            {
                Assert.Fail($"❌ {errors.Count} incohérences trouvées :\n" + string.Join("\n", errors));
            }
        }

        /// <summary>
        /// Toute route agrégée dans /gtw/wallet/api/infos/currentuser qui tape sur pulse.back.prospect
        /// (host appcegpulseprs*) doit porter ProspectExperienceHandler : c'est ce qui garantit qu'aucun appel
        /// ne part vers Prospect quand le feature flag est désactivé.
        /// Le test tolère l'absence de la route : elle disparaîtra au décommissionnement de Prospect.
        /// </summary>
        [Fact]
        public void EnsureWalletInfoProspectRouteIsGatedByProspectFeatureFlag()
        {
            var prospectWalletInfoRoute = RoutesByFile
                .SelectMany(file => file.Value)
                .FirstOrDefault(route => route["Key"]?.ToString() == "WalletInfoProspect");

            if (prospectWalletInfoRoute is null)
            {
                return;
            }

            var handlers = prospectWalletInfoRoute["DelegatingHandlers"]?.Select(handler => handler.ToString()).ToList() ?? [];

            Assert.True(handlers.Contains("ProspectExperienceHandler"),
                "❌ La route 'WalletInfoProspect' doit utiliser ProspectExperienceHandler pour que l'appel à Prospect soit court-circuité quand le feature flag est désactivé.");

            var hosts = prospectWalletInfoRoute["DownstreamHostAndPorts"]?
                .Select(hostAndPort => hostAndPort["Host"]?.ToString())
                .ToList() ?? [];

            Assert.All(hosts, host => Assert.Contains("prs", host!, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// L'endpoint de référentiel est exposé sous le domaine referential (/gtw/referential/api/infos) :
        /// il n'a rien de « prospect », il interroge le référentiel porté par Account.
        /// L'upstream historique /gtw/prospect/api/referential-infos reste en place — il est consommé par
        /// d'autres sujets — les deux routes coexistent donc en alias.
        /// </summary>
        [Fact]
        public void EnsureReferentialInfosRouteIsDeclaredInReferentialDomain()
        {
            Assert.True(RoutesByFile.ContainsKey("ocelot.referential.json"),
                "❌ Le domaine 'referential' doit être déclaré dans src/Config/ocelot.referential.json.");

            var route = RoutesByFile["ocelot.referential.json"]
                .SingleOrDefault(r => r["UpstreamPathTemplate"]?.ToString() == "/gtw/referential/api/infos");

            Assert.NotNull(route);
            Assert.Equal("/api/referentials/AccountReferentialInformation", route!["DownstreamPathTemplate"]?.ToString());
            Assert.Equal("https", route["DownstreamScheme"]?.ToString());
            Assert.Equal("Account", route["SwaggerKey"]?.ToString());

            var methods = route["UpstreamHttpMethod"]?.Select(method => method.ToString()).ToList() ?? [];
            Assert.Equal(["GET"], methods);

            var providers = route["AuthenticationOptions"]?["AuthenticationProviderKeys"]
                ?.Select(provider => provider.ToString())
                .ToList() ?? [];
            Assert.Equal(["AAD", "GIGYA MyPulse v2"], providers);

            var hosts = route["DownstreamHostAndPorts"]?
                .Select(hostAndPort => hostAndPort["Host"]?.ToString())
                .ToList() ?? [];
            Assert.All(hosts, host => Assert.Contains("acc", host!, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// /gtw/referential/api/infos est un alias strict de /gtw/prospect/api/referential-infos :
        /// seul l'UpstreamPathTemplate diffère. Les deux upstreams doivent rester alignés — si l'un évolue
        /// (downstream, host, auth, handlers, claims), l'autre doit suivre, sinon les deux consommateurs
        /// n'obtiennent plus le même comportement.
        /// L'upstream historique est conservé : il est utilisé par d'autres sujets.
        /// </summary>
        [Fact]
        public void EnsureReferentialInfosAliasMirrorsLegacyProspectRoute()
        {
            var legacyRoute = RoutesByFile["ocelot.prospect.json"]
                .SingleOrDefault(route => route["UpstreamPathTemplate"]?.ToString() == "/gtw/prospect/api/referential-infos");
            var aliasRoute = RoutesByFile["ocelot.referential.json"]
                .SingleOrDefault(route => route["UpstreamPathTemplate"]?.ToString() == "/gtw/referential/api/infos");

            Assert.NotNull(legacyRoute);
            Assert.NotNull(aliasRoute);

            var legacyWithoutUpstream = (JObject)legacyRoute!.DeepClone();
            var aliasWithoutUpstream = (JObject)aliasRoute!.DeepClone();
            legacyWithoutUpstream.Remove("UpstreamPathTemplate");
            aliasWithoutUpstream.Remove("UpstreamPathTemplate");

            Assert.True(JToken.DeepEquals(legacyWithoutUpstream, aliasWithoutUpstream),
                "❌ Les deux routes doivent être identiques hors UpstreamPathTemplate.\n"
                + $"ocelot.prospect.json  : {legacyWithoutUpstream.ToString(Newtonsoft.Json.Formatting.None)}\n"
                + $"ocelot.referential.json : {aliasWithoutUpstream.ToString(Newtonsoft.Json.Formatting.None)}");
        }

        /// <summary>
        /// Un même upstream ne doit être déclaré qu'une fois : après le merge de Merge-OcelotConfig.ps1,
        /// un doublon rend la seconde déclaration inatteignable (Ocelot retient la première correspondance).
        /// </summary>
        [Fact]
        public void EnsureUpstreamTemplatesAreNotDuplicated()
        {
            var duplicates = RoutesByFile
                .SelectMany(file => file.Value.Select(route => new
                {
                    File = file.Key,
                    Upstream = route["UpstreamPathTemplate"]?.ToString(),
                    Methods = string.Join(",", (route["UpstreamHttpMethod"]?.Select(method => method.ToString()) ?? [])
                        .OrderBy(method => method, StringComparer.Ordinal))
                }))
                .Where(route => !string.IsNullOrEmpty(route.Upstream))
                .GroupBy(route => (route.Upstream, route.Methods))
                .Where(group => group.Count() > 1)
                .Select(group => $"'{group.Key.Upstream}' [{group.Key.Methods}] déclaré {group.Count()} fois dans : [{string.Join(", ", group.Select(route => route.File))}]")
                .ToList();

            if (duplicates.Any())
            {
                Assert.Fail($"❌ {duplicates.Count} upstream(s) dupliqué(s) :\n" + string.Join("\n", duplicates));
            }
        }
    }
}
