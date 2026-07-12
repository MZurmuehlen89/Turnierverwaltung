using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Ranglisten;

public class RanglistenEintragKonfiguration : IEntityTypeConfiguration<RanglistenEintrag>
{
    public void Configure(EntityTypeBuilder<RanglistenEintrag> builder)
    {
        builder.ToTable("RanglistenEintraege");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Rang).IsRequired();

        builder.HasOne(e => e.Rangliste)
            .WithMany(r => r.Eintraege)
            .HasForeignKey(e => e.RanglisteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Mannschaft)
            .WithMany()
            .HasForeignKey(e => e.MannschaftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RanglisteId, e.MannschaftId }).IsUnique();
    }
}
