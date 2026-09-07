using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Fussballturnierverwaltung.WinUi.Converters;

public class BildDatenZuBildquelleConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
        => BildAusBytes(value as byte[]);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    /// <summary>Wiederverwendet von <see cref="MannschaftZuWappenConverter"/>, das erst noch aus einer
    /// <c>Mannschaft</c> die passenden Bilddaten (Wappen oder, bei Nationalmannschaften, Landesflagge)
    /// ermitteln muss, bevor dieselbe Bytes-zu-Bitmap-Umwandlung greift.</summary>
    public static object? BildAusBytes(byte[]? daten)
    {
        if (daten is not { Length: > 0 }) return null;

        // Stream bewusst nicht disposen: BitmapImage.SetSource dekodiert asynchron im
        // Hintergrund und liest dabei weiterhin aus dem Stream.
        var stream = new MemoryStream(daten).AsRandomAccessStream();
        var bitmap = new BitmapImage();
        bitmap.SetSource(stream);
        return bitmap;
    }
}
