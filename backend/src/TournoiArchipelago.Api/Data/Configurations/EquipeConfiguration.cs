using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class EquipeConfiguration : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        builder.ToTable("Equipe", t => t.HasCheckConstraint(
            "CK_Equipe_membres_distincts",
            "[joueur1_id] <> [joueur2_id]"));

        builder.HasKey(e => e.Id).HasName("PK_Equipe");

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Joueur1Id).HasColumnName("joueur1_id");
        builder.Property(e => e.Joueur2Id).HasColumnName("joueur2_id");

        builder.HasOne(e => e.Joueur1)
            .WithMany()
            .HasForeignKey(e => e.Joueur1Id)
            .HasConstraintName("FK_Equipe_Joueur1")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Joueur2)
            .WithMany()
            .HasForeignKey(e => e.Joueur2Id)
            .HasConstraintName("FK_Equipe_Joueur2")
            .OnDelete(DeleteBehavior.NoAction);

        // Les identifiants sont normalises (le plus petit dans joueur1_id) par EquipeService,
        // ce qui permet a cet index d'attraper aussi les duos saisis dans l'ordre inverse.
        builder.HasIndex(e => new { e.Joueur1Id, e.Joueur2Id })
            .HasDatabaseName("UQ_Equipe_joueurs")
            .IsUnique();

        builder.Ignore(e => e.MembreIds);
    }
}
