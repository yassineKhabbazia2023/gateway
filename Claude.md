# Pulse Backend — Development Conventions (Gateway)

> Ce fichier est lu par Claude Code / Claude AI.
> Les conventions partagées sont dans [`/agent.md`](./agent.md).

## Conventions partagées

👉 **Voir [`/agent.md`](./agent.md)** pour toutes les conventions communes (architecture, nommage, API REST, qualité code, EF Core, sécurité, tests, Git, etc.)

---

## Spécificités Gateway

### Exception boundaries — NON APPLICABLE

La section "Exception boundaries" de `agent.md` (Controller does NOT do `try/catch`) **ne s'applique PAS à la Gateway**.
La Gateway (Ocelot) a son propre mécanisme de gestion d'erreurs et de middlewares de proxy.

### Rôle de la Gateway

- Point d'entrée unique (API Gateway Ocelot)
- Validation JWT et injection de headers
- Routage vers les microservices downstream
- Rate limiting, CORS, health checks agrégés
