namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>#683: schätzt das Monte-Carlo-Rauschen einer Kennzahl aus der eigenen Stichprobe (Jackknife über Blöcke).</summary>
internal static class Rauschschaetzung
{
    /// <summary>Zahl der Blöcke: eine Saison je Block (Jackknife ohne je eine Saison). Mit 10 Blöcken streute der geschätzte
    /// SE selbst um ±24 % (≈ 1/√(2·(k−1))) und ließ einen Seed rot werden, dessen Abstand zur Grenze unter 2 echten SE lag;
    /// mit 100 Blöcken sind es ±7 % (Messung in #683).</summary>
    public const int BLOECKE = RealismusStichprobe.DURCHLAEUFE;

    /// <summary>Jackknife-Standardfehler: θᵢ = Kennzahl ohne Block i, SE = √((k−1)/k · Σ(θᵢ − θ̄)²).
    /// <paramref name="block"/> liefert zu jedem Datum seinen Block (0 … <paramref name="bloecke"/> − 1).</summary>
    public static double JackknifeStandardfehler<T>(
        IReadOnlyList<T> daten, Func<T, int> block, int bloecke, Func<IReadOnlyList<T>, double> messen)
    {
        var ohneBlock = new double[bloecke];
        // Die Messfunktionen sind rein; jeder Block schreibt nur seinen eigenen Platz – parallel, Ergebnis unverändert.
        Parallel.For(0, bloecke, i => ohneBlock[i] = messen([.. daten.Where(d => block(d) != i)]));
        double mittel = ohneBlock.Average();
        double summe = ohneBlock.Sum(t => (t - mittel) * (t - mittel));
        return Math.Sqrt((bloecke - 1.0) / bloecke * summe);
    }

    /// <summary>Standardfehler einer Kennzahl für die Bandprüfung: Jackknife, oder 0 für eine Kennzahl ohne Rauschzuschlag.</summary>
    public static double Standardfehler(Realismuskennzahl kennzahl, IReadOnlyList<SimuliertesSpiel> spiele) =>
        kennzahl.MitRauschzuschlag ? JackknifeStandardfehler(spiele, kennzahl.Messen) : 0;

    /// <summary>Wie oben für die Realismus-Stichprobe: Block = Durchlauf / (Durchläufe je Block).</summary>
    public static double JackknifeStandardfehler(
        IReadOnlyList<SimuliertesSpiel> spiele, Func<IReadOnlyList<SimuliertesSpiel>, double> messen) =>
        JackknifeStandardfehler(spiele, s => s.Durchlauf / (RealismusStichprobe.DURCHLAEUFE / BLOECKE), BLOECKE, messen);
}
