using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eintrag der Auswahl „Spielbedeutung“ im Rundenformular (#668), dazu der Zusatz, mit dem Spielplan und
/// Spielprotokoll sie nennen.</summary>
public record SpielbedeutungOption(string Anzeige, Spielbedeutung Wert)
{
    /// <summary>Feste Liste (Referenzabgleich der ComboBox), aufsteigend nach Bedeutung.</summary>
    public static IReadOnlyList<SpielbedeutungOption> Alle { get; } =
    [
        new("gering – es wird offen gespielt", Spielbedeutung.Gering),
        new("normal", Spielbedeutung.Normal),
        new("hoch – beide Teams spielen vorsichtig", Spielbedeutung.Hoch)
    ];

    /// <summary>Die Option zum Wert; ein unbekannter Wert (etwa von Hand in der Datenbank gesetzt) zeigt „normal“,
    /// statt das Formular beim Binden abstürzen zu lassen.</summary>
    public static SpielbedeutungOption Fuer(Spielbedeutung wert) =>
        Alle.FirstOrDefault(o => o.Wert == wert) ?? Alle.Single(o => o.Wert == Spielbedeutung.Normal);

    /// <summary>Zusatz für Rundenhinweis und Kontextzeile, wie beim Überraschungsfaktor (#360); bei „normal“ keiner.</summary>
    public static string Zusatz(Spielbedeutung? spielbedeutung) => spielbedeutung switch
    {
        Spielbedeutung.Hoch   => " · Spielbedeutung hoch",
        Spielbedeutung.Gering => " · Spielbedeutung gering",
        _                     => ""
    };
}
