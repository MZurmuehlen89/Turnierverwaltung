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

    // Ohne URL ist die Quelle von Hand gepflegt (Issue #393, z. B. eine Ligapyramide): Der Import bietet sie nicht
    // an, ihre Ranglisten werden im Ranglisten-Editor zugeordnet.
    public bool IstImportierbar => !string.IsNullOrWhiteSpace(Url);
}
