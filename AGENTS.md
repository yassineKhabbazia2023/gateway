# Pulse Gateway — Development Conventions

## Project

- Stack: .NET 8, C# 12, ASP.NET Core, Ocelot 23, Azure Service Bus
- Architecture: API Gateway (single project — not Clean Architecture)
- Database: SQL Server (CI_AS collation — case insensitive), Azure Table Storage, Redis, IMemoryCache
- Auth: Multi-scheme JWT (AAD, Gigya), custom token revocation
- Feature Flags: OpenFeature + ConfigCat
- CI/CD: Azure DevOps Pipelines
- Hosting: Azure App Services, Azure Functions


## Project Structure

```
src/
  ApiGateway/
    Program.cs                     # Entry point + Ocelot pipeline
    GlobalUsings.cs
    Configuration/                 # Ocelot config, service config, Swagger
      ocelot.json                  # Dev-only routes
    DelegatingHandlers/            # Custom Ocelot handlers (global + per-route)
      Mocks/                       # Mock response system
    Middlewares/                   # Ocelot pipeline middleware (auth, exceptions, token revocation)
    Aggregator/                    # Ocelot response aggregators
    Extensions/                    # DI registration, middleware setup, helpers
    Constants/                     # Permissions, headers, global constants
    Exceptions/                    # GatewayException + Errors
    Helpers/                       # JWT, authorization, file helpers
    Identity/                      # JWT auth, identity providers, Azure Table repos
    FeatureFlags/                  # OpenFeature integration
    Cache/                         # In-memory caching
    TokenRevocation/               # Token revocation cache
    Attributes/                    # Custom attributes ([RequirePermission])
    Models/                        # Shared models
    {Feature}/                     # Feature folders: controllers, services, models
      {Feature}Controller.cs
      {Feature}Service.cs
      Models/

Config/
  ocelot.json                     # Production Ocelot routes
  mocks.db                        # Mock responses database

tests/
  ApiGateway.UnitTests/            # Mirrors src/ structure 1:1
```

| File                      | Location                              |
|---------------------------|---------------------------------------|
| DelegatingHandlers        | `DelegatingHandlers/`                 |
| Middleware                | `Middlewares/`                        |
| Aggregators               | `Aggregator/`                         |
| Controllers               | `{Feature}/{Feature}Controller.cs`    |
| Services                  | `{Feature}/{Feature}Service.cs`       |
| Interfaces                | `{Feature}/` or feature root          |
| Models/DTOs               | `{Feature}/Models/` or `Models/`      |
| DI registration           | `Extensions/*ServiceExtensions.cs`    |
| Ocelot config (prod)      | `Config/ocelot.json`                  |
| Ocelot config (dev)       | `Configuration/ocelot.json`           |
| Constants                 | `Constants/GlobalConstants.cs`        |
| Error codes               | `Exceptions/Errors.cs`               |


## Project Configuration

- `<AnalysisLevel>latest-recommended</AnalysisLevel>` in `Directory.Build.props`
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`
- `.editorconfig` at the repo root


## Naming

- Private fields: `_camelCase` (e.g. `private readonly IUserService _userService;`)
- Constants: `PascalCase` — never `SCREAMING_CASE`
- Async methods: `Async` suffix — except controller actions
- Booleans: `Is/Has/Can/Any` prefix
- Interfaces: `I` prefix
- No abbreviations (except Id, Xml, Ftp, Uri, Url, Http, Dto)
- No Hungarian notation (`iCounter`, `strName`) — use meaningful names
- Use C# aliases (`string`, `int`, `object`) not BCL names (`String`, `Int32`, `Object`)
- Enum names: singular (`Status` not `Statuses`), no `Enum`/`Flag` suffix — `[Flags]` enums use plural
- Method names: Verb + Resource — `CreateOrder`, `DeleteBusiness`, `GetConfiguration` — never just `Create`, `Delete`, `Get`. The parameters already indicate the lookup key — avoid redundant `ByXxx` suffixes
- Code in English only (comments and commits may be in French)

### Required Suffixes

| Type                | Suffix                   | Example                        |
|---------------------|--------------------------|--------------------------------|
| Controller          | `*Controller`            | `BookingController`            |
| Service             | `*Service`               | `ContactService`               |
| Repository          | `*Repository`            | `IdentityRepository`           |
| DelegatingHandler   | `*Handler`               | `ContactHandler`               |
| Middleware           | `*Middleware`            | `GatewayExceptionMiddleware`   |
| Aggregator          | `*Aggregator`            | `ConfigurationAggregator`      |
| Guard               | `*Guard` or `*Guards`    | `BookingExperienceGuards`      |
| Provider            | `*Provider`              | `NotificationProvider`         |
| Event Handler       | `*EventHandler`          | `AccountCreatedEventHandler`   |
| Entity              | `*Entity`                | `SubscriptionEntity`           |
| Incoming DTO        | `*Request` or `*Command` | `CreateAuthorizationRequest`   |
| Outgoing DTO        | `*Response` or `*Dto`    | `AuthorizationResponse`        |


## Architecture Rules

The Gateway is a **single project** — not Clean Architecture. Code is organized by feature folder.

### Controller — orchestration + error handling

- Controllers MAY contain try/catch (Gateway override — `GatewayExceptionMiddleware` handles unhandled exceptions but controllers can catch specific cases)
- Controllers call services, services call downstream APIs or repositories
- NEVER call downstream HTTP clients directly from a controller
- NEVER return an entity/internal model directly — always map to DTO

### DelegatingHandler — request/response pipeline

- Handles cross-cutting concerns on the Ocelot pipeline (headers, auth, logging, feature flags)
- Must call `base.SendAsync()` to forward the request (or short-circuit with a direct response)
- NEVER throw exceptions from handlers — return appropriate HTTP responses
- Keep handlers focused on one responsibility

### Service — business orchestration

- Orchestrates calls to downstream APIs and/or identity repositories
- May contain business logic specific to Gateway aggregation/transformation
- NEVER access HTTP context directly — receive data via parameters


## Ocelot Configuration

### Route structure (`ocelot.json`)

- Upstream path pattern: `/gtw/{service}/...`
- Use `{everything}` catch-all for passthrough routes
- Every route MUST specify `AuthenticationOptions.AuthenticationProviderKey` (or be explicitly anonymous)
- `RouteClaimsRequirement` maps HTTP methods to permission codes: `"GET": "PERM001,PERM002"`
- Permission codes are comma-separated (OR logic within a method)
- `DelegatingHandlers` array lists per-route handlers by class name

### Route conventions

- One route per HTTP method + path combination
- `UpstreamHttpMethod` should list only the methods actually used
- `DownstreamScheme`: always `https` in production
- `DownstreamHostAndPorts` must match the target microservice App Service name
- Service name in path should match the downstream service domain (e.g. `/gtw/account/...` → Account service)

### Modifications

- NEVER remove or rename a published route without deprecation period
- Adding `RouteClaimsRequirement` to an existing open route is a breaking change — coordinate with frontend
- New routes MUST be added to both `Config/ocelot.json` (prod) and `Configuration/ocelot.json` (dev)


## DelegatingHandlers

### Global vs Per-route

| Type | Registration | Scope |
|------|-------------|-------|
| Global | `AddOcelotGlobalDelegatingHandler<T>()` | Runs on every request |
| Per-route | `AddOcelotDelegatingHandler<T>()` | Listed in route's `DelegatingHandlers` array |

### Implementation pattern

```csharp
public class MyHandler : DelegatingHandler
{
    // Inject dependencies via constructor
    public MyHandler(IDependency dep) { ... }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Pre-processing (modify request, add headers)
        request.Headers.Add("X-Custom", value);

        // Forward to next handler / downstream
        var response = await base.SendAsync(request, cancellationToken);

        // Post-processing (read response, cache, log)
        return response;
    }
}
```

### Rules

- Always propagate `CancellationToken`
- Always call `base.SendAsync()` unless intentionally short-circuiting
- Use structured logging — never string interpolation
- Global handlers MUST be resilient to missing headers/claims (not all routes have auth)
- Per-route handlers can assume auth context when the route requires authentication
- DelegatingHandlers are transient by default in Ocelot — do not store request-scoped state in fields


## Token Revocation

- `TokenRevocationMiddleware` runs BEFORE `UseAuthentication` in the pipeline
- Extracts `jti`, `uti`, or `sub:iat` as token identifier (reads JWT without validation)
- Checks `TokenRevocationCache` (IMemoryCache-backed, TTL = token expiry)
- `LogoutRevocationHandler` captures `X-Revoked-Jti`/`X-Revoked-Exp` from downstream logout responses to populate the cache
- Sticky sessions assumed — revocation is per-instance


## Feature Flags

- Standard: OpenFeature with ConfigCat provider
- In-memory fallback when no SDK key configured
- User targeting via SHA256-hashed email
- Feature flag check in DelegatingHandlers for route-level gating
- Handler pattern: check flag → if disabled, return 404/403 directly without forwarding downstream
- NEVER hardcode feature flag keys — use constants


## Validation

- Input validation → FluentValidation `AbstractValidator<T>` on `*Request`/`*Command`


## EF Core

- Always `AsNoTracking()` for read-only queries
- Always `SaveChangesAsync()`, never `SaveChanges()`
- Never `.ToLower()` or `.ToUpper()` in LINQ queries — SQL Server CI_AS handles it
- Use `ExecuteUpdateAsync`/`ExecuteDeleteAsync` for bulk operations when no change tracking, domain events, or cascade behavior is needed
- Explicitly `.Include()` navigations — no lazy loading
- Never expose `IQueryable` outside repository — materialize with `ToListAsync()`
- `AsSplitQuery()` only when multiple sibling collection navigations are included
- Prefer `AddDbContextPool` over `AddDbContext`
- `EnableRetryOnFailure()` for Azure SQL
- Enum properties persisted as strings: `.HasConversion<string>()` in `OnModelCreating`
- Wrap multiple `ExecuteUpdateAsync`/`ExecuteDeleteAsync` in an explicit transaction
- Never mix `SaveChangesAsync` and `ExecuteUpdateAsync`/`ExecuteDeleteAsync` on the same entities in one unit of work
- Anti-pattern `Detach + Update` — modify only the needed properties on the tracked entity


## HTTP Clients

- Interface defined alongside feature, implementation in same feature folder
- `ResiliencePipeline` / Polly policy built once (`private static readonly`) — never inside a method body
- NEVER `Task.Delay` for propagation timing — use a readiness probe with Polly retry (exponential backoff + `ShouldHandle` on a typed exception)
- Prefer `AddStandardResilienceHandler()` as starting point for HTTP clients
- NEVER `new HttpClient()` — use `IHttpClientFactory`
- Mandatory timeout on every external HTTP call
- Use idempotency keys for POST retries to external services


## Logging

- Structured templates: `_logger.LogInformation("User {UserId} created", userId);`
- NEVER string interpolation: `$"User {userId}"` — FORBIDDEN
- NEVER concatenation: `"User " + userId` — FORBIDDEN
- PascalCase placeholders: `{UserId}` not `{userid}`
- Propagate CorrelationId via `X-Correlation-Id` header
- NEVER log PII (email, phone, name, address) — log IDs only
- NEVER log secrets (tokens, passwords, connection strings)
- Use correct log levels: Warning = recoverable anomaly, Error = failure requiring attention — do not confuse
- Prefer `LoggerMessage` source-generated delegates on hot paths for zero-allocation logging
- DelegatingHandlers are hot path — minimize allocations in logging


## Async

- Always `async/await`, never `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
- Never `async void`
- Parallelize independent calls with `Task.WhenAll`
- Never `Task.Run` in the HTTP request path
- No fire-and-forget from controllers
- Do NOT use `ConfigureAwait(false)` in application code


## API REST (Gateway Controllers)

Gateway controllers expose aggregation/orchestration endpoints that don't pass through Ocelot.

- Plural resources: `/users`, `/subscriptions`
- No verbs in URIs: `/users` not `/getUsers`
- kebab-case for multi-word segments: `/booking-links`
- POST creation → `201 Created` with `CreatedAtAction()`
- Empty collection → `200 OK` with `[]`, not `404`
- Errors → `ProblemDetails` (RFC 9457)
- Mandatory pagination on GET collections (default: 20, max: 100)
- `[ProducesResponseType]` on every controller action
- JSON in camelCase, ignore nulls in serialization only (`WhenWritingNull`) — never ignore nulls in deserialization (explicit `null` in PATCH means deletion intent)
- 401 = not authenticated, 403 = not authorized
- DELETE success → `204 No Content`
- API versioning via URL path (`/api/v1/users`) — never break a published version
- URI depth max 3 levels
- GET with body is forbidden (RFC 9110)
- POST for reading data is forbidden — use GET with query parameters


## Security

### Authentication

- Multi-scheme JWT: AAD, AAD ADM, Gigya — configured via `AuthenticationProviderKey` per route
- Custom `PulseJwtBearerHandler` for multi-authority JWT validation
- Authority configurations stored in Azure Table Storage
- FallbackPolicy: `RequireAuthenticatedUser().Build()` — all endpoints authenticated by default, opt-out with `[AllowAnonymous]`
- Token in query string is forbidden — always in `Authorization` header
- No custom authentication — use ASP.NET Core authentication mechanisms

### Authorization

- `RouteClaimsRequirement` in ocelot.json maps HTTP methods to permission codes
- Custom `AuthorizationMiddleware` in Ocelot pipeline validates permissions per-user per-account
- `RoleHandler` (per-route): validates contact has a role on the target account (BOLA prevention)
- `contactId` is ALWAYS from JWT claims (Gateway-resolved) — never from query/body
- BFLA: `[Authorize(Roles = ...)]` on sensitive actions (Gateway-level only)

### General

- Never hardcode secrets in code or config files
- Use Managed Identity instead of connection strings with keys
- Verify ownership (BOLA): `if (entity.OwnerId != currentUserId)` → return 404 (not 403)
- HTTPS required, restrictive CORS (no `AllowAnyOrigin()` in production)
- Middleware order: UseRouting → UseCors → UseAuthentication → UseAuthorization → MapControllers
- No PII in Service Bus events
- Prefer `FromSql` / `FromSqlInterpolated` to ensure parameterization and avoid SQL injection. Use `FromSqlRaw` only when parameters are explicitly handled
- NEVER use entity as `[FromBody]` parameter — over-posting / mass assignment
- NEVER return `ex.Message` or stack trace in API response — except `GatewayException.Message` which contains controlled error messages
- Status/type fields MUST be enums (not `string`) — persist with `.HasConversion<string>()` in EF Core
- `AddServerHeader = false` on Kestrel — do not expose server version
- Headers: whitelist which headers to log — never log all request headers (may contain tokens)
- Limit request body size (`MaxRequestBodySize`) on Kestrel/IIS
- Rate limiting (.NET 7+) on authentication and sensitive endpoints
- Debug endpoints (`/swagger`, `/health-details`) disabled or restricted in production
- `UseExceptionHandler()` in non-Development environments
- NEVER `TypeNameHandling.All` or `TypeNameHandling.Auto` in JSON deserialization
- SSRF: whitelist allowed domains when URLs come from user input
- Path traversal: validate file paths with `Path.GetFileName()`
- Security headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Strict-Transport-Security`


## Exceptions

- `GatewayException` extends `BusinessException` (from `Pulse.ExceptionMiddleware`)
- Has `StatusCode`, `Code` (e.g. `GTW001`), `Message`
- Error codes/messages defined in `Exceptions/Errors.cs` (French messages)
- `GatewayExceptionMiddleware` registered as `PreErrorResponderMiddleware` in Ocelot pipeline
- Use `Pulse.ExceptionMiddleware` for centralized error handling
- Controllers MAY do try/catch (Gateway-specific override)
- Use typed exceptions with HTTP code — never `throw new Exception("...")`
- `throw;` to re-raise, never `throw ex;` (CA2200)
- No empty catch blocks — always log or re-throw
- `catch (Exception)` only at outermost boundary (middleware, handlers)
- Prefer specific `catch` with `when` clause over generic `catch (Exception)`
- `Try*` prefixed methods MUST return `bool` (with `out` for result) or `(bool Success, T Value)` tuple for async


## DateTime

- Never `DateTime.Now` — always UTC. In services, inject `TimeProvider` for testability; elsewhere use `DateTime.UtcNow`


## Event-Driven

- Event handlers MUST be idempotent (check existence before AddAsync)
- Never `catch (Exception) { return; }` in a Service Bus handler (= data loss)
- Naming: `<Entity><Action>Event` (e.g. `AccountCreatedEvent`)
- Never remove a field from an existing event schema (breaking change)
- Distinguish domain events (in-process, same transaction) from integration events (cross-service, Service Bus)
- Dead letter queue MUST be monitored
- Recommended event structure: `EventId` (Guid), `EventType`, `OccurredAt` (UTC), `CorrelationId`
- Bounded retry on consumers: configure `MaxDeliveryCount`
- Topic naming: kebab-case `{entity}-{action}` (e.g. `account-created`), subscription naming: `{consumer}-{topic}`


## Tests

- Naming: `MethodName_WhenCondition_ShouldExpectedResult`
- Pattern: Arrange-Act-Assert
- One test = one behavior
- Use `await` in async tests (not `.Result`)
- Frameworks: xUnit, Moq, FluentAssertions, AutoFixture
- Integration tests: `WebApplicationFactory` + Testcontainers + Respawn
- `IAsyncLifetime` for async setup/teardown in xUnit
- Deterministic tests: use `FakeTimeProvider` instead of `DateTime.UtcNow` for time-dependent logic
- Don't mock what you don't own — mock your own interfaces, not framework types

### DelegatingHandler testing

- Use `FakeInnerHandler` to mock the downstream response without real HTTP calls
- Use `Testable*` wrappers (e.g. `TestableContactHandler`) to expose protected `SendAsync` for testing
- Test both pre-processing (request modification) and post-processing (response handling)
- Test handler behavior when headers/claims are missing (global handlers must be resilient)

### Controller testing

- Mock services, not downstream HTTP clients
- Test error mapping (GatewayException → appropriate status code)

### Middleware testing

- Test pipeline integration: verify middleware calls `next()` or short-circuits
- Test `TokenRevocationMiddleware` with revoked and valid tokens


## Commits

- Convention: Angular / Conventional Commits
- Format: `<type>(<scope>): <subject>`
- Types: feat, fix, refactor, perf, test, docs, style, build, ci, chore, revert
- Scope: feature within the service (e.g. `booking`, `contact`, `authorization`, `ocelot`) — never a ticket number, never the API name
- Subject in imperative present tense, no initial capital, no trailing period
- Breaking changes: `!` after type (e.g. `feat(ocelot)!: remove legacy route`) or `BREAKING CHANGE:` footer
- Body (optional) explains the "why", not the "what" — the diff shows the what


## Methods & SOLID

- Maximum 4 parameters per method — beyond that, group into request/command object
- Maximum 4 constructor dependencies — beyond that, extract facade or service
- Keep method signatures concise and readable
- Avoid multiple boolean parameters — use explicit methods or enum when ambiguous. Simple optional flags are acceptable (`bool includeDeleted = false`)
- A service with too many dependencies or too many lines likely violates SRP — extract a dedicated class
- Magic values → named constants or enums — never hardcode strings or numbers with business meaning
- Thread safety in singletons: use `ConcurrentDictionary` not `Dictionary`, `Interlocked` not `++`
- DelegatingHandlers are transient by default in Ocelot — do not store request-scoped state in fields


## DI

- Registration in `Extensions/*ServiceExtensions.cs` — never in `Program.cs` directly
- Scoped for business services
- Singleton for configuration and caches
- NEVER inject Scoped into Singleton (captive dependency)
- `ValidateScopes = true` in Development
- No `BuildServiceProvider()` in extension methods
- Global handlers: `AddOcelotGlobalDelegatingHandler<T>()`
- Per-route handlers: `AddOcelotDelegatingHandler<T>()`
- ISP split: register concrete class once, forward each sub-interface via `sp.GetRequiredService<TImpl>()`
- Use `IServiceScopeFactory` when a Singleton needs to consume Scoped services
- Keyed Services (.NET 8+) for multiple implementations of the same interface
- `IHttpContextAccessor` abstracted behind `IAuthenticationContext` interface in services
- Use `IOptions<T>` / `IOptionsMonitor<T>` for configuration — never `Environment.GetEnvironmentVariable()`
- `ValidateOnStart()` on options registration for fail-fast configuration validation


## Performance

- `StringBuilder` for string concatenation in loops — never `+=` in a loop
- Materialize `IEnumerable` (`ToList()`) before multiple enumeration
- Avoid double-check existence patterns — use a single query or upsert
- Minimize allocations on hot paths: prefer `Span<T>`, `ArrayPool`, stackalloc when appropriate
- DelegatingHandlers are hot path — minimize allocations (avoid LINQ on every request, prefer `Span<T>` for header parsing)
- Cache expensive computations (permission lookups, JWT parsing) — use `IMemoryCache` with appropriate TTL
