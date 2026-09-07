namespace TournoiArchipelago.Api.Infrastructure;

/// <summary>
/// Regle metier non respectee dans une requete. Traduite en reponse 400 avec le detail
/// des champs fautifs par <see cref="GestionnaireExceptions"/>.
/// </summary>
public class RequeteInvalideException : Exception
{
    public RequeteInvalideException(string message)
        : this("requete", message)
    {
    }

    public RequeteInvalideException(string champ, string message)
        : base(message)
    {
        Erreurs = new Dictionary<string, string[]> { [champ] = [message] };
    }

    public RequeteInvalideException(IDictionary<string, string[]> erreurs)
        : base("La requete contient des erreurs de validation.")
    {
        Erreurs = erreurs;
    }

    public IDictionary<string, string[]> Erreurs { get; }
}
