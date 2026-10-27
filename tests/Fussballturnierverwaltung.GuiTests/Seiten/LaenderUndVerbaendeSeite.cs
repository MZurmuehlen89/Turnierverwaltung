using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Eine Zeile der Länderliste, soweit die Tests sie lesen.</summary>
public sealed record LandEintrag(string Name, string FifaCode, string VerbandKurzname);

/// <summary>Eine Zeile der Verbandsliste.</summary>
public sealed record VerbandEintrag(string Name, string Kurzname);

/// <summary>Seitenobjekt „Länder &amp; Verbände“ (#530) mit den Reitern Länder und Verbände. Beide Reiter teilen
/// sich die Befehlsleiste; sie wirkt auf den gewählten Reiter.</summary>
public sealed class LaenderUndVerbaendeSeite(Window fenster, Hauptfenster hauptfenster)
{
    public StammdatenEditor Laender { get; } = new(fenster, hauptfenster, "Laender", "Land löschen");
    public StammdatenEditor Verbaende { get; } = new(fenster, hauptfenster, "Verbaende", "Verband löschen");

    /// <summary>Wechselt auf den Reiter Länder. Der Kopf beginnt mit einem Symbol, darum wird er über das Wortende
    /// gesucht.</summary>
    public StammdatenEditor LaenderReiter() => Reiter("Länder", "Laender.Suche", Laender);

    public StammdatenEditor VerbaendeReiter() => Reiter("Verbände", "Verbaende.Suche", Verbaende);

    /// <summary>Legt einen Verband an (Reiter Verbände).</summary>
    public void VerbandAnlegen(string name, string kurzname)
    {
        VerbaendeReiter();
        Verbaende.Neu();
        Verbaende.WertSetzen("Name", name);
        Verbaende.WertSetzen("Kurzname", kurzname);
        Verbaende.Speichern();
    }

    /// <summary>Legt ein Land an (Reiter Länder); der Kontinent bleibt auf der Vorgabe des Formulars.</summary>
    public void LandAnlegen(string name, string fifaCode, string verband)
    {
        LaenderReiter();
        Laender.Neu();
        Laender.WertSetzen("Name", name);
        Laender.WertSetzen("Kurzname", name);
        Laender.WertSetzen("FifaCode", fifaCode);
        Laender.Auswahl("Verband", verband);
        Laender.Speichern();
    }

    public LandEintrag Land(string name) => Laender.Eintrag(name, e => new LandEintrag(
        e.TextOf("Laender.ListeName"), e.TextOf("Laender.ListeFifaCode"), e.TextOf("Laender.ListeVerband")));

    public VerbandEintrag Verband(string name) => Verbaende.Eintrag(name, e => new VerbandEintrag(
        e.TextOf("Verbaende.ListeName"), e.TextOf("Verbaende.ListeKurzname")));

    private StammdatenEditor Reiter(string kopfEnde, string suchfeld, StammdatenEditor editor)
    {
        Wait.UntilFound(
            () => fenster.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
                .FirstOrDefault(t => t.NameOrEmpty().EndsWith(kopfEnde, StringComparison.Ordinal)),
            Wait.DefaultTimeout,
            $"Reiter '{kopfEnde}'").Activate();
        fenster.FindById(suchfeld);
        hauptfenster.WarteBisRuhig();
        return editor;
    }
}
