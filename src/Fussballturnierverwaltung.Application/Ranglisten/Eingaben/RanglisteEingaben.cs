namespace Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

public record RanglistenEintragEingabe(int MannschaftId, int Rang, int? Punkte);

/// <summary>Abschnitt der Ligapyramide (#393, #477); <paramref name="Id"/> null = neu angelegt.</summary>
public record RanglistenAbschnittEingabe(int? Id, string Name, int Spielklasse, int BisPlatz);

public record RanglisteErstellenEingabe(
    string Name,
    DateOnly Datum,
    string Quelle,
    IReadOnlyList<RanglistenEintragEingabe> Eintraege,
    int? ProfigrenzeSpielklasse = null,
    IReadOnlyList<RanglistenAbschnittEingabe>? Abschnitte = null,
    int? PokalsiegerId = null);

/// <param name="Abschnitte">null = Abschnitte unverändert lassen (#477).</param>
/// <param name="PokalsiegerId">Wie die Profigrenze immer mitzugeben (#583).</param>
public record RanglisteAktualisierenEingabe(
    string Name,
    DateOnly Datum,
    string Quelle,
    IReadOnlyList<RanglistenEintragEingabe> Eintraege,
    int? ProfigrenzeSpielklasse = null,
    IReadOnlyList<RanglistenAbschnittEingabe>? Abschnitte = null,
    int? PokalsiegerId = null);
