# Tournoi Archipelago

Outil de saisie et de statistiques pour un petit tournoi amical d'Archipelago
(randomizer multiworld, checks partagés entre les participants).

- **Frontend** — Vite + React + TypeScript : classement, statistiques par jeu, historique des
  matchs, et un panneau d'admin pour la saisie.
- **Backend** — API .NET 9 (Minimal API) + EF Core sur SQL Server.
- **Base** — `Archipelago` sur l'instance SQL Server locale.

## Règles du tournoi

| Sujet | Règle |
|---|---|
| Roster | Chaque équipe compte **quatre joueurs** ; un joueur n'appartient qu'à une seule équipe |
| Format d'un match | Toujours **deux équipes**, avec **2 participants** par équipe en qualification, **3** en demi-finale, **4** en finale. Le format n'est pas configuré : il se déduit des lignes saisies, la seule exigence étant que les deux équipes alignent autant de joueurs l'une que l'autre |
| Abandon (DNF) | Le temps saisi est celui **atteint au moment de l'abandon**, majoré d'**une heure de pénalité**. Un temps est donc toujours requis |
| Score d'une équipe | **Somme des temps ainsi obtenus** pour ses participants ; le plus petit total gagne. La pénalité étant la sanction, une équipe qui abandonne est classée comme les autres |
| Égalité parfaite | Les deux équipes partagent la première place et comptent chacune une victoire |
| Type de match | `QUALIFICATION` ou `TOURNOI` |
| `total_checks` | Nombre total de checks existant dans le jeu |
| `nb_checks` | Nombre de checks **trouvés par le joueur** → complétion = `nb_checks / total_checks` |

Cette règle vit à un seul endroit : [`ScoringService`](backend/src/TournoiArchipelago.Api/Services/ScoringService.cs).
Le formulaire de saisie en affiche un aperçu en direct via
[`apercuMatch.ts`](frontend/src/pages/admin/apercuMatch.ts), qui reproduit le même classement.

> **Contrainte du modèle** — la table `Equipe` n'a pas de lien vers `Match` : le regroupement des
> lignes `MatchJeu` en équipes se déduit de l'appartenance des joueurs, portée par la table de
> liaison `EquipeJoueur`. Un joueur ne peut donc appartenir qu'à **une seule équipe**, garanti par
> un index unique sur `EquipeJoueur.joueur_id`.

## Prérequis

- .NET SDK 9
- Node.js 20+
- SQL Server local avec la base `Archipelago` et l'authentification mixte activée
- Docker Desktop (facultatif, pour `docker compose`)

## Mise en route

### 1. Login SQL et configuration

Un conteneur Docker ne peut pas utiliser l'authentification Windows intégrée vers l'instance de
l'hôte : l'application passe par un login SQL dédié.

```sh
sqlcmd -S localhost -E -i db/setup-login.sql -v password="MonMotDePasseFort"

cp .env.example .env   # puis reporter le mot de passe et définir Admin__* / Jwt__Key
```

Générer une clé de signature :

```sh
openssl rand -base64 48
```

### 2. Schéma de la base

Les cinq tables du modèle existaient avant l'introduction d'EF Core. La migration
`InitialCreate` décrit cet état et n'est jamais exécutée : elle est enregistrée comme déjà
appliquée par `db/baseline.sql`, ce qui préserve le diagramme `dbo.sysdiagrams`.

```sh
# Une seule fois, sur une base qui n'a pas encore d'historique de migrations :
sqlcmd -S localhost -E -d Archipelago -i db/baseline.sql

cd backend
dotnet tool restore
dotnet ef database update --project src/TournoiArchipelago.Api
```

Les migrations suivantes :

- `AjoutNbChecksEtAjustements` — ajoute `nb_checks`, convertit `temps_final_secs` de `time` en
  `int` (secondes), passe `Equipe.id` en `IDENTITY`, resserre les colonnes `nvarchar(max)`,
  ajoute les contraintes d'unicité et sème une douzaine de jeux ;
- `EquipesDeQuatreJoueursAvecNom` — remplace `Equipe.joueur1_id` / `joueur2_id` par la table de
  liaison `EquipeJoueur`, et ajoute `Equipe.nom` ;
- `NomsNonVides` — interdit un nom vide sur `Joueur`, `Jeu` et `Equipe` ;
- `PenaliteAbandon` — ajoute `MatchJeu.est_abandon` et rend `temps_final_secs` obligatoire
  et strictement positif. La pénalité d'une heure est calculée, jamais stockée : le temps
  d'abandon réel reste donc disponible.

### 2b. Seed des équipes

Les huit équipes du tournoi et leurs trente-deux joueurs :

```sh
sqlcmd -S localhost -E -b -d Archipelago -i db/seed-equipes.sql
```

Le script est idempotent, et refuse de s'exécuter si un joueur de la liste appartient déjà à une
autre équipe. Aucun match n'est touché.

### 3. Développement

Deux terminaux :

```sh
# API sur http://localhost:8080 (Swagger sur /swagger)
cd backend
dotnet run --project src/TournoiArchipelago.Api
```

```sh
# Frontend sur http://localhost:5173, /api est relayé vers l'API
cd frontend
npm install
npm run dev
```

En `Development`, la chaîne de connexion utilise l'authentification Windows et le compte admin est
`admin` / `admin` (voir `appsettings.Development.json`) — à ne pas utiliser ailleurs.

### 4. Exécution conteneurisée

```sh
docker compose up --build   # application complète sur http://localhost:8080
```

Les conteneurs n'appliquent pas les migrations : les lancer depuis l'hôte (étape 2).

## API

Les lectures sont publiques ; toutes les écritures exigent un jeton obtenu via
`POST /api/auth/login`.

| Méthode | Route | Rôle |
|---|---|---|
| `POST` | `/api/auth/login` | Ouvre une session d'admin, renvoie un JWT (8 h) |
| `GET` | `/api/auth/me` | Vérifie la validité du jeton |
| `GET`/`POST`/`PUT`/`DELETE` | `/api/joueurs`, `/api/jeux`, `/api/equipes` | Référentiel (une équipe se crée avec un `nom` et quatre `joueurIds`) |
| `GET` | `/api/matchs?type=&du=&au=` | Historique, du plus récent au plus ancien |
| `GET` | `/api/matchs/{id}` | Détail : résultats et classement des deux équipes |
| `POST` | `/api/matchs` | Enregistre un match complet, en transaction |
| `PUT`/`DELETE` | `/api/matchs/{id}` | Correction / suppression |
| `GET` | `/api/stats/classement?type=&tri=` | Classement général par équipe (`tri` : `Victoires` ou `Temps`) |
| `GET` | `/api/stats/jeux?type=` | Statistiques agrégées par jeu |

Les erreurs de validation reviennent en `400` sous forme de `ProblemDetails`, avec le détail par
champ dans `errors` — le formulaire de saisie les affiche telles quelles.

## Tests

```sh
cd backend  && dotnet test    # scoring, statistiques, endpoints (SQLite en mémoire)
cd frontend && npm test       # formatage des temps et aperçu du classement
cd frontend && npm run lint
```

## CI/CD

Deux workflows GitHub Actions, calqués sur ceux de `Decapent.NRS` : ils construisent l'image,
la poussent dans un ACR et déploient sur une Azure Container App. Déclenchés manuellement
(`workflow_dispatch`) ou à la fermeture d'une PR sur `main` touchant `backend/` ou `frontend/`.

- `.github/workflows/api-build-deploy.yaml` — tests, build/push, **migrations EF Core**, déploiement.
  Le job de migration s'intercale avant le déploiement : une migration en échec bloque la mise en ligne.
- `.github/workflows/web-build-deploy.yaml` — lint, tests, build/push, déploiement.

### Configuration requise

L'authentification passe par OIDC (`azure/login@v2`, sans secret de client). L'App Registration
doit porter une *federated credential* dont le sujet est
`repo:<compte>/<dépôt>:environment:build`, et le dépôt doit avoir un environnement nommé
`build`. Le principal a besoin de `AcrPush` sur le registre et de `Contributor` sur le groupe
de ressources.

| Secret | Rôle |
|---|---|
| `AZURE_CLIENT_ID` | Application (client) ID de l'App Registration |
| `AZURE_TENANT_ID` | Tenant Entra ID |
| `AZURE_SUBSCRIPTION_ID` | Abonnement cible |
| `MIGRATIONS_DB_CONNECTION` | Chaîne de connexion utilisée par `dotnet ef database update` |

| Variable | Rôle |
|---|---|
| `CONTAINER_REGISTRY` | Nom du registre, par exemple `monacr.azurecr.io` |
| `RESOURCE_GROUP` | Groupe de ressources des Container Apps |
| `API_IMAGE_REPOSITORY` / `WEB_IMAGE_REPOSITORY` | Dépôts d'images dans l'ACR |
| `API_CONTAINER_APP` / `WEB_CONTAINER_APP` | Noms des Container Apps |
| `API_URL` | Amont du proxy nginx du frontend : FQDN de la Container App de l'API |

> **Base de données** — le job de migration exige une base joignable depuis le runner GitHub
> (pare-feu Azure SQL : « Autoriser les services Azure », ou l'IP du runner), et **baselinée
> une fois** avec `db/baseline.sql`, faute de quoi `InitialCreate` serait rejouée sur des
> tables déjà présentes.

> **Frontend** — l'image nginx substitue `API_URL` au démarrage (`envsubst`). La valeur par
> défaut `http://api:8080` correspond au service de `docker-compose`, ce qui laisse
> l'exécution locale inchangée.

## Structure

```
backend/src/TournoiArchipelago.Api/
  Domain/          entités mappées sur les tables existantes (colonnes snake_case)
  Data/            DbContext, configurations Fluent API, migrations, jeux semés
  Features/        endpoints, un dossier par ressource
  Services/        ScoringService, StatsService, MatchService, EquipeService...
  Contracts/       DTOs de requête et de réponse
frontend/src/
  api/             types miroirs des DTOs, client HTTP, hooks react-query
  lib/format.ts    conversions secondes ↔ hh:mm:ss, pourcentages
  pages/           classement, stats par jeu, matchs, détail
  pages/admin/     connexion, saisie de match, référentiel
db/                setup-login.sql, baseline.sql, seed-equipes.sql
```
