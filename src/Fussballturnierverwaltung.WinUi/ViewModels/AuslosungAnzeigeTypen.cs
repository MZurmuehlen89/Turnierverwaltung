using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Darstellungstypen des Auslosung-Reiters: Lostöpfe, Ergebnisanzeige, Regeln, Quoten, ComboBox-Optionen ──

public class LostopfViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Reihenfolge { get; init; }
    public ObservableCollection<TeilnehmerAnzeige> Teilnehmer { get; init; } = [];
}

public record ErgebnisAnzeige(int PaarungsNummer, string HeimName, string AuswaertsName, bool IstFreilos,
    byte[]? HeimWappen = null, byte[]? AuswaertsWappen = null);
public record GruppenTeamAnzeige(string Name, byte[]? Wappen);
public record GruppenErgebnisAnzeige(int GruppenNummer, string GruppenName, IReadOnlyList<GruppenTeamAnzeige> Teams);

public class RegelViewModel : ObservableObject
{
    private readonly Func<bool, Task> _callback;
    private bool _istAktiv;

    public AuslosungsRegelArt Regelart { get; init; }
    public string Anzeige { get; init; }
    public string Beschreibung { get; init; }

    /// <summary>Warum die Regel bei der aktuellen Auslosungsart/Rundenart nicht wirkt — dann ausgegraut (#331).</summary>
    public string? SperrHinweis { get; set; }

    /// <summary>Warum die Regel bei der aktuellen Auslosungsart ohnehin immer gilt — dann ausgegraut und angehakt.</summary>
    public string? ImmerAktivHinweis { get; set; }

    public bool IstAktiv
    {
        get => _istAktiv;
        set { if (SetProperty(ref _istAktiv, value)) _ = _callback(value); }
    }

    /// <summary>Setzt den Zustand ohne Speicher-Callback — für bereits gespeicherte Nebenwirkungen (gegenseitiger
    /// Ausschluss der Heimrecht-Regeln).</summary>
    public void OhneSpeichernSetzen(bool aktiv) => SetProperty(ref _istAktiv, aktiv, nameof(IstAktiv));

    public RegelViewModel(AuslosungsRegelArt art, string anzeige, string beschreibung, bool initial, Func<bool, Task> callback)
    {
        Regelart = art; Anzeige = anzeige; Beschreibung = beschreibung; _istAktiv = initial; _callback = callback;
    }
}

/// <summary>Kachel im Teilnehmerpool — für Verbands- wie Landesquoten, freie Plätze und die Gesamtzahl (#189, #423, #576).
/// Eine Kachel mit <see cref="Umschalten"/> filtert per Klick beide Listen des Pools auf ihr Land bzw. ihren Verband,
/// <see cref="IstAktiv"/> hebt sie hervor, solange der Filter steht (#573); freie Plätze und Gesamtzahl sind nicht
/// anklickbar. <see cref="Verletzt"/> färbt sie rot, <see cref="Zusatz"/> nennt Teams über der Quote.</summary>
public record QuoteAnzeige(string Name, string AnzeigeText, IRelayCommand? Umschalten = null, bool IstAktiv = false,
                           string Zusatz = "", bool Verletzt = false)
{
    public bool IstKlickbar => Umschalten is not null;
    public bool HatZusatz => Zusatz.Length > 0;
}

public record AuslosungsartOption(string Anzeige, Auslosungsart Wert);
public record RanglisteOption(string Anzeige, Rangliste? Wert);
public record TeilnehmerVerbandOption(string Anzeige, int? VerbandId);
public record TeilnehmerLandOption(string Anzeige, int? LandId, int? VerbandId = null);
