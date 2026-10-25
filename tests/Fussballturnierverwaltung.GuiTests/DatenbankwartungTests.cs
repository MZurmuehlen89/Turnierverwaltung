using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Datenbankwartung (#538): „Prüfen / ergänzen“ auf der Test-Datenbank des Laufs. Die App hat sie beim ersten
/// Start selbst angelegt, eine erneute Prüfung findet also nichts mehr zu tun. Ein fehlender Seed-Eintrag wird per SQL
/// hergestellt und muss von der Wartung wieder ergänzt werden – danach ist der Seed wieder vollständig.</summary>
public sealed class DatenbankwartungTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string NICHTS_ERGAENZT = "Datenbank und Stammdaten sind vollständig — nichts ergänzt.";

    // Ein Seed-Regelwerk, das keine Vorlage benutzt: Es lässt sich samt Punkteregeln und Entscheidungsverfahren
    // (kaskadierend) löschen, ohne andere Daten mitzureißen
    private const string UNBENUTZTES_REGELWERK = "UEFA EM 2004 (Silver Goal)";

    private readonly DatenbankwartungSeite _seite = app.Hauptfenster.DatenbankwartungOeffnen();

    [Fact]
    public async Task Pruefen_AufDerFrischenDatenbank_ErgaenztNichts()
    {
        int eingespielt = await datenbank.WertLesenAsync<int>("SELECT COUNT(*) FROM __EFMigrationsHistory");

        var ergebnis = _seite.PruefenUndErgaenzen();

        ergebnis.Fehlermeldung.Should().BeEmpty();
        ergebnis.Protokoll.Should().Equal(NICHTS_ERGAENZT);
        _seite.Datenbank.Should().Be(datenbank.Name, "die Seite zeigt die Datenbank, mit der die App arbeitet");
        _seite.Migrationsstand.Should().Be($"{eingespielt} eingespielt, keine ausstehend");
    }

    [Fact]
    public async Task Pruefen_ErgaenztEinGeloeschtesSeedRegelwerk()
    {
        var vorher = await RegelwerkZaehlenAsync();
        vorher.Anzahl.Should().Be(1, "der Seed enthält das Regelwerk genau einmal");
        (await RegelwerkVerwendungenAsync()).Should().Be(0, "das Regelwerk darf von keiner Runde oder Instanz benutzt werden");

        await datenbank.AusfuehrenAsync("DELETE FROM Regelwerke WHERE Name = @name", ("@name", UNBENUTZTES_REGELWERK));
        (await RegelwerkZaehlenAsync()).Anzahl.Should().Be(0, "Ausgangslage: das Regelwerk fehlt");

        var ergebnis = _seite.PruefenUndErgaenzen();

        ergebnis.Fehlermeldung.Should().BeEmpty();
        ergebnis.Protokoll.Should().Equal("1 Regelwerke nachgerüstet.");
        (await RegelwerkZaehlenAsync()).Should().Be(vorher, "das Regelwerk ist samt Regeln wieder da");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private sealed record RegelwerkStand(int Anzahl, int Punkteregeln, int Entscheidungsverfahren);

    private async Task<RegelwerkStand> RegelwerkZaehlenAsync() => new(
        await RegelwerkWertAsync("SELECT COUNT(*) FROM Regelwerke WHERE Name = @name"),
        await RegelwerkWertAsync(
            "SELECT COUNT(*) FROM PunkteRegeln p JOIN Regelwerke r ON r.Id = p.RegelwerkId WHERE r.Name = @name"),
        await RegelwerkWertAsync(
            "SELECT COUNT(*) FROM Entscheidungsverfahren e JOIN Regelwerke r ON r.Id = e.RegelwerkId WHERE r.Name = @name"));

    private Task<int> RegelwerkVerwendungenAsync() => RegelwerkWertAsync("""
        SELECT (SELECT COUNT(*) FROM TurnierRunden t JOIN Regelwerke r ON r.Id = t.RegelwerkId WHERE r.Name = @name)
             + (SELECT COUNT(*) FROM TurnierInstanzen i JOIN Regelwerke r ON r.Id = i.StandardRegelwerkId WHERE r.Name = @name)
        """);

    private Task<int> RegelwerkWertAsync(string sql) => datenbank.WertLesenAsync<int>(sql, ("@name", UNBENUTZTES_REGELWERK));
}
