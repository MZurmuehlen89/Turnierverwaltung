namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>Ergebnis einer Bandprüfung: <paramref name="Gruen"/> und die Berichtszeile für die Testausgabe.</summary>
internal readonly record struct Bandergebnis(bool Gruen, string Zeile);

/// <summary>#683: prüft einen Messwert gegen sein Band mit Rauschzuschlag – rot erst, wenn die Bandgrenze um mehr als
/// <c>z</c> Standardfehler (Monte-Carlo-Rauschen der Stichprobe) überschritten ist.</summary>
internal static class Bandpruefung
{
    public const double Z = 2.0;

    public static Bandergebnis Pruefen(Realismuskennzahl kennzahl, double wert, double standardfehler, double z = Z) =>
        Pruefen(kennzahl.Name, wert, standardfehler, kennzahl.Von, kennzahl.Bis, kennzahl.Ziel, kennzahl.Format,
            kennzahl.MitRauschzuschlag ? z : 0);

    /// <summary>Wie oben, schätzt den Standardfehler aber nur für einen Wert außerhalb des Bandes: im Band ist die Kennzahl
    /// grün, wie groß das Rauschen auch ist – der Jackknife (100 Neuberechnungen je Kennzahl, ~8 s für alle) entfällt dann.</summary>
    public static Bandergebnis Pruefen(Realismuskennzahl kennzahl, double wert, Func<double> standardfehler, double z = Z) =>
        Pruefen(kennzahl, wert, wert >= kennzahl.Von && wert <= kennzahl.Bis ? 0 : standardfehler(), z);

    public static Bandergebnis Pruefen(
        string name, double wert, double standardfehler, double von, double bis, string ziel, string format = "P1", double z = Z)
    {
        bool gruen = wert >= von - z * standardfehler && wert <= bis + z * standardfehler;
        string rauschen = standardfehler > 0 ? $" ± {standardfehler.ToString(format)}" : "";
        string zeile = $"{name}: {wert.ToString(format)}{rauschen}  " +
                       $"(Band {Grenze(von, format)} … {Grenze(bis, format)}, {ziel}; " +
                       (z > 0 ? $"rot erst {z:0.#} SE außerhalb)" : "ohne Rauschzuschlag)");
        return new Bandergebnis(gruen, zeile);
    }

    private static string Grenze(double grenze, string format) =>
        double.IsNegativeInfinity(grenze) ? "−∞" : double.IsPositiveInfinity(grenze) ? "∞" : grenze.ToString(format);
}
