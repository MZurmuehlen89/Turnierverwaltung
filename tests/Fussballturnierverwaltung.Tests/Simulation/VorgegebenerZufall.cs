namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// <see cref="Random"/> mit vorgegebener Zahlenfolge (#615): macht Zweige der SimulationsEngine gezielt erreichbar,
/// die mit einem Startwert nur zufällig getroffen würden (Golden Goal in einer bestimmten Minute, Elfmeterschießen mit
/// festen Treffern). Alle Aufrufe ziehen aus derselben Folge; ganze Zahlen werden wie bei <see cref="Random"/> aus dem
/// Gleitkommawert abgeleitet. Ist die Folge erschöpft, liefert sie <paramref name="danach"/> – ohne diesen Wert
/// wirft sie, damit ein Test nicht unbemerkt mit anderen Zahlen weiterläuft.
/// </summary>
internal sealed class VorgegebenerZufall(IEnumerable<double> werte, double? danach = null) : Random
{
    private readonly Queue<double> _werte = new(werte);

    public int Verbraucht { get; private set; }

    public override double NextDouble()
    {
        Verbraucht++;
        if (_werte.TryDequeue(out var wert)) return wert;
        return danach ?? throw new InvalidOperationException($"Zufallsfolge nach {Verbraucht - 1} Werten erschöpft");
    }

    public override int Next(int maxValue) => (int)(NextDouble() * maxValue);

    public override int Next(int minValue, int maxValue) => minValue + (int)(NextDouble() * (maxValue - minValue));
}
