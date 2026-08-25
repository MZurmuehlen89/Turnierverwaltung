using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Ranglisten;

public class RanglistenQuelleKonfiguration : IEntityTypeConfiguration<RanglistenQuelle>
{
    public void Configure(EntityTypeBuilder<RanglistenQuelle> builder)
    {
        builder.ToTable("RanglistenQuellen");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Name).IsRequired().HasMaxLength(200);
        builder.Property(q => q.Url).IsRequired().HasMaxLength(2000);
        builder.Property(q => q.TabellenSelektor).HasMaxLength(500);
    }
}
