using FluentAssertions;
using Fussballturnierverwaltung.Application.Einstellungen;

namespace Fussballturnierverwaltung.Tests.Einstellungen;

public class DatenpfadeTests
{
    // Absolute Pfade des laufenden Systems statt fester Windows-Pfade: die Unit-Tests laufen in der CI auf Linux (#525)
    private static readonly string _anwendungsdaten = Path.Combine(Path.GetTempPath(), "Anwendungsdaten");
    private static readonly string _lauf = Path.Combine(Path.GetTempPath(), "GuiTests", "Lauf1");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BasisErmitteln_OhneUeberschreibung_LiegtUnterAnwendungsdaten(string? ueberschreibung)
    {
        var basis = Datenpfade.BasisErmitteln(ueberschreibung, _anwendungsdaten);

        basis.Should().Be(Path.Combine(_anwendungsdaten, "Turnierverwaltung"));
    }

    [Fact]
    public void BasisErmitteln_MitUeberschreibung_NimmtDiesenOrdner()
    {
        var basis = Datenpfade.BasisErmitteln(_lauf, _anwendungsdaten);

        basis.Should().Be(_lauf);
    }

    [Fact]
    public void BasisErmitteln_MitUeberschreibung_EntferntUmgebendeLeerzeichen()
    {
        var basis = Datenpfade.BasisErmitteln($"  {_lauf}  ", _anwendungsdaten);

        basis.Should().Be(_lauf);
    }

    [Fact]
    public void BasisErmitteln_MitRelativerUeberschreibung_WirdAbsolut()
    {
        var basis = Datenpfade.BasisErmitteln("GuiTestDaten", _anwendungsdaten);

        basis.Should().Be(Path.Combine(Environment.CurrentDirectory, "GuiTestDaten"));
    }

    [Fact]
    public void Logs_LiegtUnterBasis()
    {
        Datenpfade.Logs.Should().Be(Path.Combine(Datenpfade.Basis, "logs"));
    }
}
