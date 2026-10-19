using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Ranglisten;

public class RanglistenAbschnittKonfiguration : IEntityTypeConfiguration<RanglistenAbschnitt>
{
    public void Configure(EntityTypeBuilder<RanglistenAbschnitt> builder)
    {
        builder.ToTable("RanglistenAbschnitte");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(a => a.Rangliste)
            .WithMany(r => r.Abschnitte)
            .HasForeignKey(a => a.RanglisteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ein Platz gehört genau einem Abschnitt — zwei Abschnitte mit gleichem Ende wären nicht unterscheidbar.
        builder.HasIndex(a => new { a.RanglisteId, a.BisPlatz }).IsUnique();
    }
}
