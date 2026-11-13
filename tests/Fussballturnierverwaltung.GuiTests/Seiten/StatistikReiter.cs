using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Eine Zeile einer Statistiktabelle; <see cref="Werte"/> nach Spaltenkopf („Sp“, „S“, „Tore“, „Pkt“ …). Ein
/// Kopf, der mehrfach vorkommt („Ø“, „aktuell“), heißt ab dem zweiten Mal „Ø 2“, „aktuell 2“ usw.</summary>
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

    /// <summary>Der Turnierausgang der Auswertung „Turnier“ (#527): Turniersieger, dann die übrigen Platzierungen in
    /// Platzreihenfolge. Leer, solange der Sieger offen ist.</summary>
    public IReadOnlyList<string> Platzierungen()
    {
        var sieger = Wait.UntilFound(() => fenster.TryFindById("Statistik.Turniersieger"), Wait.DefaultTimeout,
            "Turniersieger in der Statistik");
        return [sieger.NameOrEmpty(), .. fenster.FindAllById("Statistik.Platzierung").Select(p => p.NameOrEmpty())];
    }

    /// <summary>Die sichtbare Tabelle; die Zellen jeder Zeile werden den Spaltenköpfen der Reihe nach zugeordnet.</summary>
    public IReadOnlyList<TabellenZeile> Tabelle()
    {
        var spalten = EindeutigeKoepfe(fenster.FindAllById("Statistik.Spaltenkopf").Select(k => k.NameOrEmpty()));
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

    /// <summary>Wählt die Auswertung <paramref name="titel"/> und liefert ihre Tabelle.</summary>
    public IReadOnlyList<TabellenZeile> Tabelle(string titel)
    {
        AuswertungWaehlen(titel);
        return Tabelle();
    }

    // ── Tabellenverlauf ───────────────────────────────────────────────────────

    /// <summary>Wählt unter dem Tabellenverlauf den Stand der Rundentabelle („aktueller Stand“, „nach dem
    /// 1. Spieltag“ …) und liefert die Mannschaften in Tabellenreihenfolge.</summary>
    public IReadOnlyList<string> RundentabelleNach(string stand)
    {
        fenster.FindById("Tabellenverlauf.Stand").AsComboBox().SelectItem(stand);
        hauptfenster.WarteBisRuhig();
        return [.. fenster.FindAllById("Tabelle.Mannschaft").Select(m => m.NameOrEmpty())];
    }

    private static List<string> EindeutigeKoepfe(IEnumerable<string> koepfe)
    {
        var gesehen = new Dictionary<string, int>();
        return [.. koepfe.Select(kopf =>
        {
            int anzahl = gesehen[kopf] = gesehen.GetValueOrDefault(kopf) + 1;
            return anzahl == 1 ? kopf : $"{kopf} {anzahl}";
        })];
    }
}
