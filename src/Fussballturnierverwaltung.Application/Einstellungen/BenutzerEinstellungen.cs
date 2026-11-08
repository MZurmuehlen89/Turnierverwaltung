using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Einstellungen;

public class BenutzerEinstellungen
{
    public string ExportPfad { get; set; } = Path.Combine(AppContext.BaseDirectory, "Exports");
    public ThemeModus ThemeModus { get; set; } = ThemeModus.Hell;

    /// <summary>Ablaufprotokoll im Log (#413): Nutzeraktionen, Wechsel und Ladevorgänge mit Dauer. Standardmäßig
    /// an, weil es sporadische Fehler aufklären soll, die sich nicht auf Zuruf wiederholen lassen.</summary>
    public bool AblaufprotokollAktiv { get; set; } = true;
}
