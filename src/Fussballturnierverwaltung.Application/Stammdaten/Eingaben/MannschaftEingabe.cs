using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

public record MannschaftErstellenEingabe(
    string Name,
    string Kurzname,
    Mannschaftsart Mannschaftsart,
    int LandId,
    int OffensivRating,
    int MittelfeldRating,
    int DefensivRating,
    int Heimstaerke,
    int Auswaertsstaerke,
    byte[]? Wappen = null,
    Grundausrichtung Grundausrichtung = Grundausrichtung.Ausgeglichen,
    Flexibilitaet Flexibilitaet = Flexibilitaet.Normal,
    Kondition Kondition = Kondition.Normal);

/// <summary>Grundausrichtung und Flexibilität (#667): <c>null</c> lässt den gespeicherten Wert unverändert – etwa beim
/// CSV-Import einer Datei ohne diese Spalten. Gleiches gilt für die Kondition (#677).</summary>
public record MannschaftAktualisierenEingabe(
    string Name,
    string Kurzname,
    int LandId,
    int OffensivRating,
    int MittelfeldRating,
    int DefensivRating,
    int Heimstaerke,
    int Auswaertsstaerke,
    byte[]? Wappen = null,
    Grundausrichtung? Grundausrichtung = null,
    Flexibilitaet? Flexibilitaet = null,
    Kondition? Kondition = null);
