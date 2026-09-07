using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Data;

namespace TournoiArchipelago.Api.Tests;

/// <summary>
/// Base SQLite en memoire creee depuis le modele EF Core. La connexion reste ouverte pendant
/// toute la duree du test : une base SQLite en memoire disparait des que sa derniere connexion
/// se ferme.
/// </summary>
internal sealed class ContexteDeTest : IDisposable
{
    private readonly SqliteConnection _connexion;

    public ContexteDeTest()
    {
        _connexion = new SqliteConnection("Filename=:memory:");
        _connexion.Open();

        Db = Creer();
        Db.Database.EnsureCreated();
    }

    public TournoiDbContext Db { get; }

    /// <summary>Nouveau contexte sur la meme base, pour repartir d'un cache vide.</summary>
    public TournoiDbContext Creer() =>
        new(new DbContextOptionsBuilder<TournoiDbContext>().UseSqlite(_connexion).Options);

    public void Dispose()
    {
        Db.Dispose();
        _connexion.Dispose();
    }
}
