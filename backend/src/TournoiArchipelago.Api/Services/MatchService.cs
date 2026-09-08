using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Infrastructure;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Saisie et consultation des matchs. Un match oppose toujours deux equipes, mais le nombre de
/// participants par equipe varie selon l'etape : deux en qualification, trois en demi-finale,
/// quatre en finale. Ce nombre n'est pas configure, il se deduit des lignes saisies ; la seule
/// exigence est que les deux equipes alignent autant de joueurs l'une que l'autre.
/// </summary>
public class MatchService(TournoiDbContext db)
{
    /// <summary>Nombre d'equipes qui s'affrontent dans un match.</summary>
    public const int NbEquipesParMatch = 2;

    /// <summary>Nombre minimal de participants par equipe (format qualification).</summary>
    public const int NbJoueursMinParEquipe = 2;

    /// <summary>Nombre maximal de participants par equipe (roster complet, format finale).</summary>
    public const int NbJoueursMaxParEquipe = 4;

    public async Task<IReadOnlyList<MatchSommaireDto>> ListerAsync(
        TypeMatch? type,
        DateOnly? du,
        DateOnly? au,
        CancellationToken annulation = default)
    {
        var requete = ChargerComplet().AsNoTracking();

        if (type is not null)
        {
            requete = requete.Where(m => m.Type == type.Value);
        }

        if (du is not null)
        {
            requete = requete.Where(m => m.Date >= du.Value);
        }

        if (au is not null)
        {
            requete = requete.Where(m => m.Date <= au.Value);
        }

        var matchs = await requete
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .ToListAsync(annulation);

        var equipes = await ChargerEquipesAsync(annulation);

        return [.. matchs.Select(match =>
        {
            var classement = ScoringService.ClasserMatch(match.MatchJeux, equipes);
            return new MatchSommaireDto(
                Id: match.Id,
                Date: match.Date,
                Type: match.Type,
                EquipeGagnanteNom: classement.FirstOrDefault(e => e.EstGagnante)?.EquipeNom,
                EquipeNoms: [.. classement.Select(e => e.EquipeNom)]);
        })];
    }

    public async Task<MatchDetailDto?> ObtenirAsync(int id, CancellationToken annulation = default)
    {
        var match = await ChargerComplet().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, annulation);

        if (match is null)
        {
            return null;
        }

        var equipes = await ChargerEquipesAsync(annulation);

        return new MatchDetailDto(
            Id: match.Id,
            Date: match.Date,
            Type: match.Type,
            Equipes: ScoringService.ClasserMatch(match.MatchJeux, equipes));
    }

    public async Task<MatchDetailDto> CreerAsync(
        MatchUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var lignes = await ValiderAsync(requete, annulation);

        // Passer par la strategie d'execution rend l'ecriture compatible avec une reprise sur
        // erreur transitoire, qu'EF Core interdit autour d'une transaction ouverte a la main.
        var matchId = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(annulation);

            var match = new Match { Date = requete.Date, Type = requete.Type };
            db.Matchs.Add(match);
            await db.SaveChangesAsync(annulation);

            foreach (var ligne in lignes)
            {
                ligne.MatchId = match.Id;
                db.MatchJeux.Add(ligne);
            }

            await db.SaveChangesAsync(annulation);
            await transaction.CommitAsync(annulation);

            return match.Id;
        });

        db.ChangeTracker.Clear();
        return (await ObtenirAsync(matchId, annulation))!;
    }

    public async Task<MatchDetailDto?> ModifierAsync(
        int id,
        MatchUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var match = await db.Matchs
            .Include(m => m.MatchJeux)
            .FirstOrDefaultAsync(m => m.Id == id, annulation);

        if (match is null)
        {
            return null;
        }

        var lignes = await ValiderAsync(requete, annulation);

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(annulation);

            match.Date = requete.Date;
            match.Type = requete.Type;

            // Le jeu fait partie de la cle primaire : les lignes sont remplacees plutot que modifiees.
            db.MatchJeux.RemoveRange(match.MatchJeux);
            await db.SaveChangesAsync(annulation);

            foreach (var ligne in lignes)
            {
                ligne.MatchId = match.Id;
                db.MatchJeux.Add(ligne);
            }

            await db.SaveChangesAsync(annulation);
            await transaction.CommitAsync(annulation);
        });

        db.ChangeTracker.Clear();
        return await ObtenirAsync(id, annulation);
    }

    public async Task<bool> SupprimerAsync(int id, CancellationToken annulation = default)
    {
        var match = await db.Matchs.FirstOrDefaultAsync(m => m.Id == id, annulation);
        if (match is null)
        {
            return false;
        }

        // Les lignes MatchJeu partent en cascade.
        db.Matchs.Remove(match);
        await db.SaveChangesAsync(annulation);
        return true;
    }

    private IQueryable<Match> ChargerComplet() =>
        db.Matchs
            .Include(m => m.MatchJeux).ThenInclude(mj => mj.Joueur)
            .Include(m => m.MatchJeux).ThenInclude(mj => mj.Jeu);

    private Task<List<Equipe>> ChargerEquipesAsync(CancellationToken annulation) =>
        db.Equipes
            .Include(e => e.Membres).ThenInclude(m => m.Joueur)
            .AsNoTracking()
            .ToListAsync(annulation);

    /// <summary>
    /// Verifie la coherence de la saisie et construit les lignes a persister.
    /// Leve <see cref="RequeteInvalideException"/> en decrivant tous les problemes trouves,
    /// afin que le formulaire puisse les afficher en une seule passe.
    /// </summary>
    private async Task<List<MatchJeu>> ValiderAsync(
        MatchUpsertRequest requete,
        CancellationToken annulation)
    {
        var erreurs = new Dictionary<string, List<string>>();

        void Ajouter(string champ, string message)
        {
            if (!erreurs.TryGetValue(champ, out var messages))
            {
                erreurs[champ] = messages = [];
            }

            messages.Add(message);
        }

        if (requete.Date == default)
        {
            Ajouter("date", "La date du match est obligatoire.");
        }

        if (!Enum.IsDefined(requete.Type))
        {
            Ajouter("type", "Le type doit valoir QUALIFICATION ou TOURNOI.");
        }

        var deuxEquipesDistinctes = requete.EquipeAId != requete.EquipeBId;
        if (!deuxEquipesDistinctes)
        {
            Ajouter("equipeBId", "Un match doit opposer deux equipes differentes.");
        }

        var equipes = await db.Equipes
            .Include(e => e.Membres)
            .AsNoTracking()
            .Where(e => e.Id == requete.EquipeAId || e.Id == requete.EquipeBId)
            .ToListAsync(annulation);

        if (deuxEquipesDistinctes && equipes.Count != NbEquipesParMatch)
        {
            var trouvees = equipes.Select(e => e.Id).ToHashSet();
            var manquantes = new[] { requete.EquipeAId, requete.EquipeBId }.Where(id => !trouvees.Contains(id));
            Ajouter("equipeAId", $"Equipe introuvable : {string.Join(", ", manquantes)}.");
        }

        var resultats = requete.Resultats ?? [];
        var joueursSaisis = resultats.Select(r => r.JoueurId).ToList();

        var doublons = joueursSaisis.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (doublons.Count > 0)
        {
            Ajouter("resultats", $"Un joueur ne peut apparaitre qu'une seule fois par match : {string.Join(", ", doublons)}.");
        }

        if (equipes.Count == NbEquipesParMatch)
        {
            var rosters = equipes.ToDictionary(e => e.Id, e => e.MembreIds.ToHashSet());

            var intrus = joueursSaisis
                .Where(id => !rosters.Values.Any(roster => roster.Contains(id)))
                .Distinct()
                .ToList();

            if (intrus.Count > 0)
            {
                Ajouter("resultats", $"Ces joueurs ne font pas partie des deux equipes : {string.Join(", ", intrus)}.");
            }

            // Le format du match n'est pas configure : il decoule du nombre de joueurs alignes.
            // On exige seulement que les deux equipes en alignent autant l'une que l'autre.
            var effectifs = equipes.ToDictionary(
                equipe => equipe,
                equipe => joueursSaisis.Count(id => rosters[equipe.Id].Contains(id)));

            var distincts = effectifs.Values.Distinct().ToList();
            if (distincts.Count > 1)
            {
                var detail = string.Join(
                    " contre ",
                    effectifs.Select(paire => $"{paire.Value} pour {paire.Key.Nom}"));

                Ajouter("resultats", $"Les deux equipes doivent aligner le meme nombre de joueurs : {detail}.");
            }
            else
            {
                var effectif = distincts.Count == 1 ? distincts[0] : 0;

                if (effectif < NbJoueursMinParEquipe || effectif > NbJoueursMaxParEquipe)
                {
                    Ajouter(
                        "resultats",
                        $"Chaque equipe doit aligner entre {NbJoueursMinParEquipe} et {NbJoueursMaxParEquipe} joueurs, {effectif} recu(s).");
                }
            }
        }
        else if (resultats.Count == 0)
        {
            Ajouter("resultats", "Aucun resultat saisi.");
        }

        var jeuxDemandes = resultats.Select(r => r.JeuId).Distinct().ToList();
        var jeuxExistants = await db.Jeux
            .Where(j => jeuxDemandes.Contains(j.Id))
            .Select(j => j.Id)
            .ToListAsync(annulation);

        var jeuxManquants = jeuxDemandes.Except(jeuxExistants).ToList();
        if (jeuxManquants.Count > 0)
        {
            Ajouter("resultats", $"Jeu introuvable : {string.Join(", ", jeuxManquants)}.");
        }

        for (var i = 0; i < resultats.Count; i++)
        {
            var resultat = resultats[i];
            var champ = $"resultats[{i}]";

            if (resultat.TotalChecks is < 0)
            {
                Ajouter(champ, "Le nombre total de checks ne peut pas etre negatif.");
            }

            if (resultat.NbChecks is < 0)
            {
                Ajouter(champ, "Le nombre de checks trouves ne peut pas etre negatif.");
            }

            if (resultat.NbChecks is not null && resultat.TotalChecks is not null
                && resultat.NbChecks > resultat.TotalChecks)
            {
                Ajouter(champ, "Le nombre de checks trouves ne peut pas depasser le total du jeu.");
            }

            // Un abandon porte l'instant ou le joueur a arrete : un temps est donc toujours requis.
            if (resultat.TempsFinalSecs <= 0)
            {
                Ajouter(
                    champ,
                    resultat.EstAbandon
                        ? "Le temps d'abandon doit etre positif."
                        : "Le temps de completion doit etre positif.");
            }
        }

        if (erreurs.Count > 0)
        {
            throw new RequeteInvalideException(
                erreurs.ToDictionary(paire => paire.Key, paire => paire.Value.ToArray()));
        }

        return [.. resultats.Select(resultat => new MatchJeu
        {
            JoueurId = resultat.JoueurId,
            JeuId = resultat.JeuId,
            Seed = string.IsNullOrWhiteSpace(resultat.Seed) ? null : resultat.Seed.Trim(),
            TotalChecks = resultat.TotalChecks,
            NbChecks = resultat.NbChecks,
            TempsFinalSecs = resultat.TempsFinalSecs,
            EstAbandon = resultat.EstAbandon,
        })];
    }
}
