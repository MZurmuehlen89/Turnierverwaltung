using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>
/// Kondition der Seed-Mannschaften (#677): Wer nicht in <see cref="KonditionSeedListe.Handliste"/> steht, ist „normal“.
/// Die Liste enthält nur die abweichenden Teams (oberes Viertel stark, unteres Viertel schwach) und ist einmalig mit
/// <c>tools/Kondition-EAFC</c> aus Spielerwerten abgeleitet und auf drei Stufen verdichtet – keine Rohdaten im Repo.
/// Schlüssel ist (Mannschaftsart, Kurzname, Land) wie bei <see cref="SpielweiseSeedDaten"/>.
/// </summary>
public static class KonditionSeedDaten
{
    public sealed record Eintrag(Mannschaftsart Art, string Kurzname, string Land, Kondition Kondition);

    private static readonly Dictionary<(Mannschaftsart, string, string), Eintrag> _nachSchluessel =
        KonditionSeedListe.Handliste.ToDictionary(e => (e.Art, e.Kurzname, e.Land));

    /// <summary>Setzt die Kondition einer neu angelegten Seed-Mannschaft.</summary>
    /// <param name="land">FIFA-Code ihres Landes.</param>
    public static Mannschaft MitKondition(this Mannschaft mannschaft, string land)
    {
        mannschaft.Kondition = _nachSchluessel.TryGetValue((mannschaft.Mannschaftsart, mannschaft.Kurzname, land), out var eintrag)
            ? eintrag.Kondition
            : Kondition.Normal;
        return mannschaft;
    }
}
