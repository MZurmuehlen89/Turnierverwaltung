using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Regelwerke;

public class RegelwerkKonfiguration : IEntityTypeConfiguration<Regelwerk>
{
    public void Configure(EntityTypeBuilder<Regelwerk> builder)
    {
        builder.ToTable("Regelwerke");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Beschreibung).HasMaxLength(1000);
        builder.Property(r => r.GueltigVon).HasColumnType("date");
        builder.Property(r => r.GueltigBis).HasColumnType("date");

        builder.HasOne(r => r.RankingRegelSet)
            .WithMany()
            .HasForeignKey(r => r.RankingRegelSetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
