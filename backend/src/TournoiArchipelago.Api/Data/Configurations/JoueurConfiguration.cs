using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class JoueurConfiguration : IEntityTypeConfiguration<Joueur>
{
    public void Configure(EntityTypeBuilder<Joueur> builder)
    {
        builder.ToTable("Joueur");

        // Le nom de contrainte existant est en minuscules, contrairement aux autres tables.
        builder.HasKey(j => j.Id).HasName("PK_joueur");

        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.Nom)
            .HasColumnName("nom")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(j => j.Nom)
            .HasDatabaseName("UQ_Joueur_nom")
            .IsUnique();
    }
}
