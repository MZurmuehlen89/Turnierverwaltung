using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Smoke-Test (#521): Jede Seite der Hauptnavigation öffnet sich, lädt fertig und zeigt keinen Dialog –
/// etwa eine Fehlermeldung beim Laden.</summary>
public sealed class NavigationSmokeTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    public static TheoryData<string> Ziele => [.. Bereich.Alle.Select(b => b.Ziel)];

    [Theory]
    [MemberData(nameof(Ziele))]
    public void Bereich_OeffnetSichOhneFehler(string ziel)
    {
        var bereich = Bereich.Zu(ziel);

        app.Hauptfenster.Oeffnen(bereich);

        app.Hauptfenster.Seitentitel.Should().Be(bereich.Titel);
        app.Hauptfenster.OffeneDialoge.Select(d => d.Title).Should().BeEmpty();
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
