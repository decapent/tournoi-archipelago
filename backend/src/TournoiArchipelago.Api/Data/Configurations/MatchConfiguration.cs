using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Match");
        builder.HasKey(m => m.Id).HasName("PK_Match");

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Date).HasColumnName("date");

        // Stocke QUALIFICATION / TOURNOI en clair pour rester lisible en SQL.
        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Ignore(m => m.EquipeIds);

        builder.HasIndex(m => m.Date).HasDatabaseName("IX_Match_date");
    }
}
