namespace Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

public class RankingRegelSet : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;

    /// <summary>Trennt der direkte Vergleich (Mini-Tabelle der gleichstehenden Teams) nur einen Teil der Teams, wird
    /// er für die übrigen mit ihren Spielen untereinander neu gerechnet, bevor die folgenden Kriterien greifen
    /// (UEFA, EM-Reglement Art. 20.01d). Sonst entscheiden gleich die folgenden Kriterien (FIFA, #628).</summary>
    public bool DirektenVergleichWiederholen { get; set; }

    public ICollection<RankingRegelSetEintrag> Eintraege { get; set; } = new List<RankingRegelSetEintrag>();
}
