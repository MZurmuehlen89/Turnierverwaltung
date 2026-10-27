using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Punkte für Sieg, Unentschieden und Niederlage, wie sie das Formular zeigt.</summary>
public sealed record Punkte(int Sieg, int Unentschieden, int Niederlage);

/// <summary>Seitenobjekt „Regelwerke“ (#530): Name, Gültigkeit und Punktesystem; Entscheidungsverfahren und
/// Rangfolge bleiben auf der Vorgabe.</summary>
public sealed class RegelwerkeSeite(Window fenster, Hauptfenster hauptfenster)
{
    public StammdatenEditor Editor { get; } = new(fenster, hauptfenster, "Regelwerke", "Regelwerk löschen");

    public Punkte Punkte => new(
        Editor.Regler("PunkteSieg"), Editor.Regler("PunkteUnentschieden"), Editor.Regler("PunkteNiederlage"));

    public void PunkteSetzen(Punkte punkte)
    {
        Editor.ReglerSetzen("PunkteSieg", punkte.Sieg);
        Editor.ReglerSetzen("PunkteUnentschieden", punkte.Unentschieden);
        Editor.ReglerSetzen("PunkteNiederlage", punkte.Niederlage);
    }

    public void Anlegen(string name, Punkte punkte)
    {
        Editor.Neu();
        Editor.WertSetzen("Name", name);
        PunkteSetzen(punkte);
        Editor.Speichern();
    }

    /// <summary>Ob <paramref name="name"/> in der Liste steht (wartet darauf).</summary>
    public string Gelistet(string name) => Editor.Eintrag(name, e => e.TextOf("Regelwerke.ListeName"));
}
