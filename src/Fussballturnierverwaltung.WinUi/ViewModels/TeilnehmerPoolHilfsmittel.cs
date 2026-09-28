using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Teilnehmer-/Platzhalter-Ermittlung für den Team-Pool einer Runde (Issue #297) — 1:1 aus
/// Wpf/ViewModels/TeilnehmerPoolHilfsmittel.cs portiert (reine Domain-Konsumenten-Logik ohne WPF-Bezug),
/// gemeinsam genutzt von <see cref="VorbelegungViewModel"/> für Vorlage- und Turnier-Status.</summary>
public static class TeilnehmerPoolHilfsmittel
{
    /// <summary>Ermittelt die unmittelbar vorangehende Runde in der Liste (Issue #198): Finale und
    /// SpielUmPlatzDrei sind stets parallele Geschwisterrunden desselben Halbfinales, nicht sequenziell
    /// verkettet, und werden deshalb ausgeschlossen.</summary>
    public static TurnierRunde? VorgaengerrundeErmitteln(IReadOnlyList<TurnierRunde> instanzRunden, TurnierRunde aktuelleRunde) =>
        instanzRunden
            .Where(r => r.Reihenfolge < aktuelleRunde.Reihenfolge
                     && r.Rundenart is not (Rundenart.Finale or Rundenart.SpielUmPlatzDrei))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();

    /// <summary>Erzeugt den Platzhalter-Teilnehmer-Pool aus den tatsächlichen eingehenden Übergangsregeln
    /// einer Zielrunde — unterstützt mehrere unabhängige Quellrunden gleichzeitig. <paramref name="eingehendeRegeln"/>
    /// muss mit geladener QuellRunde-Navigation kommen und wird als nicht leer vorausgesetzt. Mit
    /// <paramref name="kombinationstabelle"/> der Zielrunde (Issue #372) werden die besten Gruppendritten als deren
    /// feste Baumplätze ("3. Gruppe A/D/E/F") statt als Rangfolge ("1. bester 3. Platz") angeboten.</summary>
    public static IReadOnlyList<TeilnehmerAnzeige> PlatzhalterAusRegelnGenerieren(
        IReadOnlyList<Uebergangsregel> eingehendeRegeln, Kombinationstabelle? kombinationstabelle = null) =>
        eingehendeRegeln
            .OrderBy(r => r.QuellRunde.Reihenfolge)
            .ThenBy(r => r.GruppeIndex ?? 0)
            .ThenBy(r => r.PlatzierungVon)
            .SelectMany(r => PlatzhalterFuerRegel(r, kombinationstabelle))
            .ToList();

    private static IEnumerable<TeilnehmerAnzeige> PlatzhalterFuerRegel(Uebergangsregel regel, Kombinationstabelle? kombinationstabelle)
    {
        var quellRunde = regel.QuellRunde;

        if (quellRunde.Rundenart == Rundenart.Gruppenphase)
        {
            if (regel.AnzahlBeste is { } anzahlBeste)
                return kombinationstabelle is not null && kombinationstabelle.AnzahlBeste == anzahlBeste
                    ? kombinationstabelle.Plaetze.Select(TeilnehmerAnzeige.AusPlatzhalter)
                    : Enumerable.Range(1, anzahlBeste)
                        .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"{i}. bester {regel.PlatzierungVon}. Platz"));

            var buchstabe = regel.GruppeIndex is { } g ? GruppenBuchstabe(g) : "?";
            return Enumerable.Range(regel.PlatzierungVon, regel.PlatzierungBis - regel.PlatzierungVon + 1)
                .Select(platz => TeilnehmerAnzeige.AusPlatzhalter($"{platz}. Gruppe {buchstabe}"));
        }

        if (quellRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            string prefix = regel.PlatzierungVon == 2 ? "Verlierer" : "Sieger";
            int spielNr = regel.GruppeIndex ?? 1;
            return [TeilnehmerAnzeige.AusPlatzhalter($"{prefix} {quellRunde.Name} {spielNr}")];
        }

        return Enumerable.Range(regel.PlatzierungVon, regel.PlatzierungBis - regel.PlatzierungVon + 1)
            .Select(platz => TeilnehmerAnzeige.AusPlatzhalter($"Platz {platz} ({quellRunde.Name})"));
    }

    /// <summary>Fallback ohne jede konkrete Übergangsregel — der Normalfall einer Runde, die implizit der
    /// unmittelbar vorangehenden Runde in der Liste folgt.</summary>
    public static IReadOnlyList<TeilnehmerAnzeige> PlatzhalterOhneRegelnGenerieren(
        TurnierRunde aktuelleRunde, TurnierRunde vorherigeRunde)
    {
        int anzahl = aktuelleRunde.EingangsMannschaftsanzahl;

        if (vorherigeRunde.Rundenart == Rundenart.Gruppenphase && vorherigeRunde.GruppenAnzahl.HasValue)
        {
            var gruppen = vorherigeRunde.GruppenAnzahl.Value;
            var proGruppe = vorherigeRunde.AusgangsMannschaftsanzahl / gruppen;
            var liste = new List<TeilnehmerAnzeige>();
            for (int platz = 1; platz <= proGruppe; platz++)
                for (int g = 0; g < gruppen; g++)
                    liste.Add(TeilnehmerAnzeige.AusPlatzhalter($"{platz}. Gruppe {GruppenBuchstabe(g + 1)}"));
            return liste.Take(anzahl).ToList();
        }

        if (vorherigeRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            var spiele = vorherigeRunde.EingangsMannschaftsanzahl / 2;
            string prefix = aktuelleRunde.Rundenart == Rundenart.SpielUmPlatzDrei ? "Verlierer" : "Sieger";
            return Enumerable.Range(1, Math.Min(spiele, anzahl))
                .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"{prefix} {vorherigeRunde.Name} {i}"))
                .ToList();
        }

        return Enumerable.Range(1, anzahl)
            .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Platz {i} ({vorherigeRunde.Name})"))
            .ToList();
    }

    private static string GruppenBuchstabe(int nr) => nr <= 26 ? ((char)('A' + nr - 1)).ToString() : $"G{nr}";
}
