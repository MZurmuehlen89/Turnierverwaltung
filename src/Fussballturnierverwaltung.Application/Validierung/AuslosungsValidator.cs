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
        IReadOnlyList<Mannschaft> verfuegbareMannschaften)
    {
        var fehler = new List<string>();

        if (!verfuegbareMannschaften.Any())
            fehler.Add("Keine Mannschaften für die Auslosung vorhanden.");
        else if (verfuegbareMannschaften.Count < 2)
            fehler.Add($"Mindestens 2 Mannschaften werden benötigt (vorhanden: {verfuegbareMannschaften.Count}).");

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
            var alleEintraege = lostoepfe.SelectMany(t => t.Eintraege).ToList();
            var doppelte = alleEintraege
                .GroupBy(e => e.MannschaftId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var id in doppelte)
            {
                var mann = verfuegbareMannschaften.FirstOrDefault(m => m.Id == id);
                fehler.Add($"Mannschaft '{mann?.Name ?? id.ToString()}' ist in mehreren Lostöpfen vorhanden.");
            }
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
