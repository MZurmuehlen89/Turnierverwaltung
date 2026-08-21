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
        var fehler = new List<string>(FeldFehlerErmitteln(eingabe, quellRunde, zielRunde));

        if (UebergangsregelFeldValidierung.WuerdeZyklusSchliessen(
                eingabe.QuellRundeId, eingabe.ZielRundeId, kanten.Select(r => (r.QuellRundeId, r.ZielRundeId))))
            fehler.Add($"Übergangsregel '{quellRunde.Name}' → '{zielRunde.Name}' würde einen zyklischen Übergang erzeugen.");

        var summe = alleRegeln
            .Where(r => r.ZielRundeId == eingabe.ZielRundeId && r.Id != bearbeiteteRegelId)
            .Sum(r => r.PlatzierungBis - r.PlatzierungVon + 1)
            + (eingabe.PlatzierungBis - eingabe.PlatzierungVon + 1);

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
            fehler.AddRange(FeldFehlerErmitteln(eingabe, quellRunde, zielRunde));

        var duplikate = eingaben
            .Where(e => e.GruppeIndex is not null)
            .GroupBy(e => e.GruppeIndex)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var dup in duplikate)
            fehler.Add($"Gruppenindex/Spielnummer {dup} ist mehrfach in diesem Übergangsregel-Block vorhanden.");

        var kanten = alleRegeln.Where(r => !(r.QuellRundeId == quellRunde.Id && r.ZielRundeId == zielRunde.Id)).ToList();
        if (UebergangsregelFeldValidierung.WuerdeZyklusSchliessen(
                quellRunde.Id, zielRunde.Id, kanten.Select(r => (r.QuellRundeId, r.ZielRundeId))))
            fehler.Add($"Übergangsregel '{quellRunde.Name}' → '{zielRunde.Name}' würde einen zyklischen Übergang erzeugen.");

        var summeAndererBloecke = alleRegeln
            .Where(r => r.ZielRundeId == zielRunde.Id && r.QuellRundeId != quellRunde.Id)
            .Sum(r => r.PlatzierungBis - r.PlatzierungVon + 1);
        var summe = summeAndererBloecke + eingaben.Sum(e => e.PlatzierungBis - e.PlatzierungVon + 1);

        if (summe > zielRunde.EingangsMannschaftsanzahl)
            fehler.Add($"Übergangsregeln für '{zielRunde.Name}' liefern zusammen {summe} Mannschaften, " +
                       $"erwartet werden aber nur {zielRunde.EingangsMannschaftsanzahl}.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Rundenart-spezifische Feldprüfungen einer einzelnen Eingabe (Gruppenindex/Spielnummer-Bereich,
    /// Platzierungsbereich innerhalb der Quellrunde). Wird sowohl von der Einzel- als auch der Block-Validierung genutzt.</summary>
    private static IEnumerable<string> FeldFehlerErmitteln(UebergangsregelEingabe eingabe, TurnierRunde quellRunde, TurnierRunde zielRunde)
        => UebergangsregelFeldValidierung.FeldFehlerErmitteln(
            eingabe.QuellRundeId == eingabe.ZielRundeId,
            quellRunde.Rundenart, quellRunde.Name, quellRunde.EingangsMannschaftsanzahl, quellRunde.GruppenAnzahl,
            eingabe.GruppeIndex, eingabe.PlatzierungVon, eingabe.PlatzierungBis);
}
