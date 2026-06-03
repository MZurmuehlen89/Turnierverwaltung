using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Simulation;

public class SimulationsLaufKonfiguration : IEntityTypeConfiguration<SimulationsLauf>
{
    public void Configure(EntityTypeBuilder<SimulationsLauf> builder)
    {
        builder.ToTable("SimulationsLaeufe");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StartZeitpunkt).IsRequired();
        builder.Property(s => s.ZufallsSeed).IsRequired();
        builder.Property(s => s.Beschreibung).HasMaxLength(500);

        builder.HasOne(s => s.TurnierInstanz)
            .WithMany(i => i.SimulationsLaeufe)
            .HasForeignKey(s => s.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
