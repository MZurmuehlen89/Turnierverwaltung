namespace Fussballturnierverwaltung.WinUi.Navigation;

/// <summary>
/// Wird vom Spielplan-Tab an den Simulation-Tab übergeben, um ein Turnierspiel direkt beim
/// Tab-Wechsel vorauszuwählen und die Simulation automatisch zu starten. Pendant zu
/// Wpf/Navigation/VorausgewaehlteSimulation.cs (#302). <paramref name="NurAnzeigen"/> unterdrückt das
/// automatische (Neu-)Simulieren — genutzt, wenn nur der bereits vorhandene Spielverlauf eines im
/// Spielplan simulierten Spiels angesehen werden soll, ohne das Ergebnis zu überschreiben.
/// </summary>
public record VorausgewaehlteSimulation(int InstanzId, int RundeId, int SpielId, bool NurAnzeigen = false);
