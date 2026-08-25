using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Vorlagen-Variante von <see cref="UebergangsregelValidator"/> (Issue #166-Folgefix) — validiert
/// die vollständige Übergangsstruktur einer Turniervorlage in einem Zug (alle Runden + alle Übergangsregeln
/// als beabsichtigter Endzustand, analog zur Block-Validierung), damit eine unvollständige oder nicht
/// schlüssige Struktur schon beim Anlegen der Vorlage auffällt statt erst beim späteren Laden in eine echte
/// Turnierdefinition. Runden werden über ihre 1-basierte Listenposition referenziert (siehe
/// <see cref="VorlagenUebergangsRegelEingabe"/>), da Vorlagen-Runden vor dem Speichern keine reale Id haben.</summary>
public class VorlagenUebergangsregelValidator
{
    public ValidationErgebnis Validieren(
        IReadOnlyList<VorlagenRundeEingabe> runden,
        IReadOnlyList<VorlagenUebergangsRegelEingabe> regeln)
    {
        var fehler = new List<string>();

        foreach (var regel in regeln)
        {
            if (regel.QuellReihenfolge < 1 || regel.QuellReihenfolge > runden.Count ||
                regel.ZielReihenfolge < 1 || regel.ZielReihenfolge > runden.Count)
            {
                fehler.Add("Übergangsregel referenziert eine nicht vorhandene Runde.");
                continue;
            }

            var quelle = runden[regel.QuellReihenfolge - 1];
            fehler.AddRange(UebergangsregelFeldValidierung.FeldFehlerErmitteln(
                regel.QuellReihenfolge == regel.ZielReihenfolge,
                quelle.Rundenart, quelle.Name, quelle.EingangsMannschaftsanzahl, quelle.GruppenAnzahl,
                regel.GruppeIndex, regel.PlatzierungVon, regel.PlatzierungBis, regel.AnzahlBeste));
        }

        // Gültige Referenzen vorausgesetzt für die folgenden Prüfungen (sonst schon oben gemeldet).
        var gueltigeRegeln = regeln
            .Where(r => r.QuellReihenfolge >= 1 && r.QuellReihenfolge <= runden.Count &&
                        r.ZielReihenfolge >= 1 && r.ZielReihenfolge <= runden.Count)
            .ToList();

        foreach (var dup in gueltigeRegeln
                     .Where(r => r.GruppeIndex is not null)
                     .GroupBy(r => (r.QuellReihenfolge, r.GruppeIndex))
                     .Where(g => g.Count() > 1))
            fehler.Add($"Gruppenindex/Spielnummer {dup.Key.GruppeIndex} ist für Runde " +
                       $"'{runden[dup.Key.QuellReihenfolge - 1].Name}' mehrfach vergeben.");

        foreach (var dup in gueltigeRegeln
                     .Where(r => r.AnzahlBeste is not null)
                     .GroupBy(r => (r.QuellReihenfolge, r.PlatzierungVon))
                     .Where(g => g.Count() > 1))
            fehler.Add($"Gruppenübergreifende Rangfolge-Regel für Platzierung {dup.Key.PlatzierungVon} ist für Runde " +
                       $"'{runden[dup.Key.QuellReihenfolge - 1].Name}' mehrfach vergeben.");

        if (HatZyklus(gueltigeRegeln))
            fehler.Add("Die Übergangsregeln der Vorlage enthalten einen zyklischen Übergang.");

        foreach (var zielGruppe in gueltigeRegeln.GroupBy(r => r.ZielReihenfolge))
        {
            var ziel = runden[zielGruppe.Key - 1];
            var summe = zielGruppe.Sum(r => r.AnzahlBeste ?? (r.PlatzierungBis - r.PlatzierungVon + 1));
            if (summe > ziel.EingangsMannschaftsanzahl)
                fehler.Add($"Übergangsregeln für '{ziel.Name}' liefern zusammen {summe} Mannschaften, " +
                           $"erwartet werden aber nur {ziel.EingangsMannschaftsanzahl}.");
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    private static bool HatZyklus(IReadOnlyList<VorlagenUebergangsRegelEingabe> regeln)
    {
        var kanten = regeln.ToLookup(r => r.QuellReihenfolge, r => r.ZielReihenfolge);
        var besucht = new HashSet<int>();
        var aufPfad = new HashSet<int>();

        bool Dfs(int knoten)
        {
            if (aufPfad.Contains(knoten)) return true;
            if (!besucht.Add(knoten)) return false;
            aufPfad.Add(knoten);
            foreach (var naechster in kanten[knoten])
                if (Dfs(naechster)) return true;
            aufPfad.Remove(knoten);
            return false;
        }

        return kanten.Select(g => g.Key).Any(Dfs);
    }
}
