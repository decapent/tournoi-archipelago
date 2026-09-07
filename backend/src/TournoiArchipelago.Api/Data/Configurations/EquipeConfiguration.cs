using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class EquipeConfiguration : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        // Le nom vide n'est pas representable : EF ajoute un DEFAULT N'' en creant la
        // colonne, ce qui a suffi a laisser subsister une equipe sans nom.
        builder.ToTable("Equipe", t => t.HasCheckConstraint("CK_Equipe_nom_non_vide", "[nom] <> ''"));
        builder.HasKey(e => e.Id).HasName("PK_Equipe");

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(e => e.Nom)
            .HasColumnName("nom")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(e => e.Nom)
            .HasDatabaseName("UQ_Equipe_nom")
            .IsUnique();

        builder.Ignore(e => e.MembreIds);
    }
}
