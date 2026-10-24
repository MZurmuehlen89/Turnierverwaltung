namespace Fussballturnierverwaltung.Application.Einstellungen;

/// <summary>
/// Ablageort für Einstellungen und Logs. Standard ist <c>%APPDATA%\Turnierverwaltung</c>. Die Umgebungsvariable
/// <see cref="UMGEBUNGSVARIABLE"/> lenkt ihn um, damit automatisierte GUI-Tests weder die Einstellungen noch das
/// Log des Nutzers berühren (#518, #494).
/// </summary>
public static class Datenpfade
{
    public const string UMGEBUNGSVARIABLE = "TURNIERVERWALTUNG_DATENPFAD";

    public static string Basis { get; } = BasisErmitteln(
        Environment.GetEnvironmentVariable(UMGEBUNGSVARIABLE),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string Logs => Path.Combine(Basis, "logs");

    public static string BasisErmitteln(string? ueberschreibung, string anwendungsdatenOrdner) =>
        string.IsNullOrWhiteSpace(ueberschreibung)
            ? Path.Combine(anwendungsdatenOrdner, "Turnierverwaltung")
            : Path.GetFullPath(ueberschreibung.Trim());
}
