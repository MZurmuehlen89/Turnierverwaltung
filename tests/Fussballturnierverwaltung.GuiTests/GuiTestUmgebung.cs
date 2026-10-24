namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Woher die GUI-Tests App und Datenbankserver nehmen. Standard ist die von
/// <c>build/GuiTests-Ausfuehren.ps1</c> gebaute Testvariante unter <c>artifacts/guitest</c> und der lokale SQL Server;
/// beides lässt sich per Umgebungsvariable umlenken (z. B. für GitHub Actions, #525).</summary>
internal static class GuiTestUmgebung
{
    public const string APP_VARIABLE = "TURNIERVERWALTUNG_GUITEST_APP";
    public const string SERVER_VARIABLE = "TURNIERVERWALTUNG_GUITEST_SERVER";

    /// <summary>Fester Zufalls-Seed der App (#519): Auslosungen und Simulationen sind damit in jedem Lauf gleich.</summary>
    public const string ZUFALLS_SEED = "4711";

    public static string RepoWurzel { get; } = RepoWurzelSuchen();

    public static string ArtefaktOrdner => Path.Combine(RepoWurzel, "artifacts", "guitest");

    public static string AppPfad =>
        Environment.GetEnvironmentVariable(APP_VARIABLE) is { Length: > 0 } pfad
            ? pfad
            : Path.Combine(ArtefaktOrdner, "bin", "Fussballturnierverwaltung.WinUi", "debug_win-x64",
                "Fussballturnierverwaltung.WinUi.exe");

    public static string SqlServer =>
        Environment.GetEnvironmentVariable(SERVER_VARIABLE) is { Length: > 0 } server ? server : ".";

    private static string RepoWurzelSuchen()
    {
        for (var ordner = new DirectoryInfo(AppContext.BaseDirectory); ordner is not null; ordner = ordner.Parent)
        {
            if (File.Exists(Path.Combine(ordner.FullName, "Fussballturnierverwaltung.slnx")))
                return ordner.FullName;
        }
        throw new InvalidOperationException($"Repository-Wurzel oberhalb von {AppContext.BaseDirectory} nicht gefunden.");
    }
}
