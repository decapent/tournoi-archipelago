using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class MatchJeuConfiguration : IEntityTypeConfiguration<MatchJeu>
{
    public void Configure(EntityTypeBuilder<MatchJeu> builder)
    {
        // Un temps nul n'est pas representable : EF pose un DEFAULT 0 en rendant la colonne
        // obligatoire, ce qui suffirait a laisser passer une ligne sans temps.
        builder.ToTable("MatchJeu", t => t.HasCheckConstraint(
            "CK_MatchJeu_temps_positif",
            "[temps_final_secs] > 0"));
        builder.HasKey(mj => new { mj.MatchId, mj.JeuId, mj.JoueurId }).HasName("PK_MatchJeu");

        builder.Property(mj => mj.MatchId).HasColumnName("match_id");
        builder.Property(mj => mj.JeuId).HasColumnName("jeu_id");
        builder.Property(mj => mj.JoueurId).HasColumnName("joueur_id");

        builder.Property(mj => mj.Seed).HasColumnName("seed").HasMaxLength(200);
        builder.Property(mj => mj.TotalChecks).HasColumnName("total_checks");
        builder.Property(mj => mj.NbChecks).HasColumnName("nb_checks");

        // Temps brut en secondes : completion, ou instant de l'abandon. Toujours renseigne ;
        // la penalite d'abandon est calculee par ScoringService, jamais stockee.
        builder.Property(mj => mj.TempsFinalSecs).HasColumnName("temps_final_secs").IsRequired();
        builder.Property(mj => mj.EstAbandon).HasColumnName("est_abandon").IsRequired();

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
