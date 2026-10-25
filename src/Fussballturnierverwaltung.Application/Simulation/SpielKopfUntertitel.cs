using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Was die Untertitel-Regel nachschlägt (Issue #384): den Kontinentalverband einer Mannschaft (die Aufrufer laden
/// Verbände unterschiedlich) und die Liga eines Vereins.</summary>
public sealed record UntertitelQuellen(Func<Mannschaft, string?> VerbandKurzname, Ligazugehoerigkeit Ligazugehoerigkeit);

/// <summary>Untertitel unter dem Teamnamen im Spielkopf (Issue #341), je nach Kontext der Begegnung. Zustandslos.</summary>
public static class SpielKopfUntertitel
{
    /// <summary>Nutzer-Vorgabe #341/#384: Nationalmannschaft → Kontinentalverband. Verein gegen Verein desselben Landes →
    /// Liga (ohne Eintrag in einer Ligapyramide keiner); desselben Kontinentalverbands → Land; aus einem anderen
    /// Kontinentalverband → „UEFA · Deutschland“.</summary>
    public static string? Fuer(Mannschaft? mannschaft, Mannschaft? gegner, UntertitelQuellen quellen)
    {
        if (mannschaft is null) return null;
        if (mannschaft.Mannschaftsart == Mannschaftsart.Nationalmannschaft)
            return quellen.VerbandKurzname(mannschaft);
        if (gegner is not { Mannschaftsart: Mannschaftsart.Verein })
            return mannschaft.Land?.Name;
        if (gegner.LandId == mannschaft.LandId)
            return quellen.Ligazugehoerigkeit.LigaVon(mannschaft.Id);
        return IstInterkontinental(mannschaft, gegner) ? MitVerband(mannschaft, quellen.VerbandKurzname) : mannschaft.Land?.Name;
    }

    // Ohne geladenes Land ist der Verband unbekannt — dann bleibt es beim bisherigen Untertitel (Land).
    private static bool IstInterkontinental(Mannschaft mannschaft, Mannschaft gegner) =>
        mannschaft.Land is { } land && gegner.Land is { } gegnerLand && land.VerbandId != gegnerLand.VerbandId;

    private static string? MitVerband(Mannschaft mannschaft, Func<Mannschaft, string?> verbandKurzname) =>
        verbandKurzname(mannschaft) is { } verband ? $"{verband} · {mannschaft.Land.Name}" : mannschaft.Land.Name;
}
