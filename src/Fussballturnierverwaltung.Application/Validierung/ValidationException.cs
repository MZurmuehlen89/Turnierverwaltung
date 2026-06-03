using Fussballturnierverwaltung.Application.Abstraktionen;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Wird geworfen wenn ein ValidationErgebnis Fehler enthält.</summary>
public class ValidationException(ValidationErgebnis ergebnis)
    : Exception(ergebnis.FehlerZusammenfassung)
{
    public ValidationErgebnis Ergebnis { get; } = ergebnis;

    public static void WerfenWennUngueltig(ValidationErgebnis ergebnis)
    {
        if (ergebnis.IstUngueltig)
            throw new ValidationException(ergebnis);
    }
}
