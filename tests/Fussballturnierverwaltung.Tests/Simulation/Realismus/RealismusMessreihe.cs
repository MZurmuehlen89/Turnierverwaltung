using System.Globalization;
using System.Text;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>
/// #683: Messwerkzeug – alle Realismus-Kennzahlen je Seed als Tabelle, nur auf ausdrücklichen Aufruf (<c>Explicit</c>, läuft
/// weder im normalen Testlauf noch in CI). Es nutzt denselben Katalog wie <c>SimulationsRealismusTests</c> (<see cref="Realismuskennzahlen"/>)
/// und schreibt je Seed Wert und Jackknife-Standardfehler je Kennzahl in eine CSV sowie am Ende eine Zusammenfassung
/// (Mittel, Streuung über die Seeds, mittlerer Jackknife-SE, Zahl roter Seeds ohne und mit Rauschzuschlag).
/// Aufruf (PowerShell, ~6 s je Seed):
/// <c>$env:REALISMUS_SEEDS='678,1,2,3'; dotnet test --project tests/Fussballturnierverwaltung.Tests -- --filter-class "*RealismusMessreihe" --explicit only</c>
/// – <c>REALISMUS_SEEDS</c> (Standard 678 und 1–29), <c>REALISMUS_AUSGABE</c> (CSV, Standard <c>realismus-messreihe.csv</c>
/// im Temp-Ordner; die Zusammenfassung liegt daneben als <c>.zusammenfassung.txt</c>).
/// </summary>
public class RealismusMessreihe
{
    private const string STANDARD_SEEDS = "678,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29";

    [Fact(Explicit = true)]
    public void Messreihe_JeSeed()
    {
        var seeds = (Environment.GetEnvironmentVariable("REALISMUS_SEEDS") ?? STANDARD_SEEDS).Split(',').Select(int.Parse).ToList();
        var ausgabe = Environment.GetEnvironmentVariable("REALISMUS_AUSGABE")
                      ?? Path.Combine(Path.GetTempPath(), "realismus-messreihe.csv");

        var namen = new Realismuskennzahlen([]).Alle.Select(k => k.Name).ToList();
        var csv = new StringBuilder("Seed;" + string.Join(";", namen.SelectMany(n => new[] { n, n + " SE" })) + "\n");
        var werte = new List<(double Wert, double Se)[]>();

        foreach (int seed in seeds)
        {
            var spiele = RealismusStichprobe.Erzeugen(seed);
            var katalog = new Realismuskennzahlen(spiele);
            var zeile = katalog.Alle
                .Select(k => (Wert: k.Messen(spiele), Se: Rauschschaetzung.JackknifeStandardfehler(spiele, k.Messen)))
                .ToArray();
            werte.Add(zeile);
            csv.Append(seed + ";" + string.Join(";", zeile.Select(z => Zahl(z.Wert) + ";" + Zahl(z.Se))) + "\n");
            File.WriteAllText(ausgabe, csv.ToString());
        }

        var zusammenfassung = Zusammenfassen(new Realismuskennzahlen([]).Alle, werte);
        File.WriteAllText(Path.ChangeExtension(ausgabe, ".zusammenfassung.txt"), zusammenfassung);
        TestContext.Current.TestOutputHelper?.WriteLine(zusammenfassung);
    }

    private static string Zahl(double wert) => wert.ToString("F5", CultureInfo.InvariantCulture);

    private static string Zusammenfassen(IReadOnlyList<Realismuskennzahl> katalog, List<(double Wert, double Se)[]> werte)
    {
        var sb = new StringBuilder($"{werte.Count} Seeds\nKennzahl | Mittel | SD über Seeds | Ø Jackknife-SE | SE/SD | rot ohne Zuschlag | rot mit z = {Bandpruefung.Z:0.#}\n");
        for (int i = 0; i < katalog.Count; i++)
        {
            var k = katalog[i];
            var spalte = werte.Select(w => w[i]).ToList();
            double mittel = spalte.Average(s => s.Wert);
            double sd = werte.Count > 1 ? Math.Sqrt(spalte.Sum(s => (s.Wert - mittel) * (s.Wert - mittel)) / (werte.Count - 1)) : double.NaN;
            double se = spalte.Average(s => s.Se);
            int rotOhne = spalte.Count(s => !Bandpruefung.Pruefen(k, s.Wert, s.Se, 0).Gruen);
            int rotMit = spalte.Count(s => !Bandpruefung.Pruefen(k, s.Wert, s.Se).Gruen);
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"{k.Name} | {mittel:F4} | {sd:F4} | {se:F4} | {se / sd:F2} | {rotOhne} | {rotMit}\n"));
        }
        int seedsRotMit = werte.Count(w => Enumerable.Range(0, katalog.Count).Any(i => !Bandpruefung.Pruefen(katalog[i], w[i].Wert, w[i].Se).Gruen));
        int seedsRotOhne = werte.Count(w => Enumerable.Range(0, katalog.Count).Any(i => !Bandpruefung.Pruefen(katalog[i], w[i].Wert, w[i].Se, 0).Gruen));
        sb.Append($"Seeds mit mindestens einer roten Kennzahl: ohne Zuschlag {seedsRotOhne} von {werte.Count}, mit Zuschlag {seedsRotMit} von {werte.Count}\n");
        return sb.ToString();
    }
}
