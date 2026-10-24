using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Rundenart-spezifische Feld- und Zyklusprüfungen für Übergangsregeln, gemeinsam für die Einzel- und
/// Block-Prüfung (Runden über ihre Id) und die Ganzgraph-Prüfung (Runden über ihre Reihenfolge, Issue #294) in
/// <see cref="UebergangsregelValidator"/> — nur die Identität der Quell-/Zielrunde unterscheidet sich.</summary>
internal static class UebergangsregelFeldValidierung
{
#pragma warning disable S107 // die Regelfelder kommen aus zwei verschiedenen Eingabetypen (Id- bzw. Reihenfolge-basiert); ein gemeinsames Parameterobjekt nur für diesen Aufruf brächte keinen Gewinn (#499)
    public static IEnumerable<string> FeldFehlerErmitteln(
        bool quellGleichZiel, TurnierRunde quelle,
        int? gruppeIndex, int platzierungVon, int platzierungBis, int? anzahlBeste = null)
#pragma warning restore S107
    {
        IEnumerable<string> rundenartFehler = anzahlBeste is not null
            ? AnzahlBesteFehler(quelle, gruppeIndex, platzierungVon, platzierungBis, anzahlBeste.Value)
            : quelle.Rundenart switch
            {
                Rundenart.Gruppenphase => GruppenphaseFehler(quelle, gruppeIndex, platzierungBis),
                Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem
                    => LigaFehler(quelle, gruppeIndex, platzierungBis),
                Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei
                    => KoFehler(quelle, gruppeIndex, platzierungBis),
                _ => [$"Rundenart '{quelle.Rundenart}' wird als Quellrunde für Übergangsregeln nicht unterstützt."]
            };

        return [.. AllgemeineFehler(quellGleichZiel, platzierungVon, platzierungBis), .. rundenartFehler];
    }

    private static IEnumerable<string> AllgemeineFehler(bool quellGleichZiel, int platzierungVon, int platzierungBis)
    {
        if (quellGleichZiel)
            yield return "Quell- und Zielrunde dürfen nicht identisch sein.";

        if (platzierungVon < 1)
            yield return "PlatzierungVon muss mindestens 1 sein.";

        if (platzierungBis < platzierungVon)
            yield return "PlatzierungBis darf nicht kleiner als PlatzierungVon sein.";
    }

    /// <summary>Gruppenübergreifende Rangfolge-Regel ("die besten n Gruppendritten"): nur aus einer Gruppenphase,
    /// ohne Gruppenindex, für genau eine Platzierung, höchstens so viele wie Gruppen.</summary>
    private static IEnumerable<string> AnzahlBesteFehler(
        TurnierRunde quelle, int? gruppeIndex, int platzierungVon, int platzierungBis, int anzahlBeste)
    {
        if (quelle.Rundenart != Rundenart.Gruppenphase)
            yield return "Eine gruppenübergreifende Rangfolge-Regel ist nur für eine Gruppenphase als Quellrunde möglich.";
        if (gruppeIndex is not null)
            yield return "Eine gruppenübergreifende Rangfolge-Regel darf keinen Gruppenindex angeben.";
        if (platzierungVon != platzierungBis)
            yield return "Eine gruppenübergreifende Rangfolge-Regel gilt für genau eine Platzierung (PlatzierungVon muss PlatzierungBis entsprechen).";

        if (anzahlBeste < 1)
            yield return "AnzahlBeste muss mindestens 1 sein.";
        else if (quelle.GruppenAnzahl is not > 0 || anzahlBeste > quelle.GruppenAnzahl)
            yield return $"AnzahlBeste ({anzahlBeste}) übersteigt die Gruppenanzahl ({quelle.GruppenAnzahl ?? 0}) der Quellrunde '{quelle.Name}'.";

        if (quelle.Rundenart == Rundenart.Gruppenphase && Gruppengroesse(quelle) is { } groesse && platzierungBis > groesse)
            yield return GruppengroesseUeberschritten(quelle, platzierungBis, groesse);
    }

    private static IEnumerable<string> GruppenphaseFehler(TurnierRunde quelle, int? gruppeIndex, int platzierungBis)
    {
        if (gruppeIndex is null)
            yield return "Für eine Gruppenphase als Quellrunde muss ein Gruppenindex angegeben werden.";
        else if (Gruppengroesse(quelle) is not { } groesse || gruppeIndex < 1 || gruppeIndex > quelle.GruppenAnzahl)
            yield return $"Gruppenindex {gruppeIndex} ist außerhalb des gültigen Bereichs " +
                         $"(1–{quelle.GruppenAnzahl ?? 0}) der Quellrunde '{quelle.Name}'.";
        else if (platzierungBis > groesse)
            yield return GruppengroesseUeberschritten(quelle, platzierungBis, groesse);
    }

    private static IEnumerable<string> LigaFehler(TurnierRunde quelle, int? gruppeIndex, int platzierungBis)
    {
        if (gruppeIndex is not null)
            yield return "Für eine Liga-Runde als Quellrunde darf kein Gruppenindex angegeben werden.";
        if (platzierungBis > quelle.EingangsMannschaftsanzahl)
            yield return $"Platzierung {platzierungBis} übersteigt die Teilnehmerzahl " +
                         $"({quelle.EingangsMannschaftsanzahl}) der Quellrunde '{quelle.Name}'.";
    }

    /// <summary>K.-o.-Quellrunde: der Gruppenindex ist die Spielnummer, Platzierung 1 = Sieger, 2 = Verlierer.</summary>
    private static IEnumerable<string> KoFehler(TurnierRunde quelle, int? gruppeIndex, int platzierungBis)
    {
        int spiele = quelle.EingangsMannschaftsanzahl / 2;
        if (gruppeIndex is null)
            yield return "Für eine K.-o.-Runde als Quellrunde muss die Spielnummer (als Gruppenindex) angegeben werden.";
        else if (gruppeIndex < 1 || gruppeIndex > spiele)
            yield return $"Spielnummer {gruppeIndex} ist außerhalb des gültigen Bereichs " +
                         $"(1–{spiele}) der Quellrunde '{quelle.Name}'.";
        if (platzierungBis > 2)
            yield return "Eine K.-o.-Runde als Quellrunde liefert je Spiel nur Sieger (Platzierung 1) und Verlierer (Platzierung 2).";
    }

    /// <summary>Teams je Gruppe, <c>null</c> ohne gültige Gruppenanzahl.</summary>
    private static int? Gruppengroesse(TurnierRunde quelle) =>
        quelle.GruppenAnzahl is > 0 and var gruppen ? quelle.EingangsMannschaftsanzahl / gruppen : null;

    private static string GruppengroesseUeberschritten(TurnierRunde quelle, int platzierungBis, int groesse) =>
        $"Platzierung {platzierungBis} übersteigt die Gruppengröße ({groesse}) der Quellrunde '{quelle.Name}'.";

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
