using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>ViewModel-Zeile für die Rundenliste — Spiegel einer <see cref="TurnierRunde"/>, reiner UI-Zustand
/// bis zum nächsten Speichern. Bewusst ein reiner Datenträger ohne eigene Änderungs-/Berechnungslogik — die
/// Formulare binden NICHT direkt auf diese Instanz, sondern nur die Rundenliste und die (nur lesenden)
/// Übergangsregel-Zeilen referenzieren sie per Objektidentität.</summary>
public partial class RundeZeile : ObservableObject
{
    public static readonly RegelwerkOption KeinRegelwerk = new("— Turnierstandard —", null);
    public static readonly VorgegebeneAuslosungsartOption KeineAuslosungsartVorgabe = new("— frei wählbar —", null);
    public static readonly RanglistenQuelleOption KeineRanglistenQuelle = new("— keine —", null);

    public int? Id { get; set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private Rundenart _rundenart = Rundenart.Liga;
    [ObservableProperty] private int _eingangsMannschaftsanzahl = 8;
    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private int? _gruppenAnzahl;
    [ObservableProperty] private int? _anzahlSpieltage;
    [ObservableProperty] private bool _mitRueckspiel;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private bool _neutralerSpielort;
    [ObservableProperty] private int _ueberraschungsfaktor;
    [ObservableProperty] private RegelwerkOption _regelwerk = KeinRegelwerk;
    [ObservableProperty] private bool _paarungFestVorgegeben;
    [ObservableProperty] private VorgegebeneAuslosungsartOption _vorgegebeneAuslosungsart = KeineAuslosungsartVorgabe;
    [ObservableProperty] private RanglistenQuelleOption _standardRanglistenQuelle = KeineRanglistenQuelle;
    [ObservableProperty] private bool _titelverteidigerInTopf1;

    // Issue #372: im Editor nicht bearbeitbar (stammt aus dem Seed), wird beim Speichern unverändert durchgereicht.
    public string? KombinationstabelleJson { get; set; }

    // Issue #356: reiner Datenträger — angezeigt und geändert wird über RundenViewModel.RegelVorgabeOptionen.
    public IReadOnlyList<AuslosungsRegelArt> VorgegebeneAuslosungsregeln { get; set; } = [];

    /// <summary>Reine Anzeige in der Rundenliste (#187, seit #348 im Runden-Reiter): gesetzt, wenn die ausgehenden
    /// Übergangsregeln nicht zur Ausgangsanzahl passen — berechnet von <c>RundenViewModel</c>, nie gespeichert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatUnstimmigkeit))]
    private string? _unstimmigkeitText;
    public bool HatUnstimmigkeit => UnstimmigkeitText is not null;
}
