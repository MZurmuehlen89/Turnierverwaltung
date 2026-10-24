using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Eine Zeile einer Statistiktabelle; <see cref="Werte"/> nach Spaltenkopf („Sp“, „S“, „Tore“, „Pkt“ …).</summary>
public sealed record TabellenZeile(string Rang, string Mannschaft, IReadOnlyDictionary<string, string> Werte)
{
    public int Zahl(string spalte) => int.Parse(Werte[spalte].TrimStart('+'));
}

/// <summary>Reiter „Statistik“: Auswertung wählen und ihre Tabelle lesen.</summary>
public sealed class StatistikReiter(Window fenster, Hauptfenster hauptfenster)
{
    public void AuswertungWaehlen(string titel)
    {
        var eintrag = Wait.UntilFound(
            () => fenster.FindById("Statistik.Auswertungen")
                .ReadAllItems(e => ItemMatcher.Matches(e.NameOrEmpty(), e.DisplayedTexts(), [titel]) ? e : null)
                .FirstOrDefault(),
            Wait.DefaultTimeout,
            $"Auswertung '{titel}'");
        eintrag.Activate();
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Die sichtbare Tabelle; die Zellen jeder Zeile werden den Spaltenköpfen der Reihe nach zugeordnet.</summary>
    public IReadOnlyList<TabellenZeile> Tabelle()
    {
        var spalten = fenster.FindAllById("Statistik.Spaltenkopf").Select(k => k.NameOrEmpty()).ToList();
        return [.. fenster.FindAllById("Statistik.Zeile").Select(zeile =>
        {
            var zellen = zeile.FindAllById("Statistik.Zelle").Select(z => z.NameOrEmpty()).ToList();
            if (zellen.Count != spalten.Count)
                throw new InvalidOperationException(
                    $"Zeile „{zeile.NameOrEmpty()}“ hat {zellen.Count} Zellen, die Tabelle {spalten.Count} Spalten.");
            return new TabellenZeile(
                zeile.TextOf("Statistik.Rang"),
                zeile.TextOf("Statistik.Mannschaft"),
                spalten.Zip(zellen).ToDictionary(p => p.First, p => p.Second));
        })];
    }
}
