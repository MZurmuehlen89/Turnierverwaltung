using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Validiert Rohdaten aus CSV/JSON-Importen, bevor Entitäten erzeugt werden.</summary>
public class ImportValidator
{
    private static readonly string[] PflichtfelderMannschaft = ["Name", "LandFifaCode", "OffensivRating", "MittelfeldRating", "DefensivRating"];
    private static readonly string[] PflichtfelderLand       = ["Name", "FifaCode", "Kontinent", "VerbandKurzname"];
    private static readonly string[] RatingfelderMannschaft  = ["OffensivRating", "MittelfeldRating", "DefensivRating", "Heimstaerke", "Auswaertsstaerke"];

    public ValidationErgebnis ValidierenMannschaft(IDictionary<string, string> zeile, int zeilennummer)
    {
        var fehler = new List<string>();
        var praefix = $"Zeile {zeilennummer}: ";

        PflichtfelderPruefen(zeile, PflichtfelderMannschaft, praefix, fehler);

        foreach (var feld in RatingfelderMannschaft)
        {
            if (!zeile.TryGetValue(feld, out var wertStr)) continue;

            if (!int.TryParse(wertStr, out var wert))
                fehler.Add($"{praefix}{feld} ist keine gültige Zahl (war: '{wertStr}').");
            else if (wert < 1 || wert > 100)
                fehler.Add($"{praefix}{feld} muss zwischen 1 und 100 liegen (war: {wert}).");
        }

        if (zeile.TryGetValue("Mannschaftsart", out var artStr) &&
            !Enum.TryParse<Mannschaftsart>(artStr, ignoreCase: true, out _))
            fehler.Add($"{praefix}Ungültige Mannschaftsart '{artStr}'. Erlaubt: Verein, Nationalmannschaft.");

        if (zeile.TryGetValue("LandFifaCode", out var code) && code.Trim().Length != 3)
            fehler.Add($"{praefix}FIFA-Code muss genau 3 Zeichen haben (war: '{code.Trim()}').");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    public ValidationErgebnis ValidierenLand(IDictionary<string, string> zeile, int zeilennummer)
    {
        var fehler = new List<string>();
        var praefix = $"Zeile {zeilennummer}: ";

        PflichtfelderPruefen(zeile, PflichtfelderLand, praefix, fehler);

        if (zeile.TryGetValue("FifaCode", out var code) && code.Trim().Length != 3)
            fehler.Add($"{praefix}FIFA-Code muss genau 3 Zeichen haben (war: '{code.Trim()}').");

        if (zeile.TryGetValue("Kontinent", out var kontStr) &&
            !Enum.TryParse<Kontinent>(kontStr, ignoreCase: true, out _))
            fehler.Add($"{praefix}Ungültiger Kontinent '{kontStr}'. Erlaubt: {string.Join(", ", Enum.GetNames<Kontinent>())}.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    public IReadOnlyList<ValidationErgebnis> ValidierenMassendaten(
        IReadOnlyList<IDictionary<string, string>> zeilen,
        Func<IDictionary<string, string>, int, ValidationErgebnis> zeilenValidator)
    {
        return zeilen
            .Select((zeile, index) => zeilenValidator(zeile, index + 2)) // +2: Zeile 1 = Header
            .Where(e => e.IstUngueltig)
            .ToList();
    }

    private static void PflichtfelderPruefen(
        IDictionary<string, string> zeile,
        IEnumerable<string> felder,
        string praefix,
        List<string> fehler)
    {
        foreach (var feld in felder)
        {
            if (!zeile.TryGetValue(feld, out var wert) || string.IsNullOrWhiteSpace(wert))
                fehler.Add($"{praefix}Pflichtfeld '{feld}' fehlt oder ist leer.");
        }
    }
}
