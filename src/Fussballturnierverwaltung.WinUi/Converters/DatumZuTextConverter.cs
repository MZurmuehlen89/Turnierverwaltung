using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Datum einer Rangliste (<c>DateOnly</c>) im deutschen Format TT.MM.JJJJ, unabhängig von der Systemsprache
/// (#477).</summary>
public class DatumZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is DateOnly datum ? datum.ToString("dd.MM.yyyy") : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
