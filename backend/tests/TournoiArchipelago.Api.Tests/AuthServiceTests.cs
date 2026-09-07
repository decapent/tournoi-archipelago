using Microsoft.Extensions.Options;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Infrastructure;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Tests;

public class AuthServiceTests
{
    private const string Username = "admin";
    private const string MotDePasse = "MotDePasseDeTest";

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("  admin  ")]
    public void Le_nom_d_utilisateur_est_accepte_quelle_que_soit_la_casse(string saisie)
    {
        var session = Service().Authentifier(new LoginRequest(saisie, MotDePasse));

        Assert.NotNull(session);
        // Le jeton porte toujours le nom configure, pas la variante saisie.
        Assert.Equal(Username, session.Username);
        Assert.NotEmpty(session.Token);
    }

    [Theory]
    [InlineData("motdepassedetest")]
    [InlineData("MOTDEPASSEDETEST")]
    [InlineData(" MotDePasseDeTest")]
    [InlineData("MotDePasseDeTes")]
    [InlineData("")]
    [InlineData(null)]
    public void Le_mot_de_passe_reste_sensible_a_la_casse_et_aux_espaces(string? saisie)
    {
        Assert.Null(Service().Authentifier(new LoginRequest(Username, saisie)));
    }

    [Fact]
    public void Un_nom_d_utilisateur_inconnu_est_refuse()
    {
        Assert.Null(Service().Authentifier(new LoginRequest("quelqu-un-dautre", MotDePasse)));
    }

    [Fact]
    public void Un_nom_d_utilisateur_absent_est_refuse()
    {
        Assert.Null(Service().Authentifier(new LoginRequest(null, MotDePasse)));
    }

    [Fact]
    public void Aucune_connexion_n_est_possible_quand_le_mot_de_passe_n_est_pas_configure()
    {
        var sansMotDePasse = Service(motDePasse: string.Empty);

        Assert.Null(sansMotDePasse.Authentifier(new LoginRequest(Username, string.Empty)));
        Assert.Null(sansMotDePasse.Authentifier(new LoginRequest(Username, MotDePasse)));
    }

    [Fact]
    public void Le_jeton_expire_apres_la_duree_configuree()
    {
        var avant = DateTimeOffset.UtcNow;
        var session = Service().Authentifier(new LoginRequest(Username, MotDePasse));

        Assert.NotNull(session);
        Assert.InRange(
            session.ExpireLe,
            avant.AddHours(8).AddSeconds(-30),
            DateTimeOffset.UtcNow.AddHours(8).AddSeconds(30));
    }

    private static AuthService Service(string motDePasse = MotDePasse) => new(
        Options.Create(new AdminOptions { Username = Username, Password = motDePasse }),
        Options.Create(new JwtOptions
        {
            Key = "cle-de-test-suffisamment-longue-pour-hs256-0123456789",
            DureeHeures = 8,
        }));
}
