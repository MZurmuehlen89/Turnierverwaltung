using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class VorlagenRundenUebergaengePage : UserControl
{
    public VorlagenRundenUebergaengePage()
    {
        InitializeComponent();
    }
}

/// <summary>Hervorhebung der fokussierten Runden- bzw. der ausgewählten Eingehend-/Ausgehend-Zeile über den
/// Button-Hintergrund statt über einen eigenen Selektions-Control-State (siehe #284-Bugfix: ToggleButton.
/// IsChecked kollidierte mit dem nativen Klick-Toggle-Verhalten, mehrere Zeilen blieben gleichzeitig
/// markiert). Lokal für diese Page, kein globaler Converter nötig.</summary>
public class BoolZuAuswahlHintergrundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true
            ? Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
