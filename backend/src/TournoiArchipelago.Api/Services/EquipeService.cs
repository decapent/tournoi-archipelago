using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Infrastructure;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Gestion des duos. Comme la table Equipe n'a pas de lien vers Match, le regroupement des
/// resultats en equipes se deduit de l'appartenance des joueurs : ce service garantit donc
/// qu'un joueur n'est membre que d'une seule equipe, sans quoi le regroupement serait ambigu.
/// </summary>
public class EquipeService(TournoiDbContext db)
{
    public async Task<IReadOnlyList<EquipeDto>> ListerAsync(CancellationToken annulation = default)
    {
        var equipes = await ChargerAvecJoueurs().AsNoTracking().ToListAsync(annulation);

        return [.. equipes
            .Select(VersDto)
            .OrderBy(e => e.Nom, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<EquipeDto?> ObtenirAsync(int id, CancellationToken annulation = default)
    {
        var equipe = await ChargerAvecJoueurs().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, annulation);

        return equipe is null ? null : VersDto(equipe);
    }

    public async Task<EquipeDto> CreerAsync(EquipeUpsertRequest requete, CancellationToken annulation = default)
    {
        var (joueur1Id, joueur2Id) = await ValiderAsync(requete, equipeExclue: null, annulation);

        var equipe = new Equipe { Joueur1Id = joueur1Id, Joueur2Id = joueur2Id };
        db.Equipes.Add(equipe);
        await db.SaveChangesAsync(annulation);

        return (await ObtenirAsync(equipe.Id, annulation))!;
    }

    public async Task<EquipeDto?> ModifierAsync(
        int id,
        EquipeUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var equipe = await db.Equipes.FirstOrDefaultAsync(e => e.Id == id, annulation);
        if (equipe is null)
        {
            return null;
        }

        var (joueur1Id, joueur2Id) = await ValiderAsync(requete, equipeExclue: id, annulation);
        equipe.Joueur1Id = joueur1Id;
        equipe.Joueur2Id = joueur2Id;
        await db.SaveChangesAsync(annulation);

        return await ObtenirAsync(id, annulation);
    }

    public async Task<bool> SupprimerAsync(int id, CancellationToken annulation = default)
    {
        var equipe = await db.Equipes.FirstOrDefaultAsync(e => e.Id == id, annulation);
        if (equipe is null)
        {
            return false;
        }

        db.Equipes.Remove(equipe);
        await db.SaveChangesAsync(annulation);
        return true;
    }

    public static EquipeDto VersDto(Equipe equipe) => new(
        Id: equipe.Id,
        Nom: ScoringService.NomEquipe(equipe),
        Joueur1: new JoueurDto(equipe.Joueur1Id, equipe.Joueur1?.Nom ?? $"#{equipe.Joueur1Id}"),
        Joueur2: new JoueurDto(equipe.Joueur2Id, equipe.Joueur2?.Nom ?? $"#{equipe.Joueur2Id}"));

    private IQueryable<Equipe> ChargerAvecJoueurs() =>
        db.Equipes.Include(e => e.Joueur1).Include(e => e.Joueur2);

    /// <summary>
    /// Valide le duo et renvoie les identifiants normalises : le plus petit en premier, pour que
    /// l'index unique attrape aussi les duos saisis dans l'ordre inverse.
    /// </summary>
    private async Task<(int Joueur1Id, int Joueur2Id)> ValiderAsync(
        EquipeUpsertRequest requete,
        int? equipeExclue,
        CancellationToken annulation)
    {
        if (requete.Joueur1Id == requete.Joueur2Id)
        {
            throw new RequeteInvalideException("joueur2Id", "Une equipe doit reunir deux joueurs differents.");
        }

        var joueur1Id = Math.Min(requete.Joueur1Id, requete.Joueur2Id);
        var joueur2Id = Math.Max(requete.Joueur1Id, requete.Joueur2Id);

        var existants = await db.Joueurs
            .Where(j => j.Id == joueur1Id || j.Id == joueur2Id)
            .Select(j => j.Id)
            .ToListAsync(annulation);

        var manquants = new[] { joueur1Id, joueur2Id }.Except(existants).ToList();
        if (manquants.Count > 0)
        {
            throw new RequeteInvalideException(
                "joueur1Id",
                $"Joueur introuvable : {string.Join(", ", manquants)}.");
        }

        var conflits = await db.Equipes
            .Include(e => e.Joueur1)
            .Include(e => e.Joueur2)
            .Where(e => equipeExclue == null || e.Id != equipeExclue)
            .Where(e => e.Joueur1Id == joueur1Id || e.Joueur2Id == joueur1Id
                     || e.Joueur1Id == joueur2Id || e.Joueur2Id == joueur2Id)
            .ToListAsync(annulation);

        if (conflits.Count > 0)
        {
            throw new RequeteInvalideException(
                "joueur1Id",
                $"Un joueur ne peut appartenir qu'a une seule equipe. Conflit avec l'equipe {ScoringService.NomEquipe(conflits[0])}.");
        }

        return (joueur1Id, joueur2Id);
    }
}
