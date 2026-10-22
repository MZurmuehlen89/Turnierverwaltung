using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ergebnis einer abgeschlossenen Auslosung: Gruppen, Gegner-Matrix der Ligaphase oder Paarungen (#496).
/// Kind-ViewModel von <see cref="AuslosungViewModel"/>, ohne Datenbankzugriff.</summary>
public class AuslosungsErgebnisViewModel : ObservableObject
{
    private AuslosungsErgebnisart _art;
    private ObservableCollection<ErgebnisAnzeige> _ergebnisseAnzeige = [];
    private ObservableCollection<GruppenErgebnisAnzeige> _gruppenErgebnisse = [];

    // Die Sichtbarkeit des ganzen Ergebnisbereichs (abgeschlossen, keine laufende Ziehung) regelt
    // AuslosungViewModel.ZeigeErgebnis — hier geht es nur um die Darstellungsart.
    public bool IstGruppenergebnis      => _art == AuslosungsErgebnisart.Gruppen;
    public bool IstGegnerMatrixergebnis => _art == AuslosungsErgebnisart.GegnerMatrix;
    public bool IstPaarungsergebnis     => _art == AuslosungsErgebnisart.Paarungen;

    /// <summary>Gegner-Matrix der Ligaphase samt Team-Fokus (#362).</summary>
    public GegnerMatrixViewModel GegnerMatrix { get; } = new();

    public ObservableCollection<ErgebnisAnzeige> ErgebnisseAnzeige
    {
        get => _ergebnisseAnzeige;
        private set => SetProperty(ref _ergebnisseAnzeige, value);
    }

    public ObservableCollection<GruppenErgebnisAnzeige> GruppenErgebnisse
    {
        get => _gruppenErgebnisse;
        private set => SetProperty(ref _gruppenErgebnisse, value);
    }

    public void Zeigen(Auslosung detail, Qualifikanten qualifikanten)
    {
        ArtSetzen(AuslosungsErgebnisartErmittlung.Von(detail));
        GegnerMatrix.Zeigen(IstGegnerMatrixergebnis ? GegnerMatrixBuilder.Aufbauen(detail, qualifikanten) : []);
        GruppenErgebnisse = IstGruppenergebnis ? GruppenErgebnisseAus(detail, qualifikanten) : [];
        ErgebnisseAnzeige = IstGruppenergebnis ? [] : PaarungsErgebnisseAus(detail, qualifikanten);
    }

    /// <summary>Die Gegner-Matrix baut sich erst mit dem Einblenden des Ergebnisses auf (#428).</summary>
    public void SichtbarSetzen(bool sichtbar) => GegnerMatrix.SichtbarSetzen(sichtbar);

    private void ArtSetzen(AuslosungsErgebnisart art)
    {
        if (_art == art) return;
        _art = art;
        OnPropertyChanged(nameof(IstGruppenergebnis));
        OnPropertyChanged(nameof(IstGegnerMatrixergebnis));
        OnPropertyChanged(nameof(IstPaarungsergebnis));
    }

    // Team-Reihenfolge innerhalb der Gruppe bewusst NICHT alphabetisch sortiert: sie muss mit der tatsächlich
    // gespeicherten Reihenfolge übereinstimmen, aus der SpielplanService.GenerierenAusAuslosungAsync den Spielplan
    // generiert (dort ohne eigene Sortierung, ebenfalls direkt aus der PaarungsNummer-Gruppierung) — siehe
    // #303-Diskussion.
    private static ObservableCollection<GruppenErgebnisAnzeige> GruppenErgebnisseAus(Auslosung detail, Qualifikanten q) =>
        new(detail.Ergebnisse
            .GroupBy(e => e.PaarungsNummer)
            .OrderBy(g => g.Key)
            .Select(g => new GruppenErgebnisAnzeige(
                g.Key,
                $"Gruppe {GruppenHerkunft.Buchstabe(g.Key)}",
                g.Select(e => GruppenTeam(SpielendeMannschaft(e.HeimMannschaft, e.HeimPlatzhalterName, q), e.HeimPlatzhalterName))
                 .ToList())));

    private static GruppenTeamAnzeige GruppenTeam(Mannschaft? mannschaft, string? platzhalterName) =>
        new(mannschaft?.Name ?? platzhalterName ?? "?", MannschaftsIcon.Fuer(mannschaft));

    private static ObservableCollection<ErgebnisAnzeige> PaarungsErgebnisseAus(Auslosung detail, Qualifikanten q) =>
        new(detail.Ergebnisse.Select(e =>
        {
            var heim      = SpielendeMannschaft(e.HeimMannschaft, e.HeimPlatzhalterName, q);
            var auswaerts = SpielendeMannschaft(e.AuswaertsMannschaft, e.AuswaertsPlatzhalterName, q);
            return new ErgebnisAnzeige(
                e.PaarungsNummer,
                heim?.Name ?? e.HeimPlatzhalterName ?? "?",
                auswaerts?.Name ?? e.AuswaertsPlatzhalterName ?? "?",
                e.IstFreilos,
                MannschaftsIcon.Fuer(heim),
                MannschaftsIcon.Fuer(auswaerts));
        }));

    /// <summary>Die eigene Mannschaft eines Ergebnisplatzes oder die hinter seinem Platzhalter (#483).</summary>
    private static Mannschaft? SpielendeMannschaft(Mannschaft? eigene, string? platzhalterName, Qualifikanten q) =>
        eigene ?? q.Fuer(platzhalterName);
}
