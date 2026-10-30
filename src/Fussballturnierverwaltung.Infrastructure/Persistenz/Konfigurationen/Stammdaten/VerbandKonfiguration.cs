using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Stammdaten;

public class VerbandKonfiguration : IEntityTypeConfiguration<Verband>
{
    public void Configure(EntityTypeBuilder<Verband> builder)
    {
        builder.ToTable("Verbaende");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Kurzname).IsRequired().HasMaxLength(20);
        builder.HasIndex(v => v.Kurzname).IsUnique();

        builder.HasOne(v => v.Rangliste)
            .WithMany()
            .HasForeignKey(v => v.RanglisteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Zweiter Verweis auf dieselbe Tabelle: SQL Server erlaubt kein zweites ON DELETE SET NULL (mehrfache
        // Löschpfade), RanglisteRepository.LoeschenAsync löst den Verweis deshalb selbst (#574).
        builder.HasOne(v => v.VereinsRangliste)
            .WithMany()
            .HasForeignKey(v => v.VereinsRanglisteId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
