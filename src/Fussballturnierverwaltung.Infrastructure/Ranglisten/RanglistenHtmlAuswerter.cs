using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Infrastructure.Ranglisten;

public class RanglistenHtmlAuswerter : IRanglistenHtmlAuswerter
{
    // Kopfzeile + 10 Datenzeilen für die Vorschau im Reiter „Quellen“ (#477).
    private const int MaxVorschauZeilen = 11;

    private static readonly Regex FussnotenMarker = new(@"\[\w+\]", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Zahl = new(@"-?\d+(?:[.,]\d+)*", RegexOptions.Compiled);

    /// <summary>Trailing Meister-/Aufsteiger-/Absteiger-Markierung wie sie Wikipedia-Abschlusstabellen
    /// an den Vereinsnamen anhängen, z.B. "(M)", "(N)", "(R↓)", "(M, P)".</summary>
    private static readonly Regex AnnotationsMarker = new(@"\s*\([A-ZÄÖÜ]+[↑↓]?(?:,\s*[A-ZÄÖÜ]+[↑↓]?)*\)\s*$", RegexOptions.Compiled);

    /// <summary>Trailing Sternchen-Fußnoten wie sie Wikipedia-Ranglisten (z.B. FIFA-Weltrangliste)
    /// an Ländernamen anhängen, z.B. "Argentinien***".</summary>
    private static readonly Regex SternchenMarker = new(@"\*+\s*$", RegexOptions.Compiled);

    /// <summary>Verdoppelter Name durch Flaggen-Alt-Text + sichtbaren Linktext in derselben Zelle,
    /// z.B. "Spanien Spanien" -&gt; "Spanien" (häufig bei Länder-Ranglisten mit Flaggen-Icons).</summary>
    private static readonly Regex DoppelterName = new(@"^(.+)\s\1$", RegexOptions.Compiled);

    public IReadOnlyList<RanglistenRohZeile> Auswerten(string html, RanglistenQuelle quelle)
    {
        var document = new HtmlParser().ParseDocument(html);

        var tabelle = !string.IsNullOrWhiteSpace(quelle.TabellenSelektor)
            ? document.QuerySelector(quelle.TabellenSelektor)
            : document.QuerySelectorAll("table").ElementAtOrDefault(quelle.TabellenIndex);

        if (tabelle is null)
            throw new InvalidOperationException(
                $"Keine passende Tabelle auf der Seite gefunden (Quelle: '{quelle.Name}').");

        var zeilen = tabelle.QuerySelectorAll("tr").ToList();
        var maxSpalte = Math.Max(quelle.MannschaftSpalte, quelle.PunkteSpalte ?? 0);
        var ergebnis = new List<RanglistenRohZeile>();

        for (int i = quelle.KopfzeileUeberspringen ? 1 : 0; i < zeilen.Count; i++)
        {
            var zellen = zeilen[i].QuerySelectorAll("td, th").ToList();
            if (zellen.Count <= maxSpalte)
                continue;

            var rohName = NameBereinigen(zellen[quelle.MannschaftSpalte].TextContent);
            if (string.IsNullOrWhiteSpace(rohName))
                continue;

            var punkte = quelle.PunkteSpalte is int spalte
                ? PunkteParsen(zellen[spalte].TextContent)
                : null;

            ergebnis.Add(new RanglistenRohZeile(ergebnis.Count, rohName, punkte));
        }

        return ergebnis;
    }

    public IReadOnlyList<RanglistenTabellenVorschau> TabellenErmitteln(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var tabellen = document.QuerySelectorAll("table");

        var ergebnis = new List<RanglistenTabellenVorschau>();
        for (int i = 0; i < tabellen.Length; i++)
        {
            var zeilen = tabellen[i].QuerySelectorAll("tr").ToList();
            if (zeilen.Count == 0)
                continue;

            var erkannteKopfzeile = zeilen[0].QuerySelectorAll("th").Length > 0
                && zeilen[0].QuerySelectorAll("td").Length == 0;

            var vorschauZeilen = zeilen
                .Take(MaxVorschauZeilen)
                .Select(zeile => (IReadOnlyList<string>)zeile.QuerySelectorAll("td, th")
                    .Select(zelle => NameBereinigen(zelle.TextContent))
                    .ToList())
                .ToList();

            var spaltenAnzahl = vorschauZeilen.Count == 0 ? 0 : vorschauZeilen.Max(z => z.Count);

            ergebnis.Add(new RanglistenTabellenVorschau(i, zeilen.Count, spaltenAnzahl, erkannteKopfzeile, vorschauZeilen));
        }

        return ergebnis;
    }

    private static string NameBereinigen(string text)
    {
        var ohneFussnoten = FussnotenMarker.Replace(text, "");
        var ohneAnnotation = AnnotationsMarker.Replace(ohneFussnoten, "");
        var ohneSternchen = SternchenMarker.Replace(ohneAnnotation, "");
        var normalisiert = Whitespace.Replace(ohneSternchen, " ").Trim();

        var duplikat = DoppelterName.Match(normalisiert);
        return duplikat.Success ? duplikat.Groups[1].Value : normalisiert;
    }

    /// <summary>Erkennt sowohl englisches (",": Tausender, ".": Dezimal, z.B. "1,832.71") als
    /// auch deutsches Zahlenformat (".": Tausender, ",": Dezimal, z.B. "1.832,71" oder ohne
    /// Tausendertrennung "1995,88"). Stehen beide Trennzeichen im Wert, entscheidet die Position
    /// (das zuletzt stehende ist der Dezimaltrenner). Steht nur ein Komma da, gilt es als
    /// Dezimaltrenner bei 1-2 Nachkommastellen, sonst als Tausendertrenner.</summary>
    private static int? PunkteParsen(string text)
    {
        var treffer = Zahl.Match(text);
        if (!treffer.Success)
            return null;

        var roh = treffer.Value;
        var letzterPunkt = roh.LastIndexOf('.');
        var letztesKomma = roh.LastIndexOf(',');

        string normalisiert;
        if (letzterPunkt >= 0 && letztesKomma >= 0)
        {
            normalisiert = letzterPunkt > letztesKomma
                ? roh.Replace(",", "")
                : roh.Replace(".", "").Replace(',', '.');
        }
        else if (letztesKomma >= 0)
        {
            var nachkommastellen = roh.Length - letztesKomma - 1;
            normalisiert = nachkommastellen is 1 or 2 ? roh.Replace(',', '.') : roh.Replace(",", "");
        }
        else
        {
            normalisiert = roh;
        }

        return double.TryParse(normalisiert, NumberStyles.Float, CultureInfo.InvariantCulture, out var wert)
            ? (int)Math.Round(wert, MidpointRounding.AwayFromZero)
            : null;
    }
}
