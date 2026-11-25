using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eintrag der Auswahl „Punkteübernahme“ im Rundenformular (#355): wie viele Punkte die Runde aus der Tabelle ihrer
/// Quellrunde mitnimmt.</summary>
public record PunktevortragOption(string Anzeige, Punktevortrag Wert)
{
    /// <summary>Feste Liste (Referenzabgleich der ComboBox).</summary>
    public static IReadOnlyList<PunktevortragOption> Alle { get; } = (PunktevortragOption[])
    [
        new("keine – die Tabelle startet bei null", Punktevortrag.Keine),
        new("alle Punkte", Punktevortrag.Voll),
        new("halbiert, abgerundet (Österreich)", Punktevortrag.HalbiertAbgerundet),
        new("halbiert, aufgerundet (Belgien)", Punktevortrag.HalbiertAufgerundet)
    ];

    /// <summary>Die Option zum Wert; ein unbekannter Wert zeigt „keine“, statt das Formular beim Binden abstürzen zu lassen.</summary>
    public static PunktevortragOption Fuer(Punktevortrag wert) =>
        Alle.FirstOrDefault(o => o.Wert == wert) ?? Alle.Single(o => o.Wert == Punktevortrag.Keine);
}
