# Conventions Backend Pulse — Shared Agent Instructions

> Ce fichier centralise les conventions de développement partagées entre tous les outils IA (Copilot, Claude, etc.).
> Il est référencé par `/.github/copilot-instructions.md` et `/CLAUDE.md`.
> Dernière mise à jour : 2026-06-09

---

## Stack technique

- .NET 8 / C# 12
- ASP.NET Core 8 (Minimal APIs ou Controllers)
- Entity Framework Core 8
- FluentValidation
- xUnit + Moq + FluentAssertions
- Azure Service Bus (messaging)
- Polly (résilience)
- Hangfire (background jobs)

---

## Architecture — Clean Architecture

### Structure d'un microservice

```
src/
├── Domain/           ← Entités, Value Objects, Enums métier (PAS de framework)
├── Application/      ← Services, Interfaces, DTOs, Mappers, Constants, Validators
├── Infrastructure/   ← Repositories (EF Core), Providers, Event Handlers, DbContext
└── Web/              ← Controllers (orchestration), Program.cs, Middlewares
tests/                ← xUnit, Moq, FluentAssertions
```

### Règles de couche

- **Controller** → orchestration uniquement (max 5 lignes de logique). Délègue au Service.
- **Service** → logique métier. Appelle Repository/Provider.
- **Repository** → accès données uniquement. Retourne des entités matérialisées.
- JAMAIS skip de couche (Controller → Repository directement = interdit)
- Abstractions (interfaces) définies dans **Application**, jamais dans Infrastructure
- Azure Functions : max 20 lignes, déléguer à un service

### Injection de dépendances

- Max 4 paramètres constructeur. Au-delà → extraire une facade
- Enregistrement dans `ServicesConfiguration.cs` (pas dans Program.cs)
- Lifetimes : Scoped (Services, Repos, DbContext), Singleton (config, caches), Transient (légers)
- JAMAIS `BuildServiceProvider()` dans les extensions
- Un Singleton ne peut PAS injecter un Scoped (captive dependency)
- Options pattern : `IOptions<T>` pour toute configuration

---

## Nommage

### Casse

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Classes, Méthodes, Propriétés | PascalCase | `SubscriptionService` |
| Interfaces | `I` + PascalCase | `ISubscriptionService` |
| Paramètres, variables locales | camelCase | `subscriptionId` |
| Champs privés | `_camelCase` | `_subscriptionRepository` |
| Constantes | PascalCase | `MaxRetryCount` (JAMAIS SCREAMING_CASE) |
| Méthodes async | Suffixe `Async` | `GetByIdAsync` |

### Suffixes obligatoires par couche

| Couche | Type | Suffixe | Exemple |
|--------|------|---------|---------|
| Web | Contrôleur | `Controller` | `SubscriptionController` |
| Application | Service métier | `Service` | `SubscriptionService` |
| Application | Interface service | `I*Service` | `ISubscriptionService` |
| Application | DTO entrant | `Request` ou `Command` | `CreateSubscriptionRequest` |
| Application | DTO sortant | `Response` ou `Dto` | `SubscriptionDto` |
| Application | Validateur | `Validator` | `CreateSubscriptionValidator` |
| Infrastructure | Repository | `Repository` | `SubscriptionRepository` |
| Infrastructure | Interface repo | `I*Repository` | `ISubscriptionRepository` |
| Infrastructure | Event handler | `EventHandler` | `AccountCreatedEventHandler` |
| Infrastructure | Provider externe | `Provider` | `NotificationProvider` |
| Infrastructure | Client HTTP | `Client` | `PennylaneApiClient` |
| Infrastructure | DbContext | `Context` | `SubscriptionContext` |
| Domain | Entité | `Entity` | `SubscriptionEntity` |

### INTERDITS

- ❌ Suffixe `Result` sur les DTOs (utiliser `Response`)
- ❌ Abréviations (sauf : `Id`, `Dto`, `Http`, `Uri`, `Url`, `Xml`)
- ❌ Booléens sans préfixe → TOUJOURS `Is*`, `Has*`, `Can*`, `Any*`

```csharp
// ❌ MAUVAIS
bool Active; bool Success; string GetSubscriptionResult;

// ✅ BON
bool IsActive; bool IsSuccess; string GetSubscriptionResponse;
```

### Langue

- Code : anglais uniquement (classes, variables, méthodes, commentaires techniques)
- Commits : anglais (Conventional Commits)

---

## API REST

### URIs

- Minuscules, pluriel, kebab-case, sans verbes
- `/subscriptions` (POST) au lieu de `/createSubscription`

```
GET    /subscriptions          ← Liste
GET    /subscriptions/{id}     ← Détail
POST   /subscriptions          ← Créer
PUT    /subscriptions/{id}     ← Modifier
DELETE /subscriptions/{id}     ← Supprimer
```

### Réponses HTTP

| Action | Code | Pattern |
|--------|------|---------|
| GET | 200 OK | Retourne la ressource |
| POST création | 201 Created | `CreatedAtAction()` |
| PUT/PATCH | 200 OK ou 204 NoContent | |
| DELETE | 204 NoContent | |
| Erreur validation | 400 Bad Request | `ProblemDetails` (RFC 7807) |
| Non trouvé | 404 Not Found | `ProblemDetails` |
| Non autorisé | 403 Forbidden | |

### Attributs obligatoires

```csharp
[HttpPut("{subscriptionId}/confirm-transfer")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> ConfirmTransferAsync(
    int subscriptionId,
    [FromQuery] string validator,
    CancellationToken cancellationToken)
{
    await _subscriptionService.ConfirmTransferAsync(subscriptionId, validator, cancellationToken);
    return Ok();
}
```

### Query parameters

- camelCase : `/offers?isActive=true&pageIndex=1`
- JSON properties : camelCase (par défaut ASP.NET Core)

---

## Qualité Code

### Async/Await

- ✅ `async/await` partout
- ❌ JAMAIS `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` (deadlock)
- ❌ JAMAIS `async void` (utiliser `async Task`)
- ✅ Propager `CancellationToken` dans TOUTES les méthodes async I/O
- ✅ `Task.WhenAll` pour les appels indépendants parallèles
- ❌ Pas de `Task.Run` dans les controllers/services

### Validation

- **FluentValidation** pour toute validation d'input
- ❌ JAMAIS `[Required]`, `[EmailAddress]` sur les DTOs
- Validation métier (transitions de statut) → dans le Service, pas FluentValidation

### Gestion d'erreurs

- `throw;` (pas `throw ex;`) pour préserver la stack trace
- Catch spécifique (pas `catch (Exception)` générique)
- JAMAIS de catch vide
- `IExceptionHandler` (.NET 8+) pour la gestion centralisée
- Retourner `ProblemDetails` (RFC 7807) en réponse

### Exception boundaries

| Layer          | Throws                                      | Catches                                        |
|----------------|---------------------------------------------|------------------------------------------------|
| Domain         | `ArgumentException`, domain-specific         | Nothing                                        |
| Application    | Business exceptions                          | Nothing (let propagate)                        |
| Infrastructure | `DbException`, `HttpRequestException`        | Wrap into business exceptions if needed        |
| Web            | Nothing                                      | `IExceptionHandler` catches all                |

- Controller does NOT do `try/catch` with business recovery

### Logging structuré

```csharp
// ✅ BON — placeholders nommés PascalCase
_logger.LogInformation("Subscription {SubscriptionId} confirmed by {ValidatorId}", subscriptionId, validatorId);

// ❌ MAUVAIS — interpolation
_logger.LogInformation($"Subscription {subscriptionId} confirmed");
```

- Placeholders en PascalCase
- ❌ JAMAIS de données sensibles dans les logs (email, token, mot de passe → utiliser un ID)
- Propager `X-Correlation-Id` dans tous les logs

### SOLID

- **SRP** : Une classe = une responsabilité
- **Max 4 paramètres** par méthode (au-delà → objet `*Request`)
- **Pas de booléens en paramètre** (flags). Préférer des méthodes explicites ou enums

---

## EF Core / Performance

### Lectures

- `.AsNoTracking()` pour TOUTE requête en lecture seule
- `.Include()` pour charger les relations (éviter N+1)
- `.AsSplitQuery()` si 3+ Includes
- JAMAIS retourner `IQueryable<T>` hors du repository → toujours `.ToListAsync()`
- JAMAIS `ToLower()`/`ToUpper()` dans LINQ (SQL Server est CI_AS)

### Écritures

- `ExecuteUpdateAsync` / `ExecuteDeleteAsync` pour les opérations en masse
- Toujours `SaveChangesAsync()` (jamais synchrone)
- `DateTime.UtcNow` (ou `DateTimeOffset.UtcNow`) en environnement Cloud

### Entités

- JAMAIS exposer les entités EF Core dans l'API → toujours mapper vers DTOs
- Les entités restent dans Domain/Infrastructure

### Performance diverse

- `StringBuilder` ou `string.Join` au lieu de `+=` dans les boucles
- Ne pas énumérer plusieurs fois un `IEnumerable` → `.ToList()` d'abord
- `ConcurrentDictionary` pour les caches dans les Singleton
- Borner `pageSize` (max 100) dans la pagination

---

## Résilience (Polly)

- Retry avec **backoff exponentiel + jitter** (HTTP 429, 503, SQL timeout)
- Circuit Breaker pour les services externes instables
- Timeout obligatoire sur tout `HttpClient`
- `EnableRetryOnFailure` sur SQL Server
- Health Checks exposés pour chaque microservice

---

## Sécurité

### OWASP API Security

- **BOLA** : Vérifier l'ownership SYSTÉMATIQUEMENT avant de retourner/modifier une ressource
- **BFLA** : Vérifier les rôles/permissions pour les actions critiques
- **Mass Assignment** : JAMAIS binder une entité EF directement. DTOs `*Request` explicites
- **Injection SQL** : JAMAIS de concaténation dans les requêtes SQL Raw
- Valider tous les inputs avec FluentValidation

### Auth

- API Gateway (Ocelot) : point d'entrée unique, validation JWT, injection headers
- Ordre middlewares : `UseRouting → UseAuthentication → UseAuthorization → MapControllers`
- `[Authorize]` par défaut, `[AllowAnonymous]` explicite pour routes publiques
- CORS : JAMAIS `AllowAnyOrigin()` en production

### Protection des données

- ❌ Jamais de secrets en dur → Azure Key Vault / variables d'environnement
- ✅ Managed Identity pour Azure SQL, Service Bus, Blob Storage
- ❌ Jamais de PII (email, téléphone) dans les logs ou events
- ❌ Jamais de stack trace dans les réponses d'erreur en production
- HTTPS obligatoire
- Path Traversal : `Path.GetFileName()` sur tout input fichier
- `MaxRequestBodySize` limité (10MB) contre DoS

---

## Tests

### Nommage

```
MethodName_WhenCondition_ShouldExpectedResult
```

Exemples :
- `ConfirmTransferAsync_WhenStatusIsPennylaneToTransfer_ShouldUpdateStatus`
- `ConfirmTransferAsync_WhenSubscriptionNotFound_ShouldThrowNotFoundException`

### Pattern AAA (Arrange-Act-Assert)

```csharp
[Fact]
public async Task ConfirmTransferAsync_WhenStatusIsValid_ShouldUpdateAndNotify()
{
    // Arrange
    var subscription = new SubscriptionEntity { Id = 1, Status = "pennylane-to-transfer" };
    _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(subscription);

    // Act
    await _service.ConfirmTransferAsync(1, "validator@test.com");

    // Assert
    _mockRepo.Verify(r => r.UpdateSubscriptionAsync(1, "pennylane-transfer-in-progress", "validator@test.com"), Times.Once);
    _mockNotification.Verify(n => n.HandleOfferNotificationsAsync(It.IsAny<SubscriptionEntity>(), EligibleReciversRule.Collaborator), Times.Once);
}
```

### Règles

- Framework : xUnit + Moq + FluentAssertions
- Un test = un comportement
- Mocker les dépendances (repos, providers) — jamais les types framework
- Tests déterministes : injecter `IDateTimeProvider` (pas `DateTime.Now`)

---

## Git

### Branches

Format : `{type}/{ticket-id}-{description-courte}`

| Type | Usage |
|------|-------|
| `feature` | Nouvelle fonctionnalité |
| `fix` | Correction de bug |
| `hotfix` | Correction urgente production |
| `refactor` | Refactoring technique |
| `chore` | Tâches techniques (deps, CI) |

Exemple : `feature/61516-passage-d-une-demande-en-cours`

### Commits — Conventional Commits

Format : `<type>(<scope>): <subject> (Refs: #ID)`

Types : `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `style`, `ci`, `chore`
Scopes : `subscription`, `offer`, `catalog`, `account`, `contact`, `notification`, `infrastructure`

Règles :
- Impératif présent : `add feature` (pas `added`)
- Pas de majuscule initiale
- Pas de point final
- Descriptif et concis (pas `fix bug` ou `update`)

Exemple : `feat(subscription): add confirm-transfer endpoint (Refs: #61516)`

### Pull Requests

- Titre = format Conventional Commits
- Au moins 1 approbation reviewer
- Merge en Squash commit
- Branche source supprimée après merge

### Breaking Changes

```
feat(subscription)!: change status from string to enum
```

---

## Configuration projet

### .editorconfig obligatoire

- Interfaces : préfixe `I`
- Champs privés : `_camelCase`
- Accolades obligatoires
- Using hors namespace
- Null propagation favorisé
- `readonly` sur tous les champs DI

### Analyseurs Roslyn

- `AnalysisLevel: latest-recommended`
- `EnforceCodeStyleInBuild: true`
- CA1848/CA2254 : Structured logging
- CA1727 : PascalCase pour placeholders logs
- CA2200 : `throw;` (pas `throw ex;`)

---

## Event-Driven

### Nommage événements

Format : `{Entity}{Action}Event` → `AccountCreatedEvent`, `SubscriptionConfirmedEvent`

### Idempotence obligatoire

Un event handler DOIT être idempotent : vérifier l'existence avant `AddAsync`.

### Pattern Outbox

Utiliser le pattern Outbox pour garantir la cohérence entre l'écriture en base et la publication de l'événement.

---

## Modifications chirurgicales

- Ne toucher QUE les lignes nécessaires
- Pas de reformatage global
- Pas de refactoring non demandé
- Respecter le style existant du fichier

