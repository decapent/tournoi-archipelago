using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Infrastructure;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Authentification du panneau d'admin : un unique compte, defini par la configuration.
/// Suffisant pour un tournoi amical heberge en local ; a remplacer par un vrai fournisseur
/// d'identite si l'application devient accessible publiquement.
/// </summary>
public class AuthService(IOptions<AdminOptions> admin, IOptions<JwtOptions> jwt)
{
    private readonly AdminOptions _admin = admin.Value;
    private readonly JwtOptions _jwt = jwt.Value;

    /// <summary>Renvoie un jeton signe, ou <c>null</c> si les identifiants sont refuses.</summary>
    public LoginResponse? Authentifier(LoginRequest requete)
    {
        if (!Correspond(requete.Username, _admin.Username) || !Correspond(requete.Password, _admin.Password))
        {
            return null;
        }

        var expiration = DateTimeOffset.UtcNow.AddHours(_jwt.DureeHeures);

        var jeton = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, _admin.Username),
                new Claim(ClaimTypes.Name, _admin.Username),
                new Claim(ClaimTypes.Role, RolesTournoi.Admin),
            ],
            notBefore: DateTime.UtcNow,
            expires: expiration.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
                SecurityAlgorithms.HmacSha256));

        return new LoginResponse(
            Token: new JwtSecurityTokenHandler().WriteToken(jeton),
            ExpireLe: expiration,
            Username: _admin.Username);
    }

    /// <summary>
    /// Comparaison a temps constant, pour ne pas laisser fuir la longueur ni le contenu
    /// attendu via le temps de reponse.
    /// </summary>
    private static bool Correspond(string? fourni, string? attendu)
    {
        if (string.IsNullOrEmpty(attendu))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(fourni ?? string.Empty),
            Encoding.UTF8.GetBytes(attendu));
    }
}

/// <summary>Noms de roles utilises par les politiques d'autorisation.</summary>
public static class RolesTournoi
{
    public const string Admin = "admin";
}

/// <summary>Noms de claims JWT enregistres, pour eviter les chaines en dur.</summary>
internal static class JwtRegisteredClaimNames
{
    public const string Sub = "sub";
}
