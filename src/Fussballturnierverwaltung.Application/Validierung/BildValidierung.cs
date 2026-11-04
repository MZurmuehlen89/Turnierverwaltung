using Fussballturnierverwaltung.Application.Abstraktionen;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Gemeinsame Größenprüfung für hochgeladenes Bildmaterial (Wappen/Flaggen/Logos, Issue #2).</summary>
internal static class BildValidierung
{
    internal const int MaxBildGroesseBytes = 2 * 1024 * 1024;

    private const string ZU_GROSS = "Bild darf maximal 2 MB groß sein.";

    internal static void GroessePruefen(byte[]? bild, List<string> fehler)
    {
        if (IstZuGross(bild))
            fehler.Add(ZU_GROSS);
    }

    internal static void GroessePruefen(byte[]? bild, string feld, List<Feldfehler> fehler)
    {
        if (IstZuGross(bild))
            fehler.Add(new Feldfehler(ZU_GROSS, feld));
    }

    private static bool IstZuGross(byte[]? bild) => bild is not null && bild.Length > MaxBildGroesseBytes;
}
