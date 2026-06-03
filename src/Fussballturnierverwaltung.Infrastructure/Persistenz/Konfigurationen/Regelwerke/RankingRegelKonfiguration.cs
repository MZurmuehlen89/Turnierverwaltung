using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Regelwerke;

public class RankingRegelKonfiguration : IEntityTypeConfiguration<RankingRegel>
{
    public void Configure(EntityTypeBuilder<RankingRegel> builder)
    {
        builder.ToTable("RankingRegeln");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Beschreibung).HasMaxLength(500);
        builder.Property(r => r.Regelart).IsRequired();
        builder.Property(r => r.Sortierrichtung).IsRequired();
    }
}
