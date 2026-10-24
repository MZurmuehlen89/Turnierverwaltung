using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class UebergangsregelValidator
{
    /// <summary>
    /// Validiert eine Übergangsregel gegen die geladene Quell- und Zielrunde. Die Bedeutung von <see cref="Uebergangsregel.GruppeIndex"/>
    /// hängt von der Rundenart der Quellrunde ab: Gruppenphase → Gruppennummer, K.-o.-Runde/Finale → Spielnummer innerhalb
    /// der Runde (Platzierung 1 = Sieger, 2 = Verlierer), Liga → nicht verwendet (Platzierung = Tabellenrang).
    /// </summary>
    public ValidationErgebnis Validieren(
        UebergangsregelEingabe eingabe,
        TurnierRunde quellRunde,
        TurnierRunde zielRunde,
        IReadOnlyList<Uebergangsregel> alleRegeln,
        int? bearbeiteteRegelId = null)
    {
        var kanten = alleRegeln.Where(r => r.Id != bearbeiteteRegelId).ToList();
        var fehler = new List<string>(FeldFehlerErmitteln(eingabe, quellRunde));

        if (UebergangsregelFeldValidierung.WuerdeZyklusSchliessen(
                eingabe.QuellRundeId, eingabe.ZielRundeId, kanten.Select(r => (r.QuellRundeId, r.ZielRundeId))))
            fehler.Add($"Übergangsregel '{quellRunde.Name}' → '{zielRunde.Name}' würde einen zyklischen Übergang erzeugen.");

        var summe = alleRegeln
            .Where(r => r.ZielRundeId == eingabe.ZielRundeId && r.Id != bearbeiteteRegelId)
            .Sum(r => r.AnzahlBeste ?? (r.PlatzierungBis - r.PlatzierungVon + 1))
            + (eingabe.AnzahlBeste ?? (eingabe.PlatzierungBis - eingabe.PlatzierungVon + 1));

        if (summe > zielRunde.EingangsMannschaftsanzahl)
            fehler.Add($"Übergangsregeln für '{zielRunde.Name}' liefern zusammen {summe} Mannschaften, " +
                       $"erwartet werden aber nur {zielRunde.EingangsMannschaftsanzahl}.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>
    /// Validiert einen gesamten Übergangsregel-Block (alle Zeilen einer Quellrunde → Zielrunde) auf einmal, gegen
    /// den vollständigen beabsichtigten Endzustand — nicht zeilenweise gegen einen Teilstand (Issue #180). Ersetzt
    /// dabei die bereits gespeicherten Regeln dieses Blocks vollständig, d.h. <paramref name="alleRegeln"/> wird bei
    /// Kapazitäts- und Zyklusprüfung um alle Zeilen mit <c>QuellRundeId == quellRunde.Id</c> bereinigt.
    /// </summary>
    public ValidationErgebnis Validieren(
        IReadOnlyList<UebergangsregelEingabe> eingaben,
        TurnierRunde quellRunde,
        TurnierRunde zielRunde,
        IReadOnlyList<Uebergangsregel> alleRegeln)
    {
        var fehler = new List<string>();

        if (eingaben.Count == 0)
            fehler.Add("Ein Übergangsregel-Block muss mindestens eine Zeile enthalten.");

        foreach (var eingabe in eingaben)
            fehler.AddRange(FeldFehlerErmitteln(eingabe, quellRunde));

        var duplikate = eingaben
            .Where(e => e.GruppeIndex is not null)
            .GroupBy(e => e.GruppeIndex)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var dup in duplikate)
            fehler.Add($"Gruppenindex/Spielnummer {dup} ist mehrfach in diesem Übergangsregel-Block vorhanden.");

        var anzahlBesteDuplikate = eingaben
            .Where(e => e.AnzahlBeste is not null)
            .GroupBy(e => e.PlatzierungVon)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var dup in anzahlBesteDuplikate)
            fehler.Add($"Gruppenübergreifende Rangfolge-Regel für Platzierung {dup} ist mehrfach in diesem Übergangsregel-Block vorhanden.");

        var kanten = alleRegeln.Where(r => !(r.QuellRundeId == quellRunde.Id && r.ZielRundeId == zielRunde.Id)).ToList();
        if (UebergangsregelFeldValidierung.WuerdeZyklusSchliessen(
                quellRunde.Id, zielRunde.Id, kanten.Select(r => (r.QuellRundeId, r.ZielRundeId))))
            fehler.Add($"Übergangsregel '{quellRunde.Name}' → '{zielRunde.Name}' würde einen zyklischen Übergang erzeugen.");

        var summeAndererBloecke = alleRegeln
            .Where(r => r.ZielRundeId == zielRunde.Id && r.QuellRundeId != quellRunde.Id)
            .Sum(r => r.AnzahlBeste ?? (r.PlatzierungBis - r.PlatzierungVon + 1));
        var summe = summeAndererBloecke + eingaben.Sum(e => e.AnzahlBeste ?? (e.PlatzierungBis - e.PlatzierungVon + 1));

        if (summe > zielRunde.EingangsMannschaftsanzahl)
            fehler.Add($"Übergangsregeln für '{zielRunde.Name}' liefern zusammen {summe} Mannschaften, " +
                       $"erwartet werden aber nur {zielRunde.EingangsMannschaftsanzahl}.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }


    /// <summary>Ganzgraph-Validierung (Issue #294, Nachfolger von <c>VorlagenUebergangsregelValidator.Validieren</c>):
    /// validiert alle Runden + alle Übergangsregeln als beabsichtigten Endzustand in einem Zug (Zyklus-Check,
    /// Summen exakt statt "≤", Duplikat-Checks) — für den Vollersatz-Fall, in dem Runden noch keine reale Id
    /// haben und daher per 1-basierter Reihenfolge referenziert werden (siehe <see cref="UebergangsregelPositionsEingabe"/>).</summary>
    public ValidationErgebnis Validieren(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> regeln)
    {
        var fehler = new List<string>();

        RegelfelderPruefen(runden, regeln, fehler);

        // Gültige Referenzen vorausgesetzt für die folgenden Prüfungen (sonst schon oben gemeldet).
        var gueltigeRegeln = regeln
            .Where(r => r.QuellReihenfolge >= 1 && r.QuellReihenfolge <= runden.Count &&
                        r.ZielReihenfolge >= 1 && r.ZielReihenfolge <= runden.Count)
            .ToList();

        DuplikatePruefen(runden, gueltigeRegeln, fehler);

        if (HatZyklus(gueltigeRegeln))
            fehler.Add("Die Übergangsregeln enthalten einen zyklischen Übergang.");

        EingehendeSummenPruefen(runden, gueltigeRegeln, fehler);
        AusgehendeSummenPruefen(runden, gueltigeRegeln, fehler);

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    private static void RegelfelderPruefen(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> regeln, List<string> fehler)
    {
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
                regel.QuellReihenfolge == regel.ZielReihenfolge, quelle,
                regel.GruppeIndex, regel.PlatzierungVon, regel.PlatzierungBis, regel.AnzahlBeste));
        }
    }

    private static void DuplikatePruefen(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> gueltigeRegeln, List<string> fehler)
    {
        // Gruppiert zusätzlich nach ZielReihenfolge: derselbe Gruppenindex/dieselbe Spielnummer darf mehrfach
        // auftreten, wenn er/sie (mit unterschiedlicher Platzierung) auf getrennte Zielrunden verteilt wird
        // — der K.o.-Split-Sonderfall (Sieger UND Verlierer eines Spiels in zwei Zielrunden, Issue #334).
        // Mehrfachvergabe an dieselbe Zielrunde bleibt ein Fehler (redundante/uneindeutige Zeilen).
        foreach (var dup in gueltigeRegeln
                     .Where(r => r.GruppeIndex is not null)
                     .GroupBy(r => (r.QuellReihenfolge, r.GruppeIndex, r.ZielReihenfolge))
                     .Where(g => g.Count() > 1))
            fehler.Add($"Gruppenindex/Spielnummer {dup.Key.GruppeIndex} ist für Runde " +
                       $"'{runden[dup.Key.QuellReihenfolge - 1].Name}' mehrfach vergeben.");

        foreach (var dup in gueltigeRegeln
                     .Where(r => r.AnzahlBeste is not null)
                     .GroupBy(r => (r.QuellReihenfolge, r.PlatzierungVon))
                     .Where(g => g.Count() > 1))
            fehler.Add($"Gruppenübergreifende Rangfolge-Regel für Platzierung {dup.Key.PlatzierungVon} ist für Runde " +
                       $"'{runden[dup.Key.QuellReihenfolge - 1].Name}' mehrfach vergeben.");
    }

    private static void EingehendeSummenPruefen(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> gueltigeRegeln, List<string> fehler)
    {
        // Eingehend: eine Runde ganz ohne eingehende Übergangsregeln gilt als eigenständiger Startpunkt
        // (z.B. mehrere parallele Gruppenphasen/Ligen ohne gemeinsame Vorrunde) und wird hier bewusst nicht
        // verlangt. Hat eine Runde aber MINDESTENS eine eingehende Regel, muss deren Summe exakt der
        // Eingangsmannschaftszahl entsprechen, unabhängig davon, aus wie vielen Quellrunden sie sich zusammensetzt.
        var eingehendNachZiel = gueltigeRegeln.ToLookup(r => r.ZielReihenfolge);
        for (var i = 0; i < runden.Count; i++)
        {
            var regelnDiesesZiels = eingehendNachZiel[i + 1].ToList();
            if (regelnDiesesZiels.Count == 0) continue;

            var ziel = runden[i];
            var summe = regelnDiesesZiels.Sum(r => r.AnzahlBeste ?? (r.PlatzierungBis - r.PlatzierungVon + 1));
            if (summe != ziel.EingangsMannschaftsanzahl)
                fehler.Add($"Übergangsregeln für '{ziel.Name}' liefern zusammen {summe} Mannschaften, " +
                           $"erwartet werden aber {ziel.EingangsMannschaftsanzahl}.");
        }
    }

    private static void AusgehendeSummenPruefen(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> gueltigeRegeln, List<string> fehler)
    {
        // Ausgehend: eine Runde darf terminal sein (keine ausgehenden Regeln, z.B. Finale) — hat sie aber
        // mindestens eine, muss die Summe exakt der Ausgangsmannschaftszahl entsprechen, unabhängig davon,
        // in wie viele Zielrunden sie sich aufteilt (z.B. Sieger→Finale, Verlierer→Spiel um Platz 3).
        var ausgehendNachQuelle = gueltigeRegeln.ToLookup(r => r.QuellReihenfolge);
        for (var i = 0; i < runden.Count; i++)
        {
            var regelnDieserQuelle = ausgehendNachQuelle[i + 1].ToList();
            if (regelnDieserQuelle.Count == 0) continue;

            var quelle = runden[i];
            var summe = regelnDieserQuelle.Sum(r => r.AnzahlBeste ?? (r.PlatzierungBis - r.PlatzierungVon + 1));
            if (summe != quelle.AusgangsMannschaftsanzahl)
                fehler.Add($"Ausgehende Übergangsregeln von '{quelle.Name}' verteilen zusammen {summe} Mannschaften, " +
                           $"erwartet werden aber {quelle.AusgangsMannschaftsanzahl}.");
        }
    }

    /// <summary>Rundenart-spezifische Feldprüfungen einer einzelnen Eingabe (Gruppenindex/Spielnummer-Bereich,
    /// Platzierungsbereich innerhalb der Quellrunde). Wird sowohl von der Einzel- als auch der Block-Validierung genutzt.</summary>
    private static IEnumerable<string> FeldFehlerErmitteln(UebergangsregelEingabe eingabe, TurnierRunde quellRunde)
        => UebergangsregelFeldValidierung.FeldFehlerErmitteln(
            eingabe.QuellRundeId == eingabe.ZielRundeId, quellRunde,
            eingabe.GruppeIndex, eingabe.PlatzierungVon, eingabe.PlatzierungBis, eingabe.AnzahlBeste);

    private static bool HatZyklus(IReadOnlyList<UebergangsregelPositionsEingabe> regeln)
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
