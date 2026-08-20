# Configuration Ocelot (production)

La configuration de routage est découpée **par domaine fonctionnel** : un fichier
`ocelot.<domaine>.json` par microservice downstream, où `<domaine>` est le premier
segment du path upstream après `/gtw/`.

| Fichier | Contenu |
|---|---|
| `ocelot.<domaine>.json` | Les `Routes` du domaine (ex. `/gtw/prospect/api/...` → `ocelot.prospect.json`) |
| `ocelot.aggregates.json` | La section `Aggregates` (routes agrégées transverses) |
| `ocelot.swagger.json` | La section `SwaggerEndPoints` (MMLib.SwaggerForOcelot) |
| `ocelot.global.json` | La section `GlobalConfiguration` (unique, obligatoire) |

## Règles

- **Ajouter une route** : dans le fichier de son domaine, jamais ailleurs.
  Nouveau domaine → nouveau fichier `ocelot.<domaine>.json` (et l'ajouter à `ApiGateway.sln`).
- **Ordre des routes** : dans un même fichier, déclarer les routes spécifiques *avant*
  les routes génériques `{everything}`. L'ordre entre fichiers n'a aucun impact
  (les upstreams sont partitionnés par domaine).
- Ces règles sont vérifiées par `tests/ApiGateway.UnitTests/Ocelot/OcelotConfigurationTests.cs`.

## Ce qui protège une route, et ce qui ne la protège pas

Sans `AuthenticationOptions`, la route est anonyme et sort du middleware avant tout contrôle
(`IsAnonymousRoute`). Sinon `AuthorizationMiddleware` applique deux contrôles distincts, et
**aucun des deux n'est activé par défaut** :

- **Les permissions**, seulement si la route porte un `RouteClaimsRequirement`. Sans lui,
  `CheckClaims` ne fait rien : n'importe quel jeton valide passe.
- **Le rôle sur le compte**, seulement si `ValidateAccountId` trouve un `accountId` — dans
  l'ordre : `?accountId=`, le path via `/accounts/(\d+)` (**au pluriel**), les préfixes de
  `AuthorizationHelper.ProspectAccountRoutePrefixes`, l'en-tête `Account-Id`. Renommer un
  segment `account` en `accounts` active donc un contrôle d'accès sans que rien dans le diff
  ne le dise — vérifier l'intention avant d'y toucher.

Deux façons de désactiver le contrôle de rôle : `GlobalsConstants.NoAccountCheckEndpoints` (le
path *contient* une des chaînes, `Contains`, donc large) et `AuthorizationHelper.SkipRoleCheck`
(une permission de `NoRoleCheckPermissions`, `COADMI004`, court-circuite le contrôle).

⚠️ Aucun de ces contrôles ne couvre le BOLA. `RoleHandler` non plus : il extrait un `accountId`
(query d'abord, puis `/accounts/(\d+)`) et refait le même contrôle de rôle — il ne vérifie
jamais qu'un `{closingId}`, `{publicationId}` ou `{slotId}` appartient bien au compte de l'URL.
Cette vérification n'existe qu'en aval, dans le microservice.

## Feature flags

Une route portant `"Metadata": { "featureFlag": "<clé>" }` est gardée par `FeatureFlagGateHandler`
(handler global) : flag à off ou inconnu du provider → **403**, la valeur par défaut étant `false`.
En local (`ConfigCat:SdkKey` vide), le provider est alimenté par `FeatureFlags:Defaults`
d'`appsettings.Development.json` : un flag absent de cette section n'existe pas et vaut donc `false`.

## Déploiement

Au build (`pipelines/templates/update-gateway.yaml`), la fonction
`Merge-OcelotConfig` (`pipelines/Deployment/Public/Merge-OcelotConfig.ps1`) fusionne
tous les `ocelot.*.json` en un unique `ocelot.json`, qui est ensuite tokenisé
(`#{env_id}#`, `#{env}#`) et uploadé sur le partage Azure Files monté par la gateway
(`OCELOT_CONFIG_PATH`). Le contrat runtime (un seul fichier, hot-reload) est inchangé.

Pour reproduire la fusion en local :

```powershell
. ./pipelines/Deployment/Public/Merge-OcelotConfig.ps1
Merge-OcelotConfig -SourceFolder ./src/Config -OutputFile ./ocelot.merged.json
```
