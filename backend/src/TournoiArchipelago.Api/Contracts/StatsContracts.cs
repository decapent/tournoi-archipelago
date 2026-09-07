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
    int Abandons);

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
