using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Regelwerke;

public class RankingRegelSetEintragKonfiguration : IEntityTypeConfiguration<RankingRegelSetEintrag>
{
    public void Configure(EntityTypeBuilder<RankingRegelSetEintrag> builder)
    {
        builder.ToTable("RankingRegelSetEintraege");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reihenfolge).IsRequired();

        builder.HasOne(e => e.RankingRegelSet)
            .WithMany(s => s.Eintraege)
            .HasForeignKey(e => e.RankingRegelSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.RankingRegel)
            .WithMany()
            .HasForeignKey(e => e.RankingRegelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RankingRegelSetId, e.Reihenfolge }).IsUnique();
    }
}
