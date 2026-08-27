namespace Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

public record VerbandErstellenEingabe(string Name, string Kurzname, byte[]? Logo = null);
public record VerbandAktualisierenEingabe(string Name, string Kurzname, byte[]? Logo = null);
