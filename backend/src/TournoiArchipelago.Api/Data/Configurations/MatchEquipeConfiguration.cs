using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data.Configurations;

public class MatchEquipeConfiguration : IEntityTypeConfiguration<MatchEquipe>
{
    public void Configure(EntityTypeBuilder<MatchEquipe> builder)
    {
        builder.ToTable("MatchEquipe");

        // La cle composite interdit deja d'engager deux fois la meme equipe dans un match.
        builder.HasKey(me => new { me.MatchId, me.EquipeId }).HasName("PK_MatchEquipe");

        builder.Property(me => me.MatchId).HasColumnName("match_id");
        builder.Property(me => me.EquipeId).HasColumnName("equipe_id");

        builder.HasOne(me => me.Match)
            .WithMany(m => m.Equipes)
            .HasForeignKey(me => me.MatchId)
            .HasConstraintName("FK_MatchEquipe_Match")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(me => me.Equipe)
            .WithMany()
            .HasForeignKey(me => me.EquipeId)
            .HasConstraintName("FK_MatchEquipe_Equipe")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
