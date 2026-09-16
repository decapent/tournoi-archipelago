using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class IndiceHorodateConfiguration : IEntityTypeConfiguration<IndiceHorodate>
{
    public void Configure(EntityTypeBuilder<IndiceHorodate> builder)
    {
        builder.ToTable("IndiceHorodate");
        builder.HasKey(i => i.Id).HasName("PK_IndiceHorodate");

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.MatchId).HasColumnName("match_id");
        builder.Property(i => i.JoueurId).HasColumnName("joueur_id");
        builder.Property(i => i.Secondes).HasColumnName("secondes");
        builder.Property(i => i.PointsRestants).HasColumnName("points_restants");

        // Le resultat est stocke en clair : une table de tournoi amical se lit a la main.
        builder.Property(i => i.Resultat)
            .HasColumnName("resultat")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(i => i.Match)
            .WithMany(m => m.Indices)
            .HasForeignKey(i => i.MatchId)
            .HasConstraintName("FK_IndiceHorodate_Match")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Joueur)
            .WithMany()
            .HasForeignKey(i => i.JoueurId)
            .HasConstraintName("FK_IndiceHorodate_Joueur")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(i => new { i.MatchId, i.JoueurId })
            .HasDatabaseName("IX_IndiceHorodate_match_joueur");
    }
}
