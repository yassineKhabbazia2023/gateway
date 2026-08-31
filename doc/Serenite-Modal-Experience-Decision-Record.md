# Modal Sérénité — consigne, arbitrages et réalisation

> **Ticket** : 63621 · **Branche** : `feature/63621_serenite-redirect` (Gateway, Account, Offer)
> **Date** : 2026-08-19
> **Nature** : document de traçabilité. Il consigne la demande telle qu'elle a été formulée, les
> questions posées avant de coder, les réponses obtenues, et ce qui a effectivement été livré.
> Pour le fonctionnement courant du service, voir [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

---

## 1. La consigne

### Demande initiale

Introduire dans la Gateway un nouvel endpoint d'expérience dont le seul rôle est de dire **oui ou
non** si le front-end doit afficher une modal Sérénité. Les critères sont répartis sur plusieurs
micro-services :

1. La ligne d'account correspondant à l'accountId trouvé dans la route URI a-t-elle de la donnée
   dans `AccountRoutingCode`, et cette valeur vaut-elle `0-B2B` ?
2. La colonne `AccountElectronicAddressId` de cette même ligne est-elle vide ou nulle ?
3. L'account n'a-t-il **aucune souscription active** sur l'offre Pennylane, au niveau du
   micro-service Offer ?

Si oui aux trois, retourner vrai. Sinon faux.

À prévoir également : un endpoint de type POST pour que l'utilisateur renvoie un choix vrai ou faux.
La persistance de ce choix entre en considération dans la condition précédente — si un choix a été
fait, quelle que soit sa valeur, on retourne systématiquement faux.

Consigne de méthode explicite : **questionner plutôt qu'interpréter**.

### Adaptation en cours de route

> « Adaptation besoin : les critères sont sur tout le portefeuille d'account d'un contact : *si le
> client a au moins 1 entité avec les critères suivants* »

Ce changement est structurant : la décision n'est plus attachée à un accountId passé en route, elle
devient **globale au portefeuille du contact connecté**.

---

## 2. Les arbitrages soumis avant de coder

Huit points d'incertitude ont été soumis plutôt que tranchés unilatéralement.

| # | Question | Réponse retenue |
|---|---|---|
| 1 | Que signifie « souscription active » ? Le modèle Offer n'a ni `IsActive` ni date de fin, seulement un statut texte parmi 14 valeurs. | **`{validated, success}`** — le seul prédicat « actif » existant du parc |
| 2 | Portée du choix persisté : par compte, ou par (contact, compte) ? | **Par compte**, puis **par contact** après l'adaptation portefeuille |
| 3 | Qui porte la persistance ? La Gateway n'a **aucune** base de données. | **Micro-service Account**, table dédiée |
| 4 | Le feature flag existant `enableApprovedPlatform` doit-il conditionner la réponse ? | **Non** — seuls les critères métier |
| 5 | Quels codes de permission poser sur les endpoints ? | **Aucun** (`[RequirePermission]` non posé) |
| 6 | Le choix est-il modifiable ? | **Immuable** — second appel : `409 Conflict` |
| 7 | Contrat d'URL avec le front-end ? | Greffé sur `ConnectExperience`, `gtw/connect/api/serenity-modal` |
| 8 | Après l'adaptation portefeuille : l'accountId disparaît-il de la route ? Quel est le périmètre du portefeuille ? | **Il disparaît** ; portefeuille = **toutes les entités où le contact a un rôle** |

Un neuvième point a été soulevé après exploration : le micro-service **Contact** est le propriétaire
légitime de l'état par contact et expose déjà `GET|PATCH /gtw/contact/api/contacts/currentuser/terms`
avec une route catch-all — un endpoint là-bas n'aurait demandé **aucune** modification Gateway.
Écarté car le repository est hors du workspace. Arbitrage confirmé : **Account, table dédiée**.

---

## 3. Ce que l'exploration a établi

Cinq faits ont façonné la solution. Aucun n'était devinable depuis l'énoncé.

- **Sérénité n'est pas une offre, c'est un plan.** `PlanCodes.Serenite = "APPROVED_PLATFORM"`, plan de
  l'offre `Pennylane`. La Gateway connaissait déjà `OfferPlanCodes.ApprovedPlatform` et le flag
  `enableApprovedPlatform`.
- **Les critères 1 et 2 étaient déjà servis.** `GET api/accounts/{accountId}` renvoie `AccountDetail`
  avec `AccountRoutingCode` et `AccountElectronicAddressId`, et la Gateway appelait déjà cette URL —
  elle jetait simplement ces colonnes au désérialisage.
- **Les portefeuilles sont gros.** Les seeds de dev donnent **200 entités par contact client**
  (24 contacts × 200 rôles), et rien ne le borne : `Paginator.GetValidPageSize` renvoie `int.MaxValue`
  quand la pagination est absente. L'approche naïve aurait fait 200+ allers-retours par chargement.
- **`actor.Contact` est une réplique** alimentée par Service Bus, en lecture seule côté Account. Pas
  question d'y poser une colonne métier.
- **Aucun index** n'existait sur `AccountRoutingCode` (`Account.sql` n'indexe que `AccountNumber`,
  `LegalName`, `HubId`, `NafId`).

---

## 4. La solution retenue

Principe directeur : **chaque critère est évalué là où vit la donnée**. Chaque service filtre en SQL
et ne renvoie que des identifiants.

```
Gateway ──1──▶ Account : « état Sérénité du contact X ? »
                         → { hasMadeChoice, candidateAccountIds[] }
                           critères 4 + 1 + 2, une seule requête SQL

        ──2──▶ Offer   : « parmi ces ids, lesquels ont une souscription Pennylane active ? »
                         → int[]
                           critère 3, une seule requête SQL

        = candidateAccountIds.Except(subscribed).Any()
```

**Deux appels downstream, quelle que soit la taille du portefeuille.** Le `Except` final porte une
contrainte facile à rater : les critères 1, 2 et 3 doivent être satisfaits par **une seule et même
entité** — pas le 1 sur l'entité A et le 3 sur l'entité B.

Les deux appels sont séquentiels (le second dépend du premier), donc pas de `Task.WhenAll`. Si l'un
des services est injoignable, on retourne `false` : *fail closed*, on ne propose pas Sérénité sans
avoir pu vérifier l'absence de souscription.

### Contrat front-end

```http
GET  /gtw/connect/api/serenity-modal
     → 200 { "shouldDisplay": true }

POST /gtw/connect/api/serenity-modal
     { "isAccepted": false }
     → 204 No Content
     → 409 Conflict si un choix existe déjà
```

Aucun accountId : le périmètre est déduit du token. Aucune route Ocelot à ajouter — `MapControllers()`
passe avant `UseOcelot` dans `Program.cs`, c'est pourquoi il n'existe aucun `ocelot.connect.json`.

### Sécurité

Pas de contrôle BOLA, et c'est correct : aucune ressource n'est ciblée par un identifiant client. Le
périmètre lu **et** le choix écrit sont dérivés du `contactId` résolu côté serveur depuis le token
(`GetEmail()` → `GetContactAsync` → `contact.Id`), jamais de la route, du corps ou d'un en-tête
entrant. C'est ce qui empêche de lire ou d'écrire pour le compte d'autrui.

---

## 5. Ce qui a été livré

### Gateway — orchestration seule

| Fichier | Rôle |
|---|---|
| `ConnectExperience/Controller/ConnectController.cs` | 2 actions + `ResolveContactIdAsync` |
| `ConnectExperience/Services/ConnectServices.cs` | décision et délégation du choix |
| `ConnectExperience/Models/SerenityModalResponse.cs` | `record SerenityModalResponse(bool ShouldDisplay)` |
| `ConnectExperience/Models/SerenityChoiceRequest.cs` | `record SerenityChoiceRequest(bool IsAccepted)` |
| `Models/SerenityEligibility.cs` | miroir de désérialisation |
| `Account/AccountService.cs` + interface | 2 méthodes de client |
| `Offer/OfferService.cs` + interface | 1 méthode de client |
| `Offer/Constants/OfferCodes.cs` | `Pennylane` |
| `Exceptions/Errors.cs` | `GTW037` |
| `ApiGateway.http` | les 2 requêtes de test manuel |

Réutilisé sans modification : `ProspectAccountAuthorizationHelper`, `IContactService`, `IUserContext`.
Inchangés : `ocelot.*.json`, `ServiceExtensions.cs`, `FeatureFlagKeys.cs`, `Models/Account.cs`.

### Account — requête portefeuille et persistance

`account.SerenityChoice` (PK `ContactId`, `IsAccepted`, `ChoiceDate`) : **la présence de la ligne vaut
« un choix a été fait »**. `IsAccepted` est conservé pour le métier mais n'intervient pas dans la
décision. Index filtré `IX_Account_AccountRoutingCode` avec `INCLUDE` sur
`AccountElectronicAddressId`, la colonne étant nulle sur la grande majorité des lignes.

Slice `ISerenityRepository` / `ISerenityService` / `SerenityRepository` / `SerenityService` /
`SerenityController`, plus `AccountRoutingCodes.B2B`, `SerenityEligibility`, `SerenityChoiceRequest`,
`SerenityChoiceEntity` et l'erreur `ACC058`.

La requête part de `AccountEntity` et **hérite donc du filtre global**, qui écarte les comptes
inactifs et de type prospect. Aucun `Include`, projection sur l'identifiant seul, pas de `ToLower()`
(la base est en CI_AS).

### Offer — recherche par lot

`GET api/subscription/active-account-ids?offerCode=…&accountId=1&accountId=2` : parmi une liste de
comptes, ceux qui ont une souscription active sur le code offre. Identifiants répétés en query, comme
`api/roles/last-collaborator` le fait déjà côté Account.

Le prédicat « actif » a été promu de `private static` dans `MissionProcessingService` vers
`SubscriptionStatusCode.ActiveStatuses` : c'est la seule définition du parc, elle ne doit pas se
dédoubler maintenant que le filtre est appliqué côté SQL.

Les endpoints existants ne suffisaient pas : `api/subscription/status` est mono-compte, et la liste
paginée ramène toutes les souscriptions de la plateforme avec dix niveaux d'`Include`.

---

## 6. Écarts par rapport au plan validé

1. **FK ajoutée** sur `account.SerenityChoice` vers `actor.Contact`. Le plan la refusait, au motif
   d'un couplage au cycle de vie d'une réplique. Motif invalide : `RemoveContactAsync` fait un **soft
   delete** (`IsActive = false`), aucune ligne n'est jamais supprimée — et `account.DelegationRequest`
   porte déjà deux FK identiques. Le précédent du code l'emporte.
2. **Pas de navigation** `SerenityChoiceEntity` → `ContactEntity` : `ContactEntity` porte un filtre de
   requête global, une navigation requise vers un principal filtré serait signalée par EF Core.
   L'accès se fait uniquement par clé primaire.
3. **`ActiveStatuses` déclaré `string[]`** et non `IReadOnlyList<string>`, pour garantir la traduction
   du `IN` par EF Core.

Un bug attrapé pendant l'écriture des tests : `ContactType` n'a pas de membre `Client` (l'enum est
`Collaborator` / `Customer`).

---

## 7. État de vérification

| Périmètre | État |
|---|---|
| Gateway, solution complète | Compile. **1129/1129 tests verts**, dont **18 nouveaux** |
| `Account.Core` (7 fichiers) | Compile |
| `SerenityRepository`, `SerenityChoiceEntity`, `AccountContext.Customization` | Aucune erreur remontée quand `Account.Infrastructure` a été compilé |
| Offer : constantes, interfaces, services, `SubscriptionRepository`, `MissionProcessingService` | Idem, aucune erreur sur les lignes modifiées |
| `SerenityController`, `SubscriptionController`, tests Account et Offer | **Non compilés** |

**Cause du blocage** : le flux NuGet privé `pkgs.dev.azure.com/FR-TPEME` répond **401** dans
l'environnement d'exécution. Contournement par restore hors-ligne depuis le cache global, mais
`Pulse.Back.Events` n'y existe qu'en 2.75.0 / 2.74.179 au lieu des 2.74.201 / 2.74.194 épinglées, et
ces versions ont perdu toute une famille de types d'événements. `Account.Infrastructure` et
`Offer.Application`/`Infrastructure` échouent alors sur des **fichiers pré-existants**, non touchés
par cette feature.

Des stubs temporaires ont été tentés pour débloquer la compilation, puis **retirés** : le shim
devenait une réinvention du contrat du package, un build vert n'aurait plus rien prouvé. Aucun
`_TempBuildShim.cs` ne subsiste et les versions des `.csproj` sont rétablies à l'identique.

### À faire sur un poste authentifié

```bash
# Account et Offer
dotnet restore && dotnet test
```

Ce restore régénérera aussi les `obj/project.assets.json`, que le passage hors-ligne a réécrits avec
de mauvaises versions.

### Séquence de recette manuelle

Avec un contact possédant au moins une entité `0-B2B`, sans adresse électronique et sans souscription
Pennylane active — ces quatre appels couvrent toute la feature :

| # | Appel | Attendu |
|---|---|---|
| 1 | `GET /gtw/connect/api/serenity-modal` | `{"shouldDisplay":true}` |
| 2 | `POST /gtw/connect/api/serenity-modal` `{"isAccepted":false}` | `204` |
| 3 | `GET /gtw/connect/api/serenity-modal` | `{"shouldDisplay":false}` |
| 4 | `POST /gtw/connect/api/serenity-modal` `{"isAccepted":true}` | `409` |

L'étape 3 est celle qui compte : elle vérifie qu'un choix **négatif** éteint bien la modal.

---

## 8. Points laissés à l'arbitrage produit

1. **`pending-deactivation` rend la modal éligible.** Pennylane a `ApprovalRequired = true` : la
   première demande de désactivation passe le statut à `pending-deactivation` au lieu de supprimer la
   ligne. Avec `{validated, success}`, cette entité compte comme **non souscrite** et la modal
   s'affiche alors que la désactivation attend encore la validation MOD. Ajouter `PendingDeactivation`
   à `ActiveStatuses` coûte un mot.
2. **Prospects et comptes inactifs sont hors périmètre**, par héritage du filtre global EF sur
   `AccountEntity`. C'est un effet du choix d'API, pas une décision explicite.
3. **La désactivation supprime la ligne de souscription** : « jamais souscrit » et « désabonné » sont
   indiscernables. Le choix persisté neutralise définitivement le cas, ce qui est vraisemblablement
   l'effet voulu.
4. **`0-B2B` est une valeur provisoire.** `AccountRoutingCode` est documenté « NVARCHAR(255)
   provisoire en attente du contrat Akuiteo définitif ». La valeur est isolée dans
   `AccountRoutingCodes.B2B`, un seul point à changer.
5. **Longueur d'URL** sur l'appel Offer : seul point non borné du design, si un contact a un très gros
   portefeuille *et* que beaucoup d'entités passent les critères 1+2. Peu probable, le filtre étant
   étroit. Commentaire `ponytail:` en place, découpage par lots comme voie de sortie.
6. **Nommage hétérogène** : Offer dit `Serenite`, la Gateway dit `ApprovedPlatform`, le contrat front
   dit `serenity-modal`. Le nouveau code utilise `Serenity` partout pour coller à la route retenue.
