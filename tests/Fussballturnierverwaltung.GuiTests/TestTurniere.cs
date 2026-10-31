namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Anpassungen an einem frisch aus einer Vorlage erstellten Turnier, an der Oberfläche vorbei – zwischen
/// <c>Hauptfenster.TurnierAusVorlageErstellen</c> und <c>TurnierdurchfuehrungOeffnen(name)</c>.</summary>
internal static class TestTurniere
{
    /// <summary>Entfernt Verbands- und Landesquoten des Turniers: Szenarien mit eigener Rangliste losen sonst nicht
    /// aus ihr, weil die Quoten ihre Plätze aus den Ranglisten von Verband und Land füllen (#573).</summary>
    public static Task<int> QuotenEntfernenAsync(TestDatenbank datenbank, string turnier) =>
        datenbank.AusfuehrenAsync(
            """
            DELETE q FROM LandesQuoten q JOIN TurnierInstanzen t ON t.Id = q.TurnierInstanzId WHERE t.Name = @turnier;
            DELETE q FROM VerbandsQuoten q JOIN TurnierInstanzen t ON t.Id = q.TurnierInstanzId WHERE t.Name = @turnier;
            """,
            ("@turnier", turnier));
}
