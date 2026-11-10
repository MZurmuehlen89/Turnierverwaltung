using System.Reflection;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Liefert das Bildmaterial (Issue #233) für den Seed-Lauf. Kategorie entspricht dem Unterordner unter
/// <c>Seed/Bildmaterial/</c> (z. B. "Flaggen", "Verbandslogos"), Code dem Dateinamen ohne Endung (FIFA-Code bzw.
/// Verbands-Kurzname). Fehlt ein Bild, liefert sie <c>null</c> und der Seed lässt das Feld leer (Platzhalter).
/// Austauschbar, damit die Tests nicht davon abhängen, welches Bildmaterial der Build eingebettet hat (#633).</summary>
public sealed class SeedBildQuelle(Func<string, string, byte[]?> laden)
{
    private static readonly Assembly _assembly = typeof(SeedBildQuelle).Assembly;
    private static readonly Lazy<HashSet<string>> _ressourcenNamen = new(
        () => [.. _assembly.GetManifestResourceNames()]);

    /// <summary>Das vom Build eingebettete Bildmaterial.</summary>
    public static SeedBildQuelle Eingebettet { get; } = new(EingebettetLaden);

    public byte[]? Laden(string kategorie, string code) => laden(kategorie, code);

    private static byte[]? EingebettetLaden(string kategorie, string code)
    {
        var gesuchterName = $"{_assembly.GetName().Name}.Seed.Bildmaterial.{kategorie}.{code}.png";
        if (!_ressourcenNamen.Value.Contains(gesuchterName)) return null;

        using var stream = _assembly.GetManifestResourceStream(gesuchterName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
