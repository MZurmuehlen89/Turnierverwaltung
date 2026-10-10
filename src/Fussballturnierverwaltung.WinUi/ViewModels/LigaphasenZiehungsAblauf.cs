using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>TV-Ziehung der Ligaphase: deckt die Gegner-Matrizen Topf für Topf auf der Anzeige des
/// <see cref="ZiehungViewModel"/> auf (#364).</summary>
internal sealed class LigaphasenZiehungsAblauf(ZiehungViewModel anzeige)
{
    /// <summary>TV-Modus der Ligaphase (#364, Vorbild UEFA-Live-Show): Topf für Topf wird Team für Team gezogen
    /// (Reihenfolge innerhalb des Topfs nur für die Anzeige zufällig). Pro Team deckt sich seine Zeile in der
    /// Gegner-Matrix auf — gleichzeitig die gespiegelten Zellen bei den Gegnern (Heim gegen X ⇒ bei X "Auswärts"),
    /// sodass später gezogene Teams bereits vorbelegte Zellen haben. Während eines Topfs ist nur dessen Matrix
    /// sichtbar; nach jedem Topf (außer dem letzten) folgt einmal die Übersicht aller Matrizen.</summary>
    public async Task AbspielenAsync(ObservableCollection<GegnerMatrixTopf> matrizen, bool manuell)
    {
        if (matrizen.Count == 0) return;

        // Zeile je Team-Schlüssel, um die gespiegelte Zelle beim Gegner zu finden
        var zeileJeTeam = matrizen.SelectMany(m => m.Zeilen)
            .Where(z => z.Team.Schluessel is not null)
            .ToDictionary(z => z.Team.Schluessel!);
        MatrizenVerdecken(zeileJeTeam.Values);

        anzeige.ZiehungStarten(matrizen.Sum(m => m.Zeilen.Count), manuell);
        anzeige.AnimationMatrizen = matrizen;

        var markiert = new List<ObservableObject>();
        try
        {
            for (int topfIndex = 0; topfIndex < matrizen.Count; topfIndex++)
            {
                var matrix = matrizen[topfIndex];
                anzeige.AnimationFokusMatrix     = matrix;
                anzeige.AnimationZeigeUebersicht = false;
                int nochImTopf = matrix.Zeilen.Count;
                anzeige.AnimationFokusKopf = TopfKopf(matrix, nochImTopf);

                foreach (var zeile in matrix.Zeilen.OrderBy(_ => Random.Shared.Next()).ToList())
                {
                    if (anzeige.Ueberspringen) break;
                    anzeige.AnimationSchrittIndex++;

                    // 1) Team ziehen
                    nochImTopf--;
                    TeamAusMatrixZiehen(zeile, matrix, nochImTopf, markiert);
                    await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
                    if (anzeige.Ueberspringen) break;

                    // 2) Gegner aufdecken — im Banner namentlich nebeneinander, bereits feststehende sofort, neue
                    //    nacheinander (auch im manuellen Modus ohne eigenen Klick); dabei jeweils eigene Zeile +
                    //    Spiegelzelle beim Gegner aufdecken
                    anzeige.AnimationBannerGegner = BannerGegner(zeile, matrix);
                    foreach (var zelle in anzeige.AnimationBannerGegner.Where(g => g.IstNeu).Select(g => g.Zelle))
                    {
                        GegnerAufdecken(zelle, zeile, zeileJeTeam, markiert);
                        await Task.WhenAny(Task.Delay(ZiehungsTempo.Gegner), anzeige.WeiterSignal);
                        if (anzeige.Ueberspringen) break;
                    }
                    if (anzeige.Ueberspringen) break;

                    await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Aufdeck);
                    if (anzeige.Ueberspringen) break;

                    MarkierungAufheben(markiert);
                    anzeige.AnimationBannerGegner = [];
                    anzeige.BannerLeeren();
                    // Im manuellen Modus keine eigene "Weiter"-Pause — zwei Klicks je Team (ziehen, aufdecken) genügen.
                    if (!anzeige.Manuell)
                    {
                        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Pausen);
                        if (anzeige.Ueberspringen) break;
                    }
                }
                if (anzeige.Ueberspringen) break;

                // 3) Zwischen-Übersicht nach jedem Topf (nach dem letzten folgt direkt die Ergebnisanzeige)
                if (topfIndex < matrizen.Count - 1)
                    await anzeige.ZwischenstandZeigenAsync($"{matrix.Name} komplett", "Zwischenstand aller Töpfe");
            }
        }
        finally { anzeige.AnimationsZustandZuruecksetzen(); }
    }

    /// <summary>Ausgangslage der Live-Ziehung: kein Team gezogen, kein Gegner aufgedeckt.</summary>
    private static void MatrizenVerdecken(IEnumerable<GegnerMatrixZeile> zeilen)
    {
        foreach (var zeile in zeilen)
        {
            zeile.IstGezogen = false;
            foreach (var zelle in zeile.Gegner)
                if (zelle.Schluessel is not null) zelle.IstAufgedeckt = false;
        }
    }

    private static string TopfKopf(GegnerMatrixTopf matrix, int nochImTopf) =>
        $"Ziehung {matrix.Name} · noch {nochImTopf} von {matrix.Zeilen.Count} Teams im Topf";

    private void TeamAusMatrixZiehen(GegnerMatrixZeile zeile, GegnerMatrixTopf matrix, int nochImTopf, List<ObservableObject> markiert)
    {
        zeile.IstGezogen        = true;
        zeile.ZeigeGezogenHaken = true;
        anzeige.AnimationFokusKopf      = TopfKopf(matrix, nochImTopf);
        zeile.IstHervorgehoben  = true;
        markiert.Add(zeile);

        anzeige.AnimationBannerMannschaft = zeile.Team.Name;
        anzeige.AnimationBannerTopf       = matrix.Name;
        anzeige.AnimationBannerZiel       = null;
        anzeige.AnimationBannerSlot       = null;
    }

    /// <summary>Die Gegner der Zeile fürs Banner — neu ist ein Gegner, dessen Zelle noch verdeckt ist.</summary>
    private static ObservableCollection<AnimationsBannerGegner> BannerGegner(GegnerMatrixZeile zeile, GegnerMatrixTopf matrix) =>
        new(zeile.Gegner
            .Select((zelle, index) => (zelle, index))
            .Where(x => x.zelle.Schluessel is not null)
            .Select(x => new AnimationsBannerGegner(
                $"{matrix.Spalten[x.index].TopfName} · {matrix.Spalten[x.index].Seite}", x.zelle, !x.zelle.IstAufgedeckt)));

    /// <summary>Deckt <paramref name="zelle"/> in der Zeile des gezogenen Teams auf und zugleich die gespiegelte
    /// Zelle in der Zeile des Gegners.</summary>
    private static void GegnerAufdecken(
        GegnerMatrixZelle zelle, GegnerMatrixZeile gezogen,
        Dictionary<string, GegnerMatrixZeile> zeileJeTeam, List<ObservableObject> markiert)
    {
        zelle.IstAufgedeckt    = true;
        zelle.IstHervorgehoben = true;
        markiert.Add(zelle);

        var spiegel = zeileJeTeam.GetValueOrDefault(zelle.Schluessel!)?.Gegner
            .FirstOrDefault(g => g.Schluessel == gezogen.Team.Schluessel);
        if (spiegel is null) return;

        spiegel.IstAufgedeckt    = true;
        spiegel.IstHervorgehoben = true;
        markiert.Add(spiegel);
    }

    private static void MarkierungAufheben(List<ObservableObject> markiert)
    {
        foreach (var o in markiert)
        {
            if (o is GegnerMatrixZelle zelle) zelle.IstHervorgehoben = false;
            else if (o is GegnerMatrixZeile zeile) zeile.IstHervorgehoben = false;
        }
        markiert.Clear();
    }
}
