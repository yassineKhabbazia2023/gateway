# Conventions Backend Pulse — GitHub Copilot Instructions (Gateway)
> Ce fichier est automatiquement injecté dans chaque conversation Copilot Chat.
> Les conventions partagées sont dans [`/agent.md`](../agent.md).
> Dernière mise à jour : 2026-06-09
## Conventions partagées
👉 **Voir [`/agent.md`](../agent.md)** pour toutes les conventions communes (architecture, nommage, API REST, qualité code, EF Core, sécurité, tests, Git, etc.)
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
