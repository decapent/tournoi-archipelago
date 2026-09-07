using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class JeuConfiguration : IEntityTypeConfiguration<Jeu>
{
    public void Configure(EntityTypeBuilder<Jeu> builder)
    {
        builder.ToTable("Jeu", t => t.HasCheckConstraint("CK_Jeu_nom_non_vide", "[nom] <> ''"));
        builder.HasKey(j => j.Id).HasName("PK_Jeu");

        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.Nom)
            .HasColumnName("nom")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(j => j.Nom)
            .HasDatabaseName("UQ_Jeu_nom")
            .IsUnique();

        builder.HasData(SeedData.Jeux);
    }
}
