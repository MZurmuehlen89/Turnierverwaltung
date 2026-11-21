using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>
/// Nervenstärke der Seed-Mannschaften (#669): Wer nicht in <see cref="NervenstaerkeSeedListe.Handliste"/> steht, ist „normal“.
/// Die Liste enthält nur die abweichenden Teams (oberes Viertel stark, unteres Viertel schwach) und ist einmalig mit
/// <c>tools/Kondition-EAFC</c> aus Spielerwerten abgeleitet und auf drei Stufen verdichtet – keine Rohdaten im Repo.
/// Schlüssel ist (Mannschaftsart, Kurzname, Land) wie bei <see cref="KonditionSeedDaten"/>.
/// </summary>
public static class NervenstaerkeSeedDaten
{
    public sealed record Eintrag(Mannschaftsart Art, string Kurzname, string Land, Nervenstaerke Nervenstaerke);

    private static readonly Dictionary<(Mannschaftsart, string, string), Eintrag> _nachSchluessel =
        NervenstaerkeSeedListe.Handliste.ToDictionary(e => (e.Art, e.Kurzname, e.Land));

    /// <summary>Setzt die Nervenstärke einer neu angelegten Seed-Mannschaft.</summary>
    /// <param name="land">FIFA-Code ihres Landes.</param>
    public static Mannschaft MitNervenstaerke(this Mannschaft mannschaft, string land)
    {
        mannschaft.Nervenstaerke = _nachSchluessel.TryGetValue((mannschaft.Mannschaftsart, mannschaft.Kurzname, land), out var eintrag)
            ? eintrag.Nervenstaerke
            : Nervenstaerke.Normal;
        return mannschaft;
    }
}
