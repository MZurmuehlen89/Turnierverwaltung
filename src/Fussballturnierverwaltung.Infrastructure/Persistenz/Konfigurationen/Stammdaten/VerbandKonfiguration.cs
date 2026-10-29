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
    }
}
