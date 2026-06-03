using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class SpielplanGeneratorFactory(IEnumerable<ISpielplanGenerator> generatoren)
{
    private readonly IReadOnlyDictionary<Rundenart, ISpielplanGenerator> _lookup =
        generatoren
            .SelectMany(g => g.UnterstützteRundenarten.Select(r => (Rundenart: r, Generator: g)))
            .ToDictionary(x => x.Rundenart, x => x.Generator);

    public ISpielplanGenerator ErstellenFuerRundenart(Rundenart rundenart)
    {
        if (_lookup.TryGetValue(rundenart, out var generator))
            return generator;

        throw new NotSupportedException(
            $"Für Rundenart '{rundenart}' ist noch kein Spielplan-Generator implementiert. " +
            $"(SchweizerSystem und Sonderrunde folgen in einer späteren Version.)");
    }

    public bool IstUnterstuetzt(Rundenart rundenart) => _lookup.ContainsKey(rundenart);
}
