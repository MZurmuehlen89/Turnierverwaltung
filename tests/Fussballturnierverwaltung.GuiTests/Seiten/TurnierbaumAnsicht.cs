using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Rundenweise Ansicht des Turnierbaums im Reiter „Spielplan &amp; Baum“ (#567): ein Fenster aus zwei
/// Rundenspalten, geblättert wird mit ◀/▶. Sichtbar ist sie, solange eine K.-o.-Runde gewählt ist.</summary>
public sealed class TurnierbaumAnsicht(Window fenster, Hauptfenster hauptfenster)
{
    public string LinkeSpalte => fenster.TextOf("TurnierBaum.LinkeSpalteName");
    public string RechteSpalte => fenster.TextOf("TurnierBaum.RechteSpalteName");

    /// <summary>Wartet, bis das Fenster <paramref name="links"/> | <paramref name="rechts"/> zeigt.</summary>
    public void WarteAufFenster(string links, string rechts) =>
        Wait.Until(() => LinkeSpalte == links && RechteSpalte == rechts, Wait.DefaultTimeout,
            $"Turnierbaum zeigt {links} | {rechts}");

    public void NaechsteRunde()
    {
        string vorher = LinkeSpalte;
        fenster.ActivateById("TurnierBaum.NaechsteRunde");
        Wait.Until(() => LinkeSpalte != vorher, Wait.DefaultTimeout, "Turnierbaum weitergeblättert");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Senkrechte Bildschirmmitte der Karten beider Spalten, von oben nach unten. Die Karten bekommen ihre
    /// Höhe erst in den Layout-Durchläufen nach dem Laden — gewartet wird, bis die erwartete Zahl Karten steht und
    /// <paramref name="angekommen"/> zutrifft, höchstens <see cref="Wait.DefaultTimeout"/>; danach wird gelesen,
    /// was dasteht, und der Test prüft es selbst.</summary>
    public (IReadOnlyList<double> Links, IReadOnlyList<double> Rechts) KartenMittenNachLayout(int links, int rechts,
        Func<IReadOnlyList<double>, IReadOnlyList<double>, bool> angekommen)
    {
        Wait.Within(() => KartenMitten() is var (l, r) && l.Count == links && r.Count == rechts && angekommen(l, r),
            Wait.DefaultTimeout);
        return KartenMitten();
    }

    // Die beiden Spalten (ItemsControls) haben keinen UIA-Knoten — die Karten werden darum über ihre waagerechte
    // Lage den Spalten zugeordnet: links steht, was links von der Mitte zwischen äußerster linker und rechter Karte liegt.
    private (IReadOnlyList<double> Links, IReadOnlyList<double> Rechts) KartenMitten()
    {
        var karten = fenster.FindAllById("TurnierBaum.Karte").Select(k => k.BoundingRectangle).ToList();
        if (karten.Count == 0) return ([], []);

        double trenner = (karten.Min(k => k.Left) + karten.Max(k => k.Left)) / 2;
        IReadOnlyList<double> Mitten(Func<double, bool> inSpalte) =>
            [.. karten.Where(k => inSpalte(k.Left)).Select(k => k.Top + k.Height / 2.0).Order()];
        return (Mitten(x => x <= trenner), Mitten(x => x > trenner));
    }
}
