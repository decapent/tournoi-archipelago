using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class CheckHorodateConfiguration : IEntityTypeConfiguration<CheckHorodate>
{
    public void Configure(EntityTypeBuilder<CheckHorodate> builder)
    {
        builder.ToTable("CheckHorodate");
        builder.HasKey(c => c.Id).HasName("PK_CheckHorodate");

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.MatchId).HasColumnName("match_id");
        builder.Property(c => c.JoueurId).HasColumnName("joueur_id");
        builder.Property(c => c.Secondes).HasColumnName("secondes");

        builder.HasOne(c => c.Match)
            .WithMany(m => m.Checks)
            .HasForeignKey(c => c.MatchId)
            .HasConstraintName("FK_CheckHorodate_Match")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Joueur)
            .WithMany()
            .HasForeignKey(c => c.JoueurId)
            .HasConstraintName("FK_CheckHorodate_Joueur")
            .OnDelete(DeleteBehavior.NoAction);

        // Tous les acces se font par match, souvent restreints a un joueur.
        builder.HasIndex(c => new { c.MatchId, c.JoueurId })
            .HasDatabaseName("IX_CheckHorodate_match_joueur");
    }
}
