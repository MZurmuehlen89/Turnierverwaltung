namespace Fussballturnierverwaltung.Application.Abstraktionen;

public record ValidationErgebnis(bool IstGueltig, IReadOnlyList<string> Fehler)
{
    public bool IstUngueltig => !IstGueltig;

    public static ValidationErgebnis Ok() => new(true, []);
    public static ValidationErgebnis MitFehlern(params string[] fehler) => new(false, fehler);

    public string FehlerZusammenfassung => string.Join(Environment.NewLine, Fehler);
}
