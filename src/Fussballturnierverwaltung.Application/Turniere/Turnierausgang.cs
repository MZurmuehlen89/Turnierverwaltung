using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Woran sich der Ausgang eines Turniers entscheidet (#527).</summary>
public enum TurnierausgangArt
{
    /// <summary>Die Schlussrunde kürt keinen Sieger (mehrere Gruppen, K.-o.-Runde mit mehreren Paarungen).</summary>
    Keiner,
    /// <summary>Ein Finale entscheidet, ggf. mit Spiel um Platz 3 bzw. Halbfinale für Platz 3.</summary>
    Finale,
    /// <summary>Der Tabellenerste der Schlussrunde (Liga, Liga mit Töpfen, Schweizer System) ist Meister.</summary>
    Tabelle
}

public enum PlatzierungsGrund
{
    FinaleGewonnen,
    FinaleVerloren,
    SpielUmPlatzDreiGewonnen,
    SpielUmPlatzDreiVerloren,
    HalbfinaleVerloren,
    Meister
}

/// <summary>Ein feststehender Platz. <paramref name="Entscheidung"/> ist die Entscheidungsart des Spiels, das ihn
/// entschieden hat (beim Meister <c>null</c>).</summary>
public sealed record Platzierung(int Platz, Mannschaft Mannschaft, PlatzierungsGrund Grund, EntscheidungArt? Entscheidung = null);

/// <summary>Ausgang eines Turniers (#527): die feststehenden Plätze und, solange der Sieger offen ist, was über ihn
/// schon bekannt ist — die beiden Finalisten bzw. der Tabellenführer.</summary>
public sealed record Turnierausgang(
    TurnierausgangArt Art,
    IReadOnlyList<Platzierung> Plaetze,
    IReadOnlyList<Mannschaft> Finalisten,
    Mannschaft? Tabellenfuehrer = null,
    int OffeneSpieltage = 0,
    int SpieltageGesamt = 0)
{
    public static readonly Turnierausgang Keiner = new(TurnierausgangArt.Keiner, [], []);

    public Mannschaft? Sieger => Plaetze.FirstOrDefault(p => p.Platz == 1)?.Mannschaft;
}
