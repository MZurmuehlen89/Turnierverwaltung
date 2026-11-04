using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Stand einer Mannschaft nach einem Spieltag (#480). <see cref="SpieleAmSpieltag"/> sind ihre Spiele mit
/// dieser Spieltag-Nummer — leer heißt spielfrei; ein Spiel ohne Ergebnis darunter ist ein noch ausstehendes
/// (Nachhol-)Spiel, die Mannschaft steht dann mit einem Spiel weniger in der Tabelle.</summary>
public sealed record VerlaufPunkt(int Spieltag, int Platz, int Punkte, int Spiele, IReadOnlyList<Spiel> SpieleAmSpieltag)
{
    public bool SpielAusstehend => SpieleAmSpieltag.Any(s => !TabellenRechner.HatErgebnis(s));

    /// <summary>Tooltip-Text aus Sicht der Mannschaft: Stand und die Spiele des Spieltags, eigene Tore zuerst.</summary>
    public string Beschreibung(int mannschaftId)
    {
        string stand = $"{Spieltag}. Spieltag: Platz {Platz} · {Punkte} {(Punkte == 1 ? "Punkt" : "Punkte")} aus {Spiele} {(Spiele == 1 ? "Spiel" : "Spielen")}";
        if (SpieleAmSpieltag.Count == 0) return $"{stand}\nspielfrei";
        return string.Join("\n", [stand, .. SpieleAmSpieltag.Select(s => SpielText(s, mannschaftId))]);
    }

    private static string SpielText(Spiel spiel, int mannschaftId)
    {
        bool heim = spiel.HeimMannschaftId == mannschaftId;
        string gegner = (heim ? spiel.AuswaertsMannschaft : spiel.HeimMannschaft)?.Name ?? "?";
        string ort = heim ? "H" : "A";
        if (!TabellenRechner.HatErgebnis(spiel)) return $"Spiel gegen {gegner} ({ort}) ausstehend";

        var e = spiel.Ergebnis!;
        var (tore, gegentore) = heim ? (e.HeimToreGesamt, e.AuswaertsToreGesamt) : (e.AuswaertsToreGesamt, e.HeimToreGesamt);
        return $"{tore}:{gegentore} gegen {gegner} ({ort})";
    }
}

public sealed record MannschaftsVerlauf(Mannschaft Mannschaft, IReadOnlyList<VerlaufPunkt> Punkte);

/// <summary>Tabellenplatz-Verlauf einer Tabelle (#480): einer Liga bzw. einer Gruppe
/// (<see cref="GruppenNummer"/>). <see cref="Spieltage"/> reichen bis zum letzten Spieltag mit Ergebnis — spätere
/// Spieltage erscheinen nicht.</summary>
public sealed record TabellenVerlauf(
    int? GruppenNummer, IReadOnlyList<int> Spieltage, int AnzahlPlaetze, IReadOnlyList<MannschaftsVerlauf> Mannschaften)
{
    public bool HatSpieltage => Spieltage.Count > 0;
}

/// <summary>Rechnet den Tabellenplatz je Mannschaft nach jedem Spieltag, ohne Datenbankzugriff (#480). Der Stand
/// nach Spieltag X zählt alle Ergebnisse der Spieltage 1 bis X (<see cref="TabellenRechner.BisSpieltag"/>) — ein
/// später nachgetragenes Nachholspiel zählt also rückwirkend zu seinem Spieltag.</summary>
public static class TabellenVerlaufRechner
{
    /// <summary>Alle bekannten Teilnehmer stehen in jedem Stand — auch wer am ersten Spieltag spielfrei war oder bisher
    /// nur gegen offene Platzhalter angesetzt ist (#397).</summary>
    public static TabellenVerlauf Berechnen(
        int? gruppenNummer, IReadOnlyList<Spiel> spiele, TabellenRegeln regeln)
    {
        var teilnehmer = GruppenRechner.AufgeloesteTeilnehmer(spiele);
        var spieltage = TabellenRechner.SpieltageMitErgebnis(spiele);
        var staende = spieltage
            .Select(spieltag => (Spieltag: spieltag, Tabelle: TabellenRechner.TabelleBerechnen(
                TabellenRechner.BisSpieltag(spiele, spieltag), regeln, teilnehmer: teilnehmer)))
            .ToList();

        var mannschaften = staende.LastOrDefault().Tabelle ?? [];
        return new TabellenVerlauf(
            gruppenNummer, spieltage, mannschaften.Count,
            [.. mannschaften
                .OrderBy(e => e.Rang)
                .Select(e => new MannschaftsVerlauf(e.Mannschaft, [.. staende.Select(stand =>
                    PunktAus(stand.Spieltag, stand.Tabelle.Single(x => x.Mannschaft.Id == e.Mannschaft.Id), spiele))]))]);
    }

    private static VerlaufPunkt PunktAus(int spieltag, TabellenEintrag eintrag, IReadOnlyList<Spiel> spiele) =>
        new(spieltag, eintrag.Rang, eintrag.Punkte, eintrag.Spiele,
            [.. spiele.Where(s => s.Spieltag == spieltag
                && (s.HeimMannschaftId == eintrag.Mannschaft.Id || s.AuswaertsMannschaftId == eintrag.Mannschaft.Id))]);
}
