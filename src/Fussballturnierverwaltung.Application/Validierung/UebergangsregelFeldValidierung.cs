using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Rundenart-spezifische Feld- und Zyklusprüfungen für Übergangsregeln, gemeinsam für die Einzel- und
/// Block-Prüfung (Runden über ihre Id) und die Ganzgraph-Prüfung (Runden über ihre Reihenfolge, Issue #294) in
/// <see cref="UebergangsregelValidator"/> — nur die Identität der Quell-/Zielrunde unterscheidet sich. Die Fehler
/// tragen das Feld der Regelzeile (#601); Gruppenindex bzw. Spielnummer leitet das Formular aus der Quellrunde ab, dazu
/// gibt es kein Eingabefeld.</summary>
internal static class UebergangsregelFeldValidierung
{
    private const string VON = nameof(UebergangsregelPositionsEingabe.PlatzierungVon);
    private const string BIS = nameof(UebergangsregelPositionsEingabe.PlatzierungBis);
    private const string BESTE = nameof(UebergangsregelPositionsEingabe.AnzahlBeste);

#pragma warning disable S107 // die Regelfelder kommen aus zwei verschiedenen Eingabetypen (Id- bzw. Reihenfolge-basiert); ein gemeinsames Parameterobjekt nur für diesen Aufruf brächte keinen Gewinn (#499)
    public static IEnumerable<Feldfehler> FeldFehlerErmitteln(
        bool quellGleichZiel, TurnierRunde quelle,
        int? gruppeIndex, int platzierungVon, int platzierungBis, int? anzahlBeste = null)
#pragma warning restore S107
    {
        IEnumerable<Feldfehler> rundenartFehler = anzahlBeste is not null
            ? AnzahlBesteFehler(quelle, gruppeIndex, platzierungVon, platzierungBis, anzahlBeste.Value)
            : quelle.Rundenart switch
            {
                Rundenart.Gruppenphase => GruppenphaseFehler(quelle, gruppeIndex, platzierungBis),
                Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem
                    => LigaFehler(quelle, gruppeIndex, platzierungBis),
                Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei
                    => KoFehler(quelle, gruppeIndex, platzierungBis),
                _ => [new($"Rundenart '{quelle.Rundenart}' wird als Quellrunde für Übergangsregeln nicht unterstützt.")]
            };

        return [.. AllgemeineFehler(quellGleichZiel, platzierungVon, platzierungBis), .. rundenartFehler];
    }

    private static IEnumerable<Feldfehler> AllgemeineFehler(bool quellGleichZiel, int platzierungVon, int platzierungBis)
    {
        if (quellGleichZiel)
            yield return new("Quell- und Zielrunde dürfen nicht identisch sein.");

        if (platzierungVon < 1)
            yield return new("PlatzierungVon muss mindestens 1 sein.", VON);

        if (platzierungBis < platzierungVon)
            yield return new("PlatzierungBis darf nicht kleiner als PlatzierungVon sein.", BIS);
    }

    /// <summary>Gruppenübergreifende Rangfolge-Regel ("die besten n Gruppendritten"): nur aus einer Gruppenphase,
    /// ohne Gruppenindex, für genau eine Platzierung, höchstens so viele wie Gruppen.</summary>
    private static IEnumerable<Feldfehler> AnzahlBesteFehler(
        TurnierRunde quelle, int? gruppeIndex, int platzierungVon, int platzierungBis, int anzahlBeste)
    {
        if (quelle.Rundenart != Rundenart.Gruppenphase)
            yield return new("Eine gruppenübergreifende Rangfolge-Regel ist nur für eine Gruppenphase als Quellrunde möglich.");
        if (gruppeIndex is not null)
            yield return new("Eine gruppenübergreifende Rangfolge-Regel darf keinen Gruppenindex angeben.");
        if (platzierungVon != platzierungBis)
            yield return new("Eine gruppenübergreifende Rangfolge-Regel gilt für genau eine Platzierung (PlatzierungVon muss PlatzierungBis entsprechen).", BIS);

        if (anzahlBeste < 1)
            yield return new("AnzahlBeste muss mindestens 1 sein.", BESTE);
        else if (quelle.GruppenAnzahl is not > 0 || anzahlBeste > quelle.GruppenAnzahl)
            yield return new($"AnzahlBeste ({anzahlBeste}) übersteigt die Gruppenanzahl ({quelle.GruppenAnzahl ?? 0}) der Quellrunde '{quelle.Name}'.", BESTE);

        if (quelle.Rundenart == Rundenart.Gruppenphase && Gruppengroesse(quelle) is { } groesse && platzierungBis > groesse)
            yield return GruppengroesseUeberschritten(quelle, platzierungBis, groesse);
    }

    private static IEnumerable<Feldfehler> GruppenphaseFehler(TurnierRunde quelle, int? gruppeIndex, int platzierungBis)
    {
        if (gruppeIndex is null)
            yield return new("Für eine Gruppenphase als Quellrunde muss ein Gruppenindex angegeben werden.");
        else if (Gruppengroesse(quelle) is not { } groesse || gruppeIndex < 1 || gruppeIndex > quelle.GruppenAnzahl)
            yield return new($"Gruppenindex {gruppeIndex} ist außerhalb des gültigen Bereichs " +
                             $"(1–{quelle.GruppenAnzahl ?? 0}) der Quellrunde '{quelle.Name}'.");
        else if (platzierungBis > groesse)
            yield return GruppengroesseUeberschritten(quelle, platzierungBis, groesse);
    }

    private static IEnumerable<Feldfehler> LigaFehler(TurnierRunde quelle, int? gruppeIndex, int platzierungBis)
    {
        if (gruppeIndex is not null)
            yield return new("Für eine Liga-Runde als Quellrunde darf kein Gruppenindex angegeben werden.");
        if (platzierungBis > quelle.EingangsMannschaftsanzahl)
            yield return new($"Platzierung {platzierungBis} übersteigt die Teilnehmerzahl " +
                             $"({quelle.EingangsMannschaftsanzahl}) der Quellrunde '{quelle.Name}'.", BIS);
    }

    /// <summary>K.-o.-Quellrunde: der Gruppenindex ist die Spielnummer, Platzierung 1 = Sieger, 2 = Verlierer.</summary>
    private static IEnumerable<Feldfehler> KoFehler(TurnierRunde quelle, int? gruppeIndex, int platzierungBis)
    {
        int spiele = quelle.EingangsMannschaftsanzahl / 2;
        if (gruppeIndex is null)
            yield return new("Für eine K.-o.-Runde als Quellrunde muss die Spielnummer (als Gruppenindex) angegeben werden.");
        else if (gruppeIndex < 1 || gruppeIndex > spiele)
            yield return new($"Spielnummer {gruppeIndex} ist außerhalb des gültigen Bereichs " +
                             $"(1–{spiele}) der Quellrunde '{quelle.Name}'.");
        if (platzierungBis > 2)
            // Ohne Feld: die K.-o.-Zeile wählt Sieger oder Verlierer per Haken, ein Platzfeld gibt es dort nicht.
            yield return new("Eine K.-o.-Runde als Quellrunde liefert je Spiel nur Sieger (Platzierung 1) und Verlierer (Platzierung 2).");
    }

    /// <summary>Teams je Gruppe, <c>null</c> ohne gültige Gruppenanzahl.</summary>
    private static int? Gruppengroesse(TurnierRunde quelle) =>
        quelle.GruppenAnzahl is > 0 and var gruppen ? quelle.EingangsMannschaftsanzahl / gruppen : null;

    private static Feldfehler GruppengroesseUeberschritten(TurnierRunde quelle, int platzierungBis, int groesse) =>
        new($"Platzierung {platzierungBis} übersteigt die Gruppengröße ({groesse}) der Quellrunde '{quelle.Name}'.", BIS);

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
