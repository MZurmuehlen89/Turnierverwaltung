using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record GruppenpositionOption(string Anzeige, Gruppenposition Wert);

/// <summary>Auswahl der Position in der Gruppe (Issue #475) — im Auslosungs- und im Rundeneditor. Wo sie nicht
/// wirkt, bleibt sie sichtbar, aber gesperrt, mit dem Grund aus <see cref="GruppenpositionKompatibilitaet"/>.</summary>
public partial class GruppenpositionAuswahl : ObservableObject
{
    private bool _ladenAktiv;

    public IReadOnlyList<GruppenpositionOption> Optionen { get; } =
        [.. GruppenpositionKompatibilitaet.Varianten.Select(v => new GruppenpositionOption(GruppenpositionKompatibilitaet.AnzeigeName(v), v))];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ausgewaehlt))]
    private Gruppenposition _wert;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstWaehlbar))]
    private string? _sperrGrund;

    public bool IstWaehlbar => SperrGrund is null;

    /// <summary>Für die ComboBox — ein <c>null</c> beim Neuaufbau der ComboBox wird ignoriert (WinUI-Fallstrick,
    /// siehe WinUi/CLAUDE.md).</summary>
    public GruppenpositionOption? Ausgewaehlt
    {
        get => Optionen.FirstOrDefault(o => o.Wert == Wert);
        set { if (value is not null) Wert = value.Wert; }
    }

    /// <summary>Vom Nutzer geändert — nicht beim Laden über <see cref="Zeigen"/>.</summary>
    public event Action? Geaendert;

    public void Zeigen(Gruppenposition wert, Rundenart? rundenart, Auslosungsart? art)
    {
        _ladenAktiv = true;
        try { Wert = wert; }
        finally { _ladenAktiv = false; }
        OnPropertyChanged(nameof(Ausgewaehlt));
        SperreAktualisieren(rundenart, art);
    }

    public void SperreAktualisieren(Rundenart? rundenart, Auslosungsart? art) =>
        SperrGrund = GruppenpositionKompatibilitaet.SperrGrund(rundenart, art);

    partial void OnWertChanged(Gruppenposition value)
    {
        if (!_ladenAktiv) Geaendert?.Invoke();
    }
}
