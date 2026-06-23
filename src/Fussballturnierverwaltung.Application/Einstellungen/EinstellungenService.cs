using System.Text.Json;

namespace Fussballturnierverwaltung.Application.Einstellungen;

public class EinstellungenService
{
    private static readonly string _basisPfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Turnierverwaltung");

    private static readonly string _pfad = Path.Combine(_basisPfad, "einstellungen.json");

    private static readonly JsonSerializerOptions _jsonOptionen = new() { WriteIndented = true };

    public string EinstellungsPfad => _pfad;

    public string LogPfad => Path.Combine(_basisPfad, "logs");

    public BenutzerEinstellungen Laden()
    {
        if (!File.Exists(_pfad))
            return new BenutzerEinstellungen();

        try
        {
            var json = File.ReadAllText(_pfad);
            return JsonSerializer.Deserialize<BenutzerEinstellungen>(json, _jsonOptionen)
                   ?? new BenutzerEinstellungen();
        }
        catch
        {
            return new BenutzerEinstellungen();
        }
    }

    public void Speichern(BenutzerEinstellungen einstellungen)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_pfad)!);
        File.WriteAllText(_pfad, JsonSerializer.Serialize(einstellungen, _jsonOptionen));
    }
}
