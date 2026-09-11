using System.Globalization;
using System.Text.RegularExpressions;
using TournoiArchipelago.Api.Contracts;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Lit un journal de serveur Archipelago et en tire, par joueur, le nombre de checks trouves,
/// le total de son monde et l'instant de sa completion.
///
/// Le point delicat est la LIBERATION. Quand un joueur atteint son objectif, le serveur vide
/// d'un coup toutes les localisations qu'il n'avait pas trouvees, sous la meme forme
/// « X sent ... to Y (lieu) ». Sur le journal de reference, cela represente 229 lignes sur 446
/// pour un joueur : compter naivement les lignes doublerait son score.
///
/// Deux consequences :
///   - les checks REELLEMENT trouves sont ceux horodates STRICTEMENT avant l'objectif du
///     joueur ;
///   - le total de son monde est le nombre total de ses lignes, la liberation ayant justement
///     enumere le reste. Ce total n'est donc connu que pour un joueur ayant termine.
/// </summary>
public static partial class AnalyseurLogArchipelago
{
    /// <summary>Nombre de lignes au-dela duquel on refuse le fichier.</summary>
    public const int MaxLignes = 200_000;

    public static RapportLogDto Analyser(string contenu)
    {
        var evenements = new List<(DateTime Horodatage, string Message)>();
        var lignesLues = 0;
        var lignesIgnorees = 0;

        foreach (var ligne in LireLignes(contenu))
        {
            lignesLues++;

            if (lignesLues > MaxLignes)
            {
                throw new Infrastructure.RequeteInvalideException(
                    "log",
                    $"Le journal depasse {MaxLignes:N0} lignes.");
            }

            var entete = Entete().Match(ligne);
            if (!entete.Success)
            {
                lignesIgnorees++;
                continue;
            }

            if (!DateTime.TryParseExact(
                    entete.Groups[1].Value,
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var horodatage))
            {
                lignesIgnorees++;
                continue;
            }

            horodatage = horodatage.AddMilliseconds(int.Parse(entete.Groups[2].Value, CultureInfo.InvariantCulture));
            evenements.Add((horodatage, entete.Groups[3].Value));
        }

        var checks = new List<(DateTime Horodatage, string Alias)>();
        var jeuParAlias = new Dictionary<string, string>(StringComparer.Ordinal);
        var objectifParAlias = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var signaux = new Dictionary<string, (int Nb, string Exemple)>(StringComparer.Ordinal);

        void Compter(string signal, string exemple)
        {
            signaux[signal] = signaux.TryGetValue(signal, out var d)
                ? (d.Nb + 1, d.Exemple)
                : (1, exemple);
        }

        foreach (var (horodatage, message) in evenements)
        {
            var check = Check().Match(message);
            if (check.Success)
            {
                checks.Add((horodatage, check.Groups[1].Value));
                Compter(SignauxLog.Check, message);
                continue;
            }

            var connexion = Connexion().Match(message);
            if (connexion.Success)
            {
                // Un joueur peut se reconnecter : le premier jeu annonce fait foi.
                jeuParAlias.TryAdd(connexion.Groups[1].Value, connexion.Groups[2].Value.Trim());
                Compter(SignauxLog.Connexion, message);
                continue;
            }

            var objectif = Objectif().Match(message);
            if (objectif.Success)
            {
                objectifParAlias.TryAdd(objectif.Groups[1].Value, horodatage);
                Compter(SignauxLog.Objectif, message);
                continue;
            }

            Compter(Classer(message), message);
        }

        var alias = checks.Select(c => c.Alias)
            .Concat(jeuParAlias.Keys)
            .Concat(objectifParAlias.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase);

        var joueurs = alias.Select(a =>
        {
            var siens = checks.Where(c => c.Alias == a).Select(c => c.Horodatage).OrderBy(t => t).ToList();
            var objectif = objectifParAlias.TryGetValue(a, out var o) ? o : (DateTime?)null;

            // Sans objectif, la liberation n'a pas eu lieu : toutes les lignes sont de vrais
            // checks, mais le total du monde reste inconnu.
            var trouves = objectif is null ? siens : [.. siens.Where(t => t < objectif.Value)];

            return new JoueurLogDto(
                Alias: a,
                Jeu: jeuParAlias.GetValueOrDefault(a),
                ChecksTrouves: trouves.Count,
                TotalChecks: objectif is null ? null : siens.Count,
                PremierCheck: trouves.Count > 0 ? trouves[0] : null,
                DernierCheck: trouves.Count > 0 ? trouves[^1] : null,
                Objectif: objectif,
                EstAbandon: objectif is null,
                Horodatages: trouves);
        }).ToList();

        return new RapportLogDto(
            Joueurs: joueurs,
            Signaux: [.. signaux
                .Select(s => new SignalLogDto(s.Key, s.Value.Nb, Tronquer(s.Value.Exemple)))
                .OrderByDescending(s => s.Occurrences)
                .ThenBy(s => s.Signal, StringComparer.Ordinal)],
            Debut: evenements.Count > 0 ? evenements[0].Horodatage : null,
            DepartEstime: EstimerDepart(joueurs),
            Fin: evenements.Count > 0 ? evenements[^1].Horodatage : null,
            LignesLues: lignesLues,
            LignesIgnorees: lignesIgnorees);
    }

    /// <summary>
    /// Silence au-dela duquel un check n'est plus considere comme faisant partie de la course.
    /// Une fois l'hote parti, les checks tombent sans repit : sur les deux journaux de
    /// reference, aucun silence de plus de trois minutes dans les trente premiers checks.
    /// </summary>
    private static readonly TimeSpan SilenceHorsCourse = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Approxime le depart au premier check de la course.
    ///
    /// Le vrai depart n'est nulle part dans le journal : seul l'hote le connait. Mais rien ne
    /// se passe avant qu'il ne lance, alors que les joueurs sont connectes depuis longtemps —
    /// quarante minutes d'attente sur le journal de reference. Le premier check marque donc le
    /// depart a quelques minutes pres, celles qu'il faut au plus rapide pour trouver sa
    /// premiere localisation.
    ///
    /// Un joueur qui tatonne avant le depart produirait un check isole, suivi d'un long
    /// silence : celui-la est ecarte.
    /// </summary>
    private static DateTime? EstimerDepart(IEnumerable<JoueurLogDto> joueurs)
    {
        var checks = joueurs.SelectMany(j => j.Horodatages).Order().ToList();
        if (checks.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < checks.Count - 1; i++)
        {
            if (checks[i + 1] - checks[i] <= SilenceHorsCourse)
            {
                return checks[i];
            }
        }

        // Que des checks isoles : le journal est trop maigre pour departager, on prend le premier.
        return checks[0];
    }

    /// <summary>Familles reconnues mais non exploitees pour le classement.</summary>
    private static string Classer(string message) => message switch
    {
        _ when message.Contains("has released all remaining", StringComparison.Ordinal) => SignauxLog.Liberation,
        _ when message.Contains("has collected their items", StringComparison.Ordinal) => SignauxLog.Collecte,
        _ when message.Contains("has completed all of their games", StringComparison.Ordinal) => SignauxLog.EquipeTerminee,
        _ when message.Contains("has left the game", StringComparison.Ordinal) => SignauxLog.Depart,
        _ when message.Contains("has stopped tracking", StringComparison.Ordinal) => SignauxLog.SuiviArrete,
        // Un tracker (PopTracker) se branche : distinct de « playing », qui est le joueur.
        _ when Suivi().IsMatch(message) => SignauxLog.SuiviDemarre,
        _ when message.Contains("[Hint]:", StringComparison.Ordinal) => SignauxLog.Indice,
        _ when message.StartsWith("Hosting game at", StringComparison.Ordinal) => SignauxLog.Hebergement,
        _ when message.StartsWith("Loading embedded data package", StringComparison.Ordinal) => SignauxLog.ChargementJeu,
        _ when message.StartsWith("Loaded save file", StringComparison.Ordinal) => SignauxLog.Sauvegarde,
        _ when message.StartsWith("Shutting down", StringComparison.Ordinal) => SignauxLog.Arret,
        _ when message.StartsWith("Notice (Player ", StringComparison.Ordinal) => SignauxLog.MessageServeur,
        _ when Bavardage().IsMatch(message) => SignauxLog.Bavardage,
        _ => SignauxLog.Inconnu,
    };

    private static IEnumerable<string> LireLignes(string contenu)
    {
        using var lecteur = new StringReader(contenu);
        while (lecteur.ReadLine() is { } ligne)
        {
            if (!string.IsNullOrWhiteSpace(ligne))
            {
                yield return ligne;
            }
        }
    }

    private static string Tronquer(string message) =>
        message.Length <= 120 ? message : message[..117] + "...";

    [GeneratedRegex(@"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}),(\d{1,3})\]: (.*)$")]
    private static partial Regex Entete();

    /// <summary>
    /// « (Team #1) W2FF4 sent Sword to W7Z1 (Baron Castle -- 1F) » : le check appartient a
    /// l'EXPEDITEUR, qui l'a trouve dans son monde. Le destinataire n'est que le proprietaire
    /// de l'objet.
    /// </summary>
    [GeneratedRegex(@"^\(Team #\d+\) (\S+) sent .+? to \S+ \(.+\)$")]
    private static partial Regex Check();

    [GeneratedRegex(@"^Notice \(all\): (\S+) \(Team #\d+\) playing (.+?) has joined\.")]
    private static partial Regex Connexion();

    [GeneratedRegex(@"^Notice \(all\): (\S+) \(Team #\d+\) has completed their goal\.$")]
    private static partial Regex Objectif();

    [GeneratedRegex(@"^Notice \(all\): \S+: ")]
    private static partial Regex Bavardage();

    [GeneratedRegex(@"^Notice \(all\): \S+ \(Team #\d+\) tracking .+ has joined\.")]
    private static partial Regex Suivi();
}

/// <summary>Noms des familles de lignes reconnues dans un journal.</summary>
public static class SignauxLog
{
    public const string Check = "check";
    public const string Connexion = "connexion";
    public const string Objectif = "objectif";
    public const string Liberation = "liberation";
    public const string Collecte = "collecte";
    public const string EquipeTerminee = "equipe-terminee";
    public const string Depart = "depart";
    public const string SuiviDemarre = "suivi-demarre";
    public const string SuiviArrete = "suivi-arrete";
    public const string Indice = "indice";
    public const string Bavardage = "bavardage";
    public const string MessageServeur = "message-serveur";
    public const string Hebergement = "hebergement";
    public const string ChargementJeu = "chargement-jeu";
    public const string Sauvegarde = "sauvegarde";
    public const string Arret = "arret";
    public const string Inconnu = "inconnu";
}
