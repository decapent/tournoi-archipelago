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
| Score d'une équipe | **Le plus long des temps ainsi obtenus**, pas leur somme : l'équipe a fini quand son dernier joueur a fini. Le plus petit score gagne. La pénalité étant la sanction, une équipe qui abandonne est classée comme les autres |
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

### 2b. Seed des équipes et des jeux

Les huit équipes du tournoi et leurs trente-deux joueurs :

```sh
sqlcmd -S localhost -E -b -d Archipelago -i db/seed-equipes.sql
```

Le script est idempotent, et refuse de s'exécuter si un joueur de la liste appartient déjà à une
autre équipe. Aucun match n'est touché.

Les vingt-deux jeux du tournoi :

```sh
sqlcmd -S localhost -E -b -d Archipelago -i db/seed-jeux.sql
```

Il ajoute les jeux manquants sans toucher aux identifiants existants, et retire les jeux hors
liste **uniquement s'ils n'ont jamais été joués** — ceux qui portent des résultats sont conservés
et signalés. Il remplace ainsi la douzaine de jeux semés par la migration `AjoutNbChecksEtAjustements`.

> `db/seed-jeux.sql` est en **UTF-8 avec BOM** et doit le rester : sans BOM, `sqlcmd` le lit dans
> la page de codes ANSI et « Pokémon » finit en « PokÃ©mon » dans la base.

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

Deux workflows GitHub Actions : ils construisent l'image, la poussent dans un ACR et déploient
sur **Azure Container Instances**, un groupe par composant. Déclenchés manuellement
(`workflow_dispatch`) ou par un push sur `main` touchant `backend/` ou `frontend/`.

- `.github/workflows/api-build-deploy.yaml` — tests, build/push, **migrations EF Core**, déploiement.
  Le job de migration s'intercale avant le déploiement : une migration en échec bloque la mise en ligne.
- `.github/workflows/web-build-deploy.yaml` — lint, tests, build/push, déploiement.

ACI ne sait pas changer l'image d'un groupe en place : chaque déploiement **supprime puis
recrée** le groupe, avec une brève interruption. La suppression libère l'étiquette DNS, que la
recréation reprend, donc les URLs sont stables.

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
| `ACR_USERNAME` / `ACR_PASSWORD` | Utilisateur admin de l'ACR, dont ACI se sert pour tirer les images |
| `MIGRATIONS_DB_CONNECTION` | Chaîne de connexion vers Azure SQL. Sert au job de migration **et** à l'API (`ConnectionStrings__Tournoi`) |
| `ADMIN_PASSWORD` | Mot de passe du panneau d'admin |
| `JWT_KEY` | Clé de signature des jetons, 32 caractères minimum |

| Variable | Rôle |
|---|---|
| `CONTAINER_REGISTRY` | Nom du registre, par exemple `monacr.azurecr.io` |
| `RESOURCE_GROUP` | Groupe de ressources des groupes de conteneurs |
| `API_IMAGE_REPOSITORY` / `WEB_IMAGE_REPOSITORY` | Dépôts d'images dans l'ACR |
| `API_CONTAINER_GROUP` / `WEB_CONTAINER_GROUP` | Noms des groupes ACI |
| `API_DNS_LABEL` / `WEB_DNS_LABEL` | Étiquettes DNS, qui donnent `<label>.<région>.azurecontainer.io` |
| `ADMIN_USERNAME` | Nom d'utilisateur du panneau d'admin |
| `API_URL` | Amont du proxy nginx du frontend : `http://<API_DNS_LABEL>.<région>.azurecontainer.io:8080` |

L'utilisateur admin de l'ACR doit être activé (`az acr update -n <registre> --admin-enabled true`),
puis ses identifiants relevés avec `az acr credential show -n <registre>`.

Le navigateur n'appelle jamais l'API directement : nginx relaie `/api` côté serveur, donc rien à
configurer en CORS.

### Base de données Azure SQL

Serveur `noreset.database.windows.net`, base `archipelago`.

Aucun baseline n'est nécessaire : la base a été créée vide, donc `InitialCreate` crée les
tables et les migrations suivantes les font évoluer. `db/baseline.sql` ne concerne que la base
locale, dont les tables préexistaient à EF Core.

**1. Utilisateur applicatif** — une fois, avec le compte administrateur du serveur :

```sh
sqlcmd -S noreset.database.windows.net -d archipelago -U <admin> -P <motDePasseAdmin> \
       -b -i db/setup-login-azure.sql -v password="<motDePasseApp>"
```

Azure SQL n'autorise pas `USE` entre bases : le script crée un **utilisateur contenu**, dont le
mot de passe vit dans la base. Rien n'est créé dans `master`. C'est pourquoi il existe en deux
versions, `setup-login.sql` (local) et `setup-login-azure.sql`.

**2. Pare-feu** — activer « Autoriser les services Azure et les ressources à accéder à ce
serveur », sinon le runner GitHub ne peut pas se connecter.

**3. Secret** `MIGRATIONS_DB_CONNECTION` :

```
Server=tcp:noreset.database.windows.net,1433;Database=archipelago;User ID=tournoi_app;Password=<motDePasseApp>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**4. Seed des équipes** — les migrations ne créent que le schéma. Une fois appliquées :

```sh
sqlcmd -S noreset.database.windows.net -d archipelago -U tournoi_app -P <motDePasseApp> \
       -b -i db/seed-equipes.sql
```

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
