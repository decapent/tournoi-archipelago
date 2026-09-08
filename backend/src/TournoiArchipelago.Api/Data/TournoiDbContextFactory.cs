using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TournoiArchipelago.Api.Data;

/// <summary>
/// Fabrique utilisee par les outils EF Core (<c>dotnet ef migrations</c> /
/// <c>database update</c>).
///
/// Ecrire une migration ne demande que le modele et le fournisseur, pas une base joignable :
/// la chaine de connexion retombe donc sur l'instance locale. La surcharger avec la variable
/// d'environnement <c>TOURNOI_MIGRATIONS_CONNECTION</c> pour viser une vraie base, ce que fait
/// le job de migration de la CI.
///
/// Sans cette fabrique, les outils construiraient l'hote web complet, qui exige des secrets
/// (mot de passe d'admin, cle JWT) sans rapport avec le schema.
/// </summary>
public sealed class TournoiDbContextFactory : IDesignTimeDbContextFactory<TournoiDbContext>
{
    private const string ConnexionLocaleParDefaut =
        "Server=localhost;Database=Archipelago;Trusted_Connection=True;TrustServerCertificate=True";

    public TournoiDbContext CreateDbContext(string[] args)
    {
        var connexion =
            Environment.GetEnvironmentVariable("TOURNOI_MIGRATIONS_CONNECTION")
            ?? ConnexionLocaleParDefaut;

        var options = new DbContextOptionsBuilder<TournoiDbContext>()
            .UseSqlServer(connexion)
            .Options;

        return new TournoiDbContext(options);
    }
}
