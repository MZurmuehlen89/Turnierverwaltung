namespace Fussballturnierverwaltung.Infrastructure.Wartung;

/// <summary>Findet die Client-Werkzeuge von PostgreSQL (<c>pg_dump</c>, <c>pg_restore</c>) für die Sicherung (#43).
/// Gesucht wird zuerst in der neuesten Installation (ein älteres <c>pg_dump</c> verweigert die Sicherung eines neueren
/// Servers), danach im <c>PATH</c>.</summary>
public class PostgresWerkzeuge(IReadOnlyList<string> verzeichnisse)
{
    public IReadOnlyList<string> Verzeichnisse { get; } = verzeichnisse;

    public static PostgresWerkzeuge Standard() =>
        new([.. BinNachVersion(Installationen()), .. PfadVerzeichnisse()]);

    /// <returns>Den vollen Pfad des Programms, oder <c>null</c>, wenn es in keinem Verzeichnis liegt.</returns>
    public string? Finden(string programm)
    {
        var datei = OperatingSystem.IsWindows() ? programm + ".exe" : programm;
        return Verzeichnisse.Select(v => Path.Combine(v, datei)).FirstOrDefault(File.Exists);
    }

    /// <summary>Die <c>bin</c>-Ordner der Versionsordner (<c>…\PostgreSQL\18</c>), höchste Version zuerst; Ordner
    /// ohne Versionsnummer im Namen zählen nicht.</summary>
    public static IEnumerable<string> BinNachVersion(IEnumerable<string> versionsordner) =>
        versionsordner
            .Select(ordner => (Ordner: ordner, Version: VersionLesen(Path.GetFileName(ordner.TrimEnd('/', '\\')))))
            .Where(x => x.Version is not null)
            .OrderByDescending(x => x.Version)
            .Select(x => Path.Combine(x.Ordner, "bin"));

    private static Version? VersionLesen(string name)
    {
        var text = name.Contains('.') ? name : name + ".0";
        return Version.TryParse(text, out var version) ? version : null;
    }

    private static IEnumerable<string> Installationen()
    {
        var basis = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL")
            : "/usr/lib/postgresql";
        return Directory.Exists(basis) ? Directory.EnumerateDirectories(basis) : [];
    }

    private static IEnumerable<string> PfadVerzeichnisse() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
