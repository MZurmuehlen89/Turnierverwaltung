using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class RundenartErweiterungenTests
{
    // Jede Rundenart muss hier eingeordnet sein – eine neue Rundenart lässt den Test fehlschlagen (#571).
    private static readonly Dictionary<Rundenart, (bool Ko, bool Endspiel)> _erwartung = new()
    {
        [Rundenart.Liga] = (false, false),
        [Rundenart.Gruppenphase] = (false, false),
        [Rundenart.TopfLiga] = (false, false),
        [Rundenart.KoRunde] = (true, false),
        [Rundenart.SchweizerSystem] = (false, false),
        [Rundenart.Finale] = (true, true),
        [Rundenart.SpielUmPlatzDrei] = (true, true),
    };

    public static TheoryData<Rundenart> AlleRundenarten => [.. Enum.GetValues<Rundenart>()];

    [Theory]
    [MemberData(nameof(AlleRundenarten))]
    public void IstKoRunde_UndIstEndspiel_StimmenMitErwartungstabelleUeberein(Rundenart art)
    {
        Assert.True(_erwartung.TryGetValue(art, out var erwartet), $"{art} fehlt in der Erwartungstabelle");
        Assert.Equal(erwartet.Ko, art.IstKoRunde());
        Assert.Equal(erwartet.Endspiel, art.IstEndspiel());
    }

    [Fact]
    public void KoRundenarten_EnthaeltGenauDieKoRunden()
    {
        var erwartet = Enum.GetValues<Rundenart>().Where(a => a.IstKoRunde()).OrderBy(a => a);
        Assert.Equal(erwartet, RundenartErweiterungen.KoRundenarten.OrderBy(a => a));
    }

    [Theory]
    [InlineData(Rundenart.Finale, Rundenart.SpielUmPlatzDrei, true)]
    [InlineData(Rundenart.SpielUmPlatzDrei, Rundenart.Finale, true)]
    [InlineData(Rundenart.Finale, Rundenart.Finale, false)]
    [InlineData(Rundenart.KoRunde, Rundenart.Finale, false)]
    public void IstEndspielGeschwister_NurFinaleUndPlatzDreiInBeliebigerReihenfolge(Rundenart a, Rundenart b, bool erwartet)
        => Assert.Equal(erwartet, a.IstEndspielGeschwister(b));
}
