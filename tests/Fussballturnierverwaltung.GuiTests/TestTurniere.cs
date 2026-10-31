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

    /// <summary>Entfernt die Standard-Rangliste aller Runden des Turniers: Mit ihr befüllt die App die Töpfe schon beim
    /// Anlegen der Auslosung (#349), eine danach gewählte eigene Rangliste käme nicht mehr zum Zug.</summary>
    public static Task<int> StandardRanglisteEntfernenAsync(TestDatenbank datenbank, string turnier) =>
        datenbank.AusfuehrenAsync(
            """
            UPDATE r SET StandardRanglisteId = NULL
            FROM TurnierRunden r JOIN TurnierInstanzen t ON t.Id = r.TurnierInstanzId WHERE t.Name = @turnier;
            """,
            ("@turnier", turnier));
}
