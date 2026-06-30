namespace Fussballturnierverwaltung.Wpf.Navigation;

/// <summary>
/// Wird von SpielplanViewModel an SimulationsViewModel übergeben, um ein Turnierspiel
/// direkt beim Aktivieren vorauszuwählen und die Simulation automatisch zu starten.
/// </summary>
public record VorausgewaehlteSimulation(int InstanzId, int RundeId, int SpielId);
