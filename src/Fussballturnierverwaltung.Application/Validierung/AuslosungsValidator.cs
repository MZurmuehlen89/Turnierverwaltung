using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class AuslosungsValidator
{
    /// <summary>Validiert eine Auslosung vor dem Start. Die Prüfschritte melden ihre Fehler in fester Reihenfolge.</summary>
    public ValidationErgebnis Validieren(
        Auslosung auslosung,
        IReadOnlyList<LostopfEintrag> alleEintraege,
        IReadOnlyList<VerbandsQuote>? verbandsQuoten = null,
        IReadOnlyList<LandesQuote>? landesQuoten = null)
    {
        var lostoepfe = auslosung.Lostoepfe.ToList();
        var rundenart = auslosung.TurnierRunde?.Rundenart;

        string[] fehler =
        [
            .. TeilnehmerzahlPruefen(alleEintraege),
            .. VerbandsQuotenPruefen(alleEintraege, verbandsQuoten),
            .. LandesQuotenPruefen(alleEintraege, landesQuoten),
            .. LostoepfePruefen(auslosung, lostoepfe, alleEintraege),
            .. TopfZuordnungPruefen(auslosung, lostoepfe, rundenart),
            .. EineUrnePruefen(auslosung, lostoepfe),
            .. RundenerwartungPruefen(auslosung, alleEintraege, rundenart),
            .. RundenartKompatibilitaetPruefen(auslosung),
            .. TopfPaarePruefen(auslosung, lostoepfe, rundenart),
            .. GruppenphasePruefen(auslosung, lostoepfe, alleEintraege, rundenart),
            .. RegelkonfliktePruefen(auslosung),
        ];

        return fehler.Length == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern(fehler);
    }

    private static IEnumerable<string> TeilnehmerzahlPruefen(IReadOnlyList<LostopfEintrag> alleEintraege)
    {
        if (alleEintraege.Count == 0)
            yield return "Keine Teilnehmer für die Auslosung vorhanden.";
        else if (alleEintraege.Count < 2)
            yield return $"Mindestens 2 Teilnehmer werden benötigt (vorhanden: {alleEintraege.Count}).";
    }

    // ── Quoten ──────────────────────────────────────────────────────────────

    // Verband-Quoten (Issue #146/#27, feste Anzahl seit #154): unabhängig von Auslosungsart/Lostopf-Struktur —
    // prüft nur die Gesamtzusammensetzung der zugewiesenen Teams gegen die konfigurierte Anzahl je Verband.
    private static IEnumerable<string> VerbandsQuotenPruefen(
        IReadOnlyList<LostopfEintrag> alleEintraege, IReadOnlyList<VerbandsQuote>? verbandsQuoten) =>
        QuotenPruefen(
            verbandsQuoten,
            alleEintraege.Where(e => e.Mannschaft?.Land is not null).Select(e => e.Mannschaft!.Land.VerbandId),
            q => q.VerbandId,
            q => q.Anzahl,
            q => $"Verband-Quote '{q.Verband?.Name ?? $"Verband {q.VerbandId}"}'");

    // Landes-Quoten (Issue #189): wie die Verband-Quoten, aber auf Land statt Kontinentalverband skaliert —
    // relevant für Vereinswettbewerbe mit gesetztem Verbandsfilter (dort ist die Verband-Quote immer trivial).
    private static IEnumerable<string> LandesQuotenPruefen(
        IReadOnlyList<LostopfEintrag> alleEintraege, IReadOnlyList<LandesQuote>? landesQuoten) =>
        QuotenPruefen(
            landesQuoten,
            alleEintraege.Where(e => e.Mannschaft is not null).Select(e => e.Mannschaft!.LandId),
            q => q.LandId,
            q => q.Anzahl,
            q => $"Landes-Quote '{q.Land?.Name ?? $"Land {q.LandId}"}'");

    /// <summary>Jede Quote ist eine Mindestzahl (#572): mindestens so viele Teams mit dem Schlüssel (Verband bzw. Land),
    /// mehrere Quoten desselben Schlüssels (Kontingente eines Landes) zählen zusammen. Weitere Teams belegen freie
    /// Plätze; dass die Gesamtzahl stimmt, prüft <see cref="RundenerwartungPruefen"/>.</summary>
    private static IEnumerable<string> QuotenPruefen<TQuote>(
        IReadOnlyList<TQuote>? quoten,
        IEnumerable<int> zugewieseneSchluessel,
        Func<TQuote, int> schluessel,
        Func<TQuote, int> sollAnzahl,
        Func<TQuote, string> bezeichnung)
    {
        if (quoten is not { Count: > 0 })
            yield break;

        var anzahlProSchluessel = zugewieseneSchluessel.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());

        foreach (var gruppe in quoten.GroupBy(schluessel))
        {
            var anzahl = anzahlProSchluessel.GetValueOrDefault(gruppe.Key);
            var mindestens = gruppe.Sum(sollAnzahl);
            if (anzahl < mindestens)
                yield return $"{bezeichnung(gruppe.First())}: mindestens {mindestens} Team(s) erforderlich, " +
                             $"aber {anzahl} zugewiesen.";
        }
    }

    // ── Lostopf-Struktur ────────────────────────────────────────────────────

    private static IEnumerable<string> LostoepfePruefen(
        Auslosung auslosung, List<Lostopf> lostoepfe, IReadOnlyList<LostopfEintrag> alleEintraege)
    {
        if (lostoepfe.Count == 0)
        {
            if (auslosung.Auslosungsart == Auslosungsart.NachToepfen)
                yield return "Auslosungsart 'NachToepfen' erfordert mindestens einen Lostopf.";
            yield break;
        }

        foreach (var topf in lostoepfe.Where(t => t.Eintraege.Count == 0))
            yield return $"Lostopf '{topf.Name}' ist leer.";

        // Keine Mannschaft und kein Platzhalter darf in mehreren Töpfen sein
        var doppelteMannschaften = alleEintraege
            .Where(e => e.MannschaftId.HasValue)
            .GroupBy(e => e.MannschaftId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var id in doppelteMannschaften)
        {
            var name = alleEintraege.FirstOrDefault(e => e.MannschaftId == id)?.Mannschaft?.Name ?? id.ToString();
            yield return $"Mannschaft '{name}' ist in mehreren Lostöpfen vorhanden.";
        }

        var doppeltePlatzhalter = alleEintraege
            .Where(e => e.PlatzhalterName != null)
            .GroupBy(e => e.PlatzhalterName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var name in doppeltePlatzhalter)
            yield return $"Platzhalter '{name}' ist in mehreren Lostöpfen vorhanden.";
    }

    /// <summary>TopfZuordnung: mindestens 2 gleich große Töpfe.</summary>
    private static IEnumerable<string> TopfZuordnungPruefen(
        Auslosung auslosung, List<Lostopf> lostoepfe, Rundenart? rundenart)
    {
        if (auslosung.Auslosungsart != Auslosungsart.TopfZuordnung)
            yield break;

        if (lostoepfe.Count < 2)
            yield return "TopfZuordnung erfordert mindestens 2 Lostöpfe.";

        var topfGroessen = lostoepfe.Where(t => t.Eintraege.Count > 0).Select(t => t.Eintraege.Count).Distinct().ToList();
        if (topfGroessen.Count > 1)
            yield return "TopfZuordnung erfordert gleich große Lostöpfe " +
                         $"(gefunden: {string.Join(", ", topfGroessen.Select(g => $"{g} Teams"))}).";

        // Issue #347: jedes Team erhält aus dem eigenen Topf einen Heim- und einen anderen Auswärtsgegner —
        // bei nur 2 Teams wäre das zweimal dieselbe Paarung (Liga/Gruppenphase verteilen nur auf Gruppen).
        if (rundenart is not (Rundenart.Liga or Rundenart.Gruppenphase) && topfGroessen.Any(g => g < 3))
            yield return "TopfZuordnung erfordert mindestens 3 Teams je Lostopf.";

        // Issue #469: bei einem Gegner je Topf bilden die Töpfe 1/2, 3/4, … Paare.
        if (rundenart == Rundenart.TopfLiga && auslosung.TurnierRunde?.GegnerJeTopf == 1 && lostoepfe.Count % 2 != 0)
            yield return "Bei einem Gegner je Topf erfordert die TopfZuordnung eine gerade Anzahl Lostöpfe " +
                         "(die Töpfe 1/2, 3/4, … bilden Paare).";
    }

    /// <summary>Zufall/Benutzerdefiniert: höchstens 1 Lostopf (keine Topf-Struktur, eine Urne).</summary>
    private static IEnumerable<string> EineUrnePruefen(Auslosung auslosung, List<Lostopf> lostoepfe)
    {
        if (auslosung.Auslosungsart is not (Auslosungsart.Zufall or Auslosungsart.Benutzerdefiniert) ||
            lostoepfe.Count <= 1)
            yield break;

        var artName = auslosung.Auslosungsart == Auslosungsart.Zufall ? "Zufällig" : "Benutzerdefiniert";
        yield return $"Auslosungsart '{artName}' erfordert genau 1 Lostopf (keine Topf-Struktur). " +
                     "Bitte zusammenführen oder Auslosungsart wechseln.";
    }

    // ── Runde ───────────────────────────────────────────────────────────────

    /// <summary>Gesamtanzahl der zugewiesenen Teams gegen die Eingangsanzahl der Runde.</summary>
    private static IEnumerable<string> RundenerwartungPruefen(
        Auslosung auslosung, IReadOnlyList<LostopfEintrag> alleEintraege, Rundenart? rundenart)
    {
        if (auslosung.TurnierRunde?.EingangsMannschaftsanzahl is not (> 0 and var benoetigte))
            yield break;

        // Nur ein tatsächlich wirksames Freilos erlaubt ein Team weniger (#331: bei Töpfen/Setzliste gesperrt).
        bool freilosMoeglich = AuslosungsRegelKompatibilitaet.Wirkt(
            AuslosungsRegelArt.FreilosMoeglich, auslosung, rundenart);
        int min = freilosMoeglich ? benoetigte - 1 : benoetigte;

        if (alleEintraege.Count < min)
            yield return $"Die Runde erwartet {benoetigte} Teams" +
                         (freilosMoeglich ? $" (oder mindestens {min} mit Freilos)" : "") +
                         $", aber es sind erst {alleEintraege.Count} zugewiesen.";
        else if (alleEintraege.Count > benoetigte)
            yield return $"Die Runde erwartet {benoetigte} Teams, " +
                         $"aber es sind {alleEintraege.Count} zugewiesen " +
                         $"({alleEintraege.Count - benoetigte} zu viel).";
    }

    /// <summary>Auslosungsart und Rundenart auf Kompatibilität prüfen (Issue #94: zentrale Matrix statt separat
    /// gepflegter Regeln, dieselbe Quelle wie das Ausblenden in der Auswahl).</summary>
    private static IEnumerable<string> RundenartKompatibilitaetPruefen(Auslosung auslosung)
    {
        if (auslosung.TurnierRunde is { } runde &&
            AuslosungsKompatibilitaet.InkompatibilitaetsGrund(
                runde.Rundenart, auslosung.Auslosungsart, runde.PaarungFestVorgegeben) is { } grund)
            yield return grund;
    }

    /// <summary>Paarungsmodus (nicht Gruppenphase): 'Nach Töpfen' und 'Gruppensieger vs. Zweite' losen die
    /// Lostöpfe im Duell Topf 1↔2, Topf 3↔4 usw. gegeneinander aus. Weniger als 2 Töpfe liefert keine einzige
    /// Paarung, eine ungerade Anzahl lässt den letzten Topf stillschweigend unverlost.</summary>
    private static IEnumerable<string> TopfPaarePruefen(
        Auslosung auslosung, List<Lostopf> lostoepfe, Rundenart? rundenart)
    {
        bool istPaarungsmodusMitTopfPaaren =
            auslosung.Auslosungsart == Auslosungsart.GruppensiegerGegenGruppenzweite ||
            (auslosung.Auslosungsart == Auslosungsart.NachToepfen && rundenart != Rundenart.Gruppenphase);
        if (!istPaarungsmodusMitTopfPaaren)
            yield break;

        var artName = auslosung.Auslosungsart == Auslosungsart.NachToepfen
            ? "Nach Töpfen" : "Gruppensieger vs. Zweite";
        if (lostoepfe.Count < 2)
            yield return $"Auslosungsart '{artName}' erfordert mindestens 2 Lostöpfe " +
                         "(je zwei Töpfe werden gegeneinander gelost).";
        else if (lostoepfe.Count % 2 != 0)
            yield return $"Auslosungsart '{artName}' erfordert eine gerade Anzahl Lostöpfe " +
                         $"(gefunden: {lostoepfe.Count}) — sonst bleibt der letzte Topf unverlost.";
    }

    // ── Gruppenphase ────────────────────────────────────────────────────────

    /// <summary>Gruppenphase: Gruppenanzahl, gleichmäßige Verteilung und bei 'Nach Töpfen' die Topfgröße.</summary>
    private static IEnumerable<string> GruppenphasePruefen(
        Auslosung auslosung, List<Lostopf> lostoepfe, IReadOnlyList<LostopfEintrag> alleEintraege,
        Rundenart? rundenart)
    {
        if (rundenart != Rundenart.Gruppenphase)
            yield break;

        if (auslosung.TurnierRunde?.GruppenAnzahl is not (>= 2 and var gruppenAnzahl))
        {
            yield return "Gruppenphase: Die Runde benötigt eine definierte Gruppenanzahl (mindestens 2).";
            yield break;
        }

        if (alleEintraege.Count % gruppenAnzahl != 0)
            yield return $"Gruppenphase: {alleEintraege.Count} Teams können nicht gleichmäßig auf " +
                         $"{gruppenAnzahl} Gruppen verteilt werden " +
                         $"({alleEintraege.Count % gruppenAnzahl} Team(s) übrig).";

        if (auslosung.Auslosungsart == Auslosungsart.NachToepfen)
            foreach (var meldung in GruppentoepfePruefen(lostoepfe, gruppenAnzahl))
                yield return meldung;
    }

    /// <summary>Gruppenphase mit 'Nach Töpfen': jeder Topf liefert genau ein Team je Gruppe.</summary>
    private static IEnumerable<string> GruppentoepfePruefen(List<Lostopf> lostoepfe, int gruppenAnzahl)
    {
        var falscheToepfe = lostoepfe
            .Where(t => t.Eintraege.Count != gruppenAnzahl)
            .Select(t => $"'{t.Name}' ({t.Eintraege.Count})")
            .ToList();
        if (falscheToepfe.Count > 0)
            yield return $"Gruppenphase mit 'Nach Töpfen': Jeder Topf muss genau {gruppenAnzahl} Team(s) enthalten " +
                         $"(je eines pro Gruppe). Abweichung bei: {string.Join(", ", falscheToepfe)}.";
    }

    // ── Regeln ──────────────────────────────────────────────────────────────

    /// <summary>Regel-Konflikte — die Oberfläche verhindert das bereits (Häkchen schließen sich gegenseitig aus,
    /// #331), die Prüfung bleibt als Absicherung für Altdaten.</summary>
    private static IEnumerable<string> RegelkonfliktePruefen(Auslosung auslosung)
    {
        var heimrechtRegeln = auslosung.Regeln
            .Where(r => r.IstAktiv)
            .Select(r => r.Regelart)
            .Where(AuslosungsRegelKompatibilitaet.IstHeimrechtRegel)
            .Distinct()
            .ToList();
        if (heimrechtRegeln.Count > 1)
            yield return "Regelkonflikt: " +
                         string.Join(", ", heimrechtRegeln.Select(r => $"'{AuslosungsRegelKompatibilitaet.AnzeigeName(r)}'")) +
                         " legen alle das Heimrecht fest und schließen sich gegenseitig aus.";
    }

    /// <summary>Prüft ob ein Pairing-Schritt bei manueller Auslosung gültig ist.</summary>
    public ValidationErgebnis PaarungValidieren(
        Mannschaft kandidat,
        Mannschaft? bisherGewaehlt,
        IReadOnlyList<AuslosungsRegel> aktiveRegeln)
    {
        if (bisherGewaehlt is null) return ValidationErgebnis.Ok();

        bool gleichesLandVerboten =
            aktiveRegeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) &&
            kandidat.LandId == bisherGewaehlt.LandId;

        return gleichesLandVerboten
            ? ValidationErgebnis.MitFehlern(
                $"Regel 'KeinGleichesLand': '{kandidat.Name}' und '{bisherGewaehlt.Name}' " +
                "sind aus demselben Land und dürfen nicht gepaart werden.")
            : ValidationErgebnis.Ok();
    }
}
