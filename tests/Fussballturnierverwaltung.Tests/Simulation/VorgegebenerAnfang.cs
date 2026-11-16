namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// <see cref="Random"/>, das erst eine vorgegebene Zahlenfolge liefert und danach aus einem zweiten Zufallsgenerator
/// weiterzieht (#673): legt den Anfang eines Spiels fest (etwa 1:0 in Minute 1) und lässt den Rest streuen. Wie bei
/// <see cref="VorgegebenerZufall"/> ist nur <see cref="Sample"/> überschrieben.
/// </summary>
internal sealed class VorgegebenerAnfang(IEnumerable<double> anfang, Random danach) : Random
{
    private readonly Queue<double> _anfang = new(anfang);

    protected override double Sample() => _anfang.TryDequeue(out var wert) ? wert : danach.NextDouble();

    public override int Next() => throw new NotSupportedException("Next() zieht nicht aus der vorgegebenen Folge");
}
