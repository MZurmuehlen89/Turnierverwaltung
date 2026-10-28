using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Invarianten einer Statistiktabelle mit 3-Punkte-Regel, unabhängig vom Turnierformat (#522, #560).</summary>
internal static class TabellenPruefung
{
    /// <summary>Genau die Teams aus <paramref name="spieleJeTeam"/> mit so vielen Spielen; je Zeile S+U+N = Sp und
    /// Pkt = 3·S + U; über alle Zeilen so viele Tore wie Gegentore.</summary>
    public static void Stimmig(IReadOnlyList<TabellenZeile> tabelle, IReadOnlyDictionary<string, int> spieleJeTeam)
    {
        tabelle.Select(z => z.Mannschaft).Should().BeEquivalentTo(spieleJeTeam.Keys);
        foreach (var zeile in tabelle)
        {
            zeile.Zahl("Sp").Should().Be(spieleJeTeam[zeile.Mannschaft], zeile.Mannschaft);
            (zeile.Zahl("S") + zeile.Zahl("U") + zeile.Zahl("N")).Should().Be(zeile.Zahl("Sp"), zeile.Mannschaft);
            zeile.Zahl("Pkt").Should().Be(3 * zeile.Zahl("S") + zeile.Zahl("U"), zeile.Mannschaft);
        }

        var tore = tabelle.Select(z => Spielstand.Tore(z.Werte["Tore"])
            ?? throw new InvalidOperationException($"Tore von {z.Mannschaft} nicht lesbar: {z.Werte["Tore"]}")).ToList();
        tore.Sum(t => t.Heim).Should().Be(tore.Sum(t => t.Auswaerts), "jedes Tor ist ein Gegentor");
    }
}
