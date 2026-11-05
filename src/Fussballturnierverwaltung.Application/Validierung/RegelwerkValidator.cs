using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

namespace Fussballturnierverwaltung.Application.Validierung;

public class RegelwerkValidator
{
    /// <summary>Feldbezug der Meldungen (#602) = Name der Eigenschaft in der Eingabe; ein Punktevergleich markiert den
    /// Wert, der aus der Reihe fällt (Sieg bzw. Niederlage), nicht das Unentschieden.</summary>
    public ValidationErgebnis Validieren(RegelwerkErstellenEingabe eingabe)
    {
        var fehler = new List<Feldfehler>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add(new("Regelwerk-Name darf nicht leer sein.", nameof(eingabe.Name)));

        if (ErsterNegativerPunktwert(eingabe) is { } negativ)
            fehler.Add(new("Punktwerte dürfen nicht negativ sein.", negativ));

        if (eingabe.PunkteFuerSieg <= eingabe.PunkteFuerUnentschieden)
            fehler.Add(new($"Punkte für Sieg ({eingabe.PunkteFuerSieg}) müssen größer als für Unentschieden " +
                           $"({eingabe.PunkteFuerUnentschieden}) sein – sonst gibt es keinen Anreiz zu gewinnen.",
                nameof(eingabe.PunkteFuerSieg)));

        if (eingabe.PunkteFuerNiederlage > eingabe.PunkteFuerUnentschieden)
            fehler.Add(new($"Punkte für Niederlage ({eingabe.PunkteFuerNiederlage}) dürfen nicht größer als " +
                           $"für Unentschieden ({eingabe.PunkteFuerUnentschieden}) sein.",
                nameof(eingabe.PunkteFuerNiederlage)));

        if (eingabe.GueltigVon.HasValue && eingabe.GueltigBis.HasValue &&
            eingabe.GueltigBis < eingabe.GueltigVon)
            fehler.Add(new("GueltigBis darf nicht vor GueltigVon liegen.", nameof(eingabe.GueltigBis)));

        // Doppelte Entscheidungsverfahren prüfen
        var doppelt = eingabe.EntscheidungsVerfahrenReihenfolge
            .GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToList();
        if (doppelt.Any())
            fehler.Add(new($"Entscheidungsverfahren enthält Duplikate: {string.Join(", ", doppelt)}.",
                nameof(eingabe.EntscheidungsVerfahrenReihenfolge)));

        return ValidationErgebnis.Aus(fehler);
    }

    private static string? ErsterNegativerPunktwert(RegelwerkErstellenEingabe eingabe) =>
        eingabe.PunkteFuerSieg < 0 ? nameof(eingabe.PunkteFuerSieg)
        : eingabe.PunkteFuerUnentschieden < 0 ? nameof(eingabe.PunkteFuerUnentschieden)
        : eingabe.PunkteFuerNiederlage < 0 ? nameof(eingabe.PunkteFuerNiederlage)
        : null;

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
