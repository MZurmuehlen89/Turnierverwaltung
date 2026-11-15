using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Sichern und Wiederherstellen auf der Datenbankwartung (#43, AK1, AK4, AK12). Das Wiederherstellen selbst
/// prüfen die Unit-Tests: Danach startet die App neu, und die Sitzung des Tests verlöre sie (wie beim Speichern der
/// Verbindung, #49). Ein einziges Szenario, weil die Schritte aufeinander aufbauen – die Sicherungen liegen im
/// Datenpfad des Laufs, den nur diese Klasse befüllt.</summary>
public sealed class DatenbankSicherungTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private readonly DatenbankwartungSeite _seite = app.Hauptfenster.DatenbankwartungOeffnen();

    [Fact]
    public async Task Sichern_ZeigtDieSicherung_AbbrechenDerRueckfrageLaesstDieDatenbankUnveraendert()
    {
        // AK12: noch keine Sicherung
        _seite.KeineSicherungAngezeigt.Should().BeTrue();
        _seite.Sicherungen.Should().BeEmpty();
        _seite.WiederherstellenMoeglich.Should().BeFalse("ohne Sicherung gibt es nichts wiederherzustellen");

        // AK1
        _seite.Sichern();

        _seite.SicherungsFehlermeldung.Should().BeEmpty();
        _seite.Sicherungen.Should().ContainSingle().Which.Should()
            .MatchRegex($"^{datenbank.Name}_\\d{{4}}-\\d{{2}}-\\d{{2}}_\\d{{2}}-\\d{{2}}-\\d{{2}}\\.dump$");
        _seite.WiederherstellenMoeglich.Should().BeFalse("noch ist keine Sicherung gewählt");

        // AK4: Rückfrage abbrechen – die Datenbank bleibt, wie sie ist
        var verbaende = await datenbank.WertLesenAsync<long>("SELECT COUNT(*) FROM \"Verbaende\"");
        await datenbank.AusfuehrenAsync("INSERT INTO \"Verbaende\" (\"Name\", \"Kurzname\") VALUES ('Nach der Sicherung', 'NDS')");
        _seite.SicherungAuswaehlen(_seite.Sicherungen[0]);
        _seite.WiederherstellenMoeglich.Should().BeTrue();

        _seite.WiederherstellenAbbrechen();

        (await datenbank.WertLesenAsync<long>("SELECT COUNT(*) FROM \"Verbaende\"")).Should().Be(verbaende + 1,
            "der Verband von nach der Sicherung ist noch da");
        _seite.SicherungsFehlermeldung.Should().BeEmpty();
        await datenbank.AusfuehrenAsync("DELETE FROM \"Verbaende\" WHERE \"Kurzname\" = 'NDS'");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
