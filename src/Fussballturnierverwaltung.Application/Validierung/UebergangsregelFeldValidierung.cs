using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Rundenart-spezifische Feld- und Zyklusprüfungen für Übergangsregeln, unabhängig davon, ob sie
/// gegen echte <see cref="Fussballturnierverwaltung.Domain.Entitaeten.Turniere.TurnierRunde"/>n
/// (<see cref="UebergangsregelValidator"/>) oder gegen Vorlagen-Runden
/// (<see cref="VorlagenUebergangsregelValidator"/>, Issue #166-Folgefix) geprüft werden — beide haben
/// dieselbe Bedeutung von Rundenart/Eingangsanzahl/Gruppenanzahl/Gruppenindex, nur die Identität der
/// Quell-/Zielrunde unterscheidet sich (reale Id vs. Vorlagen-Reihenfolge).</summary>
internal static class UebergangsregelFeldValidierung
{
    public static IEnumerable<string> FeldFehlerErmitteln(
        bool quellGleichZiel,
        Rundenart quellRundenart, string quellName, int quellEingangsMannschaftsanzahl, int? quellGruppenAnzahl,
        int? gruppeIndex, int platzierungVon, int platzierungBis, int? anzahlBeste = null)
    {
        var fehler = new List<string>();

        if (quellGleichZiel)
            fehler.Add("Quell- und Zielrunde dürfen nicht identisch sein.");

        if (platzierungVon < 1)
            fehler.Add("PlatzierungVon muss mindestens 1 sein.");

        if (platzierungBis < platzierungVon)
            fehler.Add("PlatzierungBis darf nicht kleiner als PlatzierungVon sein.");

        if (anzahlBeste is not null)
        {
            if (quellRundenart != Rundenart.Gruppenphase)
                fehler.Add("Eine gruppenübergreifende Rangfolge-Regel ist nur für eine Gruppenphase als Quellrunde möglich.");
            if (gruppeIndex is not null)
                fehler.Add("Eine gruppenübergreifende Rangfolge-Regel darf keinen Gruppenindex angeben.");
            if (platzierungVon != platzierungBis)
                fehler.Add("Eine gruppenübergreifende Rangfolge-Regel gilt für genau eine Platzierung (PlatzierungVon muss PlatzierungBis entsprechen).");
            if (anzahlBeste < 1)
                fehler.Add("AnzahlBeste muss mindestens 1 sein.");
            else if (quellGruppenAnzahl is not > 0 || anzahlBeste > quellGruppenAnzahl)
                fehler.Add($"AnzahlBeste ({anzahlBeste}) übersteigt die Gruppenanzahl ({quellGruppenAnzahl ?? 0}) der Quellrunde '{quellName}'.");

            if (quellRundenart == Rundenart.Gruppenphase && quellGruppenAnzahl is > 0 &&
                platzierungBis > quellEingangsMannschaftsanzahl / quellGruppenAnzahl.Value)
                fehler.Add($"Platzierung {platzierungBis} übersteigt die Gruppengröße " +
                           $"({quellEingangsMannschaftsanzahl / quellGruppenAnzahl.Value}) der Quellrunde '{quellName}'.");

            return fehler;
        }

        switch (quellRundenart)
        {
            case Rundenart.Gruppenphase:
                if (gruppeIndex is null)
                    fehler.Add("Für eine Gruppenphase als Quellrunde muss ein Gruppenindex angegeben werden.");
                else if (quellGruppenAnzahl is not > 0 || gruppeIndex < 1 || gruppeIndex > quellGruppenAnzahl)
                    fehler.Add($"Gruppenindex {gruppeIndex} ist außerhalb des gültigen Bereichs " +
                               $"(1–{quellGruppenAnzahl ?? 0}) der Quellrunde '{quellName}'.");
                else if (platzierungBis > quellEingangsMannschaftsanzahl / quellGruppenAnzahl!.Value)
                    fehler.Add($"Platzierung {platzierungBis} übersteigt die Gruppengröße " +
                               $"({quellEingangsMannschaftsanzahl / quellGruppenAnzahl.Value}) der Quellrunde '{quellName}'.");
                break;

            case Rundenart.Liga:
                if (gruppeIndex is not null)
                    fehler.Add("Für eine Liga-Runde als Quellrunde darf kein Gruppenindex angegeben werden.");
                if (platzierungBis > quellEingangsMannschaftsanzahl)
                    fehler.Add($"Platzierung {platzierungBis} übersteigt die Teilnehmerzahl " +
                               $"({quellEingangsMannschaftsanzahl}) der Quellrunde '{quellName}'.");
                break;

            case Rundenart.KoRunde:
            case Rundenart.Finale:
            case Rundenart.SpielUmPlatzDrei:
                if (gruppeIndex is null)
                    fehler.Add("Für eine K.-o.-Runde als Quellrunde muss die Spielnummer (als Gruppenindex) angegeben werden.");
                else if (gruppeIndex < 1 || gruppeIndex > quellEingangsMannschaftsanzahl / 2)
                    fehler.Add($"Spielnummer {gruppeIndex} ist außerhalb des gültigen Bereichs " +
                               $"(1–{quellEingangsMannschaftsanzahl / 2}) der Quellrunde '{quellName}'.");
                if (platzierungBis > 2)
                    fehler.Add("Eine K.-o.-Runde als Quellrunde liefert je Spiel nur Sieger (Platzierung 1) und Verlierer (Platzierung 2).");
                break;

            default:
                fehler.Add($"Rundenart '{quellRundenart}' wird als Quellrunde für Übergangsregeln nicht unterstützt.");
                break;
        }

        return fehler;
    }

    /// <summary>Prüft per DFS ausgehend von der vorgeschlagenen Zielrunde, ob die Quellrunde über die
    /// übergebenen Kanten wieder erreichbar ist — dann würde die neue Kante einen Zyklus schließen.</summary>
    public static bool WuerdeZyklusSchliessen(int quellId, int zielId, IEnumerable<(int QuellId, int ZielId)> kantenQuelle)
    {
        var kanten = kantenQuelle.ToLookup(k => k.QuellId, k => k.ZielId);

        var besucht = new HashSet<int>();
        var stapel = new Stack<int>();
        stapel.Push(zielId);

        while (stapel.Count > 0)
        {
            var aktuell = stapel.Pop();
            if (aktuell == quellId)
                return true;
            if (!besucht.Add(aktuell))
                continue;

            foreach (var naechste in kanten[aktuell])
                stapel.Push(naechste);
        }

        return false;
    }
}
