using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Trennung Vorbelegung / Durchführung nur über „Paarung fest vorgegeben“ (#482).</summary>
public class VorbelegungsZustaendigkeitTests
{
    [Theory]
    [InlineData(Auslosungsart.Manuell, true,  true)]
    [InlineData(Auslosungsart.Manuell, false, false)] // Ziehung von Hand bzw. Altfall „Haken entfernt“ (#320)
    [InlineData(Auslosungsart.Zufall,  true,  false)] // Altfall „Haken nachträglich gesetzt“: bleibt zurücksetzbar
    [InlineData(Auslosungsart.Zufall,  false, false)]
    public void GehoertZurVorbelegung_NurManuellMitHaken(Auslosungsart art, bool festVorgegeben, bool erwartet)
    {
        var auslosung = new Auslosung
        {
            Auslosungsart = art,
            TurnierRunde  = new TurnierRunde { PaarungFestVorgegeben = festVorgegeben }
        };

        VorbelegungsZustaendigkeit.GehoertZurVorbelegung(auslosung).Should().Be(erwartet);
    }

    // #627: im Schweizer System gibt es keine festen Paarungen — die Gegner folgen je Spieltag aus der Tabelle.
    [Theory]
    [InlineData(Rundenart.SchweizerSystem, false)]
    [InlineData(Rundenart.KoRunde, true)]
    [InlineData(Rundenart.Gruppenphase, true)]
    [InlineData(Rundenart.Liga, true)]
    [InlineData(Rundenart.TopfLiga, true)]
    public void FesteVorgabe_NurImSchweizerSystemGesperrt(Rundenart rundenart, bool moeglich) =>
        (VorbelegungsZustaendigkeit.FesteVorgabeSperrGrund(rundenart) is null).Should().Be(moeglich);
}
