using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface ISpielRepository : IRepository<Spiel>
{
    Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Spiel>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>Alle Spiele eines Turniers, in denen die Mannschaft als Heim- oder Auswärtsteam feststeht — samt
    /// Runde, in Turnierreihenfolge (Runde, Spieltag, Spielnummer). Platzhalter, die sicher auf sie verweisen, sind
    /// nach jedem Ergebnis bereits aufgelöst und deshalb enthalten (#479).</summary>
    Task<IReadOnlyList<Spiel>> AlleNachMannschaftAsync(int turnierInstanzId, int mannschaftId, CancellationToken ct = default);

    /// <summary>Die Mannschaften, die in mindestens einem Spiel des Turniers feststehen, nach Name (#479).</summary>
    Task<IReadOnlyList<Mannschaft>> MannschaftenNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<int> HoechsteSpielNummerAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>
    /// Berechnet die nächste freie Spielnummer für eine Runde:
    /// Anzahl der Spiele in allen Runden mit Reihenfolge &lt; <paramref name="reihenfolge"/>
    /// plus bereits vorhandene Spiele in dieser Runde, plus 1.
    /// </summary>
    Task<int> NaechsteSpielNummerAsync(int turnierInstanzId, int turnierRundeId, int reihenfolge, CancellationToken ct = default);
    Task HinzufuegenMehrfachAsync(IEnumerable<Spiel> spiele, CancellationToken ct = default);
    Task AlleNachRundeLoeschenAsync(int turnierRundeId, CancellationToken ct = default);

    Task<Spiel?> FindenNachIdMitErgebnisAsync(int id, CancellationToken ct = default);
    Task ErgebnisSetzenAsync(int spielId, SpielErgebnis ergebnis, CancellationToken ct = default);
    Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default);

    /// <summary>Löscht Ergebnis und Ereignisse mehrerer Spiele mit einer Abfrage und einem Speichern (Issue #386) —
    /// Spiele ohne Ergebnis werden übergangen.</summary>
    Task ErgebnisseLoeschenAsync(IReadOnlyCollection<int> spielIds, CancellationToken ct = default);

    /// <summary>Lädt Spiel mit Teams, Ergebnis und Runde inkl. Regelwerk (für Simulation).</summary>
    Task<Spiel?> FindenNachIdFuerSimulationAsync(int id, CancellationToken ct = default);

    /// <summary>Fügt SpielEreignisse zu einem bereits gespeicherten Spiel hinzu.</summary>
    Task EreignisseHinzufuegenAsync(int spielId, IEnumerable<SpielEreignis> ereignisse, CancellationToken ct = default);

    /// <summary>Lädt Spiel mit TurnierRunde (AuswaertstorRegelAktiv) und Hinspiel-Ergebnis (für Auswärtstorregel).</summary>
    Task<Spiel?> FindenNachIdMitRundeUndHinspielAsync(int id, CancellationToken ct = default);

    /// <summary>Lädt alle Spiele einer Runde inklusive SpielEreignisse (für Fairplay-Wertung).</summary>
    Task<IReadOnlyList<Spiel>> AlleNachRundeMitEreignissenAsync(int turnierRundeId, CancellationToken ct = default);

    /// <summary>Lädt alle Spiele eines Turniers mit Runde, Ergebnis, SpielEreignissen und den Mannschaften samt
    /// Land und Verband (für die Statistik, Issue #327).</summary>
    Task<IReadOnlyList<Spiel>> AlleNachInstanzMitEreignissenAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>Lädt ein einzelnes Spiel mit Ergebnis, Teams und allen SpielEreignissen (für Liveticker).</summary>
    Task<Spiel?> FindenNachIdMitEreignissenAsync(int id, CancellationToken ct = default);

    /// <summary>Setzt je Spiel Heim- und Auswärtsseite unabhängig voneinander entweder auf eine konkrete
    /// Mannschaft oder zurück auf einen Platzhalternamen (jeweils genau eines von beiden pro Seite) —
    /// ursprünglich für Issue #88 (bereits aufgelöste Paarung teilweise ungültig geworden), seit #206
    /// auch für die reguläre, seitenunabhängige Erstauflösung genutzt. Rührt eine bereits korrekte
    /// Seite nicht unnötig an. Alle Korrekturen mit einer Abfrage und einem Speichern (Issue #411).</summary>
    Task PlatzhalterKorrigierenAsync(IReadOnlyCollection<PlatzhalterKorrektur> korrekturen, CancellationToken ct = default);

    /// <summary>Speichert mehrere bereits geladene Spiele mit einem Speichern (Issue #411).</summary>
    Task AktualisierenMehrfachAsync(IReadOnlyCollection<Spiel> spiele, CancellationToken ct = default);

    /// <summary>Ermittelt die (distinkten) TurnierInstanz-IDs der angegebenen Spiele.</summary>
    Task<IReadOnlyList<int>> TurnierInstanzIdsAsync(IReadOnlyList<int> spielIds, CancellationToken ct = default);
}

/// <summary>Soll-Besetzung eines Spiels für <see cref="ISpielRepository.PlatzhalterKorrigierenAsync"/>: je Seite
/// gilt die Mannschaft, wenn gesetzt, sonst der Platzhaltername.</summary>
public record PlatzhalterKorrektur(
    int SpielId,
    int? HeimMannschaftId, string? HeimPlatzhalterName,
    int? AuswaertsMannschaftId, string? AuswaertsPlatzhalterName);
