using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Fussballturnierverwaltung.WinUi.Converters;

public class BildDatenZuBildquelleConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] { Length: > 0 } daten) return null;

        // Stream bewusst nicht disposen: BitmapImage.SetSource dekodiert asynchron im
        // Hintergrund und liest dabei weiterhin aus dem Stream.
        var stream = new MemoryStream(daten).AsRandomAccessStream();
        var bitmap = new BitmapImage();
        bitmap.SetSource(stream);
        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
