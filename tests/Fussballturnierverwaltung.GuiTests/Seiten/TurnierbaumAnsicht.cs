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

    /// <summary>Senkrechte Bildschirmmitte der Karten einer Spalte, von oben nach unten.</summary>
    public IReadOnlyList<double> KartenMitten(Spalte spalte) =>
        [.. fenster.FindById(spalte == Spalte.Links ? "TurnierBaum.LinkeSpalte" : "TurnierBaum.RechteSpalte")
            .FindAllById("TurnierBaum.Karte")
            .Select(karte => karte.BoundingRectangle.Top + karte.BoundingRectangle.Height / 2.0)
            .Order()];
}

public enum Spalte { Links, Rechts }
