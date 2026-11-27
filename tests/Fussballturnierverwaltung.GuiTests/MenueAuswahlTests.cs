using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>#269 K9: In der Seitenleiste ist genau der Eintrag der offenen Seite markiert.</summary>
public sealed class MenueAuswahlTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    public static TheoryData<string> Ziele => [.. Bereich.Alle.Select(b => b.Ziel)];

    [Theory]
    [MemberData(nameof(Ziele))]
    public void OffeneSeite_MarkiertGenauIhrenEintrag(string ziel)
    {
        var bereich = Bereich.Zu(ziel);

        app.Hauptfenster.Oeffnen(bereich);

        app.Hauptfenster.AktiveMenueeintraege.Should().Equal(bereich);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
