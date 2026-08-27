using System.Reflection;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Lädt eingebettetes Bildmaterial (Issue #233) für den Seed-Lauf. Kategorie entspricht dem
/// Unterordner unter <c>Seed/Bildmaterial/</c> (z. B. "Flaggen", "Verbandslogos"), Code dem Dateinamen
/// ohne Endung (FIFA-Code bzw. Verbands-Kurzname).</summary>
internal static class SeedBildLader
{
    private static readonly Assembly _assembly = typeof(SeedBildLader).Assembly;
    private static readonly Lazy<HashSet<string>> _ressourcenNamen = new(
        () => [.. _assembly.GetManifestResourceNames()]);

    public static byte[]? Laden(string kategorie, string code)
    {
        var gesuchterName = $"{_assembly.GetName().Name}.Seed.Bildmaterial.{kategorie}.{code}.png";
        if (!_ressourcenNamen.Value.Contains(gesuchterName)) return null;

        using var stream = _assembly.GetManifestResourceStream(gesuchterName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
