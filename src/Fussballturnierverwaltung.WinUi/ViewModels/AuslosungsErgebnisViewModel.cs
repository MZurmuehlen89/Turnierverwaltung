using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ergebnis einer abgeschlossenen Auslosung: Gruppen, Gegner-Matrix der Ligaphase oder Paarungen (#496).
/// Kind-ViewModel von <see cref="AuslosungViewModel"/>, ohne Datenbankzugriff.</summary>
public partial class AuslosungsErgebnisViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istAbgeschlossen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istGruppenergebnis;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istGegnerMatrixergebnis;

    public bool IstPaarungsergebnis => IstAbgeschlossen && !IstGruppenergebnis && !IstGegnerMatrixergebnis;

    /// <summary>Gegner-Matrix der Ligaphase samt Team-Fokus (#362).</summary>
    public GegnerMatrixViewModel GegnerMatrix { get; } = new();

    [ObservableProperty] private ObservableCollection<ErgebnisAnzeige> _ergebnisseAnzeige = [];
    [ObservableProperty] private ObservableCollection<GruppenErgebnisAnzeige> _gruppenErgebnisse = [];

    public void Zeigen(Auslosung detail, Qualifikanten qualifikanten)
    {
        bool istGruppe = IstGruppenergebnisVon(detail);
        bool istMatrix = !istGruppe && IstGegnerMatrixergebnisVon(detail);

        IstAbgeschlossen = detail.Status == AuslosungsStatus.Abgeschlossen;
        IstGruppenergebnis = istGruppe;
        IstGegnerMatrixergebnis = istMatrix;
        GegnerMatrix.Zeigen(istMatrix ? GegnerMatrixBuilder.Aufbauen(detail, qualifikanten) : []);
        GruppenErgebnisse = istGruppe ? GruppenErgebnisseAus(detail, qualifikanten) : [];
        ErgebnisseAnzeige = istGruppe ? [] : PaarungsErgebnisseAus(detail, qualifikanten);
    }

    /// <summary>Eine Gruppenauslosung speichert je Mannschaft eine Zeile ohne Gegner.</summary>
    private static bool IstGruppenergebnisVon(Auslosung detail) =>
        detail.Ergebnisse.Any() &&
        detail.Ergebnisse.All(e => !e.IstFreilos &&
            e.AuswaertsMannschaftId == null &&
            e.AuswaertsPlatzhalterName == null);

    /// <summary>Ligaphase: Ergebnis als Gegner-Matrix je Topf statt flacher Paarungsliste (#362).</summary>
    private static bool IstGegnerMatrixergebnisVon(Auslosung detail) =>
        detail.Ergebnisse.Any() &&
        detail.Auslosungsart == Auslosungsart.TopfZuordnung && detail.Lostoepfe.Count >= 2;

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
