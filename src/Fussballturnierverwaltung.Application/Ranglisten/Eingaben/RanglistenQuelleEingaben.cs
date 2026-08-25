namespace Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

public record RanglistenQuelleErstellenEingabe(
    string Name,
    string Url,
    int TabellenIndex,
    string? TabellenSelektor,
    int MannschaftSpalte,
    int? PunkteSpalte,
    bool KopfzeileUeberspringen);

public record RanglistenQuelleAktualisierenEingabe(
    string Name,
    string Url,
    int TabellenIndex,
    string? TabellenSelektor,
    int MannschaftSpalte,
    int? PunkteSpalte,
    bool KopfzeileUeberspringen);
