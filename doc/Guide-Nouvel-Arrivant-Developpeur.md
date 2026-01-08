# 🚀 Livret d'Accueil Développeur - RYDGE

| | |
|---|---|
| ✍️ **Auteur** | *Arab AIT OUARABI* |
| 🩹 **Relecture et corrections** | Clément FORY |
| 📅 **Dernière mise à jour** | 23/12/2025 |

---

## 📑 Sommaire

| Section | Contenu |
|---------|---------|
| 1️⃣ [👋 Bienvenue chez RYDGE](#-11-bienvenue-chez-rydge) | Présentation de l'équipe |
| 2️⃣ [💡 La plateforme RYDGE](#-12-quest-ce-que-la-plateforme-rydge-) | Fonctionnalités, utilisateurs, applications |
| 3️⃣ [🏗️ Organisation & Méthodologie](#%EF%B8%8F-13-organisation-des-équipes) | Équipes, Scrum, rituels, Azure DevOps |
| 4️⃣ [📞 Contacts & Référents](#-15-contacts-clés-et-référents) | Référents techniques et fonctionnels |
| 5️⃣ [📦 Pré-boarding – Avant ton arrivée](#-partie-2---avant-ton-arrivée-pré-boarding) | Accès, logiciels, documentation |
| 6️⃣ [📅 Ta première semaine](#-partie-3---ta-première-semaine) | Checklists jour par jour |
| 7️⃣ [🏗️ Comprendre l'architecture RYDGE](#%EF%B8%8F-partie-4---comprendre-larchitecture) | Microservices, events, authentification |
| 8️⃣ [💼 Travailler au quotidien](#-partie-5---travailler-au-quotidien) | Accès, Git, déploiement |
| 9️⃣ [📚 Ressources & FAQ](#-partie-6---ressources) | Liens utiles, questions fréquentes |

---

# 🏠 PARTIE 1 - BIENVENUE

## 👋 1.1 Bienvenue chez RYDGE

Bienvenue dans l'équipe ! Ce guide t'accompagnera dans ta prise de poste et te donnera toutes les clés pour être rapidement opérationnel.

## 💡 1.2 Qu'est-ce que la plateforme Rydge ?

**La plateforme Rydge** est une solution de **collaboration** entre les **collaborateurs** (experts-comptables) et leurs **clients** (TPE/PME).

### ⭐ Fonctionnalités principales

| Fonctionnalité | Description |
|----------------|-------------|
| 📁 **GED** | Gestion Électronique des Documents (dépôt, consultation, partage) |
| 🎁 **Offres** | Souscription et gestion des offres de services |
| 📊 **Reporting** | Visualisation des chiffres de l'entreprise (tableaux de bord, indicateurs) |
| 📝 **Mandats** | Création et gestion des mandats clients |
| 📅 **Prise de RDV** | Planification de rendez-vous avec un collaborateur *(en cours de développement)* |
| 🔗 **Intégrations** | Connexion avec des outils comptables (Pennylane, Silae, etc.) |

### 👥 Les utilisateurs de l'application

| Type | Description | Authentification |
|------|-------------|------------------|
| 🏢 **Client** | TPE/PME (Small-Medium Business) | **Gigya** |
| 👔 **Collaborateur** | Employé du cabinet comptable | **Azure AD / SSO** (EntraID) |
| ⚙️ **Admin** | Membre de l'équipe Rydge | **Azure AD / SSO** (EntraID) |

### 📱 Applications front-end

- 🖥️ **Desktop App** (VueJS) : Application web principale
- 📱 **Mobile App** (MAUI) : Application iOS/Android
- ⚙️ **Admin Tools** (VueJS) : Backoffice d'administration

---

## 🏗️ 1.3 Organisation des équipes

### 📊 Organigramme

```
                         👑 CPO
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
   🔧 Head of Tech   📦 Head of Product   🎨 Head of Design
         │                 │                 │
         ▼                 ▼                 ▼
   Tech Leads (2)    PM Delivery        Designers
         │                 │
         ▼                 ▼
   ┌─────────────┐   PO/SM (x2)
   │ SQUAD BUILD │
   │  1    2     │
   └─────────────┘

   ┌─────────────┐   ┌─────────────────┐
   │  SQUAD RUN  │   │   TRANSVERSES   │
   │     3       │   │ 3 PM + 1 TL + IA│
   └─────────────┘   └─────────────────┘
```

### 📋 Résumé de l'organisation

| Équipe | Composition | Rôle |
|--------|-------------|------|
| 🔨 **Squad 1 & 2** (Build) | Tech Lead + Devs + DevOps + QA + PO/SM | Développement de nouvelles fonctionnalités |
| 🔧 **Squad 3** (Run) | Devs + DevOps + QA | Maintenance, incidents, support technique |
| 🎨 **Design** | Designers | UX/UI de la plateforme |
| 🔄 **Transverses** | 3 PM + 1 Tech Lead + 1 Spécialiste IA | Support et coordination inter-squads |

> 💡 **Note** : Les PO des squads sont également **Scrum Masters** (leur spécialisation initiale). Le **PM Delivery** prépare le travail en amont et le dispatche aux PO/SM des deux squads.

### 📈 Chaîne hiérarchique

| Rôle | Reporte à |
|------|-----------|
| 👨‍💻 Développeurs, DevOps, QA | Tech Lead de la squad |
| 🔧 Tech Leads | Head of Tech |
| 📋 PO/Scrum Master de squad | Head of Product |
| 📦 PM Delivery | Head of Product |
| 🎨 Designers | Head of Design |
| 👔 Heads (Tech, Product, Design) | **CPO** (Chief Product Officer) |

---

## 🔄 1.4 Méthodologie Agile : Scrum

Nous travaillons en **Scrum** avec des **sprints de 2 semaines**. Les boards et le backlog sont gérés sur **Azure DevOps**.

### 📅 Rituels obligatoires

| Rituel | Fréquence | Description |
|--------|-----------|-------------|
| ☀️ **Daily** | Tous les jours | Point d'équipe quotidien (~15 min) |
| 📋 **Sprint Planning** | 1x / 2 semaines | Planification du sprint à venir |
| 🔍 **Refinement** | 1x / 2 semaines | Affinage des user stories du backlog |
| 🔄 **Rétrospective** | 1x / 2 semaines | Bilan du sprint écoulé et axes d'amélioration |

### 🎫 Types de Work Items

| Type | Description | Quand l'utiliser |
|------|-------------|------------------|
| 📖 **User Story (US)** | Fonctionnalité décrite du point de vue utilisateur | Nouvelle feature métier |
| 🐛 **Bug** | Anomalie identifiée sur une fonctionnalité existante | Comportement inattendu en REC/PROD |
| 🚨 **Incident** | Problème remonté par le support ou les utilisateurs | Ticket de support nécessitant un fix |
| ⚙️ **Enabler Tech** | Tâche technique sans valeur métier directe | Refactoring, dette technique, infra, CI/CD |

### ✅ Definition of Ready (DoR)

Une carte est **prête à être traitée** (statut "Ready") si :
- [ ] La description est claire et complète
- [ ] Les critères d'acceptation sont définis
- [ ] Les dépendances sont identifiées
- [ ] L'estimation (story points) est faite
- [ ] Les maquettes/specs sont disponibles (si applicable)

### 🔄 Cycle de vie d'un Work Item

```
🆕 New → ✅ Ready → 🔨 Active → 👀 Ready for Check → ✔️ Resolved
                                                          │
🏁 Realised ← ✅ Validated ← 🧪 Ready for Test ◄──────────┘
```

| Statut | Qui | Action |
|--------|-----|--------|
| 🆕 **New** | PO | Carte créée, en attente de raffinement |
| ✅ **Ready** | PO | Carte prête à être prise (DoR validée) |
| 🔨 **Active** | Dev | En cours de développement |
| 👀 **Ready for Check** | Dev | Code terminé, PR créée, en attente de review |
| ✔️ **Resolved** | Dev | PR mergée, prêt pour les tests |
| 🧪 **Ready for Test** | QA | En cours de test |
| ✅ **Validated** | QA | Tests passés, prêt pour déploiement |
| 🏁 **Realised** | - | Déployé en production |

### 💡 Bonnes pratiques Azure DevOps

| Action | Pourquoi |
|--------|----------|
| 🔨 **Passer la carte en "Active"** | Indique que tu travailles dessus (visible au Daily) |
| ⏱️ **Mettre à jour le Remaining** | Permet de suivre l'avancement et détecter les blocages |
| 🔗 **Lier le Work Item à la PR** | Traçabilité : la PR référence automatiquement le ticket |
| ✂️ **Découper les US volumineuses** | Une US = 1-3 jours max. Si plus gros → découper en sous-tâches |

### 🔗 Lier un Work Item à une Pull Request

Dans Azure DevOps, lors de la création de la PR :
1. Section **"Work Items"** → Ajouter le numéro du ticket (ex: `#12345`)
2. Ou dans le message de commit : `feat(controller): ajout endpoint #12345`

> 💡 Le Work Item passera automatiquement en "Ready for Check" quand la PR est créée.

### 📏 Bonnes pratiques de découpage

| Taille | Recommandation |
|--------|----------------|
| 🟢 **XS** (1-2h) | Parfait, peut être fait rapidement |
| 🟢 **S** (0.5-1 jour) | Idéal pour une US |
| 🟡 **M** (1-2 jours) | Acceptable, mais envisager de découper |
| 🔴 **L** (3-5 jours) | Trop gros → **découper obligatoirement** |
| ⛔ **XL** (> 5 jours) | Epic → créer plusieurs US |

**Critères de découpage** :
- ✅ Une US = une seule fonctionnalité testable
- ✅ Préférer des US verticales (de l'UI à la DB) plutôt qu'horizontales (juste le back)
- ✅ Chaque US doit apporter de la valeur métier (sauf Enabler Tech)

---

## 📞 1.5 Contacts clés et référents

### 🔧 Référents par domaine

| Domaine | 👨‍💻 Référent Technique | 📋 Référent Fonctionnel |
|---------|-------------------|---------------------|
| Account | Arab AIT OUARABI | Suliman Lescot |
| Contact | Kevin POZDEREK | Clement PRATI |
| Authorization | Arab AIT OUARABI | Suliman LESCOT |
| Offer | Houssem DBIRA | Alex VIGREUX |
| Reporting | Arab AIT OUARABI | Suliman LESCOT |
| Delegation | Arab AIT OUARABI | Ouassim HAYANI |
| Registry | Oussama BEN AMOR | Ouassim HAYANI |
| GED | Kevin POZDEREK | Clement PRATI |
| Pennylane | Houssem DBIRA | Alex VIGREUX |
| PIA | Houssem DBIRA | Alex VIGREUX |
| Je Declare | Kevin POZDEREK | Clement PRATI |
| Docaposte | Kevin POZDEREK | Clement PRATI |
| Loop | Houssem DBIRA | Clement PRATI |
| Silae | Houssem DBIRA | Clement PRATI |
| Gigya | Riantsoa RAMANAMPAMONJY | NA |
| Akuiteo | ? | Ouassim HAYANI |
| Ventya | ? | Suliman Lescot |
| Gateway | Houssem DBIRA | NA |

### 🔄 Référents transverses

| Rôle | Contact |
|------|---------|
| 📋 **Scrum & Méthodologie** | Joan FAYARD \| Mustapha FATMI |
| ⚙️ **DevOps** | Saliou DIOUM \| Fory Clément |

### 🏢 Logistique et administratif

| Sujet | Contact |
|-------|---------|
| 🎫 Badge, accès locaux, parking, notes de frais | **CAMPISCIANO CORINNE** |
| 🏖️ Absences, télétravail, signature CRA | **Votre Lead Tech** |

### 🆘 Support technique

📞 **Créer un ticket de support** : [ici](https://conseilescgs.service-now.com/esc?id=ec_home)

☎️ **Numéro de support** : *01 57 98 30 30*

---

# 📦 PARTIE 2 - AVANT TON ARRIVÉE (Pré-boarding)

## 🔑 2.1 Accès à demander

Avant ton premier jour, assure-toi d'avoir demandé :

| Accès | URL | Type d'authentification |
|-------|-----|------------------------|
| 🔵 **Azure DevOps** | https://dev.azure.com/FR-TPEME/Pulse | Email + Password |
| ☁️ **Portail Azure** | https://portal.azure.com | Compte **ADM** |
| 🖥️ **VM de rebond** | Via Portail Azure | Credentials à demander au Lead Tech |

## 💻 2.2 Matériel et logiciels à installer

- [ ] 🔧 Visual Studio 2022 ou VS Code
- [ ] 📦 .NET 8 SDK
- [ ] 🗄️ SQL Server Management Studio
- [ ] 🔀 Git
- [ ] 🐳 Docker WSL (optionnel)

## 📚 2.3 Documentation à lire

| Document | Lien |
|----------|------|
| 📖 Ce guide | Tu y es ! |
| 🏗️ C4 Model (Architecture) | https://dev.azure.com/FR-TPEME/Pulse/_git/Pulse.Architecture.C4Model |
| 📋 ADR (Décisions d'architecture) | https://dev.azure.com/FR-TPEME/Pulse/_git/Pulse.Architecture.ADR |
| 📚 Wiki Azure DevOps | https://dev.azure.com/FR-TPEME/Pulse/_wiki/wikis/Pulse.wiki/7504/RYDGE-Pulse-product |
| 🔗 Documentation technique Loop | [Lien Loop](https://conseilescgs.sharepoint.com/:fl:/r/contentstorage/CSP_f91d2064-554e-46b4-ae52-b6590e35c9f0/Biblioth%C3%A8que%20de%20documents/LoopAppData/%F0%9F%92%BB%203.%20Conventions%20%26%20Standards%20de%20Code.loop) |

---

# 📅 PARTIE 3 - TA PREMIÈRE SEMAINE

## 📆 3.1 Jour 1 : Installation et accès

### ✅ Checklist Jour 1

- [ ] 💻 Récupérer ton matériel (PC, badge)
- [ ] 🔵 Te connecter à Azure DevOps
- [ ] ☁️ Te connecter au Portail Azure avec ton compte ADM
- [ ] 📥 Cloner les repos principaux
- [ ] 👋 Rencontrer ton Lead Tech et ton équipe

### 📥 Cloner les repos

```bash
# Exemple pour cloner un repo
git clone https://FR-TPEME@dev.azure.com/FR-TPEME/Pulse/_git/Pulse.Back.Account
```

## 📆 3.2 Jour 2-3 : Découverte de l'architecture

### ✅ Checklist Jour 2-3

- [ ] 📖 Lire ce guide en entier
- [ ] 🏗️ Explorer le C4 Model
- [ ] 🖥️ Se connecter à la VM de rebond (ITG)
- [ ] 📊 Accéder aux logs Application Insights
- [ ] 🔍 Explorer un Swagger d'API

## 📆 3.3 Jour 4-5 : Premier pas dans le code

### ✅ Checklist Jour 4-5

- [ ] 🔨 Builder une API en local
- [ ] 🧪 Lancer les tests unitaires
- [ ] 🏗️ Comprendre la structure Clean Architecture
- [ ] 🌿 Créer ta première branche Git
- [ ] 📝 Faire ta première PR

## 🌐 3.4 URLs des environnements

| Env | Usage | URL Client | URL Collaborateur |
|-----|-------|------------|-------------------|
| 🟡 **ITG** | Intégration | https://app-itg01.itg.pulse.rydge.fr/login | https://app-itg01.itg.pulse.rydge.fr/collaborateur |
| 🟠 **REC** | Recette | https://app-rec01.rec.pulse.rydge.fr/login | https://app-rec01.rec.pulse.rydge.fr/collaborateur |
| 🟢 **PROD** | Production | https://app.rydge.fr/login | https://app.rydge.fr/collaborateur |

---

# 🏗️ PARTIE 4 - COMPRENDRE L'ARCHITECTURE

## 🔭 4.1 Vue d'ensemble

```
👥 UTILISATEURS
   Client (Gigya) │ Collaborateur (SSO) │ Admin (SSO)
                           │
                           ▼
📱 APPLICATIONS FRONT-END
   Desktop App │ Mobile App │ Admin Tools
                           │ HTTPS
                           ▼
🔒 REVERSE PROXY (Nginx)
   Point d'entrée unique - exposé vers Internet
                           │
                           ▼
🚪 API GATEWAY (Ocelot)
   Authentification + Autorisation + Routing vers les APIs
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
   Account API      Contact API       Offer API
         └─────────────────┼─────────────────┘
                           ▼
📨 AZURE SERVICE BUS
   Communication asynchrone entre microservices
```

### 🎯 Principes clés

| Principe | Description |
|----------|-------------|
| 🧩 **Microservices** | Chaque domaine métier = 1 service autonome avec sa propre base de données |
| 📨 **Event-Driven** | Les services communiquent via des événements (Azure Service Bus) |
| 🚪 **Gateway unique** | Seule la Gateway est exposée vers Internet |
| 🏛️ **Clean Architecture** | Chaque API suit le pattern API → Application → Infrastructure |

## 🖥️ 4.2 Architecture Front-End

Le Front-End est le point d'entrée de la plateforme. Il se compose de **deux applications principales** partageant une base de code commune, ainsi qu'un **Back-Office technique**.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    🖥️ APPLICATIONS FRONT-END                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐       │
│   │  🏢 CUSTOMER    │   │ 👔 COLLABORATOR │   │ ⚙️ BACK-OFFICE  │       │
│   │   (Clients)     │   │ (Collaborateurs)│   │  (Équipe Pulse) │       │
│   │                 │   │                 │   │                 │       │
│   │  Auth: Gigya    │   │  Auth: MSAL/SSO │   │ Auth: MSAL ADM  │       │
│   └────────┬────────┘   └────────┬────────┘   └────────┬────────┘       │
│            │                     │                     │                 │
│            └──────────┬──────────┘                     │                 │
│                       ▼                                │                 │
│            ┌─────────────────────┐                     │                 │
│            │   📦 CODE PARTAGÉ   │                     │                 │
│            │  (Applets + Libs)   │◄────────────────────┘                 │
│            └─────────────────────┘                                       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 📱 Les deux applications principales

| Application | Utilisateurs | Authentification | Fonctionnalités |
|-------------|--------------|------------------|-----------------|
| 🏢 **Customer** | Clients des cabinets (TPE/PME) | **Gigya** | Consultation documents, actualités, connexion écosystème RYDGE (Pennylane, Ventya) |
| 👔 **Collaborator** | Collaborateurs des cabinets | **MSAL** (SSO Microsoft) | Gestion dossiers clients, accès outils métier |

### 🧩 Architecture modulaire : Applets

Les fonctionnalités métier sont découpées en **applets** : des micro-packages indépendants intégrables dans les applications.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           🧩 APPLETS                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐      │
│   │ 📁 GED  │  │📝Mandats│  │ 🎁Offres│  │📊Report.│  │   ...   │      │
│   └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘      │
│        │            │            │            │            │            │
│        └────────────┴─────┬──────┴────────────┴────────────┘            │
│                           ▼                                              │
│              ┌─────────────────────────┐                                │
│              │  Customer / Collaborator │                                │
│              └─────────────────────────┘                                │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

| Avantage | Description |
|----------|-------------|
| 🎯 **Séparation des responsabilités** | Chaque applet gère un domaine fonctionnel spécifique |
| 👥 **Développement parallèle** | Plusieurs équipes peuvent travailler simultanément |
| ♻️ **Réutilisation du code** | Les applets sont partagées entre Customer et Collaborator |

### 📚 Libraries (Code partagé)

| Library | Contenu | Usage |
|---------|---------|-------|
| 📦 **common** | Services, stores, outils | Utilisable par tout le monorepo |
| 🔗 **shared** | Code propre à l'applicatif | Spécifique aux apps Customer/Collaborator |
| 🔧 **toolbox** | Utilitaires fondamentaux | Logs, feature flags |
| 🎨 **styleguide** | Composants UI | Design System |
| 🧱 **widgets** | Système de widgets | Widgets configurables |

### ⚙️ Le Back-Office

Le **Back-Office** (`@pulse/backoffice`) est une application interne réservée à l'équipe Pulse.

| Aspect | Description |
|--------|-------------|
| 🎯 **Objectif** | Gérer les variables d'environnement (app settings) |
| 🌐 **Environnements** | ITG, REC, PRD |
| 🔐 **Authentification** | MSAL en mode ADM-CLD |
| 🔒 **Accès** | Équipe technique Pulse uniquement (pas les collaborateurs des cabinets) |

## 🧩 4.3 Les microservices

| Microservice | Rôle |
|--------------|------|
| 🏢 **Account** | Gestion des entreprises clientes, rôles et délégations |
| 👤 **Contact** | Gestion des utilisateurs (clients et collaborateurs) |
| 🔐 **Authorization** | Gestion des permissions et autorisations |
| 🎁 **Offer** | Catalogue d'offres et gestion des abonnements |
| 📋 **Registry** | Source de vérité - synchronisation avec Akuiteo via MuleSoft |
| 📁 **GED** | Gestion Électronique des Documents |
| 💰 **Pennylane** | Intégration avec l'outil de comptabilité Pennylane |
| 📝 **Mandate** | Gestion des mandats (intégration JeDeclare) |
| 📊 **Reporting** | Statistiques et rapports (Power BI) |
| 📜 **History** | Historique des actions utilisateurs |
| 📰 **Feedcenter** | Fil d'actualité dans l'application |
| 📧 **Notification** | Envoi d'emails et notifications |

## 🔄 4.4 Flux de données : Akuiteo → Registry → Rydge

### 📊 Origine des données

```
🗄️ AKUITEO ──► 🔄 MuleSoft ──► 📋 REGISTRY ──► 📨 Events ──► 🏢 RYDGE
   (CRM)                          (API)                      (Account, Contact, Role)
```

| Type de donnée | Origine | Flux |
|----------------|---------|------|
| 🏢 **Accounts** (entreprises) | Akuiteo | akuiteo → MuleSoft → Registry → Account API |
| 👤 **Contacts** | Akuiteo | Akuiteo → MuleSoft → Registry → Contact API |
| 🔗 **Roles** | Akuiteo | Akuiteo → MuleSoft → Registry → Account API |

> 💡 **Note** : Côté client, seuls les **signataires** proviennent d'Akuiteo. Les autres contacts clients sont créés directement depuis la plateforme Rydge.

### ✍️ Qu'est-ce qu'un signataire ?

Le **signataire** est le représentant légal de l'entreprise cliente (généralement l'administrateur). C'est lui qui :
- ✍️ Signe le contrat avec RYDGE
- 🗣️ Représente l'entité et parle au nom du client
- 📥 Est importé automatiquement depuis Akuiteo

## 🔐 4.5 Rôles vs Autorisations

| Concept | API | Description |
|---------|-----|-------------|
| 🔗 **Role** | Account API | Liaison entre un Contact et un Account ("Qui travaille sur quel compte") |
| 🔐 **Authorization** | Authorization API | Permissions fines ("Quelles actions sont permises") |

### 👥 Permissions : Collaborateur vs Client

| Type | Règle |
|------|-------|
| 👔 **COLLABORATEUR** | Permissions **IDENTIQUES** quel que soit l'account (mêmes droits sur tous les comptes auxquels il a accès) |
| 🏢 **CLIENT** | Permissions **DIFFÉRENTES** selon l'account (droits spécifiques par entreprise) |

## 🔑 4.6 Authentification

| Type | Système | Token |
|------|---------|-------|
| 👔 **COLLABORATEURS** (Employés du cabinet) | Azure AD / EntraID (SSO) | JWT - ContactType = 0 |
| 🏢 **CLIENTS** (TPE/PME) | GIGYA (MyPulse) | JWT - ContactType = 1 |

## 📨 4.7 Communication Event-Driven (Service Bus)

Les microservices ne s'appellent **pas directement**. Ils communiquent via des **événements** sur Azure Service Bus.

```
📤 Contact API ──► ContactCreatedEvent ──► 📨 SERVICE BUS ──► 📥 Account API
                        Topic: contact                    Met à jour sa base
                                                          locale (State Data)
```

**Avantages** :
- 🔓 **Découplage** : Les services ne se connaissent pas
- 💪 **Résilience** : Si un service tombe, les autres continuent
- 📈 **Scalabilité** : Chaque service évolue indépendamment

## 📖 4.8 Glossaire technique

| Terme | Définition |
|-------|------------|
| 🏢 **Account** | Entreprise cliente |
| 👤 **Contact** | Utilisateur (client ou collaborateur) |
| 🔗 **Role** | Liaison Contact ↔ Account |
| ✍️ **Signataire** | Représentant légal d'une entreprise cliente |
| 📦 **State Data** | Copie locale des données d'un autre microservice |
| 📢 **Topic** | Canal de publication d'événements Service Bus |
| 📬 **Subscription** | Abonnement à un topic Service Bus |
| ☠️ **DLQ** | Dead Letter Queue - file des messages en erreur |

---

# 💼 PARTIE 5 - TRAVAILLER AU QUOTIDIEN

## 🌐 5.1 Accès réseau

> ⚠️ **Important** : Vous ne pouvez **PAS** accéder aux ressources Azure depuis votre poste local.

| Ressource | Accès depuis | Prérequis |
|-----------|--------------|-----------|
| 🚪 **Swagger Gateway** | Internet | - |
| 🔍 **Swagger APIs** | VM de rebond | Compte ADM + VM |
| 📊 **Application Insights (Logs)** | Portail Azure | Compte ADM |
| 🗄️ **Bases de données SQL** | VM de rebond | Compte ADM + VM |
| 📨 **Service Bus** | VM de rebond | Compte ADM + VM |
| ⚙️ **App Configuration** | VM de rebond | Compte ADM + VM |
| 🔐 **Key Vault** | VM de rebond | Compte ADM + VM |
| 🔧 **Kudu (diagnostic API)** | VM de rebond | Compte ADM + VM |

### 🖥️ Connexion à la VM de rebond

1. 🔑 Se connecter au **Portail Azure** avec le compte **ADM**
2. 🔍 Chercher dans la liste des ressources `vmsscegpulsespkrec01` (exemple pour la REC)
3. 📋 Va dans "Instances" et sélectionne l'instance de VM, exemple: `vmsscegpulsespkrec01_2`
4. 🖥️ **Connect** → RDP

> ⚠️ **Important** : Chaque environnement a ses propres VM. Impossible d'accéder aux ressources d'un autre environnement.

## 🔍 5.2 Swagger et URLs des APIs

### 🌐 Swagger Gateway (accessible depuis internet)

```
🟢 PROD : https://api.rydge.fr/desktop/api/index.html
```

### 🔐 Swagger des APIs (via VM uniquement)

**Composition de l'URL** :

```
https://appcegpulserptrec0101.azurewebsites.net/api/index.html
        └────────┘└─┘└─┘└──┘
            │      │   │   │
            │      │   │   └── Numéro d'instance (01, 02...)
            │      │   └────── Environnement (itg, rec, prd)
            │      └────────── Trigramme du domaine
            └───────────────── Préfixe commun
```

### 📋 Trigrammes par domaine

| Domaine | Trigramme | Exemple URL (REC) |
|---------|-----------|-------------------|
| 🏢 Account | `acc` | `appcegpulseaccrec01...` |
| 👤 Contact | `cnt` | `appcegpulsecntrec01...` |
| 🔐 Authorization | `aut` | `appcegpulseautrec01...` |
| 🎁 Offer | `off` | `appcegpulseoffrec01...` |
| 📊 Reporting | `rpt` | `appcegpulserptrec01...` |
| 📋 Registry | `reg` | `appcegpulseregrec01...` |
| 📁 GED | `ged` | `appcegpulsegedrec01...` |
| 💰 Pennylane | `pnl` | `appcegpulsepnlrec01...` |
| 🚪 Gateway | `gtw` | `appcegpulsegtwrec01...` |

---

## 🔀 5.3 Git et GitHub Flow

### 🌿 Stratégie de branches : GitHub Flow

Nous utilisons **GitHub Flow**, une stratégie simple et efficace :

```
main (production)
  │
  ├── 🌿 feature/123-ma-fonctionnalite     ◄── Créer une branche depuis main
  │         │
  │         ├── 📝 commit 1
  │         ├── 📝 commit 2
  │         └── 📝 commit 3
  │                │
  │                ▼
  │         📋 Pull Request (PR)            ◄── Ouvrir une PR vers main
  │                │
  │                ▼
  │         👀 Code Review                  ◄── Review par un pair
  │                │
  │                ▼
  └─────────── ✅ Merge ──────────────────► main
```

### 💻 Workflow Git quotidien

```bash
# 1. Se mettre à jour sur main
git checkout main
git pull origin main

# 2. Créer une branche de feature
git checkout -b feature/12345-description-courte

# 3. Travailler et commiter
git add .
git commit -m "feat: description du changement"

# 4. Pousser la branche
git push origin feature/12345-description-courte

# 5. Créer une Pull Request sur Azure DevOps

# 6. Après merge, supprimer la branche locale
git checkout main
git pull origin main
git branch -d feature/12345-description-courte
```

### 📝 Convention de nommage des branches

```
{type}/{numero-ticket}-{description-courte}
```

| Type | Usage |
|------|-------|
| ✨ `feature/` | Nouvelle fonctionnalité |
| 🐛 `fix/` | Correction de bug |
| 🚨 `hotfix/` | Correction urgente en production |
| ♻️ `refactor/` | Refactoring sans changement fonctionnel |

**Exemples** :
- `feature/12345-ajout-endpoint-contact`
- `fix/12346-correction-null-reference`
- `hotfix/12347-fix-login-prod`

### 📝 Convention de messages de commit (Angular)

Nous utilisons la **convention Angular** pour les messages de commit :

```
<type>(<scope>): <subject>

<body>

<footer>
```

| Élément | Description |
|---------|-------------|
| `type` | Type de changement (obligatoire) |
| `scope` | Périmètre impacté (obligatoire) |
| `subject` | Description courte en impératif (obligatoire) |
| `body` | Description détaillée (optionnel) |
| `footer` | Breaking changes, références tickets (optionnel) |

### 🏷️ Types disponibles

| Type | Usage |
|------|-------|
| ✨ `feat` | Nouvelle fonctionnalité |
| 🐛 `fix` | Correction de bug |
| 📚 `docs` | Documentation uniquement |
| 💄 `style` | Formatage, espaces, point-virgules (pas de changement de code) |
| ♻️ `refactor` | Refactoring (ni feat, ni fix) |
| ⚡ `perf` | Amélioration des performances |
| 🧪 `test` | Ajout ou modification de tests |
| 📦 `build` | Changements du système de build (npm, NuGet, etc.) |
| 👷 `ci` | Configuration CI/CD (pipelines, scripts) |
| 🔧 `chore` | Autres changements (maintenance) |

### 🎯 Scopes disponibles

| Scope | Périmètre |
|-------|-----------|
| `controller` | Contrôleurs API |
| `service` | Services (couche Core) |
| `repository` | Repositories (couche Infrastructure) |
| `model` | Modèles métier |
| `entity` | Entités EF Core |
| `mapper` | Mappers (conversions Entity ↔ Model) |
| `event` | Event handlers et publishers |
| `config` | Configuration (appsettings, DI) |
| `middleware` | Middlewares HTTP |
| `test` | Tests unitaires / intégration |
| `pipeline` | Pipelines CI/CD |
| `db` | Scripts SQL, migrations |

**Exemples** :
- `feat(controller): ajout endpoint GET /contacts/{id}`
- `fix(service): correction NullReferenceException dans AccountService`
- `refactor(repository): extraction de la logique de requête`
- `feat(event): ajout handler ContactUpdatedEvent`
- `test(service): ajout tests unitaires OfferService`
- `ci(pipeline): ajout étape de déploiement REC`
- `fix(db): correction script migration V2`

### 📋 Pull Request (PR)

Toute modification passe par une **Pull Request** :

1. 📝 **Créer la PR** sur Azure DevOps
2. ✍️ **Remplir la description** (quoi, pourquoi, comment tester)
3. 👤 **Assigner un reviewer**
4. ⏳ **Attendre l'approbation** (minimum 2 reviewers)
5. ✅ **Merger** après validation

---

## 🚀 5.4 Déploiement (MEP)

```
🌿 Feature Branch ──► 📋 PR ──► 👀 Code Review ──► ✅ Merge main
                                                        │
                                                        ▼
                                              🔨 Build + Tests (CI)
                                                        │
                                                        ▼
                                              🟡 Deploy ITG (manuel)
                                                        │
                                                        ▼
                                              🟠 Deploy REC (manuel)
                                                        │
                                                        ▼
                                              ✅ Validation QA
                                                        │
                                                        ▼
                                              🟢 Deploy PROD (manuel)
```

**Points importants** :
- ⏱️ **Indisponibilité temporaire** : APIs indisponibles ~2-5 minutes lors du déploiement
- ↩️ **Rollback** : Possible via swap de slots ou feature flags
- 🗄️ **Base de données** : Migrations SQL déployées séparément

## ⚙️ 5.5 Configuration et secrets

| Source | Usage |
|--------|-------|
| 1️⃣ **Azure App Configuration** | Configuration centralisée (clés métier, feature flags) |
| 2️⃣ **Azure Key Vault** | Secrets (connection strings, API keys) |
| 3️⃣ **appsettings.json** | Configuration par défaut |

## 🔗 5.6 Intégrations externes

| Système | Rôle |
|---------|------|
| 💰 **Pennylane** | Comptabilité |
| 💵 **Silae** | Paie |
| 📄 **Loop / PIA** | OCR de documents |
| 🔒 **Docaposte** | Coffre-fort numérique |
| 🔄 **MuleSoft / Akuiteo** | Référentiel entreprises |
| 📝 **JeDeclare** | Mandats |
| 📊 **Power BI** | Reporting |
| 🔑 **Gigya** | Authentification clients |

---

# 📚 PARTIE 6 - RESSOURCES

## 🌐 6.1 Portails principaux

| Portail | URL | Authentification |
|---------|-----|------------------|
| 🔵 **Azure DevOps** | https://dev.azure.com/FR-TPEME/Pulse | Email + Password |
| ☁️ **Portail Azure** | https://portal.azure.com | Email *ADM* + Password |

## 📖 6.2 Documentation

| Documentation | Lien |
|---------------|------|
| 🏗️ **C4 Model** (Architecture) | https://dev.azure.com/FR-TPEME/Pulse/_git/Pulse.Architecture.C4Model |
| 📋 **ADR** (Décisions d'architecture) | https://dev.azure.com/FR-TPEME/Pulse/_git/Pulse.Architecture.ADR |
| 📚 **Wiki Azure DevOps** | https://dev.azure.com/FR-TPEME/Pulse/_wiki/wikis/Pulse.wiki/7504/RYDGE-Pulse-product |
| 📄 **Documentation par API** | Wiki ou fichier `readme` si non répertoire `/docs` de chaque API |
| 🔗 **Documentation technique Loop** | [Lien Loop](https://conseilescgs.sharepoint.com/:fl:/r/contentstorage/CSP_f91d2064-554e-46b4-ae52-b6590e35c9f0/Biblioth%C3%A8que%20de%20documents/LoopAppData/%F0%9F%92%BB%203.%20Conventions%20%26%20Standards%20de%20Code.loop) |

## ❓ 6.3 FAQ

**❓ Je n'arrive pas à me connecter à la VM de rebond**
> ✅ Vérifie que tu utilises ton compte ADM et que tu as les droits sur l'environnement et que ton VPN est activé.

**❓ Comment accéder aux logs d'une API ?**
> ✅ Via Application Insights sur le portail Azure (compte ADM).

**❓ Comment voir les messages en erreur sur le Service Bus ?**
> ✅ Via la VM de rebond : Service Bus → Topic → Subscription → Dead Letter Queue.

**❓ Qui contacter pour un problème de badge ?**
> ✅ CAMPISCIANO CORINNE.

**❓ Comment poser mes congés ?**
> ✅ Sur Shift, voir avec ton Lead Tech.

---

> 🆘 **Besoin d'aide ?** N'hésite pas à contacter ton Lead Tech ou les référents techniques de chaque domaine.
