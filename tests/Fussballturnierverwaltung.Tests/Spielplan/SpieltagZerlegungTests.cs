using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Tests.Spielplan;

/// <summary>Hält die Spieltag-Verteilung von <see cref="SpieltagZerlegung"/> je Startwert exakt fest (#504): Ein Umbau
/// darf weder das Ergebnis noch die Reihenfolge ändern, in der der Zufall gezogen wird. Die Regeln selbst (Spieltagzahl,
/// Heim-/Auswärtsrhythmus) prüft <see cref="TopfLigaGeneratorTests"/>.</summary>
public class SpieltagZerlegungTests
{
    private static string Kurzform(int[]? spieltage) =>
        spieltage is null ? "keine Verteilung" : string.Join("", spieltage);

    // Fingerabdruck der ganzen Verteilung — 144 Spieltage ausgeschrieben wären unlesbar.
    private static string Fingerabdruck(string kurzform) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kurzform)))[..16];

    [Theory]
    [InlineData(1, "645851B346EF4064")]
    [InlineData(2, "13BE447789D5E3F5")]
    [InlineData(3, "8E89ED6CC4DBCDBE")]
    public void AusgeglichenerPlan_UclLigaphase_VerteilungJeStartwertFest(int startwert, string fingerabdruck)
    {
        var paarungen = TopfLigaGeneratorTests.UclPaarungen(startwert);

        var kurzform = Kurzform(SpieltagZerlegung.Zerlegen(paarungen, new Random(startwert)));

        Fingerabdruck(kurzform).Should().Be(fingerabdruck);
    }

    [Fact]
    public void AusgeglichenerPlan_UngeraderKreis_KeineVerteilung()
    {
        // Drei Teams, jedes einmal Gastgeber und einmal Gast: der einzige Rundlauf ist ein Kreis der Länge 3. Beide
        // Wege laufen durch — die Rundlauf-Suche in der ersten, die schrittweise Verteilung in der zweiten Hälfte der
        // Versuche —, und beide scheitern: drei Spiele, von denen keine zwei am selben Spieltag stattfinden können,
        // passen nicht in zwei Spieltage.
        var paarungen = new List<(int, int)> { (1, 2), (2, 3), (3, 1) };

        Kurzform(SpieltagZerlegung.Zerlegen(paarungen, new Random(5))).Should().Be("keine Verteilung");
    }

    [Theory]
    [InlineData(1, "213545431243152")]
    [InlineData(2, "432511532425134")]
    public void SonstigerPlan_JederGegenJeden_VerteilungJeStartwertFest(int startwert, string erwartet)
    {
        var ids       = Enumerable.Range(1, 6).ToList();
        var paarungen = ids.SelectMany(a => ids.Where(b => b > a).Select(b => (a, b))).ToList();

        Kurzform(SpieltagZerlegung.Zerlegen(paarungen, new Random(startwert))).Should().Be(erwartet);
    }

    [Fact]
    public void SonstigerPlan_Unverteilbar_LiefertNull()
    {
        // Dreieck mit ungleicher Bilanz: nur die schrittweise Verteilung, die ebenso an den zwei Spieltagen scheitert.
        var paarungen = new List<(int, int)> { (1, 2), (2, 3), (1, 3) };

        Kurzform(SpieltagZerlegung.Zerlegen(paarungen, new Random(1), maxVersuche: 4)).Should().Be("keine Verteilung");
    }
}
