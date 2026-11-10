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

    /// <summary>Das vom Build eingebettete Bildmaterial (alle PNGs unter <c>Seed/Bildmaterial/</c>).</summary>
    public static SeedBildQuelle Eingebettet { get; } = new(EingebettetLaden);

    public byte[]? Laden(string kategorie, string code) => laden(kategorie, code);

    // Zuerst das öffentliche Bildmaterial (flag-icons), dann der git-ignorierte Ordner Privat – dort liegt lokal der Klon
    // des privaten Repos mit Wappen und Logos, an denen andere die Rechte haben (#633). Fehlt er, bleibt es beim Platzhalter.
    private static byte[]? EingebettetLaden(string kategorie, string code)
    {
        var praefix = $"{_assembly.GetName().Name}.Seed.Bildmaterial.";
        var gesuchterName = new[] { $"{praefix}{kategorie}.{code}.png", $"{praefix}Privat.{kategorie}.{code}.png" }
            .FirstOrDefault(_ressourcenNamen.Value.Contains);
        if (gesuchterName is null) return null;

        using var stream = _assembly.GetManifestResourceStream(gesuchterName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
