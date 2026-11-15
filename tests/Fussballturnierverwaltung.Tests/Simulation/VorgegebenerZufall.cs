namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// <see cref="Random"/> mit vorgegebener Zahlenfolge (#615): macht Zweige der SimulationsEngine gezielt erreichbar,
/// die mit einem Startwert nur zufällig getroffen würden (Golden Goal in einer bestimmten Minute, Elfmeterschießen mit
/// festen Treffern). Überschrieben ist <see cref="Sample"/>: darauf führt <see cref="Random"/> in abgeleiteten Klassen
/// NextDouble, Next(max), Next(min, max) und NextSingle zurück, ganze Zahlen also wie gewohnt aus dem Gleitkommawert.
/// Next() ohne Grenze nutzt Sample nicht und ist deshalb gesperrt. Ist die Folge erschöpft, liefert sie
/// <paramref name="danach"/> – ohne diesen Wert wirft sie, damit ein Test nicht unbemerkt mit anderen Zahlen weiterläuft.
/// </summary>
internal sealed class VorgegebenerZufall(IEnumerable<double> werte, double? danach = null) : Random
{
    private readonly Queue<double> _werte = new(werte);

    public int Verbraucht { get; private set; }

    protected override double Sample()
    {
        Verbraucht++;
        if (_werte.TryDequeue(out var wert)) return wert;
        return danach ?? throw new InvalidOperationException($"Zufallsfolge nach {Verbraucht - 1} Werten erschöpft");
    }

    public override int Next() => throw new NotSupportedException("Next() zieht nicht aus der vorgegebenen Folge");
}
