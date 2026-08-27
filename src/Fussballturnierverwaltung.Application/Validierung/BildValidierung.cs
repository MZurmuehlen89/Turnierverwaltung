namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Gemeinsame Größenprüfung für hochgeladenes Bildmaterial (Wappen/Flaggen/Logos, Issue #2).</summary>
internal static class BildValidierung
{
    internal const int MaxBildGroesseBytes = 2 * 1024 * 1024;

    internal static void GroessePruefen(byte[]? bild, List<string> fehler)
    {
        if (bild is not null && bild.Length > MaxBildGroesseBytes)
            fehler.Add("Bild darf maximal 2 MB groß sein.");
    }
}
