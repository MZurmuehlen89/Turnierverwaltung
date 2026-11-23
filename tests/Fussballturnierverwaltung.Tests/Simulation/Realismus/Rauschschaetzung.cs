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
        IReadOnlyList<T> daten, Func<T, int> block, int bloecke, Func<IReadOnlyList<T>, double> messen) =>
        JackknifeStandardfehler(daten, block, 0, bloecke, messen);

    /// <summary>Wie oben, aber nur über die Blöcke <paramref name="ersterBlock"/> … <paramref name="bloecke"/> − 1 (k = Zahl dieser
    /// Blöcke). Für eine Kennzahl, die nur einen Teil der Blöcke misst (Hälfte B, #685): ein Block ohne Wirkung
    /// ließe θᵢ unverändert und gäbe der Formel ein zu großes k.</summary>
    public static double JackknifeStandardfehler<T>(
        IReadOnlyList<T> daten, Func<T, int> block, int ersterBlock, int bloecke, Func<IReadOnlyList<T>, double> messen)
    {
        int k = bloecke - ersterBlock;
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 2, nameof(ersterBlock)); // ein Block allein hat keine Streuung
        var ohneBlock = new double[k];
        // Die Messfunktionen sind rein; jeder Block schreibt nur seinen eigenen Platz – parallel, Ergebnis unverändert.
        Parallel.For(0, k, i => ohneBlock[i] = messen([.. daten.Where(d => block(d) != ersterBlock + i)]));
        double mittel = ohneBlock.Average();
        double summe = ohneBlock.Sum(t => (t - mittel) * (t - mittel));
        return Math.Sqrt((k - 1.0) / k * summe);
    }

    /// <summary>Standardfehler einer Kennzahl für die Bandprüfung: Jackknife über die Blöcke, die sie misst (alle oder nur
    /// Hälfte B), oder 0 für eine Kennzahl ohne Rauschzuschlag.</summary>
    public static double Standardfehler(Realismuskennzahl kennzahl, IReadOnlyList<SimuliertesSpiel> spiele)
    {
        if (!kennzahl.MitRauschzuschlag) return 0;
        int ersterBlock = kennzahl.NurHaelfteB ? RealismusStichprobe.ENDE_HAELFTE_A / DURCHLAEUFE_JE_BLOCK : 0;
        return JackknifeStandardfehler(spiele, s => s.Durchlauf / DURCHLAEUFE_JE_BLOCK, ersterBlock, BLOECKE, kennzahl.Messen);
    }

    // Block = Durchlauf / (Durchläufe je Block)
    private const int DURCHLAEUFE_JE_BLOCK = RealismusStichprobe.DURCHLAEUFE / BLOECKE;
}
