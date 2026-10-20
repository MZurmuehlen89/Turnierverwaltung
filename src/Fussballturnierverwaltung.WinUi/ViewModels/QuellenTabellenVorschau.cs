using Fussballturnierverwaltung.Application.Ranglisten.Import;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public sealed record VorschauZelle(string Text, bool IstZugeordnet);

public sealed record VorschauZeile(IReadOnlyList<VorschauZelle> Zellen, bool IstUebersprungen)
{
    public double Deckkraft => IstUebersprungen ? 0.4 : 1.0;
}

/// <summary>Vorschau der ersten Zeilen einer Quell-Tabelle im Reiter „Quellen“ (#477): Kopfzeile mit Spaltennummer
/// und Rolle, zugeordnete Spalten hervorgehoben, eine übersprungene Kopfzeile abgeblendet.</summary>
public sealed record QuellenTabellenVorschau(IReadOnlyList<VorschauZelle> Kopf, IReadOnlyList<VorschauZeile> Zeilen, string Hinweis)
{
    public static QuellenTabellenVorschau Erstellen(
        RanglistenTabellenVorschau tabelle, int? mannschaftSpalte, int? punkteSpalte, bool kopfzeileUeberspringen)
    {
        var kopf = Enumerable.Range(0, tabelle.SpaltenAnzahl)
            .Select(spalte => new VorschauZelle(
                $"Spalte {spalte + 1}{Rolle(spalte, mannschaftSpalte, punkteSpalte)}",
                spalte == mannschaftSpalte || spalte == punkteSpalte))
            .ToList();

        var zeilen = tabelle.VorschauZeilen
            .Select((zeile, index) => new VorschauZeile(
                Enumerable.Range(0, tabelle.SpaltenAnzahl)
                    .Select(spalte => new VorschauZelle(
                        spalte < zeile.Count ? zeile[spalte] : "",
                        spalte == mannschaftSpalte || spalte == punkteSpalte))
                    .ToList(),
                index == 0 && kopfzeileUeberspringen))
            .ToList();

        var hinweis = $"Erste {zeilen.Count} von {tabelle.ZeilenAnzahl} Zeilen";
        return new QuellenTabellenVorschau(kopf, zeilen, hinweis);
    }

    private static string Rolle(int spalte, int? mannschaftSpalte, int? punkteSpalte) =>
        spalte == mannschaftSpalte ? " · Mannschaft" : spalte == punkteSpalte ? " · Punkte" : "";
}
