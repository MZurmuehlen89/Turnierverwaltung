using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;

namespace Fussballturnierverwaltung.Tests.Stammdaten;

public class LoeschSperreTests
{
    [Fact]
    public void FuerVerband_OhneBezuege_IstNull()
        => LoeschSperre.FuerVerband("UEFA", new VerbandBezuege(0, 0)).Should().BeNull();

    [Fact]
    public void FuerVerband_NenntLaenderUndQuotenInMehrzahl()
        => LoeschSperre.FuerVerband("UEFA", new VerbandBezuege(55, 3)).Should().Be(
            "Der Verband „UEFA“ kann nicht gelöscht werden. Noch zugeordnet: 55 Länder, Verbandsquote in 3 Turnieren.");

    [Fact]
    public void FuerVerband_NurQuoteInEinemTurnier()
        => LoeschSperre.FuerVerband("OFC", new VerbandBezuege(0, 1)).Should().Be(
            "Der Verband „OFC“ kann nicht gelöscht werden. Noch zugeordnet: Verbandsquote in 1 Turnier.");

    [Fact]
    public void FuerLand_OhneBezuege_IstNull()
        => LoeschSperre.FuerLand("Malta", new LandBezuege(0, 0)).Should().BeNull();

    [Fact]
    public void FuerLand_NenntEinzahl()
        => LoeschSperre.FuerLand("Malta", new LandBezuege(1, 1)).Should().Be(
            "Das Land „Malta“ kann nicht gelöscht werden. Noch zugeordnet: 1 Mannschaft, Landesquote in 1 Turnier.");

    [Fact]
    public void FuerLand_NurMannschaften()
        => LoeschSperre.FuerLand("Deutschland", new LandBezuege(120, 0)).Should().Be(
            "Das Land „Deutschland“ kann nicht gelöscht werden. Noch zugeordnet: 120 Mannschaften.");
}
