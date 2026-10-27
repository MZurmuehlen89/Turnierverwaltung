using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Testturnier, das ohne Vorlage von Hand aufgebaut wird (#531): Gruppenphase mit zwei Vierergruppen →
/// Halbfinale mit fest vorgegebener Paarung → Finale, 15 Spiele. Die Übergangsregeln bringen je Gruppe die Plätze
/// 1–2 ins Halbfinale und die beiden Halbfinalsieger ins Finale.</summary>
internal static class HandTurnier
{
    public const string GRUPPENPHASE = "Gruppenphase";
    public const string HALBFINALE = "Halbfinale";
    public const string FINALE = "Finale";

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
        turniere.Anlegen(name, "2027", TEAMS);

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
