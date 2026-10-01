namespace Fussballturnierverwaltung.Application.Tabelle;

public record GruppenTabelle(int GruppenNummer, IReadOnlyList<TabellenEintrag> Tabelle)
{
    /// <summary>Noch nicht aufgelöste Teilnehmerplätze der Gruppe (z.B. „Sieger Quali 6“), in der Reihenfolge
    /// ihres ersten Spiels — für die Anzeige unterhalb der Tabelle (Issue #397).</summary>
    public IReadOnlyList<string> OffenePlaetze { get; init; } = [];
}
