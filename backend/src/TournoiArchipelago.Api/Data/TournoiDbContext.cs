using Microsoft.EntityFrameworkCore;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data;

public class TournoiDbContext(DbContextOptions<TournoiDbContext> options) : DbContext(options)
{
    public DbSet<Joueur> Joueurs => Set<Joueur>();

    public DbSet<Jeu> Jeux => Set<Jeu>();

    public DbSet<Equipe> Equipes => Set<Equipe>();

    public DbSet<EquipeJoueur> EquipeJoueurs => Set<EquipeJoueur>();

    public DbSet<Match> Matchs => Set<Match>();

    public DbSet<MatchJeu> MatchJeux => Set<MatchJeu>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TournoiDbContext).Assembly);
    }
}
