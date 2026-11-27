using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>
/// Zeigt das Bereichsbild hinter dem Seitenpanel (#269) und blendet beim Wechsel von einem Bild zum anderen
/// über. Zwei übereinanderliegende <see cref="Image"/>: <c>hinten</c> trägt das gezeigte Bild, <c>vorn</c>
/// blendet das neue ein und wird danach zu <c>hinten</c>. Ohne Bild (Platzhalterseite) bleibt nur der
/// Seitenhintergrund – dann ist auch die Farbüberlagerung weg. Von „kein Bild“ auf ein Bild und zurück wird
/// nicht überblendet, nur Bild zu Bild.
/// </summary>
internal sealed class BereichsbildAnzeige(Image hinten, Image vorn, UIElement ueberlagerung)
{
    private const int UEBERBLENDEN_MS = 200;

    private string? _aktuell;
    private Storyboard? _ueberblendung;

    public void Zeigen(string? name)
    {
        if (name == _aktuell) return;
        var hattBild = _aktuell is not null;
        _aktuell = name;

        UeberblendungAbschliessen();

        if (name is null)
        {
            hinten.Source = null;
            ueberlagerung.Visibility = Visibility.Collapsed;
        }
        else if (!hattBild)
        {
            hinten.Source = Laden(name);
            ueberlagerung.Visibility = Visibility.Visible;
        }
        else
        {
            Ueberblenden(Laden(name));
        }
    }

    private static BitmapImage Laden(string name)
        => new(new Uri($"ms-appx:///Assets/Bereiche/{name}.jpg"));

    private void Ueberblenden(BitmapImage neu)
    {
        vorn.Source = neu;
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(UEBERBLENDEN_MS)),
        };
        Storyboard.SetTarget(animation, vorn);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        // Nur die eigene Überblendung abschließen: ein verspätetes Ereignis eines alten Storyboards darf die neue nicht beenden
        storyboard.Completed += (_, _) =>
        {
            if (_ueberblendung == storyboard) UeberblendungAbschliessen();
        };
        _ueberblendung = storyboard;
        storyboard.Begin();
    }

    // Das einblendende Bild wird zum gezeigten, die vordere Ebene wird wieder leer. Auch beim Abbruch durch
    // einen zweiten Wechsel: das bisher einblendende Bild gilt dann als gezeigt.
    private void UeberblendungAbschliessen()
    {
        if (_ueberblendung is null) return;
        var sb = _ueberblendung;
        _ueberblendung = null;
        sb.Stop();
        hinten.Source = vorn.Source;
        vorn.Source = null;
    }
}
