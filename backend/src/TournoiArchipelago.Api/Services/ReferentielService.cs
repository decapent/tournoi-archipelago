using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Infrastructure;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// CRUD des joueurs et des jeux. Les suppressions sont refusees des qu'une donnee de match
/// s'appuie sur l'enregistrement, pour ne pas casser l'historique.
/// </summary>
public class ReferentielService(TournoiDbContext db)
{
    public async Task<IReadOnlyList<JoueurDto>> ListerJoueursAsync(CancellationToken annulation = default) =>
        await db.Joueurs
            .AsNoTracking()
            .OrderBy(j => j.Nom)
            .Select(j => new JoueurDto(j.Id, j.Nom))
            .ToListAsync(annulation);

    public async Task<JoueurDto> CreerJoueurAsync(
        JoueurUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var nom = ValiderNom(requete.Nom, "nom", longueurMax: 100);
        await VerifierNomLibreAsync(db.Joueurs, nom, exclu: null, "Un joueur", annulation);

        var joueur = new Joueur { Nom = nom };
        db.Joueurs.Add(joueur);
        await db.SaveChangesAsync(annulation);

        return new JoueurDto(joueur.Id, joueur.Nom);
    }

    public async Task<JoueurDto?> ModifierJoueurAsync(
        int id,
        JoueurUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var joueur = await db.Joueurs.FirstOrDefaultAsync(j => j.Id == id, annulation);
        if (joueur is null)
        {
            return null;
        }

        var nom = ValiderNom(requete.Nom, "nom", longueurMax: 100);
        await VerifierNomLibreAsync(db.Joueurs, nom, exclu: id, "Un joueur", annulation);

        joueur.Nom = nom;
        await db.SaveChangesAsync(annulation);

        return new JoueurDto(joueur.Id, joueur.Nom);
    }

    public async Task<bool> SupprimerJoueurAsync(int id, CancellationToken annulation = default)
    {
        var joueur = await db.Joueurs.FirstOrDefaultAsync(j => j.Id == id, annulation);
        if (joueur is null)
        {
            return false;
        }

        if (await db.MatchJeux.AnyAsync(mj => mj.JoueurId == id, annulation))
        {
            throw new RequeteInvalideException(
                "id",
                "Ce joueur a des resultats enregistres : supprimer d'abord les matchs concernes.");
        }

        if (await db.EquipeJoueurs.AnyAsync(ej => ej.JoueurId == id, annulation))
        {
            throw new RequeteInvalideException(
                "id",
                "Ce joueur fait partie d'une equipe : le retirer du roster avant de le supprimer.");
        }

        db.Joueurs.Remove(joueur);
        await db.SaveChangesAsync(annulation);
        return true;
    }

    public async Task<IReadOnlyList<JeuDto>> ListerJeuxAsync(CancellationToken annulation = default) =>
        await db.Jeux
            .AsNoTracking()
            .OrderBy(j => j.Nom)
            .Select(j => new JeuDto(j.Id, j.Nom))
            .ToListAsync(annulation);

    public async Task<JeuDto> CreerJeuAsync(JeuUpsertRequest requete, CancellationToken annulation = default)
    {
        var nom = ValiderNom(requete.Nom, "nom", longueurMax: 200);
        await VerifierNomLibreAsync(db.Jeux, nom, exclu: null, "Un jeu", annulation);

        var jeu = new Jeu { Nom = nom };
        db.Jeux.Add(jeu);
        await db.SaveChangesAsync(annulation);

        return new JeuDto(jeu.Id, jeu.Nom);
    }

    public async Task<JeuDto?> ModifierJeuAsync(
        int id,
        JeuUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var jeu = await db.Jeux.FirstOrDefaultAsync(j => j.Id == id, annulation);
        if (jeu is null)
        {
            return null;
        }

        var nom = ValiderNom(requete.Nom, "nom", longueurMax: 200);
        await VerifierNomLibreAsync(db.Jeux, nom, exclu: id, "Un jeu", annulation);

        jeu.Nom = nom;
        await db.SaveChangesAsync(annulation);

        return new JeuDto(jeu.Id, jeu.Nom);
    }

    public async Task<bool> SupprimerJeuAsync(int id, CancellationToken annulation = default)
    {
        var jeu = await db.Jeux.FirstOrDefaultAsync(j => j.Id == id, annulation);
        if (jeu is null)
        {
            return false;
        }

        if (await db.MatchJeux.AnyAsync(mj => mj.JeuId == id, annulation))
        {
            throw new RequeteInvalideException(
                "id",
                "Ce jeu a ete joue dans au moins un match : supprimer d'abord les matchs concernes.");
        }

        db.Jeux.Remove(jeu);
        await db.SaveChangesAsync(annulation);
        return true;
    }

    private static string ValiderNom(string? nom, string champ, int longueurMax)
    {
        var propre = nom?.Trim();

        if (string.IsNullOrEmpty(propre))
        {
            throw new RequeteInvalideException(champ, "Le nom est obligatoire.");
        }

        if (propre.Length > longueurMax)
        {
            throw new RequeteInvalideException(champ, $"Le nom ne peut pas depasser {longueurMax} caracteres.");
        }

        return propre;
    }

    /// <summary>Refuse un nom deja pris, en ignorant la casse comme le fait le collation SQL.</summary>
    private static async Task VerifierNomLibreAsync<T>(
        IQueryable<T> source,
        string nom,
        int? exclu,
        string libelle,
        CancellationToken annulation)
        where T : class, INomme
    {
        var dejaPris = await source.AnyAsync(
            e => e.Nom == nom && (exclu == null || e.Id != exclu),
            annulation);

        if (dejaPris)
        {
            throw new RequeteInvalideException("nom", $"{libelle} porte deja le nom \"{nom}\".");
        }
    }
}
