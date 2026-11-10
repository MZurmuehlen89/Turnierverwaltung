# Hinweise zu Inhalten Dritter

## flag-icons

Die Länderflaggen unter `src/Fussballturnierverwaltung.Infrastructure/Seed/Bildmaterial/Flaggen/` sind aus den
SVG-Dateien von [flag-icons](https://github.com/lipis/flag-icons) gerastert (Zuordnung FIFA-Code → Flagge:
`tools/seed-bildmaterial/flaggen_mapping.tsv`).

```
The MIT License (MIT)

Copyright (c) 2013 Panayiotis Lipiridis

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Nicht enthalten: Wappen und Logos

Vereinswappen, Bilder der Nationalmannschaften, Turnier- und Verbandslogos sowie die olympische Flagge von Chinesisch
Taipei sind Marken oder urheberrechtlich geschützt und deshalb **nicht** Teil dieses Repos. Die Anwendung zeigt an
ihrer Stelle einen Platzhalter. Die Skripte unter `tools/seed-bildmaterial` dokumentieren, wie das Bildmaterial
beschafft wurde.

## Daten

Ranglisten, Abschlusstabellen und Turniermodi im Seed sind Fakten aus öffentlichen Quellen. Jede Rangliste nennt ihre
Quelle und den Abrufstand (`QuelleText`); übernommen sind nur die Werte, kein Text der Quellen.
