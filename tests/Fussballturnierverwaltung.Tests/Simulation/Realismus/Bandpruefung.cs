namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>Ergebnis einer Bandprüfung: <paramref name="Gruen"/> und die Berichtszeile für die Testausgabe.</summary>
internal readonly record struct Bandergebnis(bool Gruen, string Zeile);

/// <summary>#683: prüft einen Messwert gegen sein Band mit Rauschzuschlag – rot erst, wenn die Bandgrenze um mehr als
/// <c>z</c> Standardfehler (Monte-Carlo-Rauschen der Stichprobe) überschritten ist.</summary>
internal static class Bandpruefung
{
    public const double Z = 2.0;

    public static Bandergebnis Pruefen(Realismuskennzahl kennzahl, double wert, double standardfehler, double z = Z) =>
        Pruefen(kennzahl.Name, wert, standardfehler, kennzahl.Von, kennzahl.Bis, kennzahl.Ziel, kennzahl.Format, z);

    public static Bandergebnis Pruefen(
        string name, double wert, double standardfehler, double von, double bis, string ziel, string format = "P1", double z = Z)
    {
        bool gruen = wert >= von - z * standardfehler && wert <= bis + z * standardfehler;
        string zeile = $"{name}: {wert.ToString(format)} ± {standardfehler.ToString(format)}  " +
                       $"(Band {Grenze(von, format)} … {Grenze(bis, format)}, {ziel}; rot erst {z:0.#} SE außerhalb)";
        return new Bandergebnis(gruen, zeile);
    }

    private static string Grenze(double grenze, string format) =>
        double.IsNegativeInfinity(grenze) ? "−∞" : double.IsPositiveInfinity(grenze) ? "∞" : grenze.ToString(format);
}
