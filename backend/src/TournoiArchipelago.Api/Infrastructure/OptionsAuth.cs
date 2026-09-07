using System.ComponentModel.DataAnnotations;

namespace TournoiArchipelago.Api.Infrastructure;

/// <summary>Identifiants du panneau d'admin, fournis par la configuration.</summary>
public class AdminOptions
{
    public const string Section = "Admin";

    [Required(AllowEmptyStrings = false)]
    public string Username { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Definir Admin__Password (voir .env.example).")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Parametres de signature des jetons.</summary>
public class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>Longueur minimale de la cle, imposee par l'algorithme HS256.</summary>
    public const int LongueurCleMinimale = 32;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Definir Jwt__Key (voir .env.example).")]
    [MinLength(LongueurCleMinimale, ErrorMessage = "Jwt__Key doit faire au moins 32 caracteres.")]
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "tournoi-archipelago";

    public string Audience { get; set; } = "tournoi-archipelago";

    [Range(1, 24 * 30)]
    public int DureeHeures { get; set; } = 8;
}
