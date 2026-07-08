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
