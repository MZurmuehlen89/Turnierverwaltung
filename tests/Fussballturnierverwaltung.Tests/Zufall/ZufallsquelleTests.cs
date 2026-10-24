using FluentAssertions;
using Fussballturnierverwaltung.Application.Zufall;

namespace Fussballturnierverwaltung.Tests.Zufall;

public class ZufallsquelleTests
{
    private static List<int> Folge(Random zufall) => [.. Enumerable.Range(0, 20).Select(_ => zufall.Next())];

    [Fact]
    public void OhneFestenSeed_IstKeinSeedGesetzt()
    {
        new Zufallsquelle().FesterSeed.Should().BeNull();
    }

    [Fact]
    public void OhneFestenSeed_LiefertJederVorgangEineAndereFolge()
    {
        var quelle = new Zufallsquelle();

        Folge(quelle.Erzeugen()).Should().NotEqual(Folge(quelle.Erzeugen()));
    }

    [Fact]
    public void MitFestemSeed_LiefertSeedZiehenImmerDiesenSeed()
    {
        var quelle = new Zufallsquelle(42);

        quelle.SeedZiehen().Should().Be(42);
        quelle.SeedZiehen().Should().Be(42);
    }

    [Fact]
    public void MitFestemSeed_BeginntJederVorgangMitDerselbenFolge()
    {
        var quelle = new Zufallsquelle(42);

        Folge(quelle.Erzeugen()).Should().Equal(Folge(quelle.Erzeugen()));
    }

    [Fact]
    public void MitFestemSeed_EntsprichtDieFolgeEinemRandomMitDiesemSeed()
    {
        Folge(new Zufallsquelle(42).Erzeugen()).Should().Equal(Folge(new Random(42)));
    }
}
