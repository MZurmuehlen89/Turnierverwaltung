using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Turnier aus der Conference-League-Vorlage (Ligaphase mit 36 Teams, Vereine) unter eindeutigem Namen — die
/// einzige Europapokal-Vorlage ohne Kontingente im Seed (#574), die Szenarien beginnen so ohne Quoten.</summary>
internal static class KontingentTurnier
{
    public const string VORLAGE = "UEFA Conference League";
    public const string VARIANTE = "36 Teams · ab 2024/25";

    public static (TurnierlisteSeite Turniere, string Name) Anlegen(TurnierverwaltungApp app)
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.AusVorlageErstellen(VORLAGE, VARIANTE, "2027", name);
        turniere.Kopf().Auswaehlen(name);
        return (turniere, name);
    }

    /// <summary>Wartet, bis <paramref name="erfuellt"/> gilt, höchstens die übliche Zeit, ohne zu werfen — die
    /// folgende Prüfung meldet dann die tatsächlichen Werte.</summary>
    public static void Abwarten(Func<bool> erfuellt) => Wait.Within(erfuellt, Wait.DefaultTimeout);
}
