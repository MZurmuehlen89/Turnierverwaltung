using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Erzeugt den <see cref="SeedDatenService"/> für Tests. Das Bildmaterial mit fremden Rechten liegt nur lokal
/// in einem privaten Repo, die CI hat es nicht (#633) – deshalb liefert die Bildquelle hier für jede Anfrage ein Bild,
/// unabhängig davon, was der Build eingebettet hat.</summary>
public static class SeedTestDienst
{
    public static readonly byte[] TestBild = [0x89, 0x50, 0x4E, 0x47];

    public static readonly SeedBildQuelle AlleBilder = new((_, _) => TestBild);

    public static readonly SeedBildQuelle KeineBilder = new((_, _) => null);

    /// <summary>Ob der Build den git-ignorierten Ordner Seed/Bildmaterial/Privat eingebettet hat – für Tests, die das
    /// echte Bildmaterial prüfen und ohne es übersprungen werden.</summary>
    public static bool PrivatesBildmaterialEingebettet { get; } = typeof(SeedBildQuelle).Assembly
        .GetManifestResourceNames()
        .Any(n => n.Contains(".Seed.Bildmaterial.Privat.", StringComparison.Ordinal));

    public static SeedDatenService Erzeugen(TurnierverwaltungDbContext context, SeedBildQuelle? bildQuelle = null) =>
        new(context, NullLogger<SeedDatenService>.Instance, bildQuelle ?? AlleBilder);
}
