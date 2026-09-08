using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Agregations statistiques du tournoi. Les volumes sont ceux d'un petit tournoi amical :
/// les matchs sont charges en memoire puis passes a <see cref="ScoringService"/>, ce qui evite
/// de dupliquer la regle de classement en SQL.
/// </summary>
public class StatsService(TournoiDbContext db)
{
    public async Task<IReadOnlyList<ClassementEquipeDto>> ClassementAsync(
        TypeMatch? type,
        TriClassement tri,
        CancellationToken annulation = default)
    {
        var equipes = await db.Equipes
            .Include(e => e.Membres)
            .AsNoTracking()
            .ToListAsync(annulation);

        var matchs = await ChargerMatchsAsync(type, annulation);

        var cumuls = equipes.ToDictionary(e => e.Id, _ => new Cumul());

        foreach (var match in matchs)
        {
            var classement = ScoringService.ClasserMatch(match);

            // Un match dont un resultat reste a saisir ne compte pas encore au classement.
            if (!classement.EstComplet)
            {
                continue;
            }

            foreach (var resultat in classement.Equipes)
            {
                if (resultat.EquipeId is null || !cumuls.TryGetValue(resultat.EquipeId.Value, out var cumul))
                {
                    continue;
                }

                cumul.MatchsJoues++;
                cumul.TempsCumuleSecs += resultat.TempsTotalSecs ?? 0;
                cumul.ChecksTrouves += resultat.ChecksTrouves;

                if (resultat.EstGagnante)
                {
                    cumul.Victoires++;
                }

                cumul.Abandons += resultat.NbAbandons;
                cumul.Penalites += resultat.PenaliteSecs;

                if (resultat.TempsTotalSecs is not null)
                {
                    cumul.TotauxParMatch.Add(resultat.TempsTotalSecs.Value);
                }

                if (resultat.PourcentComplete is not null)
                {
                    cumul.Pourcentages.Add(resultat.PourcentComplete.Value);
                }
            }
        }

        var lignes = equipes.Select(equipe =>
        {
            var cumul = cumuls[equipe.Id];
            return new
            {
                Equipe = equipe,
                Cumul = cumul,
                TempsMoyen = cumul.TotauxParMatch.Count > 0 ? cumul.TotauxParMatch.Average() : (double?)null,
                PourcentMoyen = cumul.Pourcentages.Count > 0 ? cumul.Pourcentages.Average() : (double?)null,
            };
        });

        // Les equipes n'ayant encore joue aucun match sont reportees en fin de classement :
        // sans cela, leur temps cumule de zero les placerait en tete.
        var ordonnees = tri == TriClassement.Temps
            ? lignes
                .OrderByDescending(l => l.Cumul.MatchsJoues > 0)
                .ThenBy(l => l.Cumul.MatchsJoues > 0 ? l.Cumul.TempsCumuleSecs : int.MaxValue)
                .ThenByDescending(l => l.Cumul.Victoires)
                .ThenBy(l => l.Equipe.Nom, StringComparer.OrdinalIgnoreCase)
            : lignes
                .OrderByDescending(l => l.Cumul.MatchsJoues > 0)
                .ThenByDescending(l => l.Cumul.Victoires)
                .ThenBy(l => l.Cumul.MatchsJoues > 0 ? l.Cumul.TempsCumuleSecs : int.MaxValue)
                .ThenBy(l => l.Equipe.Nom, StringComparer.OrdinalIgnoreCase);

        return [.. ordonnees.Select((l, index) => new ClassementEquipeDto(
            Position: index + 1,
            EquipeId: l.Equipe.Id,
            EquipeNom: l.Equipe.Nom,
            MatchsJoues: l.Cumul.MatchsJoues,
            Victoires: l.Cumul.Victoires,
            TempsCumuleSecs: l.Cumul.TempsCumuleSecs,
            TempsMoyenSecs: l.TempsMoyen,
            ChecksTrouves: l.Cumul.ChecksTrouves,
            PourcentCompleteMoyen: l.PourcentMoyen,
            Abandons: l.Cumul.Abandons,
            PenaliteCumuleeSecs: l.Cumul.Penalites))];
    }

    public async Task<IReadOnlyList<StatsJeuDto>> StatsParJeuAsync(
        TypeMatch? type,
        CancellationToken annulation = default)
    {
        var jeux = await db.Jeux.AsNoTracking().ToListAsync(annulation);

        // Les statistiques par jeu suivent la meme regle que le classement : un match dont un
        // resultat reste a saisir est ignore, y compris ses lignes deja renseignees.
        var matchs = await ChargerMatchsAsync(type, annulation);

        var lignesParJeu = matchs
            .Where(match => ScoringService.ClasserMatch(match).EstComplet)
            .SelectMany(match => match.MatchJeux)
            .GroupBy(mj => mj.JeuId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var stats = jeux.Select(jeu =>
        {
            var lignes = lignesParJeu.GetValueOrDefault(jeu.Id) ?? [];
            // Les abandons sont exclus des temps : ils ne mesurent pas une completion.
            var terminees = lignes.Where(l => !l.EstAbandon).ToList();
            var temps = terminees.Select(l => l.TempsFinalSecs!.Value).OrderBy(t => t).ToList();
            var meilleure = terminees.MinBy(l => l.TempsFinalSecs!.Value);
            var checks = lignes.Where(l => l.NbChecks is not null).Select(l => (double)l.NbChecks!.Value).ToList();
            var pourcentages = lignes.Select(l => l.PourcentComplete).OfType<double>().ToList();

            return new StatsJeuDto(
                JeuId: jeu.Id,
                JeuNom: jeu.Nom,
                NbParties: lignes.Count,
                TempsMoyenSecs: temps.Count > 0 ? temps.Average() : null,
                TempsMedianSecs: Mediane(temps),
                MeilleurTempsSecs: meilleure?.TempsFinalSecs,
                MeilleurJoueurNom: meilleure?.Joueur?.Nom,
                NbChecksMoyen: checks.Count > 0 ? checks.Average() : null,
                PourcentCompleteMoyen: pourcentages.Count > 0 ? pourcentages.Average() : null,
                NbAbandons: lignes.Count(l => l.EstAbandon));
        });

        return [.. stats
            .OrderByDescending(s => s.NbParties)
            .ThenBy(s => s.JeuNom, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Mediane d'une liste deja triee par ordre croissant.</summary>
    internal static int? Mediane(IReadOnlyList<int> triees)
    {
        if (triees.Count == 0)
        {
            return null;
        }

        var milieu = triees.Count / 2;
        return triees.Count % 2 == 1
            ? triees[milieu]
            : (int)Math.Round((triees[milieu - 1] + triees[milieu]) / 2.0, MidpointRounding.AwayFromZero);
    }

    private Task<List<Match>> ChargerMatchsAsync(TypeMatch? type, CancellationToken annulation)
    {
        var requete = MatchService.ChargerComplet(db.Matchs).AsNoTracking();

        if (type is not null)
        {
            requete = requete.Where(m => m.Type == type.Value);
        }

        return requete.ToListAsync(annulation);
    }

    private sealed class Cumul
    {
        public int MatchsJoues { get; set; }

        public int Victoires { get; set; }

        public int TempsCumuleSecs { get; set; }

        public int ChecksTrouves { get; set; }

        public int Abandons { get; set; }

        public int Penalites { get; set; }

        /// <summary>Score de l'equipe pour chaque match joue, penalites incluses.</summary>
        public List<int> TotauxParMatch { get; } = [];

        public List<double> Pourcentages { get; } = [];
    }
}
