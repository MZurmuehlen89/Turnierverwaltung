namespace Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

/// <summary>Abschnitt einer Rangliste über Ranglistenplätze (Issue #393), z. B. „2. Bundesliga“ bis Platz 36. Der
/// Abschnitt beginnt hinter dem vorigen. Mehrere Abschnitte dürfen dieselbe Spielklasse haben (parallele Staffeln wie
/// die Regionalligen).</summary>
public class RanglistenAbschnitt : BasisEntitaet
{
    public int RanglisteId { get; set; }
    public Rangliste Rangliste { get; set; } = null!;
    public string Name { get; set; } = string.Empty;

    // 1 = höchste Klasse.
    public int Spielklasse { get; set; }
    public int BisPlatz { get; set; }
}
