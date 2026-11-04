using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Quotenliste neben dem Teilnehmerpool (#423, seit #576 der Pool statt der Lostöpfe, seit #593 eine Spalte statt
/// Kacheln): je Verband bzw. Land, wie viele
/// Mannschaften des Pools die Quote des Turniers füllen, dahinter die freien Plätze und die Gesamtzahl. Gezählt wird in
/// <see cref="Teilnehmerpool.Stand"/>. Ein Klick auf eine Quote meldet <see cref="LandGewaehlt"/> bzw.
/// <see cref="VerbandGewaehlt"/>; welche Quote hervorgehoben ist, sagt der Filter des Pools über
/// <see cref="Hervorheben"/> (#573).</summary>
public partial class QuotenAnzeigeViewModel : ObservableObject
{
    private TeilnehmerpoolStand? _stand;
    private int? _aktiverVerband;
    private int? _aktivesLand;

    [ObservableProperty] private IReadOnlyList<QuoteAnzeige> _eintraege = [];

    public event Action<int>? LandGewaehlt;

    public event Action<int>? VerbandGewaehlt;

    /// <summary><c>null</c> = kein Pool, keine Quoten.</summary>
    public void Zeigen(TeilnehmerpoolStand? stand)
    {
        _stand = stand;
        EintraegeAktualisieren();
    }

    /// <summary>Der Filterstand des Pools: die Quote seines Verbands bzw. Landes wird hervorgehoben.</summary>
    public void Hervorheben(int? verbandId, int? landId)
    {
        if (_aktiverVerband == verbandId && _aktivesLand == landId) return;
        _aktiverVerband = verbandId;
        _aktivesLand = landId;
        EintraegeAktualisieren();
    }

    private void EintraegeAktualisieren() =>
        Eintraege = _stand is null
            ? []
            : [
                .. Zeilen(_stand.Verbandsquoten, _aktiverVerband, id => VerbandGewaehlt?.Invoke(id)),
                .. Zeilen(_stand.Landesquoten, _aktivesLand, id => LandGewaehlt?.Invoke(id)),
                Gesamt(_stand.Gesamt),
            ];

    private static IEnumerable<QuoteAnzeige> Zeilen(IReadOnlyList<QuotenStand> staende, int? aktiv, Action<int> gewaehlt) =>
        staende.Select(s => new QuoteAnzeige(
            s.Name,
            $"{s.Besetzt} / {s.Anzahl}",
            s.Schluessel is int id ? new RelayCommand(() => gewaehlt(id)) : null,
            s.Schluessel is not null && s.Schluessel == aktiv,
            Teilnehmerpool.Mangel(s) ?? (s.Ueberzaehlig > 0 ? $"+ {s.Ueberzaehlig} auf freiem Platz" : null),
            s.Verletzt));

    private static QuoteAnzeige Gesamt(QuotenStand gesamt)
    {
        var mangel = Teilnehmerpool.GesamtMangel(gesamt);
        return new(gesamt.Name, $"{gesamt.Aktuell} / {gesamt.Anzahl}", Hinweis: mangel, Verletzt: mangel is not null);
    }
}
