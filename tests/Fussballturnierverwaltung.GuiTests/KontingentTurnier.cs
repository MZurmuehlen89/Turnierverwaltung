using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Turnier aus der Champions-League-Vorlage (Ligaphase mit 36 Teams, Vereine) unter eindeutigem Namen.</summary>
internal static class KontingentTurnier
{
    private const string VORLAGE = "UEFA Champions League";
    private const string VARIANTE = "36 Teams · ab 2024/25";

    public static (TurnierlisteSeite Turniere, string Name) Anlegen(TurnierverwaltungApp app)
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.AusVorlageErstellen(VORLAGE, VARIANTE, "2027", name);
        turniere.Kopf().Auswaehlen(name);
        return (turniere, name);
    }

    public static void Abwarten(Func<bool> bedingung, string beschreibung) =>
        Wait.Until(bedingung, Wait.DefaultTimeout, beschreibung);
}
