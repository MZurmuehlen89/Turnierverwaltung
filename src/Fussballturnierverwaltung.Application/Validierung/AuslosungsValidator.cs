using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class AuslosungsValidator
{
    /// <summary>Validiert eine Auslosung vor dem Start. Vollständige Lösbarkeitsanalyse kommt in Schritt 11.</summary>
    public ValidationErgebnis Validieren(
        Auslosung auslosung,
        IReadOnlyList<LostopfEintrag> alleEintraege)
    {
        var fehler = new List<string>();

        if (!alleEintraege.Any())
            fehler.Add("Keine Teilnehmer für die Auslosung vorhanden.");
        else if (alleEintraege.Count < 2)
            fehler.Add($"Mindestens 2 Teilnehmer werden benötigt (vorhanden: {alleEintraege.Count}).");

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
        }

        // Auslosungsart und Rundenart auf Kompatibilität prüfen
        var rundenart = auslosung.TurnierRunde?.Rundenart;
        if (rundenart.HasValue)
        {
            if (auslosung.Auslosungsart == Auslosungsart.GruppensiegerGegenGruppenzweite &&
                rundenart.Value is not (Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei))
                fehler.Add("Auslosungsart 'Gruppensieger vs. Zweite' ist nur für K.O.-Runden geeignet, " +
                           "die auf eine Gruppenphase folgen.");

            if (auslosung.Auslosungsart == Auslosungsart.TopfZuordnung &&
                rundenart.Value is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
                fehler.Add("Auslosungsart 'Topf-Zuordnung' weist Teams direkt Gruppenplätzen zu " +
                           "und ist für K.O.-Runden nicht geeignet. " +
                           "Verwenden Sie stattdessen 'Zufällig' oder 'Nach Töpfen'.");
        }

        // Regel-Konflikte prüfen
        var aktiveRegeln = auslosung.Regeln.Where(r => r.IstAktiv).Select(r => r.Regelart).ToList();

        if (aktiveRegeln.Contains(AuslosungsRegelArt.GruppensiegerGegenGruppenzweite) &&
            aktiveRegeln.Contains(AuslosungsRegelArt.GesetzteGegenUngesetzte))
            fehler.Add("Regelkonflikt: 'GruppensiegerGegenGruppenzweite' und 'GesetzteGegenUngesetzte' " +
                       "können nicht gleichzeitig aktiv sein.");

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
