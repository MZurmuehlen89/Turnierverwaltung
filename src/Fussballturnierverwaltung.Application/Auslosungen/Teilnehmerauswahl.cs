using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Eine Mannschaft, die für die erste Runde in Frage kommt (#573) — zugelassen nach dem Turnierfilter.</summary>
public sealed record AuswahlKandidat(int MannschaftId, string Name, int LandId, int? VerbandId);

/// <summary>Eine Landesquote samt der Rangliste ihres Landes (Einträge und Abschnitte). Mit Spielklasse ist sie ein
/// Kontingent über feste Ranglistenplätze (#572), mit <paramref name="Pokalsieger"/> der Platz des Pokalsiegers der
/// Rangliste (#583, <paramref name="AbPlatz"/> = „Nachrücken ab“), sonst zählen die ersten Vereine des Landes.</summary>
public sealed record LandesKontingent(
    int LandId, string LandName, int Anzahl, int? Spielklasse, int? AbPlatz, Rangliste? Rangliste, bool Pokalsieger = false);

/// <summary>Eine Verbandsquote samt der Rangliste des Verbands (#572).</summary>
public sealed record VerbandsKontingent(int VerbandId, string Name, int Anzahl, Rangliste? Rangliste);

/// <summary>Die Landes- und Verbandsquoten eines Turniers samt Ranglisten (#573).</summary>
public sealed record Kontingentvorgabe(IReadOnlyList<LandesKontingent> Landesquoten, IReadOnlyList<VerbandsKontingent> Verbandsquoten)
{
    public bool Leer => Landesquoten.Count == 0 && Verbandsquoten.Count == 0;
}

/// <summary>Welche Mannschaften „Töpfe automatisch befüllen“ in der ersten Runde nimmt, wenn das Turnier Quoten hat
/// (#573): erst die Landesquoten (Spielklassen, dann Pokalsieger, dann ganze Länder), dann die Verbandsquoten, dann die
/// freien Plätze nach der Rangliste der Automatik.
/// <see cref="Mannschaften"/> enthält nur neu gewählte, nicht die schon eingelegten; <see cref="Fehlbestaende"/>
/// nennt jeden Quotenplatz, der sich nicht besetzen lässt. Ein offener Quotenplatz wird nicht über die freien Plätze
/// aufgefüllt — nachgerückt wird von Hand im Teilnehmerpool (#576).</summary>
public sealed record Teilnehmerauswahl(IReadOnlyList<int> Mannschaften, IReadOnlyList<string> Fehlbestaende)
{
    /// <param name="pool">Alle zugelassenen Mannschaften, auch die schon eingelegten.</param>
    /// <param name="eingelegt">Schon in den Töpfen liegende Mannschaften; sie zählen auf ihre Quote.</param>
    /// <param name="eingang">Mannschaftszahl der Runde.</param>
    /// <param name="rangAutomatik">Platz je Mannschaft in der Rangliste für die Automatik — ordnet die freien Plätze.</param>
    public static Teilnehmerauswahl Waehlen(
        Kontingentvorgabe quoten,
        IReadOnlyList<AuswahlKandidat> pool,
        IReadOnlySet<int> eingelegt,
        int eingang,
        IReadOnlyDictionary<int, int> rangAutomatik)
    {
        var lauf = new Lauf(pool, eingelegt);
        // Kontingente mit Spielklasse zuerst: sie verlangen feste Vereine. Der Pokalsieger danach, denn ob er nachrückt,
        // hängt daran, ob er schon über die Liga im Turnier ist; die übrigen nehmen, was danach übrig ist.
        foreach (var kontingent in quoten.Landesquoten.OrderBy(Bearbeitungsfolge))
            lauf.LandesKontingentBesetzen(kontingent);
        foreach (var kontingent in quoten.Verbandsquoten)
            lauf.VerbandsKontingentBesetzen(kontingent);
        lauf.FreiePlaetzeBesetzen(eingang, rangAutomatik);
        return new Teilnehmerauswahl(lauf.Neu, lauf.Fehlbestaende);
    }

    private static int Bearbeitungsfolge(LandesKontingent k) => k switch
    {
        { Spielklasse: not null } => 0,
        { Pokalsieger: true } => 1,
        _ => 2,
    };

    private sealed class Lauf(IReadOnlyList<AuswahlKandidat> pool, IReadOnlySet<int> eingelegt)
    {
        private readonly Dictionary<int, AuswahlKandidat> _pool =
            pool.GroupBy(k => k.MannschaftId).ToDictionary(g => g.Key, g => g.First());
        private readonly HashSet<int> _drin = [.. eingelegt];
        private readonly HashSet<int> _aufLandesquote = [];
        private readonly HashSet<int> _aufQuote = [];
        private int _offen;

        public List<int> Neu { get; } = [];
        public List<string> Fehlbestaende { get; } = [];

        public void LandesKontingentBesetzen(LandesKontingent k)
        {
            if (k.Pokalsieger)
                PokalsiegerBesetzen(k);
            else if (k.Spielklasse is int klasse)
                SpielklassenKontingentBesetzen(k, klasse);
            else
                LandesquoteBesetzen(k);
        }

        /// <summary>Genau die Ranglistenplätze des Kontingents; ein Platz, dessen Verein fehlt oder nicht im Pool ist,
        /// bleibt offen.</summary>
        private void SpielklassenKontingentBesetzen(LandesKontingent k, int klasse)
        {
            int abPlatz = k.AbPlatz ?? 1;
            if (k.Rangliste is null)
            {
                Fehlt(k.LandName, k.Anzahl, "keine Rangliste");
                return;
            }
            if (Kontingente.Bereich(k.Rangliste, klasse, abPlatz, k.Anzahl) is not { } bereich)
            {
                Fehlt($"{k.LandName} (Spielklasse {klasse} ab Platz {abPlatz})", k.Anzahl, "nicht in der Rangliste");
                return;
            }

            var vereine = Reihenfolge(k.Rangliste, e => e.Rang >= bereich.ErsterRang && e.Rang <= bereich.LetzterRang)
                .Where(id => AusLand(id, k.LandId) && !_aufLandesquote.Contains(id));
            int besetzt = Besetzen(vereine, k.Anzahl, AufLandesquote);
            NichtBesetzbar($"{k.LandName} ({bereich.Anzeige})", k.Anzahl - besetzt);
        }

        /// <summary>Der Pokalsieger der Rangliste — es sei denn, er ist schon qualifiziert: über eine andere Landesquote
        /// oder als Verein der obersten Spielklasse vor „Nachrücken ab“ (diese Plätze gehören höheren Wettbewerben).
        /// Dann rückt der erste Verein der obersten Spielklasse ab diesem Platz nach, der noch auf keiner Landesquote
        /// steht; kein Verein zählt doppelt (Reglement der UEFA). Ein Pokalsieger außerhalb der Rangliste ist nie über die
        /// Liga qualifiziert.</summary>
        private void PokalsiegerBesetzen(LandesKontingent k)
        {
            var bezeichnung = $"{k.LandName} (Pokalsieger)";
            if (k.Rangliste is null)
                Fehlt(bezeichnung, k.Anzahl, "keine Rangliste");
            else if (k.Rangliste.PokalsiegerId is not int sieger)
                Fehlt(bezeichnung, k.Anzahl, "kein Pokalsieger an der Rangliste");
            else if (Kontingente.Nachrueckbereich(k.Rangliste, k.AbPlatz ?? 1) is not { } nachrueckbereich)
                Fehlt(bezeichnung, k.Anzahl, "Nachrücken ab nicht in der Rangliste");
            else
            {
                var kandidaten = PokalKandidaten(k.Rangliste, sieger, nachrueckbereich)
                    .Where(id => AusLand(id, k.LandId) && !_aufLandesquote.Contains(id));
                NichtBesetzbar(bezeichnung, k.Anzahl - Besetzen(kandidaten, k.Anzahl, AufLandesquote));
            }
        }

        private IEnumerable<int> PokalKandidaten(Rangliste rangliste, int sieger, KontingentBereich nachrueckbereich)
        {
            int? rang = rangliste.Eintraege.FirstOrDefault(e => e.MannschaftId == sieger)?.Rang;
            bool qualifiziert = _aufLandesquote.Contains(sieger) || rang < nachrueckbereich.ErsterRang;
            return qualifiziert
                ? Reihenfolge(rangliste, e => e.Rang >= nachrueckbereich.ErsterRang && e.Rang <= nachrueckbereich.LetzterRang)
                : [sieger];
        }

        /// <summary>Die ersten Vereine des Landes; schon eingelegte zählen zuerst.</summary>
        private void LandesquoteBesetzen(LandesKontingent k)
        {
            // Als Liste: das Besetzen erweitert die Menge, über die sonst noch gezählt würde.
            var eingelegteDesLandes = _drin.Where(id => AusLand(id, k.LandId) && !_aufLandesquote.Contains(id)).ToList();
            var ausRangliste = k.Rangliste is null
                ? []
                : Reihenfolge(k.Rangliste, _ => true).Where(id => AusLand(id, k.LandId) && !_aufLandesquote.Contains(id));
            int besetzt = Besetzen(eingelegteDesLandes.Concat(ausRangliste), k.Anzahl, AufLandesquote);

            if (besetzt < k.Anzahl && k.Rangliste is null)
                Fehlt(k.LandName, k.Anzahl - besetzt, "keine Rangliste");
            else
                NichtBesetzbar(k.LandName, k.Anzahl - besetzt);
        }

        /// <summary>Die ersten des Verbands in seiner Rangliste. Wer schon über eine Landesquote gewählt oder eingelegt
        /// ist, zählt mit (ein Team zählt für beide Quoten).</summary>
        public void VerbandsKontingentBesetzen(VerbandsKontingent k)
        {
            var schonDrin = _drin.Where(id => AusVerband(id, k.VerbandId)).ToList();
            var ausRangliste = k.Rangliste is null
                ? []
                : Reihenfolge(k.Rangliste, _ => true).Where(id => AusVerband(id, k.VerbandId));
            int besetzt = Besetzen(schonDrin.Concat(ausRangliste), k.Anzahl, AufQuote);

            if (besetzt < k.Anzahl && k.Rangliste is null)
                Fehlt(k.Name, k.Anzahl - besetzt, "keine Rangliste");
            else
                NichtBesetzbar(k.Name, k.Anzahl - besetzt);
        }

        /// <summary>Freie Plätze = Eingang ohne die über Quoten gewählten Teams und ohne die offenen Quotenplätze. Schon
        /// eingelegte Teams ohne Quote belegen sie zuerst, danach zählt die Rangliste der Automatik, dann der Name.</summary>
        public void FreiePlaetzeBesetzen(int eingang, IReadOnlyDictionary<int, int> rangAutomatik)
        {
            int frei = eingang - _aufQuote.Count - _offen - _drin.Count(id => !_aufQuote.Contains(id));
            var kandidaten = _pool.Values
                .Where(k => !_drin.Contains(k.MannschaftId))
                .OrderBy(k => rangAutomatik.TryGetValue(k.MannschaftId, out var rang) ? rang : int.MaxValue)
                .ThenBy(k => k.Name, StringComparer.CurrentCulture)
                .Take(Math.Max(0, frei))
                .Select(k => k.MannschaftId)
                .ToList();
            foreach (var id in kandidaten)
                Hinzufuegen(id);
        }

        private static int Besetzen(IEnumerable<int> kandidaten, int anzahl, Action<int> besetzen)
        {
            int besetzt = 0;
            foreach (var id in kandidaten.Distinct())
            {
                if (besetzt == anzahl) break;
                besetzen(id);
                besetzt++;
            }
            return besetzt;
        }

        private static IEnumerable<int> Reihenfolge(Rangliste rangliste, Func<RanglistenEintrag, bool> filter) =>
            rangliste.Eintraege.Where(filter).OrderBy(e => e.Rang).Select(e => e.MannschaftId).Distinct();

        private bool AusLand(int id, int landId) => _pool.TryGetValue(id, out var k) && k.LandId == landId;

        private bool AusVerband(int id, int verbandId) => _pool.TryGetValue(id, out var k) && k.VerbandId == verbandId;

        private void AufLandesquote(int id)
        {
            _aufLandesquote.Add(id);
            AufQuote(id);
        }

        private void AufQuote(int id)
        {
            _aufQuote.Add(id);
            Hinzufuegen(id);
        }

        private void Hinzufuegen(int id)
        {
            if (_drin.Add(id))
                Neu.Add(id);
        }

        private void Fehlt(string bezeichnung, int anzahl, string grund)
        {
            _offen += anzahl;
            Fehlbestaende.Add($"{bezeichnung}: {anzahl} verlangt, {grund}");
        }

        private void NichtBesetzbar(string bezeichnung, int anzahl)
        {
            if (anzahl <= 0) return;
            _offen += anzahl;
            Fehlbestaende.Add($"{bezeichnung}: {anzahl} {(anzahl == 1 ? "Platz" : "Plätze")} nicht besetzbar");
        }
    }
}
