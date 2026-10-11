namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Ein einzelner Schritt eines <see cref="KaskadenPlan"/> (Issue #88).</summary>
public abstract record KaskadenSchritt(int RundeId, string RundeName);

/// <summary>Ein einzelnes, bereits vorhandenes Spiel, dessen Platzhalter-Zuordnung nicht mehr zum
/// aktuellen Stand seiner Quellrunde passt. Absichtlich auf Spiel- statt Runden-Ebene — andere,
/// nicht betroffene Spiele derselben Runde (inkl. ihrer Ergebnisse) bleiben unangetastet.
/// <see cref="NeuesHeimMannschaftId"/>/<see cref="NeuesAuswaertsMannschaftId"/> sind gesetzt, wenn die
/// neue Zuordnung bereits vollständig auflösbar ist (direkte Korrektur); sonst wird das Spiel mit
/// <see cref="HeimPlatzhalterName"/>/<see cref="AuswaertsPlatzhalterName"/> auf Platzhalter zurückgesetzt.</summary>
public record KaskadenSpielSchritt(
    int SpielId, int RundeId, string RundeName, bool HatteErgebnis,
    int? NeuesHeimMannschaftId, int? NeuesAuswaertsMannschaftId,
    string? HeimPlatzhalterName, string? AuswaertsPlatzhalterName)
    : KaskadenSchritt(RundeId, RundeName);

/// <summary>Eine Auslosung-basierte Platzhalter-Runde ohne ein einziges Spiel (z.B. weil die enge
/// #125-Logik das einzige, noch nicht gespielte Spiel bereits entfernt hat) — muss komplett neu aus
/// ihrer Auslosung generiert werden. Verliert per Definition keine vorhandenen Ergebnisse.</summary>
public record KaskadenRundeNeuGenerierenSchritt(int RundeId, string RundeName) : KaskadenSchritt(RundeId, RundeName);

/// <summary>Vorab berechnete Auswirkung eines Ergebnis-Rücksetzens auf Folgerunden (Issue #88) — von
/// <see cref="PlatzhalterKaskadeService.PlanenAsync"/> als reine Leseoperation ermittelt, von
/// <see cref="PlatzhalterKaskadeService.AusfuehrenAsync"/> tatsächlich angewendet.</summary>
public record KaskadenPlan(IReadOnlyList<KaskadenSchritt> Schritte)
{
    /// <summary>Anzahl der Ergebnisse, die durch die Kaskade tatsächlich verloren gehen — Grundlage für
    /// den Bestätigungsdialog. Zählt bewusst nicht die reinen Paarungskorrekturen ohne Ergebnisverlust
    /// und nicht <see cref="KaskadenRundeNeuGenerierenSchritt"/> (dort existiert nichts, was verloren
    /// gehen könnte).</summary>
    public int GesamtBetroffeneSpiele =>
        Schritte.OfType<KaskadenSpielSchritt>().Count(s => s.HatteErgebnis);

    public bool HatAuswirkungen => Schritte.Count > 0;
}
