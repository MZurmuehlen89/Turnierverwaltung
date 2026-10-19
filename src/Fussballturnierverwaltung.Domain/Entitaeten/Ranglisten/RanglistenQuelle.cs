namespace Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

public class RanglistenQuelle : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int TabellenIndex { get; set; }
    public string? TabellenSelektor { get; set; }
    public int MannschaftSpalte { get; set; }
    public int? PunkteSpalte { get; set; }
    public bool KopfzeileUeberspringen { get; set; } = true;
}
