namespace TournoiArchipelago.Api.Contracts;

public record ClassementEquipeDto(
    int Position,
    int EquipeId,
    string EquipeNom,
    int MatchsJoues,
    int Victoires,
    int TempsCumuleSecs,
    double? TempsMoyenSecs,
    int ChecksTrouves,
    double? PourcentCompleteMoyen,
    int Abandons,
    /// <summary>Total des penalites d'abandon comprises dans le temps cumule.</summary>
    int PenaliteCumuleeSecs);

/// <summary>
/// Bilan d'un joueur sur le tournoi. Un joueur court une seule seed par match : le nombre de
/// seeds jouees est donc aussi son nombre de matchs.
/// </summary>
public record StatsJoueurDto(
    int JoueurId,
    string JoueurNom,
    int? EquipeId,
    string EquipeNom,
    int SeedsJouees,
    /// <summary>Matchs complets remportes par son equipe.</summary>
    int Victoires,
    /// <summary>Moyenne des seeds terminees. Les abandons sont exclus : ils ne mesurent pas une completion.</summary>
    double? TempsMoyenSecs,
    int? TempsMedianSecs,
    int? MeilleurTempsSecs,
    string? MeilleurJeuNom,
    int ChecksTrouves,
    double? PourcentCompleteMoyen,
    /// <summary>Rythme sur les seeds terminees : checks trouves par heure de jeu.</summary>
    double? ChecksParHeure,
    int NbAbandons,
    /// <summary>
    /// Nombre de fois ou sa seed a fixe le temps de son equipe, en etant la plus longue. Une
    /// mesure de qui fait attendre les autres.
    /// </summary>
    int SeedsDeterminantes);

public record StatsJeuDto(
    int JeuId,
    string JeuNom,
    int NbParties,
    double? TempsMoyenSecs,
    int? TempsMedianSecs,
    int? MeilleurTempsSecs,
    string? MeilleurJoueurNom,
    double? NbChecksMoyen,
    double? PourcentCompleteMoyen,
    int NbAbandons);
