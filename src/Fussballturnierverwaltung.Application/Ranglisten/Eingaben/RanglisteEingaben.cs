namespace Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

public record RanglistenEintragEingabe(int MannschaftId, int Rang, int? Punkte);

public record RanglisteErstellenEingabe(
    string Name,
    DateOnly Datum,
    string Quelle,
    IReadOnlyList<RanglistenEintragEingabe> Eintraege,
    int? RanglistenQuelleId = null,
    int? ProfigrenzeSpielklasse = null);

public record RanglisteAktualisierenEingabe(
    string Name,
    DateOnly Datum,
    string Quelle,
    IReadOnlyList<RanglistenEintragEingabe> Eintraege,
    int? RanglistenQuelleId = null,
    int? ProfigrenzeSpielklasse = null);
