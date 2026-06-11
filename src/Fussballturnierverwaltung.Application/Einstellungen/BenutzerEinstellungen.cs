using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Einstellungen;

public class BenutzerEinstellungen
{
    public string ExportPfad { get; set; } = Path.Combine(AppContext.BaseDirectory, "Exports");
    public SimulationsModus StandardSimulationsModus { get; set; } = SimulationsModus.SchnellSimulation;
    public int? StandardSimulationsSeed { get; set; }
}
