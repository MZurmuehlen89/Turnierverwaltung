using FluentAssertions;
using Fussballturnierverwaltung.Application.Einstellungen;

namespace Fussballturnierverwaltung.Tests.Einstellungen;

public class DatenpfadeTests
{
    private const string ANWENDUNGSDATEN = @"C:\Benutzer\Test\AppData\Roaming";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BasisErmitteln_OhneUeberschreibung_LiegtUnterAnwendungsdaten(string? ueberschreibung)
    {
        var basis = Datenpfade.BasisErmitteln(ueberschreibung, ANWENDUNGSDATEN);

        basis.Should().Be(@"C:\Benutzer\Test\AppData\Roaming\Turnierverwaltung");
    }

    [Fact]
    public void BasisErmitteln_MitUeberschreibung_NimmtDiesenOrdner()
    {
        var basis = Datenpfade.BasisErmitteln(@"D:\GuiTests\Lauf1", ANWENDUNGSDATEN);

        basis.Should().Be(@"D:\GuiTests\Lauf1");
    }

    [Fact]
    public void BasisErmitteln_MitUeberschreibung_EntferntUmgebendeLeerzeichen()
    {
        var basis = Datenpfade.BasisErmitteln(@"  D:\GuiTests\Lauf1  ", ANWENDUNGSDATEN);

        basis.Should().Be(@"D:\GuiTests\Lauf1");
    }

    [Fact]
    public void BasisErmitteln_MitRelativerUeberschreibung_WirdAbsolut()
    {
        var basis = Datenpfade.BasisErmitteln("GuiTestDaten", ANWENDUNGSDATEN);

        basis.Should().Be(Path.Combine(Environment.CurrentDirectory, "GuiTestDaten"));
    }

    [Fact]
    public void Logs_LiegtUnterBasis()
    {
        Datenpfade.Logs.Should().Be(Path.Combine(Datenpfade.Basis, "logs"));
    }
}
