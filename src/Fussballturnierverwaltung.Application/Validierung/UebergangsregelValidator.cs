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
        var fehler = new List<string>(FeldFehlerErmitteln(eingabe, quellRunde).Select(f => f.Text));

        if (UebergangsregelFeldValidierung.WuerdeZyklusSchliessen(
                eingabe.QuellRundeId, eingabe.ZielRundeId, kanten.Select(r => (r.QuellRundeId, r.ZielRundeId))))
            fehler.Add($"Übergangsregel '{quellRunde.Name}' → '{zielRunde.Name}' würde einen zyklischen Übergang erzeugen.");

        var summe = alleRegeln
            .Where(r => r.ZielRundeId == eingabe.ZielRundeId && r.Id != bearbeiteteRegelId)
            .Sum(r => MannschaftsAnzahl(r.AnzahlBeste, r.PlatzierungVon, r.PlatzierungBis))
            + MannschaftsAnzahl(eingabe.AnzahlBeste, eingabe.PlatzierungVon, eingabe.PlatzierungBis);

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
            fehler.AddRange(FeldFehlerErmitteln(eingabe, quellRunde).Select(f => f.Text));

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
            .Sum(r => MannschaftsAnzahl(r.AnzahlBeste, r.PlatzierungVon, r.PlatzierungBis));
        var summe = summeAndererBloecke + eingaben.Sum(e => MannschaftsAnzahl(e.AnzahlBeste, e.PlatzierungVon, e.PlatzierungBis));

        if (summe > zielRunde.EingangsMannschaftsanzahl)
            fehler.Add($"Übergangsregeln für '{zielRunde.Name}' liefern zusammen {summe} Mannschaften, " +
                       $"erwartet werden aber nur {zielRunde.EingangsMannschaftsanzahl}.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }


    /// <summary>Ganzgraph-Validierung (Issue #294, Nachfolger von <c>VorlagenUebergangsregelValidator.Validieren</c>):
    /// validiert alle Runden + alle Übergangsregeln als beabsichtigten Endzustand in einem Zug (Zyklus-Check,
    /// Summen exakt statt "≤", Duplikat-Checks) — für den Vollersatz-Fall, in dem Runden noch keine reale Id
    /// haben und daher per 1-basierter Reihenfolge referenziert werden (siehe <see cref="UebergangsregelPositionsEingabe"/>).
    /// Feldbezug (#601): <see cref="Feldfehler.Zeile"/> = Index der Quellrunde (bei Summen: der geprüften Runde),
    /// <see cref="Feldfehler.Unterzeile"/> = Index der Regel in <paramref name="regeln"/>.</summary>
    public ValidationErgebnis Validieren(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> regeln)
    {
        // Gültige Referenzen vorausgesetzt für alle Prüfungen nach den Regelfeldern (sonst dort schon gemeldet).
        var gueltigeRegeln = regeln.Where(r => ReferenziertVorhandeneRunden(r, runden.Count)).ToList();

        Feldfehler[] fehler =
        [
            .. RegelfelderPruefen(runden, regeln),
            .. DuplikatePruefen(runden, gueltigeRegeln),
            .. ZyklusPruefen(gueltigeRegeln),
            .. EingehendeSummenPruefen(runden, gueltigeRegeln),
            .. AusgehendeSummenPruefen(runden, gueltigeRegeln),
        ];

        return ValidationErgebnis.Aus(fehler);
    }

    // ── Ganzgraph: Prüfschritte ─────────────────────────────────────────────

    private static bool ReferenziertVorhandeneRunden(UebergangsregelPositionsEingabe regel, int rundenAnzahl) =>
        regel.QuellReihenfolge >= 1 && regel.QuellReihenfolge <= rundenAnzahl &&
        regel.ZielReihenfolge >= 1 && regel.ZielReihenfolge <= rundenAnzahl;

    private static IEnumerable<Feldfehler> RegelfelderPruefen(
        IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> regeln) =>
        regeln.SelectMany((regel, index) => ReferenziertVorhandeneRunden(regel, runden.Count)
            ? UebergangsregelFeldValidierung.FeldFehlerErmitteln(
                    regel.QuellReihenfolge == regel.ZielReihenfolge, runden[regel.QuellReihenfolge - 1],
                    regel.GruppeIndex, regel.PlatzierungVon, regel.PlatzierungBis, regel.AnzahlBeste)
                .Select(f => f with { Zeile = regel.QuellReihenfolge - 1, Unterzeile = index })
            : [new Feldfehler("Übergangsregel referenziert eine nicht vorhandene Runde.")]);

    private static IEnumerable<Feldfehler> DuplikatePruefen(
        IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> gueltigeRegeln)
    {
        // Gruppiert zusätzlich nach ZielReihenfolge: derselbe Gruppenindex/dieselbe Spielnummer darf mehrfach
        // auftreten, wenn er/sie (mit unterschiedlicher Platzierung) auf getrennte Zielrunden verteilt wird
        // — der K.o.-Split-Sonderfall (Sieger UND Verlierer eines Spiels in zwei Zielrunden, Issue #334).
        // Mehrfachvergabe an dieselbe Zielrunde bleibt ein Fehler (redundante/uneindeutige Zeilen).
        foreach (var dup in gueltigeRegeln
                     .Where(r => r.GruppeIndex is not null)
                     .GroupBy(r => (r.QuellReihenfolge, r.GruppeIndex, r.ZielReihenfolge))
                     .Where(g => g.Count() > 1))
            yield return new($"Gruppenindex/Spielnummer {dup.Key.GruppeIndex} ist für Runde " +
                             $"'{runden[dup.Key.QuellReihenfolge - 1].Name}' mehrfach vergeben.",
                Zeile: dup.Key.QuellReihenfolge - 1);

        foreach (var dup in gueltigeRegeln
                     .Where(r => r.AnzahlBeste is not null)
                     .GroupBy(r => (r.QuellReihenfolge, r.PlatzierungVon))
                     .Where(g => g.Count() > 1))
            yield return new($"Gruppenübergreifende Rangfolge-Regel für Platzierung {dup.Key.PlatzierungVon} ist für Runde " +
                             $"'{runden[dup.Key.QuellReihenfolge - 1].Name}' mehrfach vergeben.",
                Zeile: dup.Key.QuellReihenfolge - 1);
    }

    private static IEnumerable<Feldfehler> ZyklusPruefen(IReadOnlyList<UebergangsregelPositionsEingabe> gueltigeRegeln)
    {
        if (HatZyklus(gueltigeRegeln))
            yield return new("Die Übergangsregeln enthalten einen zyklischen Übergang.");
    }

    /// <summary>Eine Runde ganz ohne eingehende Übergangsregeln gilt als eigenständiger Startpunkt (z.B. mehrere
    /// parallele Gruppenphasen/Ligen ohne gemeinsame Vorrunde) und wird bewusst nicht verlangt. Hat eine Runde aber
    /// MINDESTENS eine eingehende Regel, muss deren Summe exakt der Eingangsmannschaftszahl entsprechen, unabhängig
    /// davon, aus wie vielen Quellrunden sie sich zusammensetzt.</summary>
    private static IEnumerable<Feldfehler> EingehendeSummenPruefen(
        IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> gueltigeRegeln) =>
        SummenJeRundePruefen(runden, gueltigeRegeln.ToLookup(r => r.ZielReihenfolge),
            r => r.EingangsMannschaftsanzahl,
            (ziel, summe) => $"Übergangsregeln für '{ziel.Name}' liefern zusammen {summe} Mannschaften, " +
                             $"erwartet werden aber {ziel.EingangsMannschaftsanzahl}.");

    /// <summary>Eine Runde darf terminal sein (keine ausgehenden Regeln, z.B. Finale) — hat sie aber mindestens eine,
    /// muss die Summe exakt der Ausgangsmannschaftszahl entsprechen, unabhängig davon, in wie viele Zielrunden sie
    /// sich aufteilt (z.B. Sieger→Finale, Verlierer→Spiel um Platz 3).</summary>
    private static IEnumerable<Feldfehler> AusgehendeSummenPruefen(
        IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> gueltigeRegeln) =>
        SummenJeRundePruefen(runden, gueltigeRegeln.ToLookup(r => r.QuellReihenfolge),
            r => r.AusgangsMannschaftsanzahl,
            (quelle, summe) => $"Ausgehende Übergangsregeln von '{quelle.Name}' verteilen zusammen {summe} Mannschaften, " +
                               $"erwartet werden aber {quelle.AusgangsMannschaftsanzahl}.");

    /// <summary>Für jede Runde mit mindestens einer Regel (Schlüssel = 1-basierte Reihenfolge): die Summe der
    /// Mannschaften muss genau der Sollanzahl entsprechen.</summary>
    private static IEnumerable<Feldfehler> SummenJeRundePruefen(
        IReadOnlyList<TurnierRunde> runden,
        ILookup<int, UebergangsregelPositionsEingabe> regelnJeRunde,
        Func<TurnierRunde, int> sollAnzahl,
        Func<TurnierRunde, int, string> meldung)
    {
        for (var i = 0; i < runden.Count; i++)
        {
            if (!regelnJeRunde.Contains(i + 1)) continue;

            var summe = regelnJeRunde[i + 1].Sum(r => MannschaftsAnzahl(r.AnzahlBeste, r.PlatzierungVon, r.PlatzierungBis));
            if (summe != sollAnzahl(runden[i]))
                yield return new(meldung(runden[i], summe), Zeile: i);
        }
    }

    /// <summary>Mannschaften, die eine Regel weitergibt: die besten n bei einer gruppenübergreifenden Rangfolge-Regel,
    /// sonst jede Platzierung von–bis.</summary>
    private static int MannschaftsAnzahl(int? anzahlBeste, int platzierungVon, int platzierungBis) =>
        anzahlBeste ?? (platzierungBis - platzierungVon + 1);

    /// <summary>Rundenart-spezifische Feldprüfungen einer einzelnen Eingabe (Gruppenindex/Spielnummer-Bereich,
    /// Platzierungsbereich innerhalb der Quellrunde). Wird sowohl von der Einzel- als auch der Block-Validierung genutzt.</summary>
    private static IEnumerable<Feldfehler> FeldFehlerErmitteln(UebergangsregelEingabe eingabe, TurnierRunde quellRunde)
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
