namespace Fussballturnierverwaltung.Wpf.Navigation;

/// <summary>
/// Wird von SpielplanViewModel an SimulationsViewModel übergeben, um ein Turnierspiel
/// direkt beim Aktivieren vorauszuwählen und die Simulation automatisch zu starten.
/// Issue #240: <paramref name="NurAnzeigen"/> unterdrückt das automatische (Neu-)Simulieren —
/// genutzt, wenn nur der bereits vorhandene Spielverlauf eines im Spielplan simulierten Spiels
/// angesehen werden soll, ohne das Ergebnis zu überschreiben.
/// </summary>
public record VorausgewaehlteSimulation(int InstanzId, int RundeId, int SpielId, bool NurAnzeigen = false);
