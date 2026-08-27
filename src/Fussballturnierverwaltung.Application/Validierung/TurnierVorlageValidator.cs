using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Validiert Basisdaten/Turniereinstellungen und Turnierrunden einer <c>TurnierVorlage</c> (Issue #234)
/// — bisher waren nur Übergangsregeln geprüft (<see cref="VorlagenUebergangsregelValidator"/>), Name, Anzahl
/// Mannschaften, Gültigkeitszeitraum und einzelne Runden-Felder liefen ungeprüft durch. Wird sowohl serverseitig
/// aus <c>TurnierVorlageService</c> als auch clientseitig als Live-Vorschau aus <c>TurnierVorlageViewModel</c>
/// aufgerufen, damit dieselbe Prüfung an beiden Stellen greift.</summary>
public class TurnierVorlageValidator
{
    public ValidationErgebnis BasisdatenValidieren(string name, int anzahlMannschaften, DateOnly? gueltigVon, DateOnly? gueltigBis)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            fehler.Add("Name darf nicht leer sein.");

        if (anzahlMannschaften < 2)
            fehler.Add($"Erforderliche Mannschaftsanzahl muss mindestens 2 betragen (war: {anzahlMannschaften}).");

        if (gueltigVon is not null && gueltigBis is not null && gueltigVon > gueltigBis)
            fehler.Add("Gültig von darf nicht nach Gültig bis liegen.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    public ValidationErgebnis RundenValidieren(IReadOnlyList<VorlagenRundeEingabe> runden)
    {
        var fehler = new List<string>();

        for (int i = 0; i < runden.Count; i++)
        {
            var r = runden[i];
            var label = string.IsNullOrWhiteSpace(r.Name) ? $"Runde {i + 1}" : $"'{r.Name}'";

            if (string.IsNullOrWhiteSpace(r.Name))
                fehler.Add($"Runde {i + 1}: Name darf nicht leer sein.");

            if (r.EingangsMannschaftsanzahl < 1)
                fehler.Add($"{label}: Eingangsmannschaftsanzahl muss mindestens 1 betragen.");

            if (r.AusgangsMannschaftsanzahl < 1)
                fehler.Add($"{label}: Ausgangsmannschaftsanzahl muss mindestens 1 betragen.");

            if (r.Rundenart == Rundenart.Gruppenphase && (r.GruppenAnzahl is null || r.GruppenAnzahl < 2))
                fehler.Add($"{label}: Gruppenphase erfordert mindestens 2 Gruppen.");
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
