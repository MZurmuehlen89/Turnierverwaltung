using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Spielbedeutung, mit der eine Runde ihrer Rundenart nach vorbelegt wird (#668): im Finale steht alles auf dem
/// Spiel, im Spiel um Platz 3 wenig. Rundenformular, Vorlagen und die Migration des Bestands nutzen dieselbe Regel.</summary>
public static class SpielbedeutungVorbelegung
{
    public static Spielbedeutung Fuer(Rundenart rundenart) => rundenart switch
    {
        Rundenart.Finale           => Spielbedeutung.Hoch,
        Rundenart.SpielUmPlatzDrei => Spielbedeutung.Gering,
        _                          => Spielbedeutung.Normal
    };
}
