namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Ein Ziel der Hauptnavigation: Kennung (AutomationId <c>Navigation.Ziel</c>) und der Seitentitel, den
/// die Befehlsleiste danach zeigt.</summary>
public sealed record Bereich(string Ziel, string Titel)
{
    public static readonly Bereich Mannschaften = new("Mannschaften", "Mannschaften");
    public static readonly Bereich LaenderUndVerbaende = new("LaenderUndVerbaende", "Länder & Verbände");
    public static readonly Bereich Regelwerke = new("Regelwerke", "Regelwerke");
    public static readonly Bereich Ranglisten = new("Ranglisten", "Ranglisten");
    public static readonly Bereich Turniere = new("Turniere", "Turniere");
    public static readonly Bereich Turnierdurchfuehrung = new("Turnierdurchfuehrung", "Turnierdurchführung");
    public static readonly Bereich EinzelspielSimulation = new("EinzelspielSimulation", "Einzelspiel-Simulation");
    public static readonly Bereich ImportExport = new("ImportExport", "Import / Export");
    public static readonly Bereich Datenbankwartung = new("Datenbankwartung", "Datenbankwartung");

    public static IReadOnlyList<Bereich> Alle { get; } =
    [
        Mannschaften, LaenderUndVerbaende, Regelwerke, Ranglisten, Turniere, Turnierdurchfuehrung,
        EinzelspielSimulation, ImportExport, Datenbankwartung,
    ];

    public string AutomationId => $"Navigation.{Ziel}";

    public static Bereich Zu(string ziel) => Alle.Single(b => b.Ziel == ziel);
}
