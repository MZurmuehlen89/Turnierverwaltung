using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten.Import;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

public class ImportZusammenfassungTests
{
    [Fact]
    public void Text_OhneZeilen_Leer()
    {
        ImportZusammenfassung.Text([]).Should().BeEmpty();
    }

    [Fact]
    public void Text_ZaehltJeStandInFesterReihenfolge_StaendeOhneZeileEntfallen()
    {
        var status = new[]
        {
            ImportZeilenStatus.Offen, ImportZeilenStatus.Automatisch, ImportZeilenStatus.Automatisch,
            ImportZeilenStatus.Uebersprungen, ImportZeilenStatus.Offen
        };

        ImportZusammenfassung.Text(status).Should().Be("5 Zeilen · 2 automatisch · 2 offen · 1 übersprungen");
    }

    [Fact]
    public void Text_EineZeile_Einzahl()
    {
        ImportZusammenfassung.Text([ImportZeilenStatus.Zugeordnet]).Should().Be("1 Zeile · 1 zugeordnet");
    }

    [Fact]
    public void Rueckfrage_NichtsOffenUndNeueRangliste_KeineRueckfrage()
    {
        ImportZusammenfassung.Rueckfrage(0, null).Should().BeNull();
    }

    [Fact]
    public void Rueckfrage_OffeneZeilen_NenntAnzahl()
    {
        ImportZusammenfassung.Rueckfrage(1, null).Should().Be("1 Zeile ist noch nicht zugeordnet und wird nicht importiert. Fortfahren?");
        ImportZusammenfassung.Rueckfrage(3, null).Should().Be("3 Zeilen sind noch nicht zugeordnet und werden nicht importiert. Fortfahren?");
    }

    [Fact]
    public void Rueckfrage_OffenUndUeberschreiben_BeidesInEinerFrage()
    {
        ImportZusammenfassung.Rueckfrage(2, "UEFA-Koeffizienten").Should().Be(
            "2 Zeilen sind noch nicht zugeordnet und werden nicht importiert. "
            + "Alle bisherigen Einträge von 'UEFA-Koeffizienten' werden ersetzt. Fortfahren?");
    }
}
