using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Regelwerke;

public class RankingRegelSetKonfiguration : IEntityTypeConfiguration<RankingRegelSet>
{
    public void Configure(EntityTypeBuilder<RankingRegelSet> builder)
    {
        builder.ToTable("RankingRegelSets");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Beschreibung).HasMaxLength(500);
    }
}
