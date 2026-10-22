namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Wer bei einer Paarungs-Auslosung nach Töpfen auf wen treffen darf — für die automatische Auslosung „Nach Töpfen“
/// (#495) und die Ziehung von Hand (#482) gleich: Topf 1 gegen Topf 2, Topf 3 gegen Topf 4 …, bis einer der beiden
/// leer ist; die übrigen Teams der größeren Töpfe und eines überzähligen letzten Topfs treffen untereinander — wie im
/// DFB-Pokal ab der 2. Runde, wenn Amateur- und Profitopf ungleich groß sind. Mit nur einem Topf sind alle Teams im
/// Überhang. Regeln wie „Kein gleiches Land“ prüft der Aufrufer zusätzlich.
/// </summary>
internal sealed class TopfPaarStruktur
{
    private readonly IReadOnlyList<int> _topfJeTeam;
    private readonly bool[] _imUeberhang;

    /// <param name="topfGroessen">Teamzahl je Topf.</param>
    /// <param name="topfJeTeam">Topf-Index (ab 0) je Team-Index.</param>
    public TopfPaarStruktur(IReadOnlyList<int> topfGroessen, IReadOnlyList<int> topfJeTeam)
    {
        _topfJeTeam = topfJeTeam;
        TopfPaare   = [.. Enumerable.Range(0, topfGroessen.Count / 2).Select(i => (2 * i, 2 * i + 1))];

        var ueberhangToepfe = new HashSet<int>();
        foreach (var (a, b) in TopfPaare)
        {
            if (topfGroessen[a] > topfGroessen[b]) ueberhangToepfe.Add(a);
            if (topfGroessen[b] > topfGroessen[a]) ueberhangToepfe.Add(b);
        }
        if (topfGroessen.Count % 2 == 1) ueberhangToepfe.Add(topfGroessen.Count - 1);
        _imUeberhang = [.. topfJeTeam.Select(ueberhangToepfe.Contains)];
    }

    public IReadOnlyList<(int A, int B)> TopfPaare { get; }

    /// <summary>Ob das Team in der Schlussphase untereinander gepaart werden darf (und damit auch das Freilos
    /// bekommen kann).</summary>
    public bool ImUeberhang(int team) => _imUeberhang[team];

    /// <summary>Beide Teams aus demselben Topfpaar, aber aus verschiedenen Töpfen.</summary>
    public bool UeberKreuz(int x, int y)
    {
        int tx = _topfJeTeam[x], ty = _topfJeTeam[y];
        return tx != ty && tx / 2 == ty / 2 && tx / 2 < TopfPaare.Count;
    }

    /// <summary>Aus demselben Topfpaar nur über Kreuz, untereinander nur im Überhang.</summary>
    public bool DuerfenSichTreffen(int x, int y) => UeberKreuz(x, y) || _imUeberhang[x] && _imUeberhang[y];

    /// <summary>Bei ungerader Teamzahl ohne Freilos bliebe ein Team ohne Gegner.</summary>
    public static void UngeradeAnzahlPruefen(int anzahl, RegelKontext regeln)
    {
        if (anzahl % 2 == 1 && !regeln.FreilosMoeglich)
            throw new InvalidOperationException(
                $"Die Lostöpfe enthalten {anzahl} Teams — bei ungerader Anzahl bliebe ein Team ohne " +
                "Gegner. Bitte die Regel „Freilos möglich“ aktivieren oder die Teams anpassen.");
    }
}
