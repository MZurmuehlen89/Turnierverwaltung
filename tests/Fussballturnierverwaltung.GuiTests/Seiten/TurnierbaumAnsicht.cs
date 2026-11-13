using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Eine Karte des Turnierbaums: Paarung und Stand, wie die Karte sie zeigt (Tore einschließlich Verlängerung,
/// bei Hin- und Rückspiel das Gesamtergebnis). Ein offenes Spiel hat leere Tore.</summary>
public sealed record BaumKarte(string Heim, string HeimTore, string AuswaertsTore, string Auswaerts)
{
    /// <summary>„a:b“ wie in der Ergebnisspalte des Spielplans, „-:-“ für ein offenes Spiel.</summary>
    public string Ergebnis => HeimTore.Length == 0 ? "-:-" : $"{HeimTore}:{AuswaertsTore}";
}

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

    /// <summary>Name im Siegerfeld (#527), leer, solange der Turniersieger offen ist. In der rundenweisen Ansicht steht
    /// das Feld nur unter der Finalkarte, also nur, wenn das Finale im Fenster ist.</summary>
    public string Turniersieger => fenster.TextOf("Turnierbaum.Turniersieger");

    /// <summary>Die Karten mit Mannschaften, die gerade zu sehen sind — rundenweise beide Spalten, in der
    /// Gesamtübersicht alle. Freilose (Karte ohne Namen) fehlen.</summary>
    public IReadOnlyList<BaumKarte> Karten() =>
        [.. fenster.FindAllById("TurnierBaum.Karte")
            .Select(karte => new BaumKarte(
                karte.TextOf("TurnierBaum.KarteHeim"),
                karte.TextOf("TurnierBaum.KarteHeimTore"),
                karte.TextOf("TurnierBaum.KarteAuswaertsTore"),
                karte.TextOf("TurnierBaum.KarteAuswaerts")))
            .Where(karte => karte.Heim.Length > 0)];

    /// <summary>Wartet, bis <paramref name="anzahl"/> Karten dastehen — nach dem Blättern oder Umschalten baut die
    /// Ansicht sie neu auf — und liefert sie.</summary>
    public IReadOnlyList<BaumKarte> KartenNachAufbau(int anzahl)
    {
        Wait.Within(() => Karten().Count == anzahl, Wait.DefaultTimeout);
        return Karten();
    }

    /// <summary>Schaltet auf die Gesamtübersicht (#238) um.</summary>
    public void GesamtuebersichtZeigen()
    {
        fenster.ActivateById("TurnierBaum.Gesamtuebersicht");
        Wait.UntilFound(() => fenster.TryFindById("TurnierBaum.Rundenweise"), Wait.DefaultTimeout, "Gesamtübersicht");
        hauptfenster.WarteBisRuhig();
    }

    public void RundenweiseZeigen()
    {
        fenster.ActivateById("TurnierBaum.Rundenweise");
        Wait.UntilFound(() => fenster.TryFindById("TurnierBaum.Gesamtuebersicht"), Wait.DefaultTimeout, "rundenweise Ansicht");
        hauptfenster.WarteBisRuhig();
    }

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
