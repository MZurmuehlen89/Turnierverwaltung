using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;

namespace GuiTest.Framework.Tests;

public class WaitTests
{
    private static readonly TimeSpan _kurz = TimeSpan.FromMilliseconds(300);

    [Fact]
    public void UntilFound_LiefertDenErstenTreffer()
    {
        int versuche = 0;

        var ergebnis = Wait.UntilFound(() => ++versuche < 3 ? null : "gefunden", TimeSpan.FromSeconds(5), "Testwert");

        ergebnis.Should().Be("gefunden");
        versuche.Should().Be(3);
    }

    [Fact]
    public void UntilFound_OhneTreffer_MeldetZeitueberschreitungMitBeschreibung()
    {
        var aktion = () => Wait.UntilFound<string>(() => null, _kurz, "Hauptfenster");

        aktion.Should().Throw<WaitTimeoutException>().WithMessage("*Hauptfenster*");
    }

    [Fact]
    public void UntilFound_ComFehler_ZaehltAlsNochNichtUndWirdBeimTimeoutMitgeliefert()
    {
        var aktion = () => Wait.UntilFound<string>(() => throw new COMException("Element wird neu aufgebaut"), _kurz, "Element");

        aktion.Should().Throw<WaitTimeoutException>().WithInnerException<COMException>();
    }

    [Fact]
    public void UntilFound_AndereFehler_WerdenSofortWeitergereicht()
    {
        var dauer = Stopwatch.StartNew();
        var aktion = () => Wait.UntilFound<string>(() => throw new InvalidOperationException("App beendet"), TimeSpan.FromSeconds(5), "Element");

        aktion.Should().Throw<InvalidOperationException>();
        dauer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Until_ErfuellteBedingung_KehrtZurueck()
    {
        var aktion = () => Wait.Until(() => true, _kurz, "Bedingung");

        aktion.Should().NotThrow();
    }

    [Fact]
    public void UntilStable_BedingungHaeltLangGenug_KehrtZurueck()
    {
        var dauer = Stopwatch.StartNew();

        Wait.UntilStable(() => true, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(5), "stabil");

        dauer.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void UntilStable_FlackerndeBedingung_ErreichtNieDieStabilitaet()
    {
        bool wert = false;

        var aktion = () => Wait.UntilStable(() => wert = !wert, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), "flackert");

        aktion.Should().Throw<WaitTimeoutException>().WithMessage("*flackert*");
    }
}
