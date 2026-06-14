using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class TurnierValidator
{
    /// <summary>Validiert die Runden-Eingaben vor dem Anlegen einer TurnierInstanz.</summary>
    public ValidationErgebnis RundenValidieren(IReadOnlyList<TurnierRundeEingabe> runden)
    {
        var fehler = new List<string>();

        if (runden.Count == 0)
        {
            fehler.Add("Ein Turnier muss mindestens eine Runde haben.");
            return ValidationErgebnis.MitFehlern([.. fehler]);
        }

        foreach (var r in runden)
        {
            if (string.IsNullOrWhiteSpace(r.Name))
                fehler.Add($"Runde muss einen Namen haben.");

            if (r.EingangsMannschaftsanzahl < 2)
                fehler.Add($"Runde '{r.Name}': EingangsMannschaftsanzahl muss mindestens 2 betragen (war: {r.EingangsMannschaftsanzahl}).");

            if (r.AusgangsMannschaftsanzahl < 1)
                fehler.Add($"Runde '{r.Name}': AusgangsMannschaftsanzahl muss mindestens 1 betragen.");

            // Liga-Runden können mehr Ausgangsteams als erwartet haben (alle spielen alle)
            if (r.Rundenart != Rundenart.Liga && r.AusgangsMannschaftsanzahl >= r.EingangsMannschaftsanzahl)
                fehler.Add($"Runde '{r.Name}': In K.-o.-Runden müssen Ausgangsteams ({r.AusgangsMannschaftsanzahl}) " +
                           $"kleiner als Eingangsteams ({r.EingangsMannschaftsanzahl}) sein.");

            if (r.AuswaertstorRegelAktiv && !r.MitRueckspiel)
                fehler.Add($"Runde '{r.Name}': Auswärtstorregel ist aktiv, obwohl keine Hin- und Rückspiele gespielt werden.");
        }

        // Rundenübergänge: Ausgang[i] muss Eingang[i+1] sein
        for (int i = 1; i < runden.Count; i++)
        {
            var vorrunde = runden[i - 1];
            var runde    = runden[i];
            if (runde.EingangsMannschaftsanzahl != vorrunde.AusgangsMannschaftsanzahl)
                fehler.Add($"Runde {i + 1} ('{runde.Name}') erwartet {runde.EingangsMannschaftsanzahl} Mannschaften, " +
                           $"Runde {i} ('{vorrunde.Name}') liefert aber {vorrunde.AusgangsMannschaftsanzahl}.");
        }

        // Finale muss genau 1 Sieger liefern
        var finale = runden.FirstOrDefault(r => r.Rundenart == Rundenart.Finale);
        if (finale is not null && finale.AusgangsMannschaftsanzahl != 1)
            fehler.Add($"Das Finale ('{finale.Name}') muss genau 1 Sieger liefern (AusgangsMannschaftsanzahl war: {finale.AusgangsMannschaftsanzahl}).");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Prüft ob eine Rundenart mit dem verknüpften Regelwerk kompatibel ist.</summary>
    public ValidationErgebnis RundeUndRegelwerkKompatibel(TurnierRundeEingabe runde, Regelwerk regelwerk)
    {
        var fehler = new List<string>();

        var brauchtEntscheidung = runde.Rundenart is
            Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

        if (brauchtEntscheidung && !regelwerk.Entscheidungsverfahren.Any())
            fehler.Add($"K.-o.-Runde '{runde.Name}' benötigt einen Sieger, aber Regelwerk '{regelwerk.Name}' " +
                       $"hat kein Entscheidungsverfahren definiert (z.B. Verlängerung, Elfmeter).");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Validiert eine persistierte TurnierInstanz auf Konsistenz.</summary>
    public ValidationErgebnis TurnierInstanzValidieren(TurnierInstanz instanz)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(instanz.Name))
            fehler.Add("Turniername darf nicht leer sein.");

        if (string.IsNullOrWhiteSpace(instanz.SaisonOderJahr))
            fehler.Add("Saison/Jahr darf nicht leer sein.");

        if (instanz.Startdatum.HasValue && instanz.Enddatum.HasValue &&
            instanz.Enddatum < instanz.Startdatum)
            fehler.Add("Enddatum darf nicht vor dem Startdatum liegen.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
