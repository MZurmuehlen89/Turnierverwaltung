using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Ranglisten;

public class RanglisteKonfiguration : IEntityTypeConfiguration<Rangliste>
{
    public void Configure(EntityTypeBuilder<Rangliste> builder)
    {
        builder.ToTable("Ranglisten");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Quelle).HasMaxLength(500);
        builder.Property(r => r.Datum).HasColumnType("date");

        builder.HasOne(r => r.RanglistenQuelle)
            .WithMany()
            .HasForeignKey(r => r.RanglistenQuelleId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Pokalsieger (#583): eine gelöschte Mannschaft hinterlässt keinen Pokalsieger.
        builder.HasOne(r => r.Pokalsieger)
            .WithMany()
            .HasForeignKey(r => r.PokalsiegerId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
