using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

/// <summary>Platzierungsbereich einer Liga- oder Gruppentabelle, der nicht über eine Übergangsregel in eine Runde
/// desselben Turniers führt (Issue #420) — z. B. „Platz 1–4 Champions League“, „Platz 17–18 Abstieg“. Heute nur
/// Tabellenzone; ein Ziel (Liga-Ebene der Folgesaison) lässt sich für #379 nachrüsten. Gilt für jede Gruppe der
/// Runde. Plätze, die eine Übergangsregel abdeckt, nehmen deren Zone.</summary>
public class TabellenBereich : BasisEntitaet
{
    public int TurnierRundeId { get; set; }
    public TurnierRunde TurnierRunde { get; set; } = null!;

    public int PlatzVon { get; set; } // inklusiv
    public int PlatzBis { get; set; } // inklusiv
    public TabellenBereichArt Art { get; set; }
    public string Bezeichnung { get; set; } = string.Empty;
}
