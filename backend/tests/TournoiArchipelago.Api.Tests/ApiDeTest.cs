using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Data;

namespace TournoiArchipelago.Api.Tests;

/// <summary>
/// Heberge l'API complete sur une base SQLite en memoire, pour exercer le pipeline reel
/// (routage, autorisation, serialisation, gestion d'erreurs).
/// </summary>
internal sealed class ApiDeTest : WebApplicationFactory<Program>
{
    public const string Username = "admin-test";
    public const string MotDePasse = "motdepasse-de-test";

    private readonly SqliteConnection _connexion = new("Filename=:memory:");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public ApiDeTest()
    {
        _connexion.Open();
    }

    /// <summary>Client sans jeton : seules les lectures doivent aboutir.</summary>
    public HttpClient CreerClientAnonyme() => CreateClient();

    /// <summary>Client authentifie comme admin.</summary>
    public async Task<HttpClient> CreerClientAdminAsync()
    {
        var client = CreateClient();

        var reponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(Username, MotDePasse),
            Json);

        reponse.EnsureSuccessStatusCode();
        var session = await reponse.Content.ReadFromJsonAsync<LoginResponse>(Json);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        return client;
    }

    /// <summary>Contexte sur la meme base, pour preparer ou verifier des donnees.</summary>
    public TournoiDbContext CreerContexte() =>
        new(new DbContextOptionsBuilder<TournoiDbContext>().UseSqlite(_connexion).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Tournoi"] = "remplacee-par-sqlite",
                ["Admin:Username"] = Username,
                ["Admin:Password"] = MotDePasse,
                ["Jwt:Key"] = "cle-de-test-suffisamment-longue-pour-hs256-0123456789",
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TournoiDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TournoiDbContext>>();
            services.AddDbContext<TournoiDbContext>(options => options.UseSqlite(_connexion));

            using var portee = services.BuildServiceProvider().CreateScope();
            portee.ServiceProvider.GetRequiredService<TournoiDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connexion.Dispose();
        }
    }
}
