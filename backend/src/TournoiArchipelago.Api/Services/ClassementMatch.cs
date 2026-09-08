using TournoiArchipelago.Api.Contracts;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Resultat du classement d'un match. <paramref name="EstComplet"/> vaut faux tant qu'un
/// resultat reste a saisir : le classement est alors indicatif, aucun vainqueur n'est designe
/// et les statistiques ignorent le match.
/// </summary>
public sealed record ClassementMatch(
    bool EstComplet,
    IReadOnlyList<EquipeResultatDto> Equipes);
