using FluentAssertions;
using Fussballturnierverwaltung.Infrastructure.Seed;

namespace Fussballturnierverwaltung.Tests.Seed;

public class SeedBildQuelleTests
{
    // Die Flaggen aus flag-icons (MIT) liegen im öffentlichen Repo und sind in jedem Build eingebettet (#633).
    [Fact]
    public void Eingebettet_FlaggeAusFlagIcons_IstVorhanden()
    {
        var flagge = SeedBildQuelle.Eingebettet.Laden("Flaggen", "GER");

        flagge.Should().NotBeNull();
        flagge.Take(4).Should().Equal([0x89, 0x50, 0x4E, 0x47], "das ist der Anfang der PNG-Signatur");
    }

    [Fact]
    public void Eingebettet_UnbekannterCode_LiefertNull()
    {
        SeedBildQuelle.Eingebettet.Laden("Flaggen", "GIBTSNICHT").Should().BeNull();
    }
}
