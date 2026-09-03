using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Validiert die festen Paarungszuweisungen einer Turniervorlage (Issue #277) — analog
/// <see cref="VorlagenUebergangsregelValidator"/> als Prüfung des beabsichtigten Endzustands (alle Runden +
/// alle Paarungen), damit eine unschlüssige Struktur schon beim Speichern der Vorlage auffällt.</summary>
public class VorlagenPaarungValidator
{
    public ValidationErgebnis Validieren(
        IReadOnlyList<VorlagenRundeEingabe> runden,
        IReadOnlyList<VorlagenUebergangsRegelEingabe> uebergangsregeln,
        IReadOnlyList<VorlagenPaarungEingabe> paarungen)
    {
        var fehler = new List<string>();
        if (paarungen.Count == 0) return ValidationErgebnis.Ok();

        var zielReihenfolgenMitEingang = uebergangsregeln.Select(r => r.ZielReihenfolge).ToHashSet();

        foreach (var gruppe in paarungen.GroupBy(p => p.RundenReihenfolge))
        {
            if (gruppe.Key < 1 || gruppe.Key > runden.Count)
            {
                fehler.Add("Paarung referenziert eine nicht vorhandene Runde.");
                continue;
            }

            var runde = runden[gruppe.Key - 1];
            var label = string.IsNullOrWhiteSpace(runde.Name) ? $"Runde {gruppe.Key}" : $"'{runde.Name}'";

            if (!runde.PaarungFestVorgegeben)
            {
                fehler.Add($"{label}: Paarungen vorhanden, aber die Runde ist nicht als 'Paarung fest vorgegeben' markiert.");
                continue;
            }

            if (!zielReihenfolgenMitEingang.Contains(gruppe.Key))
            {
                fehler.Add($"{label}: Feste Paarungen erfordern eingehende Übergangsregeln — die erste Runde einer " +
                           "Vorlage durchläuft immer eine echte Auslosung.");
                continue;
            }

            foreach (var dup in gruppe.GroupBy(p => (p.PaarungsNummer, p.SlotNr)).Where(g => g.Count() > 1))
                fehler.Add($"{label}: Slot {dup.Key.SlotNr} von Paarung/Gruppe {dup.Key.PaarungsNummer} ist mehrfach vergeben.");

            foreach (var p in gruppe.Where(p => string.IsNullOrWhiteSpace(p.PlatzhalterBezeichnung)))
                fehler.Add($"{label}: Paarung/Gruppe {p.PaarungsNummer}, Slot {p.SlotNr} hat keinen zugewiesenen Platzhalter.");
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
