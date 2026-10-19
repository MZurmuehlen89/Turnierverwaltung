using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Feste Gruppenköpfe einer Gruppenphase im Rundeneditor (Issue #487): Gruppen der Gastgeber („A, B, D“) und
/// des Titelverteidigers („A“). Eine ungültige Eingabe zeigt sofort den Fehler und wird nicht übernommen.</summary>
public partial class GruppenkoepfeVorgabe : ObservableObject
{
    private bool _ladenAktiv;
    private int? _gruppenAnzahl;

    [ObservableProperty] private string _gastgeberText = "";
    [ObservableProperty] private string _titelverteidigerText = "";
    [ObservableProperty] private string? _fehler;

    public IReadOnlyList<int> GastgeberGruppen { get; private set; } = [];
    public int? TitelverteidigerGruppe { get; private set; }

    /// <summary>Der Nutzer hat eine gültige Änderung eingegeben — nicht beim Laden über <see cref="Zeigen"/>.</summary>
    public event Action? Geaendert;

    public void Zeigen(IReadOnlyList<int> gastgeberGruppen, int? titelverteidigerGruppe, int? gruppenAnzahl)
    {
        _ladenAktiv = true;
        try
        {
            _gruppenAnzahl       = gruppenAnzahl;
            GastgeberText        = Gruppenkoepfe.AlsText(gastgeberGruppen);
            TitelverteidigerText = titelverteidigerGruppe is { } tv ? Gruppenkoepfe.Buchstabe(tv) : "";
            Pruefen();
        }
        finally { _ladenAktiv = false; }
    }

    /// <summary>Eine geänderte Gruppenanzahl kann eine Vorgabe ungültig machen — nur anzeigen, gespeichert prüft
    /// der TurnierValidator.</summary>
    public void GruppenAnzahlSetzen(int? gruppenAnzahl)
    {
        _gruppenAnzahl = gruppenAnzahl;
        Pruefen();
    }

    partial void OnGastgeberTextChanged(string value)        => Uebernehmen();
    partial void OnTitelverteidigerTextChanged(string value) => Uebernehmen();

    private void Uebernehmen()
    {
        if (_ladenAktiv || !Pruefen()) return;
        Geaendert?.Invoke();
    }

    private bool Pruefen()
    {
        var gastgeber = Gruppenkoepfe.AusText(GastgeberText, _gruppenAnzahl, out var fehler);
        var tv        = gastgeber is null ? null : Gruppenkoepfe.AusText(TitelverteidigerText, _gruppenAnzahl, out fehler);
        if (tv is { Count: > 1 }) fehler = "Der Titelverteidiger führt nur eine Gruppe an.";
        int? tvGruppe = tv is [var einzige] ? einzige : null;
        if (gastgeber is not null) fehler ??= Gruppenkoepfe.Widerspruch(gastgeber, tvGruppe, _gruppenAnzahl);

        Fehler = fehler;
        if (fehler is not null) return false;
        GastgeberGruppen       = gastgeber!;
        TitelverteidigerGruppe = tvGruppe;
        return true;
    }
}
