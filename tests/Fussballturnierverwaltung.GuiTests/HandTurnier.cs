using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Testturnier, das ohne Vorlage von Hand aufgebaut wird (#531): Gruppenphase mit zwei Vierergruppen →
/// Halbfinale mit fest vorgegebener Paarung → Finale, 15 Spiele. Die Übergangsregeln bringen je Gruppe die Plätze
/// 1–2 ins Halbfinale und die beiden Halbfinalsieger ins Finale.
/// Die Szenarien sind auf vier Testklassen verteilt, jede mit eigener App-Sitzung: Bricht ein Test mitten in der
/// Vorbelegung ab, fragt die App beim nächsten Speichern „Vorbelegung verwerfen?“, und dieser offene Dialog ließ in
/// der Gegenprobe alle folgenden Tests derselben Sitzung scheitern – ein Fehlschlag soll nur seinen eigenen Test
/// treffen.</summary>
internal static class HandTurnier
{
    public const string GRUPPENPHASE = "Gruppenphase";
    public const string HALBFINALE = "Halbfinale";
    public const string FINALE = "Finale";

    public const string REGELWERK = "FIFA Standard Modern (3-Punkte)";

    public const int TEAMS = 8;
    public const int SPIELE_GRUPPENPHASE = 12;
    public const int SPIELE_GESAMT = 15;

    /// <summary>Die Halbfinal-Paarungen über Kreuz, in der Reihenfolge der Plätze (Heim, Auswärts, Heim, Auswärts).</summary>
    public static IReadOnlyList<string> KreuzPaarungen { get; } = ["1. Gruppe A", "2. Gruppe B", "1. Gruppe B", "2. Gruppe A"];

    public static string EindeutigerName() => $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>Legt Turnierkopf, Runden und Übergangsregeln an; das Turnier bleibt in der Liste gewählt. Die
    /// Vorbelegung des Halbfinales bleibt offen.</summary>
    public static TurnierlisteSeite RundenAufbauen(Hauptfenster hauptfenster, string name)
    {
        var turniere = hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", TEAMS, REGELWERK);

        var runden = turniere.Runden();
        runden.Anlegen(GRUPPENPHASE, "Gruppenphase", TEAMS, 4, gruppen: 2);
        runden.Anlegen(HALBFINALE, "K.o.-Runde", 4, 2);
        runden.Anlegen(FINALE, "Finale", 2, 1);
        runden.Auswaehlen(GRUPPENPHASE);
        runden.AusgehendeRegelAnlegen(HALBFINALE);
        runden.Auswaehlen(HALBFINALE);
        runden.AusgehendeRegelAnlegen(FINALE);
        runden.PaarungFestVorgeben();
        runden.Speichern();
        return turniere;
    }

    /// <summary>Wie <see cref="RundenAufbauen"/>, dazu die Halbfinal-Paarungen über Kreuz vorbelegt und abgeschlossen.</summary>
    public static void Aufbauen(Hauptfenster hauptfenster, string name)
    {
        var vorbelegung = RundenAufbauen(hauptfenster, name).Vorbelegung();
        vorbelegung.RundeWaehlen(HALBFINALE);
        foreach (var platzhalter in KreuzPaarungen)
            vorbelegung.Platzieren(platzhalter);
        vorbelegung.Abschliessen();
    }
}
