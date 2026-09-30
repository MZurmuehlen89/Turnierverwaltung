using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class AuslosungsValidator
{
    /// <summary>Validiert eine Auslosung vor dem Start. Vollständige Lösbarkeitsanalyse kommt in Schritt 11.</summary>
    public ValidationErgebnis Validieren(
        Auslosung auslosung,
        IReadOnlyList<LostopfEintrag> alleEintraege,
        IReadOnlyList<VerbandsQuote>? verbandsQuoten = null,
        IReadOnlyList<LandesQuote>? landesQuoten = null)
    {
        var fehler = new List<string>();

        if (!alleEintraege.Any())
            fehler.Add("Keine Teilnehmer für die Auslosung vorhanden.");
        else if (alleEintraege.Count < 2)
            fehler.Add($"Mindestens 2 Teilnehmer werden benötigt (vorhanden: {alleEintraege.Count}).");

        // Verband-Quoten (Issue #146/#27, feste Anzahl seit #154): unabhängig von Auslosungsart/Lostopf-Struktur —
        // prüft nur die Gesamtzusammensetzung der zugewiesenen Teams gegen die konfigurierte Anzahl je Verband.
        if (verbandsQuoten is { Count: > 0 })
        {
            var anzahlProVerband = alleEintraege
                .Where(e => e.Mannschaft?.Land is not null)
                .GroupBy(e => e.Mannschaft!.Land!.VerbandId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var quote in verbandsQuoten)
            {
                var anzahl = anzahlProVerband.GetValueOrDefault(quote.VerbandId);
                var verbandName = quote.Verband?.Name ?? $"Verband {quote.VerbandId}";

                if (anzahl != quote.Anzahl)
                    fehler.Add($"Verband-Quote '{verbandName}': genau {quote.Anzahl} Team(s) erforderlich, " +
                               $"aber {anzahl} zugewiesen.");
            }
        }

        // Landes-Quoten (Issue #189): analog Verband-Quoten oben, aber auf Land statt Kontinentalverband
        // skaliert — relevant für Vereinswettbewerbe mit gesetztem Verbandsfilter (dort ist die
        // Verband-Quote immer trivial, siehe oben). Mannschaft.LandId ist ein direktes Scalar-Feld,
        // keine Indirektion über Land.VerbandId nötig.
        if (landesQuoten is { Count: > 0 })
        {
            var anzahlProLand = alleEintraege
                .Where(e => e.Mannschaft is not null)
                .GroupBy(e => e.Mannschaft!.LandId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var quote in landesQuoten)
            {
                var anzahl = anzahlProLand.GetValueOrDefault(quote.LandId);
                var landName = quote.Land?.Name ?? $"Land {quote.LandId}";

                if (anzahl != quote.Anzahl)
                    fehler.Add($"Landes-Quote '{landName}': genau {quote.Anzahl} Team(s) erforderlich, " +
                               $"aber {anzahl} zugewiesen.");
            }
        }

        // Lostöpfe prüfen
        var lostoepfe = auslosung.Lostoepfe.ToList();
        if (!lostoepfe.Any())
        {
            if (auslosung.Auslosungsart == Auslosungsart.NachToepfen)
                fehler.Add("Auslosungsart 'NachToepfen' erfordert mindestens einen Lostopf.");
        }
        else
        {
            foreach (var topf in lostoepfe.Where(t => !t.Eintraege.Any()))
                fehler.Add($"Lostopf '{topf.Name}' ist leer.");

            // Keine Mannschaft darf in mehreren Töpfen sein
            var doppelteMannschaften = alleEintraege
                .Where(e => e.MannschaftId.HasValue)
                .GroupBy(e => e.MannschaftId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var id in doppelteMannschaften)
            {
                var name = alleEintraege.FirstOrDefault(e => e.MannschaftId == id)?.Mannschaft?.Name ?? id.ToString();
                fehler.Add($"Mannschaft '{name}' ist in mehreren Lostöpfen vorhanden.");
            }

            // Kein Platzhalter darf doppelt vorkommen
            var doppeltePlatzhalter = alleEintraege
                .Where(e => e.PlatzhalterName != null)
                .GroupBy(e => e.PlatzhalterName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var name in doppeltePlatzhalter)
                fehler.Add($"Platzhalter '{name}' ist in mehreren Lostöpfen vorhanden.");
        }

        // TopfZuordnung: gleich große Töpfe, mindestens 2 Töpfe
        if (auslosung.Auslosungsart == Auslosungsart.TopfZuordnung)
        {
            if (lostoepfe.Count < 2)
                fehler.Add("TopfZuordnung erfordert mindestens 2 Lostöpfe.");

            var topfGroessen = lostoepfe.Where(t => t.Eintraege.Any()).Select(t => t.Eintraege.Count).Distinct().ToList();
            if (topfGroessen.Count > 1)
                fehler.Add("TopfZuordnung erfordert gleich große Lostöpfe " +
                           $"(gefunden: {string.Join(", ", topfGroessen.Select(g => $"{g} Teams"))}).");

            // Issue #347: jedes Team erhält aus dem eigenen Topf einen Heim- und einen anderen Auswärtsgegner —
            // bei nur 2 Teams wäre das zweimal dieselbe Paarung (Liga/Gruppenphase verteilen nur auf Gruppen).
            if (auslosung.TurnierRunde?.Rundenart is not (Rundenart.Liga or Rundenart.Gruppenphase) &&
                topfGroessen.Any(g => g < 3))
                fehler.Add("TopfZuordnung erfordert mindestens 3 Teams je Lostopf.");
        }

        // Zufall/Benutzerdefiniert: höchstens 1 Lostopf (keine Topf-Struktur, eine Urne)
        if (auslosung.Auslosungsart is Auslosungsart.Zufall or Auslosungsart.Benutzerdefiniert &&
            lostoepfe.Count > 1)
            fehler.Add($"Auslosungsart '{(auslosung.Auslosungsart == Auslosungsart.Zufall ? "Zufällig" : "Benutzerdefiniert")}' " +
                       "erfordert genau 1 Lostopf (keine Topf-Struktur). Bitte zusammenführen oder Auslosungsart wechseln.");

        // Gesamtanzahl gegen Rundenerwartung prüfen
        var aktiveRegelnVorab = auslosung.Regeln.Where(r => r.IstAktiv).Select(r => r.Regelart).ToList();
        var benoetigte = auslosung.TurnierRunde?.EingangsMannschaftsanzahl;
        if (benoetigte is > 0)
        {
            // Nur ein tatsächlich wirksames Freilos erlaubt ein Team weniger (#331: bei Töpfen/Setzliste gesperrt).
            bool freilosMoeglich = AuslosungsRegelKompatibilitaet.Wirkt(
                AuslosungsRegelArt.FreilosMoeglich, aktiveRegelnVorab.Contains(AuslosungsRegelArt.FreilosMoeglich),
                auslosung.Auslosungsart, auslosung.TurnierRunde?.Rundenart, folgtAufGruppenphase: true);
            int min = freilosMoeglich ? benoetigte.Value - 1 : benoetigte.Value;
            if (alleEintraege.Count < min)
                fehler.Add($"Die Runde erwartet {benoetigte} Teams" +
                           (freilosMoeglich ? $" (oder mindestens {min} mit Freilos)" : "") +
                           $", aber es sind erst {alleEintraege.Count} zugewiesen.");
            else if (alleEintraege.Count > benoetigte.Value)
                fehler.Add($"Die Runde erwartet {benoetigte} Teams, " +
                           $"aber es sind {alleEintraege.Count} zugewiesen " +
                           $"({alleEintraege.Count - benoetigte.Value} zu viel).");
        }

        // Auslosungsart und Rundenart auf Kompatibilität prüfen (Issue #94: zentrale Matrix statt
        // separat gepflegter Regeln, dieselbe Quelle wie das Ausblenden in der WPF-ComboBox).
        var rundenart = auslosung.TurnierRunde?.Rundenart;
        if (rundenart.HasValue &&
            AuslosungsKompatibilitaet.InkompatibilitaetsGrund(rundenart.Value, auslosung.Auslosungsart) is { } grund)
            fehler.Add(grund);

        // Paarungsmodus (nicht Gruppenphase): 'Nach Töpfen' und 'Gruppensieger vs. Zweite' losen die
        // Lostöpfe im Duell Topf 1↔2, Topf 3↔4 usw. gegeneinander aus. Weniger als 2 Töpfe liefert
        // keine einzige Paarung, eine ungerade Anzahl lässt den letzten Topf stillschweigend unverlost.
        bool istPaarungsmodusMitTopfPaaren =
            auslosung.Auslosungsart == Auslosungsart.GruppensiegerGegenGruppenzweite ||
            (auslosung.Auslosungsart == Auslosungsart.NachToepfen && rundenart != Rundenart.Gruppenphase);
        if (istPaarungsmodusMitTopfPaaren)
        {
            var artName = auslosung.Auslosungsart == Auslosungsart.NachToepfen
                ? "Nach Töpfen" : "Gruppensieger vs. Zweite";
            if (lostoepfe.Count < 2)
                fehler.Add($"Auslosungsart '{artName}' erfordert mindestens 2 Lostöpfe " +
                           "(je zwei Töpfe werden gegeneinander gelost).");
            else if (lostoepfe.Count % 2 != 0)
                fehler.Add($"Auslosungsart '{artName}' erfordert eine gerade Anzahl Lostöpfe " +
                           $"(gefunden: {lostoepfe.Count}) — sonst bleibt der letzte Topf unverlost.");
        }

        // Gruppenphase: GruppenAnzahl und Teilnehmerzahl prüfen
        if (rundenart == Rundenart.Gruppenphase)
        {
            var gruppenAnzahl = auslosung.TurnierRunde?.GruppenAnzahl;
            if (!gruppenAnzahl.HasValue || gruppenAnzahl.Value < 2)
            {
                fehler.Add("Gruppenphase: Die Runde benötigt eine definierte Gruppenanzahl (mindestens 2).");
            }
            else
            {
                if (alleEintraege.Count % gruppenAnzahl.Value != 0)
                    fehler.Add($"Gruppenphase: {alleEintraege.Count} Teams können nicht gleichmäßig auf " +
                               $"{gruppenAnzahl.Value} Gruppen verteilt werden " +
                               $"({alleEintraege.Count % gruppenAnzahl.Value} Team(s) übrig).");

                if (auslosung.Auslosungsart == Auslosungsart.NachToepfen && lostoepfe.Any())
                {
                    var falscheToepfe = lostoepfe
                        .Where(t => t.Eintraege.Count != gruppenAnzahl.Value)
                        .Select(t => $"'{t.Name}' ({t.Eintraege.Count})")
                        .ToList();
                    if (falscheToepfe.Any())
                        fehler.Add($"Gruppenphase mit 'Nach Töpfen': Jeder Topf muss genau {gruppenAnzahl.Value} Team(s) enthalten " +
                                   $"(je eines pro Gruppe). Abweichung bei: {string.Join(", ", falscheToepfe)}.");
                }
            }
        }

        // Regel-Konflikte prüfen — die Oberfläche verhindert das bereits (Häkchen schließen sich gegenseitig
        // aus, #331), die Prüfung bleibt als Absicherung für Altdaten.
        var aktiveRegeln = aktiveRegelnVorab;

        if (aktiveRegeln.Contains(AuslosungsRegelArt.HeimrechtNachLos) &&
            aktiveRegeln.Contains(AuslosungsRegelArt.HeimrechtNachSetzliste))
            fehler.Add("Regelkonflikt: 'HeimrechtNachLos' und 'HeimrechtNachSetzliste' schließen sich gegenseitig aus.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Prüft ob ein Pairing-Schritt bei manueller Auslosung gültig ist.</summary>
    public ValidationErgebnis PaarungValidieren(
        Mannschaft kandidat,
        Mannschaft? bisherGewaehlt,
        IReadOnlyList<AuslosungsRegel> aktiveRegeln)
    {
        var fehler = new List<string>();

        if (bisherGewaehlt is null) return ValidationErgebnis.Ok();

        if (aktiveRegeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) &&
            kandidat.LandId == bisherGewaehlt.LandId)
            fehler.Add($"Regel 'KeinGleichesLand': '{kandidat.Name}' und '{bisherGewaehlt.Name}' " +
                       "sind aus demselben Land und dürfen nicht gepaart werden.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
