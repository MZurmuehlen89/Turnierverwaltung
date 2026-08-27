using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Fussballturnierverwaltung.Wpf.Converters;

[ValueConversion(typeof(byte[]), typeof(ImageSource))]
public class BildDatenZuBildquelleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => BildAusBytes(value as byte[]);

    public static ImageSource? BildAusBytes(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 }) return null;

        using var stream = new MemoryStream(bytes);
        var bild = new BitmapImage();
        bild.BeginInit();
        bild.CacheOption = BitmapCacheOption.OnLoad;
        bild.StreamSource = stream;
        bild.EndInit();
        bild.Freeze();
        return bild;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
