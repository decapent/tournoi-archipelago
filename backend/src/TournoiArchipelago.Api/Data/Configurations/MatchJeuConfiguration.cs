using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class MatchJeuConfiguration : IEntityTypeConfiguration<MatchJeu>
{
    public void Configure(EntityTypeBuilder<MatchJeu> builder)
    {
        builder.ToTable("MatchJeu");
        builder.HasKey(mj => new { mj.MatchId, mj.JeuId, mj.JoueurId }).HasName("PK_MatchJeu");

        builder.Property(mj => mj.MatchId).HasColumnName("match_id");
        builder.Property(mj => mj.JeuId).HasColumnName("jeu_id");
        builder.Property(mj => mj.JoueurId).HasColumnName("joueur_id");

        builder.Property(mj => mj.Seed).HasColumnName("seed").HasMaxLength(200);
        builder.Property(mj => mj.TotalChecks).HasColumnName("total_checks");
        builder.Property(mj => mj.NbChecks).HasColumnName("nb_checks");

        // Nombre de secondes. NULL = abandon.
        builder.Property(mj => mj.TempsFinalSecs).HasColumnName("temps_final_secs");

        builder.Ignore(mj => mj.EstAbandon);
        builder.Ignore(mj => mj.PourcentComplete);

        builder.HasOne(mj => mj.Match)
            .WithMany(m => m.MatchJeux)
            .HasForeignKey(mj => mj.MatchId)
            .HasConstraintName("FK_MatchJeu_Match")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mj => mj.Jeu)
            .WithMany(j => j.MatchJeux)
            .HasForeignKey(mj => mj.JeuId)
            .HasConstraintName("FK_MatchJeu_Jeu")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(mj => mj.Joueur)
            .WithMany(j => j.MatchJeux)
            .HasForeignKey(mj => mj.JoueurId)
            .HasConstraintName("FK_MatchJeu_Joueur")
            .OnDelete(DeleteBehavior.NoAction);

        // Un joueur ne joue qu'un seul jeu par match : le score d'une equipe est la somme
        // de exactement deux lignes.
        builder.HasIndex(mj => new { mj.MatchId, mj.JoueurId })
            .HasDatabaseName("UQ_MatchJeu_match_joueur")
            .IsUnique();
    }
}
