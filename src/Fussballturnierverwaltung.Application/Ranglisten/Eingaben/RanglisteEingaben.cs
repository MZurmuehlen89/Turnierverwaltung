namespace Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

public record RanglistenEintragEingabe(int MannschaftId, int Rang, int? Punkte);

/// <summary>Abschnitt der Ligapyramide (#393, #477); <paramref name="Id"/> null = neu angelegt.</summary>
public record RanglistenAbschnittEingabe(int? Id, string Name, int Spielklasse, int BisPlatz);

public record RanglisteErstellenEingabe(
    string Name,
    DateOnly Datum,
    string Quelle,
    IReadOnlyList<RanglistenEintragEingabe> Eintraege,
    int? RanglistenQuelleId = null,
    int? ProfigrenzeSpielklasse = null,
    IReadOnlyList<RanglistenAbschnittEingabe>? Abschnitte = null);

/// <param name="Abschnitte">null = Abschnitte unverändert lassen (Import, #477).</param>
public record RanglisteAktualisierenEingabe(
    string Name,
    DateOnly Datum,
    string Quelle,
    IReadOnlyList<RanglistenEintragEingabe> Eintraege,
    int? RanglistenQuelleId = null,
    int? ProfigrenzeSpielklasse = null,
    IReadOnlyList<RanglistenAbschnittEingabe>? Abschnitte = null);
