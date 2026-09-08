using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Infrastructure;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Saisie et consultation des matchs. Un match oppose toujours deux equipes, mais ses resultats
/// peuvent etre completes au fur et a mesure : seuls la date, le type et les deux equipes sont
/// requis a la creation.
///
/// Le nombre de participants par equipe n'est pas configure, il se deduit des lignes saisies
/// (deux en qualification, trois en demi-finale, quatre en finale). La coherence du format
/// n'est donc verifiee qu'a la lecture, via <see cref="ClassementMatch.EstComplet"/>, et non a
/// l'enregistrement, sans quoi une saisie progressive serait impossible.
/// </summary>
public class MatchService(TournoiDbContext db)
{
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

        return [.. matchs.Select(match =>
        {
            var classement = ScoringService.ClasserMatch(match);

            return new MatchSommaireDto(
                Id: match.Id,
                Date: match.Date,
                Type: match.Type,
                EstComplet: classement.EstComplet,
                NbResultatsEnAttente: classement.Equipes.Sum(e => e.NbResultatsEnAttente),
                EquipeGagnanteNom: classement.Equipes.FirstOrDefault(e => e.EstGagnante)?.EquipeNom,
                EquipeNoms: [.. classement.Equipes.Select(e => e.EquipeNom)]);
        })];
    }

    public async Task<MatchDetailDto?> ObtenirAsync(int id, CancellationToken annulation = default)
    {
        var match = await ChargerComplet().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, annulation);

        return match is null ? null : VersDetail(match);
    }

    public async Task<MatchDetailDto> CreerAsync(
        MatchUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var (equipeIds, lignes) = await ValiderAsync(requete, annulation);

        // Passer par la strategie d'execution rend l'ecriture compatible avec une reprise sur
        // erreur transitoire, qu'EF Core interdit autour d'une transaction ouverte a la main.
        var matchId = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(annulation);

            var match = new Match
            {
                Date = requete.Date,
                Type = requete.Type,
                Equipes = [.. equipeIds.Select(id => new MatchEquipe { EquipeId = id })],
            };

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
            .Include(m => m.Equipes)
            .Include(m => m.MatchJeux)
            .FirstOrDefaultAsync(m => m.Id == id, annulation);

        if (match is null)
        {
            return null;
        }

        var (equipeIds, lignes) = await ValiderAsync(requete, annulation);

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(annulation);

            match.Date = requete.Date;
            match.Type = requete.Type;

            foreach (var partant in match.Equipes.Where(e => !equipeIds.Contains(e.EquipeId)).ToList())
            {
                match.Equipes.Remove(partant);
            }

            var dejaEngagees = match.Equipes.Select(e => e.EquipeId).ToHashSet();
            foreach (var equipeId in equipeIds.Where(equipeId => !dejaEngagees.Contains(equipeId)))
            {
                match.Equipes.Add(new MatchEquipe { MatchId = id, EquipeId = equipeId });
            }

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

        // Les lignes MatchEquipe et MatchJeu partent en cascade.
        db.Matchs.Remove(match);
        await db.SaveChangesAsync(annulation);
        return true;
    }

    /// <summary>Charge un match avec tout ce dont <see cref="ScoringService"/> a besoin.</summary>
    internal static IQueryable<Match> ChargerComplet(IQueryable<Match> source) =>
        source
            .Include(m => m.Equipes).ThenInclude(e => e.Equipe!).ThenInclude(e => e.Membres)
            .Include(m => m.MatchJeux).ThenInclude(mj => mj.Joueur)
            .Include(m => m.MatchJeux).ThenInclude(mj => mj.Jeu);

    internal static MatchDetailDto VersDetail(Match match)
    {
        var classement = ScoringService.ClasserMatch(match);

        return new MatchDetailDto(
            Id: match.Id,
            Date: match.Date,
            Type: match.Type,
            EstComplet: classement.EstComplet,
            Equipes: classement.Equipes);
    }

    private IQueryable<Match> ChargerComplet() => ChargerComplet(db.Matchs);

    /// <summary>
    /// Verifie la coherence de la saisie et construit les lignes a persister. Une saisie
    /// partielle est acceptee : ce qui est verifie ici, c'est que ce qui est saisi est
    /// coherent, pas que le match soit termine.
    /// </summary>
    private async Task<(List<int> EquipeIds, List<MatchJeu> Lignes)> ValiderAsync(
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

        if (deuxEquipesDistinctes && equipes.Count != ScoringService.NbEquipesParMatch)
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

        if (equipes.Count == ScoringService.NbEquipesParMatch)
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

            // On ne peut pas exiger un effectif complet lors d'une saisie progressive, mais on
            // refuse de depasser la taille du roster.
            foreach (var equipe in equipes)
            {
                var effectif = joueursSaisis.Count(id => rosters[equipe.Id].Contains(id));

                if (effectif > ScoringService.NbJoueursMaxParEquipe)
                {
                    Ajouter(
                        "resultats",
                        $"{equipe.Nom} aligne {effectif} joueurs, au-dela du maximum de {ScoringService.NbJoueursMaxParEquipe}.");
                }
            }
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

            // Le temps peut rester a saisir, mais jamais valoir zero ou moins.
            if (resultat.TempsFinalSecs is <= 0)
            {
                Ajouter(
                    champ,
                    resultat.EstAbandon
                        ? "Le temps d'abandon doit etre positif, ou laisse vide s'il reste a saisir."
                        : "Le temps de completion doit etre positif, ou laisse vide s'il reste a saisir.");
            }
        }

        if (erreurs.Count > 0)
        {
            throw new RequeteInvalideException(
                erreurs.ToDictionary(paire => paire.Key, paire => paire.Value.ToArray()));
        }

        var lignes = resultats.Select(resultat => new MatchJeu
        {
            JoueurId = resultat.JoueurId,
            JeuId = resultat.JeuId,
            Seed = string.IsNullOrWhiteSpace(resultat.Seed) ? null : resultat.Seed.Trim(),
            TotalChecks = resultat.TotalChecks,
            NbChecks = resultat.NbChecks,
            TempsFinalSecs = resultat.TempsFinalSecs,
            EstAbandon = resultat.EstAbandon,
        }).ToList();

        return ([requete.EquipeAId, requete.EquipeBId], lignes);
    }
}
