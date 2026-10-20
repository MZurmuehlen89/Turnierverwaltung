namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Kopfzeile eines aufklappbaren Bereichs (Turnierkopf #399, Runden #476): Titel plus Kurzfassung des
/// Inhalts, damit auch zugeklappt erkennbar bleibt, ob etwas festgelegt ist.</summary>
public record AbschnittKopf(string Titel, string Zusammenfassung)
{
    /// <summary>Verbindet die vorhandenen Teile mit „ · “; ohne Teile steht <paramref name="leer"/> da.</summary>
    public static AbschnittKopf Aus(string titel, IEnumerable<string?> teile, string leer = "nicht festgelegt")
    {
        var zusammenfassung = string.Join(" · ", teile.OfType<string>());
        return new(titel, zusammenfassung.Length > 0 ? zusammenfassung : leer);
    }
}
