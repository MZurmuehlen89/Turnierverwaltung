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
        var fehler = new List<string>();

        if (eingabe.QuellRundeId == eingabe.ZielRundeId)
            fehler.Add("Quell- und Zielrunde dürfen nicht identisch sein.");

        if (eingabe.PlatzierungVon < 1)
            fehler.Add("PlatzierungVon muss mindestens 1 sein.");

        if (eingabe.PlatzierungBis < eingabe.PlatzierungVon)
            fehler.Add("PlatzierungBis darf nicht kleiner als PlatzierungVon sein.");

        if (WuerdeZyklusSchliessen(eingabe, alleRegeln, bearbeiteteRegelId))
            fehler.Add($"Übergangsregel '{quellRunde.Name}' → '{zielRunde.Name}' würde einen zyklischen Übergang erzeugen.");

        var summe = alleRegeln
            .Where(r => r.ZielRundeId == eingabe.ZielRundeId && r.Id != bearbeiteteRegelId)
            .Sum(r => r.PlatzierungBis - r.PlatzierungVon + 1)
            + (eingabe.PlatzierungBis - eingabe.PlatzierungVon + 1);

        if (summe > zielRunde.EingangsMannschaftsanzahl)
            fehler.Add($"Übergangsregeln für '{zielRunde.Name}' liefern zusammen {summe} Mannschaften, " +
                       $"erwartet werden aber nur {zielRunde.EingangsMannschaftsanzahl}.");

        switch (quellRunde.Rundenart)
        {
            case Rundenart.Gruppenphase:
                if (eingabe.GruppeIndex is null)
                    fehler.Add("Für eine Gruppenphase als Quellrunde muss ein Gruppenindex angegeben werden.");
                else if (quellRunde.GruppenAnzahl is not > 0 || eingabe.GruppeIndex < 1 || eingabe.GruppeIndex > quellRunde.GruppenAnzahl)
                    fehler.Add($"Gruppenindex {eingabe.GruppeIndex} ist außerhalb des gültigen Bereichs " +
                               $"(1–{quellRunde.GruppenAnzahl ?? 0}) der Quellrunde '{quellRunde.Name}'.");
                else if (eingabe.PlatzierungBis > quellRunde.EingangsMannschaftsanzahl / quellRunde.GruppenAnzahl!.Value)
                    fehler.Add($"Platzierung {eingabe.PlatzierungBis} übersteigt die Gruppengröße " +
                               $"({quellRunde.EingangsMannschaftsanzahl / quellRunde.GruppenAnzahl.Value}) der Quellrunde '{quellRunde.Name}'.");
                break;

            case Rundenart.Liga:
                if (eingabe.GruppeIndex is not null)
                    fehler.Add("Für eine Liga-Runde als Quellrunde darf kein Gruppenindex angegeben werden.");
                if (eingabe.PlatzierungBis > quellRunde.EingangsMannschaftsanzahl)
                    fehler.Add($"Platzierung {eingabe.PlatzierungBis} übersteigt die Teilnehmerzahl " +
                               $"({quellRunde.EingangsMannschaftsanzahl}) der Quellrunde '{quellRunde.Name}'.");
                break;

            case Rundenart.KoRunde:
            case Rundenart.Finale:
            case Rundenart.SpielUmPlatzDrei:
                if (eingabe.GruppeIndex is null)
                    fehler.Add("Für eine K.-o.-Runde als Quellrunde muss die Spielnummer (als Gruppenindex) angegeben werden.");
                else if (eingabe.GruppeIndex < 1 || eingabe.GruppeIndex > quellRunde.EingangsMannschaftsanzahl / 2)
                    fehler.Add($"Spielnummer {eingabe.GruppeIndex} ist außerhalb des gültigen Bereichs " +
                               $"(1–{quellRunde.EingangsMannschaftsanzahl / 2}) der Quellrunde '{quellRunde.Name}'.");
                if (eingabe.PlatzierungBis > 2)
                    fehler.Add("Eine K.-o.-Runde als Quellrunde liefert je Spiel nur Sieger (Platzierung 1) und Verlierer (Platzierung 2).");
                break;

            default:
                fehler.Add($"Rundenart '{quellRunde.Rundenart}' wird als Quellrunde für Übergangsregeln nicht unterstützt.");
                break;
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Prüft per DFS ausgehend von der vorgeschlagenen Zielrunde, ob die Quellrunde über bestehende
    /// Regeln (QuellRundeId → ZielRundeId) wieder erreichbar ist — dann würde die neue Kante einen Zyklus schließen.</summary>
    private static bool WuerdeZyklusSchliessen(
        UebergangsregelEingabe eingabe, IReadOnlyList<Uebergangsregel> alleRegeln, int? bearbeiteteRegelId)
    {
        var kanten = alleRegeln
            .Where(r => r.Id != bearbeiteteRegelId)
            .ToLookup(r => r.QuellRundeId, r => r.ZielRundeId);

        var besucht = new HashSet<int>();
        var stapel = new Stack<int>();
        stapel.Push(eingabe.ZielRundeId);

        while (stapel.Count > 0)
        {
            var aktuell = stapel.Pop();
            if (aktuell == eingabe.QuellRundeId)
                return true;
            if (!besucht.Add(aktuell))
                continue;

            foreach (var naechste in kanten[aktuell])
                stapel.Push(naechste);
        }

        return false;
    }
}
