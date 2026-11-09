namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Auswahl der Rangliste eines Landes oder Verbands (#572); <see cref="Id"/> null = keine.</summary>
public record RanglistenOption(string Anzeige, int? Id)
{
    public static readonly RanglistenOption Keine = new("— keine —", null);

    /// <summary>Die Option zur Rangliste aus <paramref name="optionen"/>; unbekannt oder null = <see cref="Keine"/>.</summary>
    public static RanglistenOption Fuer(IReadOnlyList<RanglistenOption> optionen, int? ranglisteId) =>
        optionen.FirstOrDefault(o => o.Id == ranglisteId) ?? Keine;
}
