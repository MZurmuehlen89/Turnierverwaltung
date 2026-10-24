using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Testturnier aus der Seed-Vorlage „Mini-Turnier, 8 Teams“ (#522): zwei Vierergruppen → Halbfinale →
/// Spiel um Platz 3 und Finale, 16 Spiele. Ohne Länderfilter, die Töpfe lassen sich daher aus einer beliebigen
/// Rangliste automatisch befüllen.</summary>
internal static class MiniTurnier
{
    public const string VORLAGE = "Mini-Turnier";
    public const string VARIANTE = "8 Teams";
    public const string RANGLISTE = "Ligapyramide Deutschland 2025/26";

    public const string GRUPPENPHASE = "Gruppenphase";
    public const string HALBFINALE = "Halbfinale";
    public const string SPIEL_UM_PLATZ_DREI = "Spiel um Platz 3";
    public const string FINALE = "Finale";

    public const int TEAMS = 8;
    public const int SPIELE_GRUPPENPHASE = 12;
    public const int SPIELE_GESAMT = 16;

    /// <summary>K.-o.-Runden nach der Gruppenphase mit ihrer Spielzahl; jede braucht eine eigene Auslosung, deren
    /// Töpfe sich mit den Qualifikanten der Vorrunde befüllen.</summary>
    public static IReadOnlyList<(string Runde, int Spiele)> KoRunden { get; } =
        [(HALBFINALE, 2), (SPIEL_UM_PLATZ_DREI, 1), (FINALE, 1)];

    /// <summary>Legt ein neues Mini-Turnier unter eindeutigem Namen an, öffnet es in der Turnierdurchführung und
    /// bereitet die Auslosung der Gruppenphase bis zu befüllten Töpfen vor.</summary>
    public static (TurnierdurchfuehrungSeite Turnier, AuslosungReiter Auslosung) GruppenauslosungVorbereiten(
        Hauptfenster hauptfenster, string? auslosungsart = null)
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        hauptfenster.TurniereOeffnen().AusVorlageErstellen(VORLAGE, VARIANTE, "2027", name);

        var turnier = hauptfenster.TurnierdurchfuehrungOeffnen();
        if (turnier.GewaehltesTurnier != name)
            throw new InvalidOperationException(
                $"Turnierdurchführung zeigt „{turnier.GewaehltesTurnier}“ statt des neuen Turniers „{name}“.");

        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(GRUPPENPHASE, auslosungsart);
        auslosung.RanglisteWaehlen(RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        return (turnier, auslosung);
    }
}
