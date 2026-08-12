# Lancer la gateway sur un poste

En `Development`, la gateway construit sa configuration Ocelot en mémoire à partir de
`src/Config/ocelot.*.json` : les fichiers sont fusionnés, les tokens `#{env_id}#` et `#{env}#` sont
remplacés, et chaque downstream est réorienté vers une cible joignable depuis votre poste.

## 1. Démarrer

1. Copier `src/ApiGateway/appsettings.local.example.json` en
   `src/ApiGateway/appsettings.local.json`. Ce fichier est personnel et ignoré par git.
2. Lancer le profil `ApiGateway` (`ASPNETCORE_ENVIRONMENT=Development`).

La gateway écoute sur `http://localhost:5080` — valeur de `LocalRouting:BaseUrl`. En l'état, toutes
les routes tapent l'environnement `itg01` : vous pouvez déjà appeler
`http://localhost:5080/gtw/account/api/accounts/currentuser` avec un token valide — voir le §5 pour
le parcours client, dont l'autorité se comporte différemment de celle des collaborateurs.

## 2. Router une route vers un service lancé sur votre poste

Déclarez le service dans `appsettings.local.json`. La clé est le segment de service de l'upstream
(`/gtw/`**`account`**`/api/...`), la valeur la racine locale :

```json
{
  "LocalRouting": { "Services": { "account": "https://localhost:60349" } },
  "AccountApiUri": "https://localhost:60349"
}
```

Toutes les routes `/gtw/account/**` partent alors sur `localhost:60349`, en conservant leur
`DownstreamPathTemplate`. Les services non déclarés continuent de viser `itg01`.

La seconde clé (`AccountApiUri`) vise les appels que la gateway émet elle-même — contrôleurs et
agrégateurs, qui ne passent pas par Ocelot. Ajoutez-la si vous voulez que ces appels aussi visent
votre instance locale ; sinon, omettez-la.

Une clé `*ApiUri` désigne la **racine du service**, sans `/api` : ce segment appartient aux chemins
des clients, qui sont tous relatifs (`api/subscription`, jamais `/api/subscription` — un chemin
absolu ferait perdre le préfixe porté par la base). `GetBaseUri` retire un `/api` final s'il en
trouve un, pour rester compatible avec les valeurs déployées qui le portent encore : avant que les
chemins ne passent en relatif, ce suffixe était ignoré par `HttpClient` et donc sans effet.

Si un segment déclaré ne correspond à aucune route, un avertissement `[LocalRouting]` s'affiche sur
la sortie standard au démarrage : c'est le signe d'une faute de frappe dans le segment.

## 3. Où part une route

Trois cas, évalués dans cet ordre :

| Cas | Condition | Cible |
|---|---|---|
| a | segment présent dans `LocalRouting:Services` | votre poste, chemin d'origine conservé |
| b | code de service présent dans `LocalRouting:ServicePrefixes` | point d'entrée public, préfixe devant le chemin du microservice |
| c | sinon | gateway déployée, sous le préfixe `/desktop` |

Le **code de service** du cas b se lit dans le host downstream de la route, selon la convention
`appcegpulse{svc}{env_id}{instance}` :

```
ocelot.account.json
  Host            : appcegpulseacc#{env_id}#01.azurewebsites.net   ->  code acc01
  DownstreamPath  : /api/accounts/currentuser
appsettings.Development.json
  ServicePrefixes : "acc01": "/account"
Cible
  https://api-itg01.itg.pulse.rydge.fr/account/api/accounts/currentuser
```

Le cas c passe par la gateway déployée, qui matche sur l'**upstream** :

```
Upstream : /gtw/feedcenter/api/feeds
Cible    : https://api-itg01.itg.pulse.rydge.fr/desktop/gtw/feedcenter/api/feeds
```

> **Pourquoi les deux cas ?** Les `DelegatingHandlers` traduisent le chemin destiné au microservice —
> `ContactHandler` remplace le segment `currentuser` par un `contactId`, et deux autres fragments
> subissent le même sort. Cette traduction n'a de sens que face au microservice lui-même : chaînée
> vers la gateway déployée, elle produit une URL qu'aucune de ses routes upstream ne reconnaît, donc
> un 404. Le cas c convient donc aux seuls services dont les routes ne portent pas ces fragments.

La section `Aggregates` n'est pas réécrite : elle délègue à ses `RouteKeys`, qui le sont.

## 4. Une route renvoie un 404 nginx

Un 404 émis par nginx — page d'erreur brute, pas un JSON de la gateway — signifie que le point
d'entrée public ne connaît pas le chemin demandé. La cause habituelle est une entrée périmée de
`LocalRouting:ServicePrefixes` : cette table est maintenue par l'équipe infra et évolue.

1. Ouvrir `src/Config/ocelot.<domaine>.json` et relever le
   `DownstreamHostAndPorts[0].Host` de la route en cause.
2. En déduire le code de service : `appcegpulse`**`acc`**`#{env_id}#`**`01`** → `acc01`.
3. Comparer avec l'entrée correspondante de `ServicePrefixes` dans `appsettings.Development.json`.
4. Tester le préfixe directement sur le point d'entrée public :
   `curl -i https://api-itg01.itg.pulse.rydge.fr/<prefixe>/swagger/index.html`.
   Un 404 nginx confirme que le préfixe n'est pas le bon ; demander le préfixe courant à l'infra.
5. Corriger l'entrée dans `appsettings.Development.json` et **commiter** : la table est partagée par
   toute l'équipe.

**Repli** : retirer l'entrée du service de `ServicePrefixes` bascule ses routes en cas c. Cela ne
fonctionne que pour les routes qui ne passent pas par un `DelegatingHandler` réécrivant le chemin —
sinon vous échangez un 404 nginx contre un 404 Ocelot.

## 5. Tester le parcours client (Gigya)

Les autorités JWT sont lues depuis la section `Authorities` d'`appsettings.Development.json` : la
table Azure qui les porte en déployé est derrière un private endpoint, injoignable depuis un poste.

`AAD` et `AAD ADM` couvrent le parcours collaborateur, `GIGYA MyPulse v2` le parcours client. Ce
dernier est exigé par 128 routes, dont la plupart acceptent aussi `AAD` — **un token collaborateur
masque donc une autorité Gigya cassée**. Tester en collab ne prouve rien pour le client.

Cette autorité ne porte aucune clé de signature : la clé publique est récupérée au démarrage sur
`accounts.getJWTPublicKey`, endpoint public dont l'`apiKey` est celle du SDK front.

Cette `apiKey` n'est pas versionnée : `appsettings.Development.json` la référence par le token
`#{gigya_api_key}#`, dans l'URL de fetch comme dans le nom de l'issuer, et
`ConfigurationAuthorityRepository` le résout au démarrage avec l'entrée `GigyaApiKey`
d'`appsettings.local.json`. Elle n'est donc à renseigner qu'**à un seul endroit**.

Le login (`POST /gtw/authentication/api/login`) est une route **anonyme** : il ne valide aucun token,
et ne teste donc pas Gigya. Les avertissements `ContactHandler` sur le bearer token absent y sont
normaux. Il renvoie un jeton intermédiaire du service Contact ; c'est l'appel suivant, sur une route
authentifiée, qui exerce l'autorité Gigya.

> **Un token client rejeté par un 401 sans `error_description`, c'est l'issuer.**
> `PulseJwtBearerHandler.IsValidToken` compare l'`iss` du token à `ValidIssuers[0].Name` par égalité
> stricte et décline silencieusement en cas de différence. C'est ce mécanisme qui permet à une route
> d'accepter `AAD` *ou* Gigya, chaque schéma déclinant le token de l'autre — mais il rend aussi toute
> erreur de clé muette.
>
> L'`apiKey` du site est écrite dans le token lui-même, c'est la source la plus fiable et elle ne
> demande aucun accès Azure. Dans la console du navigateur :
> `JSON.parse(atob(t.split('.')[1])).iss` → `https://fidm.gigya.com/jwt/<apiKey>/`.
>
> En cas de remplacement, une seule valeur est à changer : `GigyaApiKey` dans
> `appsettings.local.json`.

Une fois le token accepté, `AuthorizationMiddleware` appelle `ValidateCustomerAsync`, qui interroge
`accounts.search` avec `GigyaApiKey`, `GigyaSecret` et `GigyaUserKey`. Sans ces trois valeurs le
parcours client s'arrête sur un **500 `GTW012`** portant l'erreur Gigya
`403005 — The supplied userkey was not found` : toute réponse Gigya avec un `errorCode` non nul
devient une anomalie, pas un « utilisateur inexistant ».

Les trois se déclarent dans `appsettings.local.json`, jamais dans le fichier versionné.
`GigyaApiKey` est publique et se lit dans le token ; `GigyaSecret` et `GigyaUserKey` sont de vraies
credentials partenaire, à relever dans l'App Config de l'environnement.

Pour obtenir un token client : se connecter au front MyPulse et relever le JWT émis par le navigateur.

## 6. Points connexes

- **CORS** — pour appeler la gateway depuis un front servi sur une autre origine, ajoutez cette
  origine à `LocalCorsOrigins` dans `appsettings.Development.json` (les ports front usuels y sont
  déjà). La politique s'active dès que la liste est non vide.
- **Authentification** — les schémas JWT sont lus dans la section `Authorities`
  d'`appsettings.Development.json`. Pour en ajouter un, ajoutez-y une entrée ; voir le §5 pour le
  parcours client et ses pièges.
- **Ajouter une route** — dans `src/Config/ocelot.<domaine>.json`, source unique pour tous les
  environnements. Elle est disponible en local au redémarrage suivant.
- **Stockage** — aucun stockage Azure n'est nécessaire tant que les mocks sont désactivés
  (`isMocksEnabled: false`) : le client blob n'est construit qu'à la première utilisation.
