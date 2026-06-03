using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Auslosungen;

public class AuslosungKonfiguration : IEntityTypeConfiguration<Auslosung>
{
    public void Configure(EntityTypeBuilder<Auslosung> builder)
    {
        builder.ToTable("Auslosungen");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Auslosungsart).IsRequired();
        builder.Property(a => a.Status).IsRequired();
        builder.Property(a => a.IstAutomatisch).IsRequired();
        builder.Property(a => a.Datum).HasColumnType("date");

        // Primärer Cascade-Pfad für Auslosung: über TurnierInstanz
        builder.HasOne(a => a.TurnierInstanz)
            .WithMany(i => i.Auslosungen)
            .HasForeignKey(a => a.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction: vermeidet Multi-Cascade über TurnierInstanz → TurnierRunde → Auslosung
        builder.HasOne(a => a.TurnierRunde)
            .WithMany()
            .HasForeignKey(a => a.TurnierRundeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
