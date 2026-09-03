namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

/// <summary>Ein fest zugewiesener Platzhalter-Slot einer <see cref="VorlagenRunde"/> mit
/// <see cref="VorlagenRunde.PaarungFestVorgegeben"/> = true (Issue #277) — Vorlagen-Pendant zu den auf
/// Turnier-Instanz-Ebene über <c>Auslosung</c>/<c>AuslosungsErgebnis</c> (Auslosungsart.Manuell) verwalteten
/// festen Paarungen. Da auf Vorlagenebene nie echte Mannschaften existieren, wird ausschließlich der
/// Anzeigename des zugewiesenen Platzhalters gespeichert (analog <c>Spiel.HeimPlatzhalterName</c>/
/// <c>AuswaertsPlatzhalterName</c>), keine ID-Referenz auf die erzeugende <see cref="VorlagenUebergangsRegel"/>.</summary>
public class VorlagenPaarung : BasisEntitaet
{
    public int VorlagenRundeId { get; set; }
    public VorlagenRunde VorlagenRunde { get; set; } = null!;

    // K.o.-Rundenart: Nummer des Paars (1-basiert). Gruppenphase/Liga/SchweizerSystem: Nummer der Gruppe
    // (1-basiert; Liga/SchweizerSystem haben stets genau eine "Gruppe").
    public int PaarungsNummer { get; set; }

    // K.o.-Rundenart: 1 = Heim, 2 = Auswärts. Gruppenphase/Liga/SchweizerSystem: 1-basierter Platz innerhalb
    // der Gruppe.
    public int SlotNr { get; set; }

    public string PlatzhalterBezeichnung { get; set; } = string.Empty;
}
