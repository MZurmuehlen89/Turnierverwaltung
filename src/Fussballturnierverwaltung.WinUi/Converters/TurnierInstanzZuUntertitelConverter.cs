using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Zweite Zeile eines Eintrags in der Turnierliste (Issue #367): bei Vorlagen die
/// <see cref="TurnierInstanz.Variante"/> (unterscheidet gleichnamige Vorlagen wie "FIFA WM"), sonst
/// "Status · Saison". Nur lokal in TurnierlistePage registriert.</summary>
public class TurnierInstanzZuUntertitelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is TurnierInstanz instanz ? Untertitel(instanz) : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    public static string Untertitel(TurnierInstanz instanz)
    {
        if (instanz.Status == TurnierStatus.Vorlage)
            return string.IsNullOrWhiteSpace(instanz.Variante) ? "Vorlage" : instanz.Variante;
        return $"{TurnierStatusZuTextConverter.StatusAnzeigeName(instanz.Status)} · {instanz.SaisonOderJahr}";
    }
}
