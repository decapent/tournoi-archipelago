namespace TournoiArchipelago.Api.Contracts;

/// <summary>Ce qu'une demande d'indice a donne.</summary>
public enum ResultatIndice
{
    /// <summary>Le serveur a revele au moins un emplacement.</summary>
    Obtenu,

    /// <summary>Refusee faute de points d'indice.</summary>
    Refuse,

    /// <summary>
    /// Restee sans reponse : le nom d'objet ne correspond a rien. Le joueur cherche son
    /// orthographe et reessaie aussitot, comme « !hint lantern » suivi de « !hint lamp ».
    /// </summary>
    SansReponse,
}

/// <summary>
/// Une demande d'indice, telle que le journal la raconte. Une demande peut reveler plusieurs
/// emplacements quand le nom d'objet est ambigu, et redemander un indice deja obtenu le
/// reaffiche sans rien couter.
/// </summary>
public record IndiceLogDto(
    DateTime Horodatage,
    /// <summary>Terme cherche, tel que tape par le joueur.</summary>
    string Terme,
    ResultatIndice Resultat,
    /// <summary>Solde de points au moment de la demande, quand le serveur l'annonce.</summary>
    int? PointsRestants,
    /// <summary>Prix d'un indice, proportionnel a la taille du monde, quand il est annonce.</summary>
    int? Cout);

/// <summary>Ce qu'un journal Archipelago apprend sur un joueur.</summary>
public record JoueurLogDto(
    /// <summary>Pseudonyme dans la partie, a rapprocher d'un joueur du roster.</summary>
    string Alias,
    /// <summary>Jeu annonce a la connexion, tel qu'ecrit par Archipelago.</summary>
    string? Jeu,
    /// <summary>Checks trouves avant l'objectif. Exclut la liberation qui suit la completion.</summary>
    int ChecksTrouves,
    /// <summary>
    /// Nombre total de localisations du monde. Connu seulement si le joueur a termine : c'est
    /// la liberation qui enumere celles qu'il n'avait pas trouvees.
    /// </summary>
    int? TotalChecks,
    DateTime? PremierCheck,
    DateTime? DernierCheck,
    /// <summary>Instant de la completion, ou <c>null</c> si le joueur n'a jamais termine.</summary>
    DateTime? Objectif,
    /// <summary>Vrai en l'absence d'objectif : le joueur n'a pas fini sa seed.</summary>
    bool EstAbandon,
    /// <summary>Horodatage de chaque check trouve, pour tracer la progression.</summary>
    IReadOnlyList<DateTime> Horodatages,
    /// <summary>
    /// Horodatage des lignes emises par une rafale de collecte ou de liberation. Elles ne
    /// comptent pas comme des checks trouves, mais la courbe les montre : ce sont elles qui
    /// la menent jusqu'a la taille du monde, a l'instant exact de la completion.
    /// </summary>
    IReadOnlyList<DateTime> HorodatagesRafale,
    /// <summary>Chaque demande d'indice, dans l'ordre.</summary>
    IReadOnlyList<IndiceLogDto> Indices,
    /// <summary>
    /// Emplacements differents reellement reveles au joueur. Plus petit que le nombre de
    /// demandes abouties : redemander un indice connu le reaffiche.
    /// </summary>
    int IndicesDistincts,
    /// <summary>
    /// Parmi eux, ceux qui pointaient un lieu deja visite. L'indice n'apprend alors rien.
    /// </summary>
    int IndicesDejaTrouves);

/// <summary>Une famille de lignes reconnue dans le journal, avec un exemple.</summary>
public record SignalLogDto(string Signal, int Occurrences, string Exemple);

/// <summary>Resultat de l'analyse d'un journal, avant tout rapprochement avec le tournoi.</summary>
public record RapportLogDto(
    IReadOnlyList<JoueurLogDto> Joueurs,
    IReadOnlyList<SignalLogDto> Signaux,
    /// <summary>Premiere ligne horodatee du journal : le serveur demarre, pas la course.</summary>
    DateTime? Debut,
    /// <summary>
    /// Depart approxime au premier check de la course. Les joueurs se connectent longtemps
    /// avant que l'hote ne lance -- quarante minutes sur le journal de reference -- et le vrai
    /// depart n'apparait nulle part dans le journal. L'admin corrige au besoin.
    /// </summary>
    DateTime? DepartEstime,
    DateTime? Fin,
    int LignesLues,
    int LignesIgnorees);

/// <summary>Journal soumis pour analyse, sans rien enregistrer.</summary>
public record AnalyseLogRequest(string? Contenu);

/// <summary>Rapprochement d'un pseudonyme du journal avec un joueur du roster.</summary>
public record CorrespondanceAliasDto(string Alias, int JoueurId);

/// <summary>
/// Import d'un journal pour une equipe d'un match. Le depart est commun a tous les joueurs :
/// c'est de lui que se compte le temps de chacun.
/// </summary>
public record ImportLogRequest(
    string? Contenu,
    DateTime DepartCourse,
    IReadOnlyList<CorrespondanceAliasDto>? Correspondances);

/// <summary>Une demande d'indice situee dans la course, en secondes depuis le depart.</summary>
public record IndiceProgressionDto(int Secondes, ResultatIndice Resultat, int? PointsRestants);

/// <summary>Progression d'un joueur, en secondes depuis le depart de la course.</summary>
public record ProgressionJoueurDto(
    int JoueurId,
    string JoueurNom,
    int? EquipeId,
    string EquipeNom,
    string JeuNom,
    /// <summary>Taille du monde, pour situer la progression par rapport a son terme.</summary>
    int? TotalChecks,
    /// <summary>Instants des checks reellement trouves.</summary>
    IReadOnlyList<int> Secondes,
    /// <summary>
    /// Instants des lignes de rafale. La courbe les trace a la suite des precedents : elle
    /// rejoint ainsi la taille du monde a l'instant de la completion.
    /// </summary>
    IReadOnlyList<int> SecondesRafale,
    /// <summary>Ses demandes d'indice, pour la frise qui accompagne la courbe.</summary>
    IReadOnlyList<IndiceProgressionDto> Indices);

/// <summary>Courbes de progression d'un match, pretes a tracer.</summary>
public record ProgressionMatchDto(
    int MatchId,
    IReadOnlyList<ProgressionJoueurDto> Joueurs);
