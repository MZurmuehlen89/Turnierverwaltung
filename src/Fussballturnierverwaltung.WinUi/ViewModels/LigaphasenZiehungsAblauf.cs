using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>TV-Ziehung der Ligaphase (#364, Vorbild UEFA-Live-Show) auf der Anzeige des
/// <see cref="ZiehungViewModel"/>: Topf für Topf wird Team für Team gezogen (Reihenfolge innerhalb des Topfs nur für
/// die Anzeige zufällig). Pro Team deckt sich seine Zeile in der Gegner-Matrix auf — gleichzeitig die gespiegelten
/// Zellen bei den Gegnern (Heim gegen X ⇒ bei X "Auswärts"), sodass später gezogene Teams bereits vorbelegte Zellen
/// haben. Während eines Topfs ist nur dessen Matrix sichtbar; nach jedem Topf (außer dem letzten) folgt einmal die
/// Übersicht aller Matrizen.</summary>
internal sealed class LigaphasenZiehungsAblauf(ZiehungViewModel anzeige, ObservableCollection<GegnerMatrixTopf> matrizen)
{
    /// <summary>Zeile je Team-Schlüssel, um die gespiegelte Zelle beim Gegner zu finden.</summary>
    private readonly Dictionary<string, GegnerMatrixZeile> _zeileJeTeam = matrizen
        .SelectMany(m => m.Zeilen)
        .Where(z => z.Team.Schluessel is not null)
        .ToDictionary(z => z.Team.Schluessel!);

    /// <summary>Zeilen und Zellen, die für das gerade gezogene Team hervorgehoben sind.</summary>
    private readonly List<ObservableObject> _markiert = [];
    private bool _manuell;

    public async Task AbspielenAsync(bool manuell)
    {
        if (matrizen.Count == 0) return;

        _manuell = manuell;
        MatrizenVerdecken();
        await anzeige.AbspielenAsync(matrizen.Sum(m => m.Zeilen.Count), manuell, ToepfeZiehenAsync);
    }

    /// <summary>Ausgangslage der Live-Ziehung: kein Team gezogen, kein Gegner aufgedeckt.</summary>
    private void MatrizenVerdecken()
    {
        foreach (var zeile in _zeileJeTeam.Values)
        {
            zeile.IstGezogen = false;
            foreach (var zelle in zeile.Gegner)
                if (zelle.Schluessel is not null) zelle.IstAufgedeckt = false;
        }
    }

    private async Task ToepfeZiehenAsync()
    {
        anzeige.AnimationMatrizen = matrizen;
        for (int topfIndex = 0; topfIndex < matrizen.Count; topfIndex++)
        {
            await TopfZiehenAsync(matrizen[topfIndex]);
            // Nach dem letzten Topf folgt direkt die Ergebnisanzeige
            if (topfIndex < matrizen.Count - 1)
                await anzeige.ZwischenstandZeigenAsync($"{matrizen[topfIndex].Name} komplett", "Zwischenstand aller Töpfe");
        }
    }

    // ── Ein Topf ──────────────────────────────────────────────────────────────

    private async Task TopfZiehenAsync(GegnerMatrixTopf matrix)
    {
        anzeige.AnimationFokusMatrix     = matrix;
        anzeige.AnimationZeigeUebersicht = false;
        anzeige.AnimationFokusKopf       = TopfKopf(matrix, matrix.Zeilen.Count);

        int nochImTopf = matrix.Zeilen.Count;
        foreach (var zeile in matrix.Zeilen.OrderBy(_ => Random.Shared.Next()).ToList())
        {
            anzeige.AnimationSchrittIndex++;
            nochImTopf--;
            await TeamZiehenAsync(zeile, matrix, nochImTopf);
        }
    }

    private static string TopfKopf(GegnerMatrixTopf matrix, int nochImTopf) =>
        $"Ziehung {matrix.Name} · noch {nochImTopf} von {matrix.Zeilen.Count} Teams im Topf";

    // ── Ein Team ──────────────────────────────────────────────────────────────

    private async Task TeamZiehenAsync(GegnerMatrixZeile zeile, GegnerMatrixTopf matrix, int nochImTopf)
    {
        TeamAusTopfNehmen(zeile, matrix, nochImTopf);
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);

        await GegnerAufdeckenAsync(zeile, matrix);
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Aufdecken);

        MarkierungAufheben();
        anzeige.AnimationBannerGegner = [];
        anzeige.BannerLeeren();
        // Im manuellen Modus keine eigene "Weiter"-Pause — zwei Klicks je Team (ziehen, aufdecken) genügen.
        if (!_manuell) await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Pause);
    }

    private void TeamAusTopfNehmen(GegnerMatrixZeile zeile, GegnerMatrixTopf matrix, int nochImTopf)
    {
        zeile.IstGezogen           = true;
        zeile.ZeigeGezogenHaken    = true;
        zeile.IstHervorgehoben     = true;
        _markiert.Add(zeile);
        anzeige.AnimationFokusKopf = TopfKopf(matrix, nochImTopf);
        anzeige.BannerMannschaftZeigen(zeile.Team.Name, matrix.Name);
    }

    /// <summary>Die Gegner erscheinen im Banner namentlich nebeneinander — bereits feststehende sofort, neue
    /// nacheinander (auch im manuellen Modus ohne eigenen Klick).</summary>
    private async Task GegnerAufdeckenAsync(GegnerMatrixZeile zeile, GegnerMatrixTopf matrix)
    {
        anzeige.AnimationBannerGegner = BannerGegner(zeile, matrix);
        foreach (var zelle in anzeige.AnimationBannerGegner.Where(g => g.IstNeu).Select(g => g.Zelle))
        {
            GegnerAufdecken(zelle, zeile);
            await anzeige.VerzoegernAsync(ZiehungsTempo.Gegner);
        }
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
    private void GegnerAufdecken(GegnerMatrixZelle zelle, GegnerMatrixZeile gezogen)
    {
        ZelleAufdecken(zelle);

        var spiegel = _zeileJeTeam.GetValueOrDefault(zelle.Schluessel!)?.Gegner
            .FirstOrDefault(g => g.Schluessel == gezogen.Team.Schluessel);
        if (spiegel is not null) ZelleAufdecken(spiegel);
    }

    private void ZelleAufdecken(GegnerMatrixZelle zelle)
    {
        zelle.IstAufgedeckt    = true;
        zelle.IstHervorgehoben = true;
        _markiert.Add(zelle);
    }

    private void MarkierungAufheben()
    {
        foreach (var markiertes in _markiert)
        {
            if (markiertes is GegnerMatrixZelle zelle) zelle.IstHervorgehoben = false;
            else if (markiertes is GegnerMatrixZeile zeile) zeile.IstHervorgehoben = false;
        }
        _markiert.Clear();
    }
}
