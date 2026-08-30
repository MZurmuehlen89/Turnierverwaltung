using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>
/// Teilnehmer-/Platzhalter-Ermittlung für den Team-Pool einer Runde — gemeinsam genutzt von
/// <see cref="AuslosungViewModel"/> (echte Auslosungsarten) und
/// <see cref="ManuellePaarungWorkspaceViewModel"/> (Auslosungsart.Manuell, Issue #251), damit die
/// Logik nicht zweimal gepflegt werden muss. Ursprünglich Teil von AuslosungViewModel.LadeDetailAsync.
/// </summary>
public static class TeilnehmerPoolHilfsmittel
{
    /// <summary>Ermittelt die unmittelbar vorangehende Runde in der Liste (Issue #198): Finale und
    /// SpielUmPlatzDrei sind stets parallele Geschwisterrunden desselben Halbfinales, nicht sequenziell
    /// verkettet, und werden deshalb ausgeschlossen. Nur noch als Fallback relevant, wenn für die
    /// Zielrunde keine einzige eingehende Übergangsregel existiert (siehe <see cref="PlatzhalterOhneRegelnGenerieren"/>)
    /// — sobald mindestens eine Regel existiert, ist <see cref="PlatzhalterAusRegelnGenerieren"/> maßgeblich.</summary>
    public static TurnierRunde? VorgaengerrundeErmitteln(IReadOnlyList<TurnierRunde> instanzRunden, TurnierRunde aktuelleRunde) =>
        instanzRunden
            .Where(r => r.Reihenfolge < aktuelleRunde.Reihenfolge
                     && r.Rundenart is not (Rundenart.Finale or Rundenart.SpielUmPlatzDrei))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();

    /// <summary>Erzeugt den Platzhalter-Teilnehmer-Pool aus den tatsächlichen eingehenden Übergangsregeln
    /// einer Zielrunde — unterstützt dabei auch mehrere unabhängige Quellrunden gleichzeitig (Issue #251-
    /// Folgefund, Champions-League-Testfall: das Achtelfinale erhält Platz 1-8 direkt aus der Liga-Phase
    /// UND zusätzlich die Sieger der Playoff-Runde). Vorher wurde dafür ausschließlich die "nächstgelegene
    /// einzelne Vorgängerrunde" herangezogen (<see cref="VorgaengerrundeErmitteln"/>), was bei mehreren
    /// Quellrunden nur eine davon berücksichtigte und bei einer Playoff-Runde mit zufällig derselben
    /// Team-Anzahl wie die Zielrunde sogar zu irreführenden Platzhaltern führte (z.B. "Sieger Achtelfinale"
    /// als Eingang für die Runde "Achtelfinale" selbst, weil die Kurzform rein aus der Team-Anzahl
    /// abgeleitet wurde statt aus dem tatsächlichen Rundennamen). <paramref name="eingehendeRegeln"/> muss
    /// mit geladener QuellRunde-Navigation kommen (siehe UebergangsregelRepository.AlleNachZielRundeIdAsync)
    /// und wird als nicht leer vorausgesetzt — bei einer leeren Liste entscheidet der Aufrufer selbst über
    /// den Fallback (<see cref="PlatzhalterOhneRegelnGenerieren"/> oder echte Teams für die erste Runde).</summary>
    public static IReadOnlyList<TeilnehmerAnzeige> PlatzhalterAusRegelnGenerieren(
        IReadOnlyList<Uebergangsregel> eingehendeRegeln) =>
        eingehendeRegeln
            .OrderBy(r => r.QuellRunde.Reihenfolge)
            .ThenBy(r => r.GruppeIndex ?? 0)
            .ThenBy(r => r.PlatzierungVon)
            .SelectMany(PlatzhalterFuerRegel)
            .ToList();

    private static IEnumerable<TeilnehmerAnzeige> PlatzhalterFuerRegel(Uebergangsregel regel)
    {
        var quellRunde = regel.QuellRunde;

        if (quellRunde.Rundenart == Rundenart.Gruppenphase)
        {
            // Gruppenübergreifende Rangfolge-Regel (Issue #184): GruppeIndex ist dann null, PlatzierungVon
            // == PlatzierungBis (genau eine Platzierung) — die AnzahlBeste besten Teams an dieser
            // Platzierung über alle Gruppen hinweg (Issue #213: "N. bester X. Platz" statt normaler
            // "X. Gruppe Y"-Einträge, sonst würde ein simples Take(anzahl) willkürlich Gruppen abschneiden).
            if (regel.AnzahlBeste is { } anzahlBeste)
                return Enumerable.Range(1, anzahlBeste)
                    .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"{i}. bester {regel.PlatzierungVon}. Platz"));

            var buchstabe = regel.GruppeIndex is { } g ? GruppenBuchstabe(g) : "?";
            return Enumerable.Range(regel.PlatzierungVon, regel.PlatzierungBis - regel.PlatzierungVon + 1)
                .Select(platz => TeilnehmerAnzeige.AusPlatzhalter($"{platz}. Gruppe {buchstabe}"));
        }

        if (quellRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            // Ein Regel-Eintrag je Spiel der Quellrunde (GruppeIndex = Spielnummer), PlatzierungVon ==
            // PlatzierungBis per Konvention (1 = Sieger, 2 = Verlierer, siehe
            // UebergangsregelZeile.IstSieger/IstVerlierer in RundenUebergaengeViewModel.cs). Der
            // tatsächliche Rundenname statt einer aus der Team-Anzahl abgeleiteten Kurzform (Issue #251-
            // Folgefund) — vermeidet Kollisionen wie oben beschrieben.
            string prefix = regel.PlatzierungVon == 2 ? "Verlierer" : "Sieger";
            int spielNr = regel.GruppeIndex ?? 1;
            return [TeilnehmerAnzeige.AusPlatzhalter($"{prefix} {quellRunde.Name} {spielNr}")];
        }

        // Liga/SchweizerSystem/TopfLiga/Sonderrunde: flache Rangliste ohne Gruppen/Spiele.
        return Enumerable.Range(regel.PlatzierungVon, regel.PlatzierungBis - regel.PlatzierungVon + 1)
            .Select(platz => TeilnehmerAnzeige.AusPlatzhalter($"Platz {platz} ({quellRunde.Name})"));
    }

    /// <summary>Fallback ohne jede konkrete Übergangsregel — der Normalfall einer Runde, die implizit der
    /// unmittelbar vorangehenden Runde in der Liste folgt (EingangUeberUebergangsregeln = false, siehe
    /// RundenUebergaengeViewModel). Rein strukturelle Heuristik ohne Bezug zu tatsächlichen
    /// Platzierungsbereichen — Escape-Hatch für aktuell nicht modellierbare Fälle (Issue #177).</summary>
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
