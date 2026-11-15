using System.Reflection;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Woher die GUI-Tests App und Datenbankserver nehmen. Standard ist die von
/// <c>build/GuiTests-Ausfuehren.ps1</c> gebaute Testvariante unter <c>artifacts/guitest</c> und das lokale PostgreSQL
/// mit der Anmeldung aus <c>build/PostgreSQL-Einrichten.ps1</c> (Kennwort aus pgpass.conf); beides lässt sich per
/// Umgebungsvariable umlenken (z. B. für GitHub Actions, #525, #49).</summary>
internal static class GuiTestUmgebung
{
    public const string APP_VARIABLE = "TURNIERVERWALTUNG_GUITEST_APP";
    public const string VERBINDUNG_VARIABLE = "TURNIERVERWALTUNG_GUITEST_VERBINDUNG";

    /// <summary>Fester Zufalls-Seed der App (#519): Auslosungen und Simulationen sind damit in jedem Lauf gleich.</summary>
    public const string ZUFALLS_SEED = "4711";

    /// <summary>Beim Bauen festgehalten (AssemblyMetadata im csproj), weil die Testausgabe außerhalb des Repos
    /// liegen kann.</summary>
    public static string RepoWurzel { get; } =
        typeof(GuiTestUmgebung).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "RepoWurzel").Value!;

    public static string ArtefaktOrdner => Path.Combine(RepoWurzel, "artifacts", "guitest");

    public static string AppPfad =>
        Environment.GetEnvironmentVariable(APP_VARIABLE) is { Length: > 0 } pfad
            ? pfad
            : Path.Combine(ArtefaktOrdner, "bin", "Fussballturnierverwaltung.WinUi", "debug_win-x64",
                "Fussballturnierverwaltung.WinUi.exe");

    /// <summary>Verbindung ohne Datenbank; die Testdatenbank setzt <c>TestDatenbank</c> dazu.</summary>
    public static string Grundverbindung =>
        Environment.GetEnvironmentVariable(VERBINDUNG_VARIABLE) is { Length: > 0 } verbindung
            ? verbindung
            : "Host=localhost;Port=5432;Username=turnierverwaltung";
}
