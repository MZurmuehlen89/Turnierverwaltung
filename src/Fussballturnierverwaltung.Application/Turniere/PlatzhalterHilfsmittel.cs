using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Rundenart-neutrale Beschreibung einer eingehenden <c>VorlagenUebergangsRegel</c>-Zeile, aus der
/// sich Platzhalter-Anzeigenamen ableiten lassen — entkoppelt <see cref="VorlagenPlatzhalterHilfsmittel"/>
/// bewusst vom konkreten Domain-Entity, damit sowohl der WinUI3-Vorlagen-Editor (arbeitet rein clientseitig
/// auf ViewModel-Objekten bis zum Speichern) als auch die WPF-Übernahme-Logik beim Vorlage-Laden (arbeitet auf
/// geladenen Domain-Entities) dieselbe Kernlogik nutzen können.</summary>
public record VorlagenPlatzhalterQuelle(
    int QuellReihenfolge,
    Rundenart QuellRundenart,
    string QuellName,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis,
    int? AnzahlBeste);

/// <summary>Rundenart-neutrale Ableitung von Platzhalter-Anzeigenamen aus eingehenden Übergangsregeln
/// (ursprünglich <c>VorlagenPlatzhalterHilfsmittel</c> aus Issue #277, mit #294 auf die vereinheitlichte
/// TurnierInstanz/-Runde umbenannt) — analog zu <c>TeilnehmerPoolHilfsmittel.PlatzhalterAusRegelnGenerieren</c>
/// (WPF, Issue #251), aber ohne <c>TeilnehmerAnzeige</c>/Mannschafts-Bezug, da eine Vorlage nie echte
/// Mannschaften besitzt.</summary>
public static class PlatzhalterHilfsmittel
{
    public static IReadOnlyList<string> PlatzhalterGenerieren(IReadOnlyList<VorlagenPlatzhalterQuelle> eingehendeRegeln) =>
        eingehendeRegeln
            .OrderBy(r => r.QuellReihenfolge)
            .ThenBy(r => r.GruppeIndex ?? 0)
            .ThenBy(r => r.PlatzierungVon)
            .SelectMany(PlatzhalterFuerRegel)
            .ToList();

    private static IEnumerable<string> PlatzhalterFuerRegel(VorlagenPlatzhalterQuelle regel)
    {
        if (regel.QuellRundenart == Rundenart.Gruppenphase)
        {
            if (regel.AnzahlBeste is { } anzahlBeste)
                return Enumerable.Range(1, anzahlBeste).Select(i => $"{i}. bester {regel.PlatzierungVon}. Platz");

            var buchstabe = regel.GruppeIndex is { } g ? GruppenHerkunft.Buchstabe(g) : "?";
            return Enumerable.Range(regel.PlatzierungVon, regel.PlatzierungBis - regel.PlatzierungVon + 1)
                .Select(platz => $"{platz}. Gruppe {buchstabe}");
        }

        if (regel.QuellRundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            string prefix = regel.PlatzierungVon == 2 ? "Verlierer" : "Sieger";
            int spielNr = regel.GruppeIndex ?? 1;
            return [$"{prefix} {regel.QuellName} {spielNr}"];
        }

        return Enumerable.Range(regel.PlatzierungVon, regel.PlatzierungBis - regel.PlatzierungVon + 1)
            .Select(platz => $"Platz {platz} ({regel.QuellName})");
    }
}
