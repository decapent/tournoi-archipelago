using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Infrastructure;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Reporte un journal Archipelago sur les resultats d'une equipe dans un match.
///
/// Un journal correspond a une seule equipe : chacune court dans sa propre partie. L'import
/// se fait donc equipe par equipe, et ne touche jamais aux lignes de l'autre camp.
///
/// Les lignes de resultat doivent exister au prealable — c'est la saisie qui fixe le jeu et
/// le seed de chaque joueur. L'import ne fait que les completer : checks, total et temps.
/// </summary>
public class ImportLogService(TournoiDbContext db)
{
    public static RapportLogDto Analyser(AnalyseLogRequest requete)
    {
        if (string.IsNullOrWhiteSpace(requete.Contenu))
        {
            throw new RequeteInvalideException("contenu", "Le journal est vide.");
        }

        return AnalyseurLogArchipelago.Analyser(requete.Contenu);
    }

    /// <summary>Progression de tous les joueurs d'un match, prete a tracer.</summary>
    public async Task<ProgressionMatchDto?> ProgressionAsync(
        int matchId,
        CancellationToken annulation = default)
    {
        var match = await MatchService.ChargerComplet(db.Matchs)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matchId, annulation);

        if (match is null)
        {
            return null;
        }

        var checks = await db.Checks
            .Where(c => c.MatchId == matchId)
            .AsNoTracking()
            .ToListAsync(annulation);

        var indices = await db.Indices
            .Where(i => i.MatchId == matchId)
            .AsNoTracking()
            .ToListAsync(annulation);

        var equipeParJoueur = match.Equipes
            .Where(e => e.Equipe is not null)
            .SelectMany(e => e.Equipe!.Membres.Select(m => (m.JoueurId, e.Equipe)))
            .ToDictionary(p => p.JoueurId, p => p.Equipe!);

        // Un joueur peut n'avoir que des indices, ou que des checks : la reunion des deux
        // sources evite de le perdre.
        var joueurIds = checks.Select(c => c.JoueurId)
            .Concat(indices.Select(i => i.JoueurId))
            .Distinct();

        var joueurs = joueurIds
            .Select(joueurId =>
            {
                var ligne = match.MatchJeux.FirstOrDefault(mj => mj.JoueurId == joueurId);
                var equipe = equipeParJoueur.GetValueOrDefault(joueurId);

                return new ProgressionJoueurDto(
                    JoueurId: joueurId,
                    JoueurNom: ligne?.Joueur?.Nom ?? $"#{joueurId}",
                    EquipeId: equipe?.Id,
                    EquipeNom: equipe?.Nom ?? ScoringService.NomEquipeInconnue,
                    JeuNom: ligne?.Jeu?.Nom ?? string.Empty,
                    TotalChecks: ligne?.TotalChecks,
                    Secondes: [.. checks.Where(c => c.JoueurId == joueurId)
                                        .Select(c => c.Secondes)
                                        .Order()],
                    Indices: [.. indices.Where(i => i.JoueurId == joueurId)
                                        .OrderBy(i => i.Secondes)
                                        .Select(i => new IndiceProgressionDto(
                                            i.Secondes, i.Resultat, i.PointsRestants))]);
            })
            .OrderBy(j => j.EquipeNom, StringComparer.OrdinalIgnoreCase)
            .ThenBy(j => j.JoueurNom, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProgressionMatchDto(matchId, joueurs);
    }

    public async Task<MatchDetailDto?> ImporterAsync(
        int matchId,
        int equipeId,
        ImportLogRequest requete,
        CancellationToken annulation = default)
    {
        var match = await MatchService.ChargerComplet(db.Matchs)
            .FirstOrDefaultAsync(m => m.Id == matchId, annulation);

        if (match is null)
        {
            return null;
        }

        var equipe = match.Equipes.FirstOrDefault(e => e.EquipeId == equipeId)?.Equipe;
        if (equipe is null)
        {
            throw new RequeteInvalideException("equipeId", "Cette equipe ne participe pas au match.");
        }

        var rapport = Analyser(new AnalyseLogRequest(requete.Contenu));
        var affectations = Valider(match, equipe, rapport, requete);

        foreach (var (ligne, joueurLog) in affectations)
        {
            ligne.NbChecks = joueurLog.ChecksTrouves;
            ligne.EstAbandon = joueurLog.EstAbandon;
            ligne.NbIndicesDemandes = joueurLog.Indices.Count;
            ligne.NbIndicesObtenus = joueurLog.IndicesDistincts;

            // Le total du monde n'est connu que si le joueur a termine : la liberation est ce
            // qui enumere les localisations restantes. Sinon on laisse la saisie en place.
            if (joueurLog.TotalChecks is not null)
            {
                ligne.TotalChecks = joueurLog.TotalChecks;
            }

            // Pour un abandon, le temps retenu est celui atteint au dernier check.
            var fin = joueurLog.Objectif ?? joueurLog.DernierCheck;
            if (fin is not null)
            {
                ligne.TempsFinalSecs = Secondes(requete.DepartCourse, fin.Value);
            }
        }

        // L'import remplace la progression de ces joueurs : reimporter un journal corrige ne
        // doit pas empiler les courbes.
        var joueurIds = affectations.Select(a => a.Ligne.JoueurId).ToList();
        var ancienne = await db.Checks
            .Where(c => c.MatchId == matchId && joueurIds.Contains(c.JoueurId))
            .ToListAsync(annulation);

        var anciensIndices = await db.Indices
            .Where(i => i.MatchId == matchId && joueurIds.Contains(i.JoueurId))
            .ToListAsync(annulation);

        db.Checks.RemoveRange(ancienne);
        db.Indices.RemoveRange(anciensIndices);

        foreach (var (ligne, joueurLog) in affectations)
        {
            foreach (var horodatage in joueurLog.Horodatages)
            {
                db.Checks.Add(new CheckHorodate
                {
                    MatchId = matchId,
                    JoueurId = ligne.JoueurId,
                    Secondes = Secondes(requete.DepartCourse, horodatage),
                });
            }

            foreach (var indice in joueurLog.Indices)
            {
                db.Indices.Add(new IndiceHorodate
                {
                    MatchId = matchId,
                    JoueurId = ligne.JoueurId,
                    Secondes = Secondes(requete.DepartCourse, indice.Horodatage),
                    Resultat = indice.Resultat,
                    PointsRestants = indice.PointsRestants,
                });
            }
        }

        await db.SaveChangesAsync(annulation);
        db.ChangeTracker.Clear();

        var relu = await MatchService.ChargerComplet(db.Matchs).AsNoTracking()
            .FirstAsync(m => m.Id == matchId, annulation);

        return MatchService.VersDetail(relu);
    }

    /// <summary>Secondes ecoulees depuis le depart, jamais negatives.</summary>
    private static int Secondes(DateTime depart, DateTime instant) =>
        Math.Max(0, (int)Math.Round((instant - depart).TotalSeconds));

    /// <summary>
    /// Verifie le rapprochement demande et rend, pour chaque alias, la ligne de resultat a
    /// completer. Signale tous les problemes d'un coup.
    /// </summary>
    private static List<(MatchJeu Ligne, JoueurLogDto Log)> Valider(
        Match match,
        Equipe equipe,
        RapportLogDto rapport,
        ImportLogRequest requete)
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

        if (requete.DepartCourse == default)
        {
            Ajouter("departCourse", "Le depart de la course est obligatoire.");
        }

        var correspondances = requete.Correspondances ?? [];
        if (correspondances.Count == 0)
        {
            Ajouter("correspondances", "Associer au moins un pseudonyme du journal a un joueur.");
        }

        var doublonsAlias = correspondances.GroupBy(c => c.Alias, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (doublonsAlias.Count > 0)
        {
            Ajouter("correspondances", $"Pseudonyme associe plusieurs fois : {string.Join(", ", doublonsAlias)}.");
        }

        var doublonsJoueur = correspondances.GroupBy(c => c.JoueurId)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (doublonsJoueur.Count > 0)
        {
            Ajouter("correspondances", $"Joueur associe a plusieurs pseudonymes : {string.Join(", ", doublonsJoueur)}.");
        }

        var roster = equipe.MembreIds.ToHashSet();
        var affectations = new List<(MatchJeu, JoueurLogDto)>();

        foreach (var c in correspondances)
        {
            var joueurLog = rapport.Joueurs.FirstOrDefault(j => j.Alias == c.Alias);
            if (joueurLog is null)
            {
                Ajouter("correspondances", $"Le pseudonyme « {c.Alias} » est absent du journal.");
                continue;
            }

            if (!roster.Contains(c.JoueurId))
            {
                Ajouter("correspondances", $"Le joueur {c.JoueurId} n'appartient pas a l'equipe {equipe.Nom}.");
                continue;
            }

            var ligne = match.MatchJeux.FirstOrDefault(mj => mj.JoueurId == c.JoueurId);
            if (ligne is null)
            {
                Ajouter(
                    "correspondances",
                    $"Le joueur {c.JoueurId} n'a pas de ligne dans ce match : saisir son jeu et son seed avant d'importer.");
                continue;
            }

            affectations.Add((ligne, joueurLog));
        }

        if (erreurs.Count > 0)
        {
            throw new RequeteInvalideException(
                erreurs.ToDictionary(paire => paire.Key, paire => paire.Value.ToArray()));
        }

        return affectations;
    }
}
