namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Auswahl der Rangliste eines Landes oder Verbands (#572); <see cref="Id"/> null = keine.</summary>
public record RanglistenOption(string Anzeige, int? Id)
{
    public static readonly RanglistenOption Keine = new("— keine —", null);
}
