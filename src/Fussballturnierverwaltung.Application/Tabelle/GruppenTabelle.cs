namespace Fussballturnierverwaltung.Application.Tabelle;

public record GruppenTabelle(int GruppenNummer, IReadOnlyList<TabellenEintrag> Tabelle);
