using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class TurnierVorlageKonfiguration : IEntityTypeConfiguration<TurnierVorlage>
{
    public void Configure(EntityTypeBuilder<TurnierVorlage> builder)
    {
        builder.ToTable("TurnierVorlagen");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Beschreibung).HasMaxLength(2000);
        builder.Property(t => t.ErforderlicheMannschaftsanzahl).IsRequired();
        builder.Property(t => t.VorlagenKategorie).IsRequired();
        builder.Property(t => t.IstHistorisch).IsRequired();
        builder.Property(t => t.GueltigVon).HasColumnType("date");
        builder.Property(t => t.GueltigBis).HasColumnType("date");
    }
}
