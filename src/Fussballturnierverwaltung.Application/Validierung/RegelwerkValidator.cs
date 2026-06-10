using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

namespace Fussballturnierverwaltung.Application.Validierung;

public class RegelwerkValidator
{
    public ValidationErgebnis Validieren(RegelwerkErstellenEingabe eingabe)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add("Regelwerk-Name darf nicht leer sein.");

        if (eingabe.PunkteFuerSieg < 0 || eingabe.PunkteFuerUnentschieden < 0 || eingabe.PunkteFuerNiederlage < 0)
            fehler.Add("Punktwerte dürfen nicht negativ sein.");

        if (eingabe.PunkteFuerSieg <= eingabe.PunkteFuerUnentschieden)
            fehler.Add($"Punkte für Sieg ({eingabe.PunkteFuerSieg}) müssen größer als für Unentschieden " +
                       $"({eingabe.PunkteFuerUnentschieden}) sein – sonst gibt es keinen Anreiz zu gewinnen.");

        if (eingabe.PunkteFuerNiederlage > eingabe.PunkteFuerUnentschieden)
            fehler.Add($"Punkte für Niederlage ({eingabe.PunkteFuerNiederlage}) dürfen nicht größer als " +
                       $"für Unentschieden ({eingabe.PunkteFuerUnentschieden}) sein.");

        if (eingabe.GueltigVon.HasValue && eingabe.GueltigBis.HasValue &&
            eingabe.GueltigBis < eingabe.GueltigVon)
            fehler.Add("GueltigBis darf nicht vor GueltigVon liegen.");

        // Doppelte Entscheidungsverfahren prüfen
        var doppelt = eingabe.EntscheidungsVerfahrenReihenfolge
            .GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToList();
        if (doppelt.Any())
            fehler.Add($"Entscheidungsverfahren enthält Duplikate: {string.Join(", ", doppelt)}.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    public ValidationErgebnis Validieren(RegelwerkAktualisierenEingabe eingabe)
    {
        // Identische Regeln wie beim Erstellen
        var erstellen = new RegelwerkErstellenEingabe(
            eingabe.Name, eingabe.Beschreibung, eingabe.GueltigVon, eingabe.GueltigBis,
            eingabe.RankingRegelSetId, eingabe.PunkteFuerSieg, eingabe.PunkteFuerUnentschieden,
            eingabe.PunkteFuerNiederlage, eingabe.EntscheidungsVerfahrenReihenfolge);
        return Validieren(erstellen);
    }

    /// <summary>Validiert ein bereits persistiertes Regelwerk auf Vollständigkeit.</summary>
    public ValidationErgebnis Validieren(Regelwerk regelwerk)
    {
        var fehler = new List<string>();

        if (!regelwerk.PunkteRegeln.Any())
            fehler.Add($"Regelwerk '{regelwerk.Name}' hat keine Punkte-Regel definiert.");

        // Reihenfolge der Entscheidungsverfahren muss lückenlos sein
        if (regelwerk.Entscheidungsverfahren.Any())
        {
            var reihenfolgen = regelwerk.Entscheidungsverfahren.Select(e => e.Reihenfolge).OrderBy(r => r).ToList();
            for (int i = 0; i < reihenfolgen.Count; i++)
            {
                if (reihenfolgen[i] != i + 1)
                {
                    fehler.Add($"Regelwerk '{regelwerk.Name}': Entscheidungsverfahren-Reihenfolge ist nicht lückenlos " +
                               $"(erwartet {i + 1}, gefunden {reihenfolgen[i]}).");
                    break;
                }
            }
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
