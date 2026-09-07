using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class EquipeJoueurConfiguration : IEntityTypeConfiguration<EquipeJoueur>
{
    public void Configure(EntityTypeBuilder<EquipeJoueur> builder)
    {
        builder.ToTable("EquipeJoueur");
        builder.HasKey(ej => new { ej.EquipeId, ej.JoueurId }).HasName("PK_EquipeJoueur");

        builder.Property(ej => ej.EquipeId).HasColumnName("equipe_id");
        builder.Property(ej => ej.JoueurId).HasColumnName("joueur_id");

        builder.HasOne(ej => ej.Equipe)
            .WithMany(e => e.Membres)
            .HasForeignKey(ej => ej.EquipeId)
            .HasConstraintName("FK_EquipeJoueur_Equipe")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ej => ej.Joueur)
            .WithMany(j => j.Appartenances)
            .HasForeignKey(ej => ej.JoueurId)
            .HasConstraintName("FK_EquipeJoueur_Joueur")
            .OnDelete(DeleteBehavior.NoAction);

        // Un joueur ne peut appartenir qu'a une seule equipe. La contrainte est desormais
        // portee par la base, et non plus seulement par la couche service.
        builder.HasIndex(ej => ej.JoueurId)
            .HasDatabaseName("UQ_EquipeJoueur_joueur")
            .IsUnique();
    }
}
