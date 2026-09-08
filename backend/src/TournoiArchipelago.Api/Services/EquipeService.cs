using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Infrastructure;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Gestion des equipes et de leur roster. Les equipes engagees dans un match sont portees par
/// MatchEquipe, mais rattacher un resultat a l'un des deux camps passe par l'appartenance du
/// joueur : un joueur ne peut donc figurer que dans une seule equipe, sans quoi ce
/// rattachement serait ambigu.
/// </summary>
public class EquipeService(TournoiDbContext db)
{
    /// <summary>Taille du roster : quatre joueurs, dont deux a quatre jouent selon l'etape.</summary>
    public const int TailleRoster = 4;

    public async Task<IReadOnlyList<EquipeDto>> ListerAsync(CancellationToken annulation = default)
    {
        var equipes = await ChargerAvecMembres().AsNoTracking().ToListAsync(annulation);

        return [.. equipes
            .Select(VersDto)
            .OrderBy(e => e.Nom, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<EquipeDto?> ObtenirAsync(int id, CancellationToken annulation = default)
    {
        var equipe = await ChargerAvecMembres().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, annulation);

        return equipe is null ? null : VersDto(equipe);
    }

    public async Task<EquipeDto> CreerAsync(
        EquipeUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var (nom, joueurIds, capitaineId) = await ValiderAsync(requete, equipeExclue: null, annulation);

        var equipe = new Equipe
        {
            Nom = nom,
            Membres = [.. joueurIds.Select(id => new EquipeJoueur
            {
                JoueurId = id,
                EstCapitaine = id == capitaineId,
            })],
        };

        db.Equipes.Add(equipe);
        await db.SaveChangesAsync(annulation);
        db.ChangeTracker.Clear();

        return (await ObtenirAsync(equipe.Id, annulation))!;
    }

    public async Task<EquipeDto?> ModifierAsync(
        int id,
        EquipeUpsertRequest requete,
        CancellationToken annulation = default)
    {
        var equipe = await db.Equipes
            .Include(e => e.Membres)
            .FirstOrDefaultAsync(e => e.Id == id, annulation);

        if (equipe is null)
        {
            return null;
        }

        var (nom, joueurIds, capitaineId) = await ValiderAsync(requete, equipeExclue: id, annulation);

        equipe.Nom = nom;

        // Le roster est remplace : on retire les membres partis et ajoute les nouveaux, plutot
        // que de tout supprimer, pour ne pas violer inutilement l'unicite sur joueur_id.
        foreach (var partant in equipe.Membres.Where(m => !joueurIds.Contains(m.JoueurId)).ToList())
        {
            equipe.Membres.Remove(partant);
        }

        var dejaMembres = equipe.Membres.Select(m => m.JoueurId).ToHashSet();
        foreach (var arrivantId in joueurIds.Where(joueurId => !dejaMembres.Contains(joueurId)))
        {
            equipe.Membres.Add(new EquipeJoueur { EquipeId = id, JoueurId = arrivantId });
        }

        // Le capitaine est retire avant d'etre attribue : l'index filtre n'en tolere qu'un,
        // et SaveChanges ne garantit pas l'ordre des mises a jour.
        foreach (var membre in equipe.Membres)
        {
            membre.EstCapitaine = false;
        }

        await db.SaveChangesAsync(annulation);

        if (capitaineId is not null)
        {
            equipe.Membres.Single(membre => membre.JoueurId == capitaineId.Value).EstCapitaine = true;
        }

        await db.SaveChangesAsync(annulation);
        db.ChangeTracker.Clear();

        return await ObtenirAsync(id, annulation);
    }

    public async Task<bool> SupprimerAsync(int id, CancellationToken annulation = default)
    {
        var equipe = await db.Equipes.FirstOrDefaultAsync(e => e.Id == id, annulation);
        if (equipe is null)
        {
            return false;
        }

        // Les lignes EquipeJoueur partent en cascade ; les resultats de match, eux, restent
        // rattaches aux joueurs et se retrouveraient sans equipe dans le classement.
        var membreIds = await db.EquipeJoueurs
            .Where(ej => ej.EquipeId == id)
            .Select(ej => ej.JoueurId)
            .ToListAsync(annulation);

        if (await db.MatchJeux.AnyAsync(mj => membreIds.Contains(mj.JoueurId), annulation))
        {
            throw new RequeteInvalideException(
                "id",
                "Cette equipe a des resultats enregistres : supprimer d'abord les matchs concernes.");
        }

        db.Equipes.Remove(equipe);
        await db.SaveChangesAsync(annulation);
        return true;
    }

    public static EquipeDto VersDto(Equipe equipe) => new(
        Id: equipe.Id,
        Nom: equipe.Nom,
        Membres: [.. equipe.Membres
            .Select(membre => new MembreDto(
                membre.JoueurId,
                membre.Joueur?.Nom ?? $"#{membre.JoueurId}",
                membre.EstCapitaine))
            // Le capitaine en tete, puis par ordre alphabetique.
            .OrderByDescending(membre => membre.EstCapitaine)
            .ThenBy(membre => membre.Nom, StringComparer.OrdinalIgnoreCase)]);

    private IQueryable<Equipe> ChargerAvecMembres() =>
        db.Equipes.Include(e => e.Membres).ThenInclude(m => m.Joueur);

    /// <summary>
    /// Valide le nom et le roster. Leve <see cref="RequeteInvalideException"/> en decrivant
    /// tous les problemes trouves.
    /// </summary>
    private async Task<(string Nom, List<int> JoueurIds, int? CapitaineId)> ValiderAsync(
        EquipeUpsertRequest requete,
        int? equipeExclue,
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

        var nom = requete.Nom?.Trim() ?? string.Empty;
        if (nom.Length == 0)
        {
            Ajouter("nom", "Le nom de l'equipe est obligatoire.");
        }
        else if (nom.Length > 100)
        {
            Ajouter("nom", "Le nom ne peut pas depasser 100 caracteres.");
        }
        else if (await db.Equipes.AnyAsync(
                     e => e.Nom == nom && (equipeExclue == null || e.Id != equipeExclue),
                     annulation))
        {
            Ajouter("nom", $"Une equipe porte deja le nom \"{nom}\".");
        }

        var joueurIds = (requete.JoueurIds ?? []).Distinct().ToList();

        if (joueurIds.Count != TailleRoster)
        {
            Ajouter(
                "joueurIds",
                $"Une equipe compte exactement {TailleRoster} joueurs distincts, {joueurIds.Count} recu(s).");
        }

        if (joueurIds.Count > 0)
        {
            var existants = await db.Joueurs
                .Where(j => joueurIds.Contains(j.Id))
                .Select(j => j.Id)
                .ToListAsync(annulation);

            var manquants = joueurIds.Except(existants).ToList();
            if (manquants.Count > 0)
            {
                Ajouter("joueurIds", $"Joueur introuvable : {string.Join(", ", manquants)}.");
            }

            var conflits = await db.EquipeJoueurs
                .Include(ej => ej.Equipe)
                .Where(ej => joueurIds.Contains(ej.JoueurId))
                .Where(ej => equipeExclue == null || ej.EquipeId != equipeExclue)
                .ToListAsync(annulation);

            foreach (var conflit in conflits)
            {
                Ajouter(
                    "joueurIds",
                    $"Le joueur {conflit.JoueurId} fait deja partie de l'equipe {conflit.Equipe?.Nom ?? conflit.EquipeId.ToString()}.");
            }
        }

        if (requete.CapitaineId is not null && !joueurIds.Contains(requete.CapitaineId.Value))
        {
            Ajouter("capitaineId", "Le capitaine doit faire partie du roster.");
        }

        if (erreurs.Count > 0)
        {
            throw new RequeteInvalideException(
                erreurs.ToDictionary(paire => paire.Key, paire => paire.Value.ToArray()));
        }

        return (nom, joueurIds, requete.CapitaineId);
    }
}
