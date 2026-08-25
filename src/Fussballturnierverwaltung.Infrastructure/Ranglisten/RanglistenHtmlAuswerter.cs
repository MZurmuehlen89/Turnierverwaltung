using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Infrastructure.Ranglisten;

public class RanglistenHtmlAuswerter : IRanglistenHtmlAuswerter
{
    private static readonly Regex FussnotenMarker = new(@"\[\w+\]", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Zahl = new(@"-?\d+(\.\d+)?", RegexOptions.Compiled);

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

            ergebnis.Add(new RanglistenRohZeile(rohName, punkte));
        }

        return ergebnis;
    }

    private static string NameBereinigen(string text)
    {
        var ohneFussnoten = FussnotenMarker.Replace(text, "");
        return Whitespace.Replace(ohneFussnoten, " ").Trim();
    }

    private static int? PunkteParsen(string text)
    {
        var bereinigt = text.Replace(",", "");
        var treffer = Zahl.Match(bereinigt);
        if (!treffer.Success)
            return null;

        return double.TryParse(treffer.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wert)
            ? (int)Math.Round(wert, MidpointRounding.AwayFromZero)
            : null;
    }
}
