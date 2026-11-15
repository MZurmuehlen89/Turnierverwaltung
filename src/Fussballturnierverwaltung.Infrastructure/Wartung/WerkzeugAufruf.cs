using System.Diagnostics;
using Npgsql;

namespace Fussballturnierverwaltung.Infrastructure.Wartung;

/// <summary>Startet <c>pg_dump</c>/<c>pg_restore</c> mit der Anmeldung der App (#43) und liefert deren Fehlerausgabe.</summary>
public static class WerkzeugAufruf
{
    public const string KENNWORT_VARIABLE = "PGPASSWORD";

    /// <summary>Verbindungsangaben als Argumente, nie eine Kennwortabfrage (<c>-w</c>): Ohne Kennwort in der App liest
    /// das Werkzeug wie Npgsql die Kennwortdatei (pgpass.conf). Ein Kennwort aus der App geht per Umgebungsvariable an
    /// den Kindprozess – auf der Befehlszeile wäre es für andere Prozesse sichtbar.</summary>
    public static ProcessStartInfo Erstellen(
        string programm, NpgsqlConnectionStringBuilder ziel, IEnumerable<string> argumente)
    {
        var start = new ProcessStartInfo(programm)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in (string[])["-h", ziel.Host ?? "localhost", "-p", ziel.Port.ToString(), "-U", ziel.Username ?? "", "-w"])
            start.ArgumentList.Add(argument);
        foreach (var argument in argumente)
            start.ArgumentList.Add(argument);
        if (!string.IsNullOrEmpty(ziel.Password))
            start.Environment[KENNWORT_VARIABLE] = ziel.Password;
        return start;
    }

    /// <returns>Die Fehlerausgabe, oder <c>null</c>, wenn das Werkzeug ohne Fehler endete.</returns>
    public static async Task<string?> AusfuehrenAsync(ProcessStartInfo start, CancellationToken ct)
    {
        using var prozess = Process.Start(start)
            ?? throw new DatenbankSicherungException($"{Path.GetFileName(start.FileName)} ließ sich nicht starten.");
        var fehler = prozess.StandardError.ReadToEndAsync(ct);
        var ausgabe = prozess.StandardOutput.ReadToEndAsync(ct);
        try
        {
            await prozess.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            prozess.Kill(entireProcessTree: true);
            throw;
        }
        await ausgabe;
        var text = (await fehler).Trim();
        if (prozess.ExitCode == 0)
            return null;
        return text.Length > 0 ? text : $"Exitcode {prozess.ExitCode}";
    }
}
