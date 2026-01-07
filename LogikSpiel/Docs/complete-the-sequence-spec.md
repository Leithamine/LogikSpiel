## Spiel: **Complete the Sequence**

**Untertitel:** Unendliche, mathematisch korrekte Zahlenrätsel

---

## 0) Ziel des Dokuments

Dieses Dokument beschreibt:

* das **Spielprinzip**, die **UX**, die **Regeln**
* die **prozedurale Level-Generierung** (unendlich viele Level pro Difficulty)
* die **Template-Bible** (je Difficulty inkl. Parametergrenzen, MissingIndex-Regeln, Eindeutigkeitsrisiken)
* das **Anti-Boring-System** (damit es sich nicht wie “immer das Gleiche” anfühlt)
* optionalen zweiten Leveltyp: **Mapping / Zuordnung** (`a→b`, `c→d`, …, `g→?`)

Ziel: Der Entwickler kann daraus direkt das Datenmodell, Generator, UI und Gameflow implementieren.

---

# 1) Produktidee / High-Level Konzept

„Complete the Sequence“ ist ein Zahlenrätsel-Spiel, in dem der Spieler eine Zahlenfolge sieht, bei der **genau ein Element fehlt**. Der Spieler muss anhand einer **mathematisch-logischen Regel** die fehlende Zahl bestimmen.

**Kernprinzipien**

* **Unendlich viele Level** pro Schwierigkeitsgrad durch prozedurale Generierung
* Jede Aufgabe ist:

  * **mathematisch korrekt**
  * **fair** (kein Rätsel ohne eindeutige Lösung)
  * **eindeutig** (es gibt exakt eine plausible Missing-Zahl)
* Die **Position der Lücke** ist variabel und wechselt pro Level (unter Template-spezifischen Einschränkungen).

---

# 2) Spiel-Modi / Level-Typen

Das Spiel kann als **ein Modus** starten (Sequence), später optional erweitert werden.
Empfohlen: als Option „Mode: Sequence | Mapping | Mix“.

## 2.1 Mode A: **Sequence Puzzle**

* Eingabe: eine Folge `x0..x(L-1)` mit einer Lücke an Index `missingIndex`
* Output: `correctAnswer` (fehlende Zahl)

## 2.2 Mode B: **Mapping Puzzle (a→b)**

Der Spieler sieht mehrere Paare:

* `a → b`
* `c → d`
* `e → f`
* `g → ?`
  Alle Paare folgen derselben Regel `out = F(in)`.

**Wichtig:** Dieser Leveltyp braucht stärkere Eindeutigkeitsregeln (mehr Paare), sonst sind mehrere Funktionen möglich.

---

# 3) Zielgruppe & Spielgefühl

* **Easy:** “Ich erkenne es sofort”
* **Normal:** “Ich muss kurz überlegen”
* **Hard:** “Ich muss die Struktur analysieren (Differenzen / Interleaving)”
* **Master:** “Kopfnuss, aber fair – ich kann sie lösen”

---

# 4) Gameflow (Run Loop)

## 4.1 Startscreen

* Auswahl:

  * Difficulty: Easy / Normal / Hard / Master
  * Mode: Sequence / Mapping / Mix (optional; wenn Mix später kommt)
* Start → neues Run-Objekt

## 4.2 Level-Schleife

1. Generator erzeugt `LevelDefinition` (Sequence oder Mapping)
2. UI zeigt Puzzle
3. Spieler gibt Antwort (Multiple Choice oder Eingabe)
4. Bewertung:

   * korrekt → Score, Streak, nächstes Level
   * falsch → Strafe (Lives / Time / Score), Feedback, ggf. Retry je nach Design
5. LevelIndex++ → neues Level

## 4.3 Game Over

Abhängig vom Run-Regelset:

* Variante A: **3 Leben**
* Variante B: **Zeitlimit pro Run**
* Variante C: Master: **Timer + Lives** (empfohlen)

**Empfehlung (klar und simpel):**

* Easy/Normal/Hard: 3 Lives, kein globaler Timer
* Master: 3 Lives + per Level Timer

---

# 5) UI/UX Spezifikation (Sequence)

## 5.1 Layout (Screen)

### Header (oben)

* Difficulty Label
* LevelIndex („Level 37“)
* Lives (3 Herzen)
* Score (optional)
* Streak (optional)

### Puzzle Area (Mitte)

* Horizontaler Strip aus Kacheln (typisch 6–10 Kacheln)
* Eine Kachel ist `_` / `?` / leeres Feld
* Kacheln sollen groß genug sein (Mobile): min. ~48dp Touch-Target

### Answer Area (unten)

**Variante A: Multiple Choice**

* 4–6 Buttons/Kacheln mit Zahlen
* 1 ist korrekt, Rest sind Distraktoren

**Variante B: Freie Eingabe**

* Numeric keypad oder Eingabe-Feld + Confirm Button
* Optional: `+` und `-` Buttons für kleine Anpassungen
* Optional: „Clear“ / Backspace

## 5.2 Interaktionen

* Tap auf Choice → sofort prüfen (oder Prüfen-Button, je nach UX)
* Bei Eingabe: Confirm → prüfen
* Hint Button (optional):

  * liefert Kategorie-Hint (nicht Lösung)
  * kostet Score oder 1 Hint pro Level
* Next Level: automatische Transition (0.4–0.7s)

## 5.3 Feedback

* Correct:

  * kurze Animation (Glow/Scale)
  * Sound/Haptic optional
* Wrong:

  * rotes Feedback
  * Leben -1
  * optional: kurz richtige Antwort anzeigen (je nach Difficulty: auf Easy/Normal ja, Hard/Master optional)

## 5.4 Accessibility

* Große Schrift (Dynamic Type)
* Hoher Kontrast
* Screenreader: jede Kachel liest “Wert X”, Missing: “fehlend”
* Motion Reduction: Animationen abschaltbar

---

# 6) Scoring & Progression (empfohlen, aber optional)

## 6.1 Score

* Basis pro Level abhängig von Difficulty:

  * Easy: 10
  * Normal: 20
  * Hard: 35
  * Master: 60
* Bonus:

  * Streak: + (Streak * smallFactor) capped
  * Speed: wenn Level-Timer existiert, schneller = mehr Bonus

## 6.2 Streak

* +1 bei richtig
* Reset bei falsch

## 6.3 Lives

* Start 3
* Falsch → -1
* (Optional) bei Master Timeout → -1

---

# 7) Prozedurale Generierung (Kernsystem)

## 7.1 Ziele des Generators

* **Unendlich viele Level** (kein statischer Content)
* **Eindeutig** (genau 1 plausible Antwort)
* **Nicht repetitiv** (Anti-Boring)
* **Schwierigkeit skaliert** (LevelIndex beeinflusst Range/Länge/Template-Komplexität)
* **Schnell** (Levelgenerierung < 50ms normal, < 200ms worst-case mit Re-rolls)

## 7.2 Input/Output

### Input

* `difficulty`
* `mode` (Sequence/Mapping/Mix)
* `levelIndex`
* `runSeed`
* `history` (z.B. letzte 10–20 Level-Metadaten)

### Output: `LevelDefinition`

Common:

* `id` (GUID oder hash)
* `difficulty`, `mode`, `levelIndex`, `seedUsed`
* `timeLimitSeconds` (optional)
* `answerFormat` (Choice/Input)
* `hintCategory` (für Hint Button)

Sequence-spezifisch:

* `sequenceFull[]` (nur intern/Debug)
* `sequenceShown[]` (mit Missing)
* `missingIndex`
* `correctAnswer`
* `templateId`
* `templateCategory`
* `params` (für Debug/Telemetry)

Mapping-spezifisch:

* `pairs[]` (z.B. 4–6 Paare; manche ggf. unvollständig)
* `missingField` (welches Feld fehlt)
* `correctAnswer`
* `ruleFamilyId`
* `params`

---

# 8) Anti-Boring System (damit es „unendlich“ fühlt)

## 8.1 Kategorien-Rotation (Pflicht)

Jedem Template ist eine Kategorie zugeordnet:

* linear (arithmetisch)
* multiplicative (geometrisch)
* periodic
* figurate (Quadrat/Dreieck/Pronic)
* recursion-light (Fibo)
* polynomial/differences
* interleaving-2
* interleaving-3
* digit-rule
* piecewise/index-based

**Regel:** Nicht zwei Level hintereinander aus derselben Kategorie.
Zusatz: Wenn in den letzten 5 Levels Kategorie X zu oft vorkam → Gewicht reduzieren.

## 8.2 Similarity Score (Pflicht)

Speichere pro Level Metadaten:

* `templateId`
* `category`
* `growthType` (linear/quadratic/exponential/recursive)
* `operationSignature` (z.B. "+", "×", "+,+", "×,+", "interleave")
* `lengthClass` (short 5–6 / mid 7–8 / long 9–11)
* `missingIndexClass` (early/mid/late)

Berechne Ähnlichkeit zu letzten 10 Levels (0..1).
Wenn Score > Threshold (z.B. 0.65) → re-roll Template oder Parameter.

## 8.3 Variation-Hebel (Pflicht)

Pro Level variieren:

* `L` (Folgenlänge)
* `missingIndex`
* Parameter (a,d,r,t, offsets)
* Antwortformat (Choice vs Input) ab Hard
* Timer-Optionen (nur Hard/Master)

---

# 9) Eindeutigkeits-Check (kritisch)

## 9.1 Warum?

Viele Zahlenfolgen lassen sich mit mehreren Regeln erklären. Das zerstört Fairness.

## 9.2 Minimal-Strategie (wenn Performance wichtig)

* Easy: fast keine Ambiguity, einfacher Check reicht
* Normal+: Ambiguity check gegen begrenzte Liste „Konkurrenz-Templates“

## 9.3 Empfohlene Strategie: Ambiguity Score

### Ablauf

1. Generator erstellt Puzzle mit `templateId`
2. Erzeuge Kandidatenliste `competitors` (Templates der gleichen oder niedrigeren Komplexität)
3. Für jeden competitor:

   * Prüfe, ob competitor alle sichtbaren Werte erklärt
   * Falls ja: berechne implied missing value
   * Wenn implied missing ≠ correctAnswer → Ambiguity++
4. Puzzle ist gültig, wenn:

   * Easy: Ambiguity == 0
   * Normal: Ambiguity == 0 (empfohlen)
   * Hard/Master: Ambiguity == 0 (Pflicht)

### Guardrails (super wichtig)

* Rekursive Templates (Fibo/Recurrence): missingIndex darf nicht in Startwerten liegen
* Interleaving: sicherstellen, dass jede Teilfolge genug sichtbare Werte hat

---

# 10) Antwortformat-Policy (Developer Rules)

## 10.1 Default pro Difficulty

* Easy: **Multiple Choice**, 4 Optionen
* Normal: **Multiple Choice**, 4–6 Optionen
* Hard: 50% Choice (6 Optionen), 50% Input
* Master: Input (oder Choice 6 Optionen + Timer)

## 10.2 Distraktoren (Multiple Choice) – Pflicht pro Template

Für jedes Template muss ein Distraktor-Generator existieren:

* `GenerateDistractors(correct, template, params, shownSequence)`

**Distraktor-Typen (standard)**

1. Off-by-one: `correct ± 1`, `±2`
2. Parameter-Fehler: z.B. `d±1`, `r±1`, Offset ±1
3. „Falsche Regel“-Kandidat: z.B. arithmetisch statt geometrisch
4. Nachbarwert: ein anderer echter Folgewert (x_{missing±1}) falls sinnvoll
5. Index-Shift: MissingIndex um 1 versetzt interpretiert

**Filter**

* keine Duplikate
* alle Werte im erlaubten Bereich
* nicht alle Distraktoren zu nah am correct (z.B. min. Spread-Regel)

---

# 11) Difficulty Scaling (LevelIndex → schwerer ohne unfair zu werden)

Jede Difficulty hat Basisspannen; LevelIndex erweitert diese langsam.

## 11.1 Parameter Expansion (Beispiel)

* Easy:

  * Anfang: |d| ≤ 12, a ≤ 60
  * später: |d| ≤ 20, a ≤ 120
* Normal:

  * t, offsets, Startwerte wachsen leicht
  * L wird häufiger 8–9
* Hard/Master:

  * mehr Interleaving/Polynom/Recurrence
  * missingIndex häufiger early/late (schwieriger)
  * Timer häufiger / strenger (Master)

---

# 12) TEMPLATE BIBLE – SEQUENCE MODE (Developer Version)

## Allgemeine Regeln für MissingIndex

* `missingIndex` wird zufällig gewählt, aber nur aus `allowedIndices(templateId, L)`
* MissingIndex-Policy:

  * 40% mid
  * 30% early
  * 30% late
  * dann auf allowedIndices clampen (wenn nicht erlaubt → neu ziehen)

---

## 12.1 EASY Templates

**Ziel:** 1 Regel, sofort sichtbar
**L:** 6–8 (manchmal 9)
**Range:** 0..200 (clamp)

### E1 Arithmetisch

* Regel: `x_n = a + n*d`
* Parameter:

  * a ∈ [0..60] (später [0..120])
  * d ∈ [-12..12] \ {0} (später [-20..20] \ {0})
* Allowed Missing: i ∈ [1..L-2]
* Risiken:

  * d=0 → trivial (verwerfen)
* Anti-Boring:

  * negative d zulassen
  * größere |d|
  * L variieren

### E2 Geometrisch

* Regel: `x_n = a * r^n`
* Parameter:

  * a ∈ [1..20]
  * r ∈ {2,3,4} (5 selten)
* L: 5–7
* Allowed Missing: i ∈ [1..L-2]
* Guardrails:

  * r=1 verboten
  * max(x) ≤ 5000
* Risiko:

  * bei sehr kleinen Zahlen kann’s „arithmetisch wirken“ → mit L≥6 entschärfen

### E3 Alternierende Schritte

* Regel: Δ alterniert: `+p, +q, +p, +q…`
* Parameter:

  * a ∈ [0..50]
  * p,q ∈ [-12..12], p≠0, q≠0, p≠q
* L: 6–9
* Allowed Missing: i ∈ [2..L-2]
* Guardrails:

  * p=q verboten (sonst E1)
  * p=-q nur selten (sonst Pendel)
* Anti-Boring:

  * Vorzeichenmix (+7, -2) etc.

### E4 Periodisches Muster

* Regel: pattern length t ∈ {3,4}
* Parameter:

  * pattern[i] ∈ [0..40], alle verschieden
* L: 7–10
* Allowed Missing: i ∈ [0..L-1]
* Guardrails:

  * verwerfe zu triviale patterns (1,2,3)
* Risiko:

  * ähnelt Interleaving → ok in Easy

### E5 A/B Alternation

* Regel: A,B,A,B…
* Parameter: A,B ∈ [0..80], A≠B
* L: 6–10
* Allowed Missing: any
* Guardrails:

  * sehr selten einsetzen (sonst langweilig)

### E6 Multiplikationstabelle

* Regel: `x_n = k*(n+1)`
* Parameter: k ∈ [2..12]
* L: 6–9
* Allowed Missing: i ∈ [1..L-2]
* Risiko:

  * ähnlich wie E1 (arithmetisch). Anti-Boring: selten + cooldown.

### E7 Wachsender Schritt

* Regel: Δ = s*n (oder s*(n+1))
* Parameter:

  * x0 ∈ [0..30]
  * s ∈ {1,2,3}
* L: 6–8
* Allowed Missing: i ∈ [2..L-2]
* Risiko:

  * kollidiert mit quadratischen Folgen → ok in Easy, aber in Normal checken.

---

## 12.2 NORMAL Templates

**Ziel:** bekannte Folgen + 1–2 Schritte
**L:** 6–9
**Range:** 0..1500 (clamp)

### N1 Quadrate (shifted)

* Regel: `(n+t)^2`
* t ∈ [1..10]
* L: 6–8 (prefer ≥7)
* Missing: i ∈ [1..L-2]
* Risiko:

  * kann als S1 wirken, wenn zu kurz → L≥7 + competitor check.

### N2 Kubikzahlen

* Regel: `(n+t)^3`
* t ∈ [1..6]
* L: 5–7
* Missing: i ∈ [1..L-2]
* Guardrail: max ≤ 8000

### N3 Dreieckszahlen

* Regel: `(n+t)(n+t+1)/2` (optional +c oder *2)
* t ∈ [0..8], c ∈ [-5..5] optional
* L: 6–8
* Missing: i ∈ [1..L-2]
* Risiko:

  * ähnelt E7 (wachsende Schritte) → competitor check.

### N4 Fibonacci-artig

* Regel: `x_n = x_{n-1} + x_{n-2}`
* Start:

  * x0 ∈ [1..10], x1 ∈ [1..15]
* L: 6–9 (prefer ≥7)
* Missing: i ∈ [2..L-2]
* Guardrails:

  * nie i=0/1
  * nicht zu oft x0=x1=1 (sonst immer gleich)

### N5 Alternierend ×p und +q

* Regel: abwechselnd multiplizieren/addieren
* p ∈ {2,3}, q ∈ [1..15], a ∈ [1..20]
* L: 6–8
* Missing: i ∈ [2..L-2]
* Guardrails:

  * q=0 vermeiden
  * zu kleine Startwerte vermeiden

### N6 Interleaving 2 arithmetische Folgen

* Regel:

  * even idx: a + k*d1
  * odd idx: b + k*d2
* Parameter:

  * a,b ∈ [0..50]
  * d1,d2 ∈ [-10..10]{0}
* L: 8–10
* Missing: i ∈ [2..L-3]
* Guardrails:

  * konstante Teilfolgen verhindern

### N7 Prime Run (optional + offset)

* Regel: prime(n+t) + c
* t ∈ [1..20], c ∈ [-5..5]
* L: 6–8
* Missing: i ∈ [1..L-2]
* Guardrail:

  * nicht zu häufig (wissen-lastig)

### N8 Geometrisch + Offset

* Regel: a*r^n + c
* a ∈ [1..10], r ∈ {2,3}, c ∈ [-10..10]
* L: 5–7
* Missing: i ∈ [1..L-2]
* Guardrails:

  * r=1 verboten
  * max clamp

---

## 12.3 HARD Templates

**Ziel:** differenzen / polynom / interleaving / rekurrenz
**L:** 7–10
**Range:** 0..8000 (clamp)

### S1 Quadratisches Polynom (2. Differenz konstant)

* Regel: a*n^2 + b*n + c
* a ∈ [1..4], b ∈ [-10..10], c ∈ [0..50]
* L: 7–9
* Missing: i ∈ [2..L-3]
* Guardrails:

  * verwerfe Spezialfall „reine Quadrate“ (a=1,b=0,c=0) → sonst N1
* Risiko:

  * kann zu N1 kollidieren, competitor check Pflicht

### S2 Differenzen arithmetisch (Story-Variante)

* Regel: Δx_n = p + n*q (q>0)
* p ∈ [-8..15], q ∈ [1..6], x0 ∈ [0..40]
* L: 7–10
* Missing: i ∈ [2..L-2]
* Guardrail:

  * q=0 vermeiden

### S3 Affine Rekurrenz

* Regel: x_{n+1} = p*x_n + q
* p ∈ {2,3}, q ∈ [-10..20], x0 ∈ [1..20]
* L: 6–8
* Missing: i ∈ [1..L-2]
* Guardrails:

  * q=0 selten (sonst geometrisch)
  * wenn negatives verboten: clamp/resample

### S4 Rekurrenz Ordnung 2 (gewichtet)

* Regel: x_n = p*x_{n-1} + q*x_{n-2}
* p,q ∈ {1,2}, start x0,x1 ∈ [1..12]
* L: 7–9
* Missing: i ∈ [2..L-3]
* Guardrails:

  * p=q=1 selten (sonst Fibonacci)

### S5 Interleaving 2 Folgen (verschiedene Typen)

* Regel: A geometrisch, B arithmetisch (oder andere Kombinationen)
* L: 9–11
* Missing: i ∈ [3..L-3]
* Guardrails:

  * pro Teilfolge ≥ 3 sichtbare Werte (nach Missing)
  * missingIndex nicht so setzen, dass A oder B nur 2 Werte sichtbar hat

### S6 Potenzen von 2 + alternierender Offset

* Regel: 2^(n+t) + (n%2?u:v)
* t ∈ [0..6], u,v ∈ [-5..5], u≠v bevorzugt
* L: 6–8
* Missing: i ∈ [2..L-2]
* Risiko:

  * u=v → geometrisch+offset → vermeiden

### S7 Digit-rule: x + sumDigits(x)

* Regel: x_{n+1} = x_n + sumDigits(x_n)
* x0 ∈ [1..80]
* L: 6–8
* Missing: i ∈ [1..L-2]
* Guardrail:

  * wähle x0 so, dass Schritte sichtbar sind

### S8 Pronic

* Regel: (n+t)(n+t+1)
* t ∈ [1..20]
* L: 6–8
* Missing: i ∈ [1..L-2]
* Guardrail: L≥7 bevorzugen

---

## 12.4 MASTER Templates

**Ziel:** sehr komplex, aber eindeutig
**L:** 8–11 (Interleaving-3: 10–13)
**Range:** 0..20000 (clamp)

### M1 Kubisches Polynom (3. Differenz konstant)

* Regel: a*n^3 + b*n^2 + c*n + d
* a ∈ [1..2], b ∈ [-6..6], c ∈ [-15..15], d ∈ [0..60]
* L: 9–11
* Missing: i ∈ [3..L-4]
* Guardrail:

  * L≥9 Pflicht
  * 3. Differenz muss sichtbar sein (sonst verwerfen)

### M2 Interleaving 3 Folgen

* Regel: A,B,C,A,B,C…
* L: 10–13
* Missing: i ∈ [4..L-4]
* Guardrails:

  * jede Teilfolge muss ≥ 3 sichtbare Werte haben
  * missingIndex darf nicht die Sichtbarkeit einer Teilfolge „zerstören“

### M3 Rekurrenz Ordnung 3

* Regel: x_n = p*x_{n-1}+q*x_{n-2}+r*x_{n-3}
* p,q,r ∈ {1,2} (nicht alle 1)
* Start: x0,x1,x2 ∈ [1..8]
* L: 9–11
* Missing: i ∈ [3..L-3]
* Guardrails:

  * nie i in {0,1,2}
  * L≥9

### M4 Differenzen folgen Fibonacci

* Regel: Δx Fibonacci-like, x ist Summe
* Δ0 ∈ [1..8], Δ1 ∈ [1..12], x0 ∈ [0..40]
* L: 8–10
* Missing: i ∈ [3..L-2]
* Guardrails:

  * competitor check gegen Polynomfolgen Pflicht

### M5 Stückweise nach Index (n mod 3)

* Regel: abhängig von n%3 (3 verschiedene Operationen)
* p ∈ [1..15], q ∈ {2,3}, r ∈ [1..12], start x0 ∈ [1..20]
* L: 8–10
* Missing: i ∈ [3..L-3]
* Guardrails:

  * Operationen müssen sichtbar sein (×2 erzeugt Sprung)
  * nicht „zufällig“ wirken: start/parameter so setzen, dass Pattern erkennbar

### M6 Komposition (sparsam)

* Regel: square(triangle(n+t)) ODER triangle(square(n+t))
* t ∈ [0..4]
* L: 6–8
* Missing: i ∈ [2..L-2]
* Guardrail:

  * niedrige Häufigkeit (Boss-Level)
  * optional Hint anbieten (Kategorie: “Figurate Komposition”)

### M7 Prime(n)+n² (sehr selten)

* Regel: prime(n+t) + (n+t)^2
* t ∈ [1..10]
* L: 6–8
* Missing: i ∈ [2..L-2]
* Guardrails:

  * sehr selten
  * optional Hint “Primzahlen beteiligt”

---

# 13) TEMPLATE BIBLE – MAPPING MODE (a→b)

## 13.1 Grundregeln (Pflicht)

* Zeige **mindestens 4 Paare** (besser 5–6)
  Beispiel:

  * 12 → 27
  * 8 → 19
  * 25 → 51
  * 7 → ?
* Die fehlende Stelle rotiert:

  * häufig: output fehlt (`in → ?`)
  * selten: input fehlt (`? → out`)
  * selten: ein mittleres Paar fehlt (nicht immer das letzte)

## 13.2 Eindeutigkeitsregeln (noch strenger)

* Eindeutigkeitscheck gegen andere erlaubte Rule-Families ist Pflicht
* Vermeide zu kleine Inputs (1–3), da dort viele Regeln passen
* Verwende Rule-Families so, dass Outputs nicht zufällig gleich werden

---

## 13.3 Mapping Rule Families pro Difficulty

### EASY Mapping

**Inputs:** 0..50
**Pairs:** 4
**Rules:**

1. out = in + k (k ∈ [2..20])
2. out = in * k (k ∈ [2..6])
3. out = in*k + c (k ∈ [2..4], c ∈ [-10..10])
4. out = in^2 + c (in ∈ [0..12], c ∈ [-5..+15])
5. piecewise even/odd:

   * even: out=in+c1
   * odd: out=in+c2
     (c1,c2 klein, c1≠c2)

**Missing field:** 80% output, 20% input

### NORMAL Mapping

**Inputs:** 0..200
**Pairs:** 5
**Rules:**

1. out = p*in + q (p ∈ {2,3,4}, q ∈ [-20..20])
2. out = (in mod m) + c (m ∈ [4..12], c ∈ [0..20])
3. out = sumDigits(in) + c (c ∈ [0..30])
4. out = in + sumDigits(in)
5. out = productDigits(in) (nur 2-stellig, in 10..99)
6. nextPrime(in) (sparsam)

### HARD Mapping

**Inputs:** 0..999
**Pairs:** 5–6
**Rules:**

1. piecewise Collatz-like (klar definiert):

   * even: out = in/2 + c
   * odd: out = 3*in + c
2. out = reverse(in) + c (nur wenn reverse eindeutig)
3. out = numberOfDivisors(in) + c (in range eng wählen)
4. out = sumPrimeFactors(in) (+ optional c) (klar definieren: mit Wiederholungen oder ohne)
5. out = f(in) dann g(…) (z.B. sumDigits → *k + c)

### MASTER Mapping

**Inputs:** 0..2000 (aber Kopfrechnen beachten)
**Pairs:** 6
**Rules:**

1. out = p*in + sumDigits(in) (p klein)
2. piecewise nach in mod 3 (3 Fälle)
3. out = nextPrime(in) - in (Prime-Gap)
4. out = sumDigits(in^2) (sparsam)
5. XOR mask (nur wenn du “bitwise” als Kategorie akzeptierst; sonst weglassen)

---

# 14) Hint-System (empfohlen)

Hints sollen **Kategorie** verraten, nicht Lösung.

**Hint-Ausgabe (Sequence)**

* “Tipp: Schau dir die Differenzen an.”
* “Tipp: Es sind zwei Teilfolgen (abwechselnd).”
* “Tipp: Das ist rekursiv (nutzt vorherige Zahlen).”
* “Tipp: Potenzen / Quadrate könnten beteiligt sein.”

**Hint-Ausgabe (Mapping)**

* “Tipp: Es ist eine lineare Funktion (× und +).”
* “Tipp: Es hängt von Ziffern ab.”
* “Tipp: Es ist stückweise (abhängig von gerade/ungerade).”

**Kosten**

* Easy/Normal: -Score oder 1 Hint pro Level
* Hard/Master: Limit pro Run (z.B. 3 Hints)

---

# 15) Edge Cases / Guardrails (wichtig für stabile Qualität)

* Keine Division, wenn nicht ganzzahlig
* Keine negativen Zahlen, wenn du das nicht willst (ansonsten UI klar)
* Keine extrem großen Zahlen (clamp/resample)
* Keine Folgen, die konstant sind (außer Tutorial)
* Interleaving: jede Teilfolge muss genug sichtbare Werte haben
* Rekursiv: missingIndex darf nicht in Startwerten sein
* Mapping: nie nur 3 Paare (zu mehrdeutig)

---

# 16) Telemetry

Logge anonym (für Balancing):

* difficulty, templateId, category
* timeToSolve
* wrongAttempts
* hintUsed
* ambiguityScore (wie viele competitor templates gepasst hätten)
* abandonment (Level verlassen)

Damit kannst du später genau sehen: „Master Template M5 ist zu frustrierend“.

---

A) Performanter Eindeutigkeits-Check: Architektur
A1) Grundidee

Jedes Template T hat:

Category (linear / multiplicative / periodic / figurate / recursion / polynomial / interleaving / digit / piecewise …)

GrowthType (linear / quadratic / cubic / exponential / recursive / periodic)

AllowedIndices(L) (wo die Lücke sein darf)

Generate(params, L) → fullSequence[]

Fit(shownSequence, missingIndex) → entweder “passt nicht” oder (params, impliedMissing)

Ambiguity-Check pro Level

Erzeuge Puzzle mit Template T und correctAnswer

Nimm competitors = CompetitorMap[T]

Für jedes C ∈ competitors:

wenn C.Fit(...) passt und impliedMissing != correctAnswer → Ambiguous

Nur wenn keiner der Konkurrenten eine andere Missing-Lösung liefert → gültig

A2) Performance-Regel

Pro Template maximal 6–12 Konkurrenten.

Pro Fit O(L) oder O(L·k) (k klein).

Wenn ein Level nach z.B. 30 Re-Rolls nicht eindeutig wird: Parameterbereich anpassen (größeres L, andere Werte, anderes Template).

A3) „CompetitorMap“ Prinzip (warum diese Konkurrenten?)

Du prüfst nur Templates, die typischerweise dieselben sichtbaren Werte erklären können, z.B.:

Linear ↔ Multiplikationstabelle ↔ „fast linear“ Interleaving Spezialfälle

Geometrisch ↔ Affin (wenn Offset 0) ↔ Geometrisch+Offset

Quadratzahlen ↔ allgemeines quadratisches Polynom ↔ „wachsende Schritte“

Fibonacci ↔ allgemeine Rekurrenz Ordnung 2

Interleaving ↔ Periodisch ↔ Stückweise nach Index (mod 2 / mod 3)

Digit-Rule ↔ Affin (bei kleinen Zahlen manchmal zufällig ähnlich)

B) SEQUENCE MODE – Komplette Template-Bible + Konkurrentenliste
Globale Defaults (Sequence)

Länge L (typisch)

Easy: 6–8 (selten 9)

Normal: 6–9

Hard: 7–10

Master: 8–11 (Interleave-3: 10–13)

Range-Clamp (max Wert, damit’s nicht nervig wird)

Easy: ≤ 300

Normal: ≤ 1500

Hard: ≤ 8000

Master: ≤ 20000

MissingIndex-Policy (falls Template es zulässt)

40% Mitte, 30% früh, 30% spät
Dann in AllowedIndices(L) clampen.

EINFACH (E1–E7)
E1 – Arithmetische Folge (linear)

Kategorie: linear

Regel: x_n = a + n*d

Parametergrenzen:

a ∈ [0..60] (später [0..120])

d ∈ [-12..12] \ {0} (später [-20..20] \ {0})

L: 6–8

Allowed MissingIndices: i ∈ [1..L-2]

Guardrails:

verwerfe, wenn Werte < 0 (falls negative Zahlen nicht erlaubt)

verwerfe d=0 (trivial)

verwerfe, wenn Folge konstant/zu klein variiert (z.B. |d|=1 zu oft → Gewicht senken)

Fit-Methode (für Ambiguity-Check):

bestimme d aus zwei sichtbaren Positionen (z.B. i0,i1):
d = (x[i1]-x[i0])/(i1-i0) muss ganzzahlig sein

bestimme a = x[i0] - i0*d

verifiziere alle sichtbaren Werte

missing: a + missingIndex*d

Konkurrentenliste (prüfen gegen):

E6 (Multiplikationstabelle) – ist auch arithmetisch

E7 (Wachsender Schritt) – kann bei kurzen/kleinen Sequenzen „fast linear“ wirken

N6 (Interleave 2 arith.) – Spezialfälle können wie reine Arithmetik wirken

N1 (Quadrate) – nur als Safety, wenn L klein/werte klein (optional)

S2 (Δ arithmetisch) – kann bei sehr kleinem q wie linear wirken (optional)

E2 – Geometrische Folge (exponential)

Kategorie: multiplicative

Regel: x_n = a * r^n

Parametergrenzen:

a ∈ [1..20]

r ∈ {2,3,4} (5 sehr selten)

L: 5–7

Allowed MissingIndices: i ∈ [1..L-2]

Guardrails:

r=1 verboten

clamp: max(x) ≤ 5000

Fit-Methode:

nutze zwei sichtbare Werte:
r^(Δn) = x[j]/x[i] → prüfe ob Quotient eine perfekte Potenz ist

bestimme a = x[i]/r^i (muss ganzzahlig)

Konkurrentenliste:

N8 (Geometrisch + Offset) – wenn Offset zufällig 0/klein wirkt

S3 (Affine Rekurrenz) – wenn q=0 ist das geometrisch

E1 (Arithmetik) – bei sehr kleinen Zahlen/kurzer Länge kann’s „linear aussehen“ (seltener, aber check ist billig)

E3 – Alternierende Schritte (+p, +q, +p, +q…)

Kategorie: mixed ops

Regel: Differenzen alternieren: Δ = p, q, p, q…

Parametergrenzen:

a ∈ [0..50]

p,q ∈ [-12..12], p≠0, q≠0, p≠q

L: 6–9

Allowed MissingIndices: i ∈ [2..L-2]

Guardrails:

verwerfe p=q (würde E1)

p=-q nur selten (Pendeln)

Fit-Methode:

aus den sichtbaren Differenzen an Positionen ohne Missing: prüfe Alternation

bestimme p/q über erste passende Differenzen

Konkurrentenliste:

E1 (Arithmetisch) – falls p≈q (Edgecases)

E4 (Periodisch) – Pendel-Fälle können wie Pattern wirken

N6 (Interleave 2 arith.) – Alternation kann wie Interleaving wirken

M5 (Piecewise nach Index) – in Master existiert mod-Index Logik (optional nur wenn du Cross-Difficulty prüfst)

E4 – Periodisches Muster (t=3 oder 4)

Kategorie: periodic

Regel: x_n = pattern[n mod t]

Parametergrenzen:

t ∈ {3,4}

pattern values ∈ [0..40], alle verschieden

L: 7–10

Allowed MissingIndices: i ∈ [0..L-1]

Guardrails:

verwerfe zu triviale Pattern (1,2,3) zu oft

Fit-Methode:

teste t=3 und t=4:
für alle sichtbaren n muss x[n] == pattern[n mod t] konsistent sein

Konkurrentenliste:

E5 (AB Alternation) – Sonderfall von Periodisch (t=2), aber du nutzt t=3/4; trotzdem falls später t=2 erlaubt

N6 (Interleave 2 arith.) – Interleaving kann periodisch aussehen, wenn d1=d2=0 (Guardrail, aber check)

E3 (Alternierende Schritte) – in Pendel/kleinen Fällen ähnlich

E5 – Zwei-Wert Alternation (A,B,A,B…)

Kategorie: periodic (t=2)

Regel: A,B,A,B…

Parametergrenzen: A,B ∈ [0..80], A≠B

L: 6–10

Allowed MissingIndices: any

Guardrails: sehr geringe Gewichtung + Cooldown hoch

Fit-Methode: prüfe x[even]=A und x[odd]=B

Konkurrentenliste:

E4 (Periodisch) – generischer

N6 (Interleave 2 arith.) – konstante Teilfolgen wären gleich

E6 – Multiplikationstabelle (k·1, k·2, …)

Kategorie: linear (arithmetisch)

Regel: x_n = k*(n+1) (Index-Start fix!)

Parametergrenzen: k ∈ [2..12]

L: 6–9

Allowed MissingIndices: i ∈ [1..L-2]

Guardrails: nicht zu häufig neben E1

Fit-Methode:

k = x[0] (falls missingIndex != 0) sonst aus anderem sichtbaren Index ableiten

prüfe x[n] == k*(n+1)

Konkurrentenliste:

E1 (Arithmetisch) – das ist das Haupt-Confusion Template

E7 – Wachsender Schritt (Δ = 1,2,3,… skaliert)

Kategorie: differences (quadratic growth)

Regel: x_{n+1} = x_n + s*(n+1) (oder s*n, konsistent definieren)

Parametergrenzen: x0 ∈ [0..30], s ∈ {1,2,3}

L: 6–8

Allowed MissingIndices: i ∈ [2..L-2]

Guardrails: verhindere zu schnelles Wachstum (clamp)

Fit-Methode:

bilde sichtbare Differenzen (ohne Missing)

prüfe ob Differenzen linear mit n wachsen

Konkurrentenliste:

N3 (Dreieckszahlen) – sehr ähnlich vom „Wachsenden Schritt“-Gefühl

S1 (Quadratisches Polynom) – mathematisch gleiche Klasse (quadratisch)

S2 (Δ arithmetisch) – praktisch identisch in anderer Parametrisierung

E1 (Arithmetisch) – falls s sehr klein und L kurz (optional)

NORMAL (N1–N8)
N1 – Quadratzahlen (shifted)

Kategorie: figurate / polynomial(2)

Regel: x_n = (n+t)^2

Parametergrenzen: t ∈ [1..10]

L: 6–8 (prefer ≥7)

Allowed MissingIndices: i ∈ [1..L-2]

Fit-Methode:

teste t in [1..10], prüfe ob alle sichtbaren Werte quadratisch sind

Konkurrentenliste:

S1 (Quadratisches Polynom) – allgemeiner Quadratik kann Quadrate imitieren

S2 (Δ arithmetisch) – quadratische Klasse

E7 (Wachsender Schritt) – kann Quadrate „ähnlich“ wirken lassen

N3 (Dreieckszahlen) – in kurzen Segmenten manchmal ähnlich

N2 – Kubikzahlen (shifted)

Kategorie: figurate / polynomial(3)

Regel: x_n = (n+t)^3

Parametergrenzen: t ∈ [1..6]

L: 5–7

Allowed MissingIndices: i ∈ [1..L-2]

Konkurrentenliste:

M1 (Kubisches Polynom) – generalisiert Kubikzahlen

(optional) M4 (Differenzen Fibonacci) – selten, aber kann zufällig ähnlich sein; meist weglassen

N3 – Dreieckszahlen (shifted, optional +c/*2)

Kategorie: figurate / quadratic

Regel: T(n+t) = (n+t)(n+t+1)/2 (optional +c oder *2)

Parametergrenzen: t ∈ [0..8], c ∈ [-5..5] optional

L: 6–8

Allowed MissingIndices: i ∈ [1..L-2]

Konkurrentenliste:

E7 (Wachsender Schritt) – sehr nah

S1 (Quadratisches Polynom) – gleiche Klasse

S2 (Δ arithmetisch)

N1 (Quadrate) – wenn sehr kurz/klein, optional

N4 – Fibonacci-artig (Ordnung 2)

Kategorie: recursion-light

Regel: x_n = x_{n-1} + x_{n-2}

Parametergrenzen: x0 ∈ [1..10], x1 ∈ [1..15]

L: 6–9 (prefer ≥7)

Allowed MissingIndices: i ∈ [2..L-2] (nie 0/1)

Konkurrentenliste:

S4 (Rekurrenz Ordnung 2) – generalisiert Fibonacci (p,q)

S3 (Affine Rekurrenz) – kann bei kurzen L zufällig passen (optional)

(optional) M3 (Rekurrenz Ordnung 3) – meist nicht nötig, kostet Performance

N5 – Alternierend ×p und +q

Kategorie: mixed ops

Regel: abwechselnd ×p und +q (genau definieren, ob zuerst × oder +)

Parametergrenzen: p ∈ {2,3}, q ∈ [1..15], a ∈ [1..20]

L: 6–8

Allowed MissingIndices: i ∈ [2..L-2]

Konkurrentenliste:

S3 (Affine Rekurrenz) – “×p + q” kann wie Affin wirken

E2 (Geometrisch) – wenn q klein/0 wirkt

N8 (Geometrisch + Offset) – wenn sich Offset „stabil“ anfühlt

E3 (Alternierende Schritte) – selten, aber alternierende Operatoren können verwechselt werden

N6 – Interleaving 2 arithmetische Folgen

Kategorie: interleaving-2

Regel:

even idx: A_k = a + k*d1

odd idx: B_k = b + k*d2

Parametergrenzen: a,b ∈ [0..50], d1,d2 ∈ [-10..10]\{0}

L: 8–10

Allowed MissingIndices: i ∈ [2..L-3]

Guardrails: jede Teilfolge braucht ≥3 sichtbare Werte

Konkurrentenliste:

E4 (Periodisch) – wenn d1/d2 zufällig 0 werden (Guardrail, aber check)

E3 (Alternierende Schritte) – Alternation kann wie Interleave wirken

E1 (Arithmetisch) – Spezialfall, wenn beide Teilfolgen gleiche Steigung (Guardrail)

S5 (Interleave 2 mixed) – generalisiert Interleaving, optional

N7 – Primzahlen-Run (optional +c)

Kategorie: number-theory / lookup

Regel: prime(n+t) + c

Parametergrenzen: t ∈ [1..20], c ∈ [-5..5]

L: 6–8

Allowed MissingIndices: i ∈ [1..L-2]

Guardrails: selten einsetzen (wissenlastig)

Konkurrentenliste (minimal):

E1 (Arithmetisch) – nur als Sicherheitscheck (fast nie passt)

(optional) N1 Quadrate – fast nie passt, meist weglassen

N8 – Geometrisch + Offset

Kategorie: multiplicative + offset

Regel: x_n = a*r^n + c

Parametergrenzen: a ∈ [1..10], r ∈ {2,3}, c ∈ [-10..10]

L: 5–7

Allowed MissingIndices: i ∈ [1..L-2]

Konkurrentenliste:

E2 (Geometrisch) – Sonderfall c=0

S3 (Affine Rekurrenz) – “×r + c” ist affine recurrence

N5 (Alternierend ×/+ ) – kann bei kurzen L ähnlich aussehen

SCHWER (S1–S8)
S1 – Quadratisches Polynom (2. Differenz konstant)

Kategorie: polynomial(2)

Regel: x_n = a*n^2 + b*n + c

Parametergrenzen: a ∈ [1..4], b ∈ [-10..10], c ∈ [0..50]

L: 7–9

Allowed MissingIndices: i ∈ [2..L-3]

Guardrails: verwerfe, wenn exakt N1 (Quadrate) oder N3 (Dreieck) entsteht

Konkurrentenliste:

N1 (Quadrate)

N3 (Dreieck)

S2 (Δ arithmetisch)

E7 (Wachsender Schritt)

S2 – Differenzen sind arithmetisch (quadratic via differences)

Kategorie: differences → quadratic

Regel: Δx_n = p + n*q

Parametergrenzen: p ∈ [-8..15], q ∈ [1..6], x0 ∈ [0..40]

L: 7–10

Allowed MissingIndices: i ∈ [2..L-2]

Konkurrentenliste:

S1 (Quadratisches Polynom)

N3 (Dreieck)

E7 (Wachsender Schritt)

N1 (Quadrate) – optional

S3 – Affine Rekurrenz (x → p*x + q)

Kategorie: affine / multiplicative+offset

Regel: x_{n+1} = p*x_n + q

Parametergrenzen: p ∈ {2,3}, q ∈ [-10..20], x0 ∈ [1..20]

L: 6–8

Allowed MissingIndices: i ∈ [1..L-2]

Guardrails: q=0 nur selten (sonst E2)

Konkurrentenliste:

E2 (Geometrisch) – q=0 Sonderfall

N8 (Geometrisch+Offset)

N5 (Alternierend ×/+)

S4 (Rekurrenz Ordnung 2) – kann kurze Segmente imitieren (optional)

S4 – Rekurrenz Ordnung 2 (gewichtet)

Kategorie: recursion(2)

Regel: x_n = p*x_{n-1} + q*x_{n-2}

Parametergrenzen: p,q ∈ {1,2} (p=q=1 selten), x0,x1 ∈ [1..12]

L: 7–9

Allowed MissingIndices: i ∈ [2..L-3]

Konkurrentenliste:

N4 (Fibonacci) – p=q=1

S3 (Affine) – kurze Segmente können passen

(optional) M3 (Rekurrenz Ordnung 3) – meist weglassen

S5 – Interleaving 2 Folgen (Mixed Typen)

Kategorie: interleaving-2 mixed

Regel: A-Teilfolge folgt Regel A, B-Teilfolge Regel B (z.B. A geo, B arith)

Parametergrenzen (Beispiel-Set, du kannst mehrere Sets zulassen):

A: aA ∈ [1..10], r ∈ {2,3}

B: aB ∈ [0..40], d ∈ [-10..10]\{0}

L: 9–11

Allowed MissingIndices: i ∈ [3..L-3]

Guardrails: pro Teilfolge ≥3 sichtbare Werte

Konkurrentenliste:

N6 (Interleave 2 arith.) – Sonderfall

E4 (Periodisch) – wenn eine Teilfolge konstant wird

M5 (Index-piecewise) – mod-Index kann wie Interleave wirken (optional)

S6 – Potenzen von 2 + alternierender Offset

Kategorie: exponential + parity offset

Regel: x_n = 2^(n+t) + (n%2 ? u : v)

Parametergrenzen: t ∈ [0..6], u,v ∈ [-5..5], nicht beide 0, u≠v bevorzugt

L: 6–8

Allowed MissingIndices: i ∈ [2..L-2]

Konkurrentenliste:

E2 (Geometrisch)

N8 (Geometrisch+Offset)

N5 (Alternierend ×/+)

S3 (Affine) – “×2 + const” kann passen

S7 – Digit-Rule: x + sumDigits(x)

Kategorie: digit-rule

Regel: x_{n+1} = x_n + sumDigits(x_n)

Parametergrenzen: x0 ∈ [1..80]

L: 6–8

Allowed MissingIndices: i ∈ [1..L-2]

Konkurrentenliste:

S3 (Affine) – bei kleinen Zahlen kann’s zufällig ähnlich wirken

E1 (Arithmetisch) – sehr selten, optional

S8 – Pronic (Rechteckzahlen)

Kategorie: figurate / quadratic

Regel: x_n = (n+t)(n+t+1)

Parametergrenzen: t ∈ [1..20]

L: 6–8 (prefer ≥7)

Allowed MissingIndices: i ∈ [1..L-2]

Konkurrentenliste:

S1 (Quadratisches Polynom)

N3 (Dreieck) – beide quadratisch/figurat

S2 (Δ arithmetisch)

MASTER (M1–M7)
M1 – Kubisches Polynom (3. Differenz konstant)

Kategorie: polynomial(3)

Regel: x_n = a*n^3 + b*n^2 + c*n + d

Parametergrenzen: a ∈ [1..2], b ∈ [-6..6], c ∈ [-15..15], d ∈ [0..60]

L: 9–11

Allowed MissingIndices: i ∈ [3..L-4]

Guardrails: L≥9 Pflicht

Konkurrentenliste:

M4 (Δ Fibonacci) – selten, aber kann in kurzen Fällen ähnlich wirken

S1 (Quadratisch) – optional (eigentlich sollte kubisch nicht quadratisch sein; check ist billig)

M2 – Interleaving 3 Folgen (A,B,C,…)

Kategorie: interleaving-3

Regel: drei Teilfolgen mit eigenen Regeln

L: 10–13

Allowed MissingIndices: i ∈ [4..L-4]

Guardrails: jede Teilfolge ≥3 sichtbare Werte

Konkurrentenliste:

M5 (Index-piecewise n mod 3) – sehr ähnlich vom „mod 3“-Gefühl

E4 (Periodisch) – wenn Teilfolgen konstant werden (optional)

S5 (Interleave 2 mixed) – manchmal wirkt 3-Interleave wie 2-Interleave + Pattern (optional)

M3 – Rekurrenz Ordnung 3

Kategorie: recursion(3)

Regel: x_n = p*x_{n-1} + q*x_{n-2} + r*x_{n-3}

Parametergrenzen: p,q,r ∈ {1,2} (nicht alle 1), Start x0,x1,x2 ∈ [1..8]

L: 9–11

Allowed MissingIndices: i ∈ [3..L-3] (nie 0..2)

Konkurrentenliste:

S4 (Rekurrenz Ordnung 2)

N4 (Fibonacci)

(optional) M1 (kubisch) – meist unnötig

M4 – Differenzen folgen Fibonacci (und x ist Summenfolge)

Kategorie: nested-differences / recursion-in-differences

Regel: Δx folgt Fibonacci-artig, x ist kumuliert

Parametergrenzen: Δ0 ∈ [1..8], Δ1 ∈ [1..12], x0 ∈ [0..40]

L: 8–10

Allowed MissingIndices: i ∈ [3..L-2]

Konkurrentenliste:

M1 (kubisch) – Differenzen-Strukturen können kollidieren

S1 (quadratisch) – optional

S2 (Δ arithmetisch) – optional (wenn Δ zufällig fast linear)

M5 – Stückweise nach Index (n mod 3)

Kategorie: piecewise/index-based

Regel: abhängig von n mod 3 drei verschiedene Operationen (Permutation erlaubt)

Parametergrenzen: p ∈ [1..15], q ∈ {2,3}, r ∈ [1..12], x0 ∈ [1..20]

L: 8–10

Allowed MissingIndices: i ∈ [3..L-3]

Konkurrentenliste:

M2 (Interleave-3) – mod3 wirkt wie 3 Teilfolgen

E4 (Periodisch) – wenn Operationen zufällig wiederholen (optional)

M6 – Komposition (Figurate in Figurate) – Boss-Template

Kategorie: composition/figurate

Regel: square(triangle(n+t)) ODER triangle(square(n+t))

Parametergrenzen: t ∈ [0..4]

L: 6–8

Allowed MissingIndices: i ∈ [2..L-2]

Konkurrentenliste:

N1 (Quadrate)

N3 (Dreieck)

S1 (quadratisches Polynom)

M7 – Hybrid: prime(n+t) + (n+t)^2 – Boss-Template

Kategorie: hybrid number-theory + polynomial

Regel: prime(n+t) + (n+t)^2

Parametergrenzen: t ∈ [1..10]

L: 6–8

Allowed MissingIndices: i ∈ [2..L-2]

Konkurrentenliste:

N1 (Quadrate) – wenn Prime-Teil klein wirkt, könnte’s wie Quadrate+Offset aussehen

S1 (quadratisch) – optional

N7 (Prime-Run) – optional

C) Konkurrenten-Listen als „Map“ (copy-paste-fähig)

Wenn dein Dev es direkt codieren will, kann er es so als Map anlegen:

Sequence CompetitorMap (empfohlen, performant)

E1 → [E6, E7, N6, N1]

E2 → [N8, S3, E1]

E3 → [E1, E4, N6]

E4 → [E5, N6, E3]

E5 → [E4, N6]

E6 → [E1]

E7 → [N3, S1, S2]

N1 → [S1, S2, E7, N3]

N2 → [M1]

N3 → [E7, S1, S2]

N4 → [S4, S3]

N5 → [S3, E2, N8, E3]

N6 → [E4, E3, E1]

N7 → [E1]

N8 → [E2, S3, N5]

S1 → [N1, N3, S2, E7]

S2 → [S1, N3, E7]

S3 → [E2, N8, N5]

S4 → [N4, S3]

S5 → [N6, E4]

S6 → [E2, N8, N5, S3]

S7 → [S3]

S8 → [S1, N3, S2]

M1 → [M4, S1]

M2 → [M5, E4]

M3 → [S4, N4]

M4 → [M1, S1]

M5 → [M2, E4]

M6 → [N1, N3, S1]

M7 → [N1, N7]

Hinweis: Das ist bewusst nicht maximal, sondern performant. Wenn du in Tests merkst, dass ein bestimmtes Template oft doppeldeutig wird, erweiterst du nur dessen Konkurrentenliste.

D) MAPPING MODE – komplette Bible + Konkurrentenliste
D1) Mapping-Global Rules

Pairs count:

Easy: 4 Paare

Normal: 5 Paare

Hard: 5–6 Paare

Master: 6 Paare

MissingField Rotation:

70–80%: Output fehlt (in → ?)

15–25%: Input fehlt (? → out)

5–10%: mittleres Paar unvollständig (nicht immer das letzte)

Eindeutigkeitscheck: Pflicht (Mapping ist sonst schnell mehrdeutig)

EASY Mapping Rules (ME1–ME5)
ME1 – Additiv

Regel: out = in + k

Params: k ∈ [2..20], in ∈ [0..50]

Konkurrenten:

MN1 (Linear p*in+q) – linear generalisiert additiv

ME3 (Affine in*k+c) – kann bei k=1 kollidieren (verbiete k=1 in ME3)

ME5 (Parity piecewise) – wenn c1=c2=k wäre (Guardrail)

Guardrail: nutze Inputs nicht zu klein/gleichmäßig (1,2,3…), sonst passt auch out=in*2 auf Teilmenge

ME2 – Multiplikativ

Regel: out = in * k

Params: k ∈ [2..6], in ∈ [0..50]

Konkurrenten:

MN1 (Linear) – linear fit kann zufällig passen bei kleinen Daten

ME3 (Affine) – Sonderfall c=0

ME1 (Additiv) – bei kleinen in kann’s täuschen (optional)

Guardrail: vermeide viele in=0 (sonst out=0 immer)

ME3 – Affin (Multiply + Add)

Regel: out = in*k + c

Params: k ∈ [2..4], c ∈ [-10..10], in ∈ [0..50]

Konkurrenten:

MN1 (Linear) – ist die gleiche Familie (nur andere Param-Ranges)

ME1 (Additiv) – wenn k=1 (verbieten)

ME2 (Multiplikativ) – wenn c=0

Guardrails:

verbiete k=1

c=0 nur selten

ME4 – Quadrat + Offset

Regel: out = in^2 + c

Params: in ∈ [0..12], c ∈ [-5..15]

Konkurrenten:

MN1 (Linear) – linear kann auf wenigen Punkten täuschen

ME3 (Affine) – selten, aber check billig

Guardrail: mindestens 2 verschiedene in-Abstände (nicht nur 2,3,4)

ME5 – Gerade/Ungerade piecewise

Regel:

wenn even: out = in + c1

wenn odd: out = in + c2

Params: c1,c2 ∈ [-10..20], c1≠c2

Konkurrenten:

ME1 (Additiv) – Sonderfall c1=c2 (verboten)

MH1 (Collatz piecewise) – ähnliche piecewise-Struktur (falls du cross-tier prüfst)

MN1 (Linear) – kann bei unglücklichen Daten passen

NORMAL Mapping Rules (MN1–MN6)
MN1 – Linear allgemein

Regel: out = p*in + q

Params: p ∈ {2,3,4}, q ∈ [-20..20], in ∈ [0..200]

Konkurrenten:

ME1/ME2/ME3 (Add/Mult/Affin) – alle sind Unterfälle

MN2 (Mod+Offset) – kann bei kleiner Spanne linear wirken

MN3/MN4 (Digit-Sum Varianten) – selten, aber check

Guardrail: wähle Inputs so, dass Ergebnisse nicht zufällig wie mod aussehen (z.B. nicht nur 0..m-1)

MN2 – Modulo + Offset

Regel: out = (in mod m) + c

Params: m ∈ [4..12], c ∈ [0..20]

Konkurrenten:

MN1 (Linear) – in kleinen Bereichen kann mod „linear“ wirken

MM2 (mod3 piecewise) – falls du Master-Regeln als Konkurrenten zulässt

Guardrail: Inputs über mehrere Mod-Zyklen verteilen (damit Regel sichtbar ist)

MN3 – sumDigits(in) + c

Regel: out = sumDigits(in) + c

Params: in ∈ [0..500], c ∈ [0..30]

Konkurrenten:

MN4 (in + sumDigits(in))

MH5 (Compose digit then affine) – wenn du cross-tier prüfst

MN1 (Linear) – selten, aber check

Guardrail: wähle Inputs mit unterschiedlicher Ziffernstruktur (nicht nur 10,11,12…)

MN4 – in + sumDigits(in)

Regel: out = in + sumDigits(in)

Params: in ∈ [0..500]

Konkurrenten:

MN3 (sumDigits + c)

MN1 (Linear) – selten

Guardrail: vermeide nur kleine in (sonst fast linear)

MN5 – productDigits(in) (2-stellig)

Regel: out = productDigits(in)

Params: in ∈ [10..99]

Konkurrenten:

MN3 (sumDigits + c)

MN1 (Linear) – selten

Guardrail: Inputs so wählen, dass Produkt nicht konstant/0 wird (z.B. viele 10,20 vermeiden)

MN6 – nextPrime(in) (sparsam)

Regel: out = nextPrime(in)

Params: in ∈ [0..200]

Konkurrenten:

MM3 (PrimeGap) – verwandte Prime-Funktion

MN1 (Linear) – fast nie, optional

Guardrail: selten einsetzen

HARD Mapping Rules (MH1–MH5)
MH1 – Collatz-artig piecewise

Regel:

even: out = in/2 + c

odd: out = 3*in + c

Params: c ∈ [-10..20], in ∈ [0..999] (aber so wählen, dass even/odd gemischt)

Konkurrenten:

ME5 (Parity piecewise) – ähnliche Struktur

MN1 (Linear) – kann bei wenigen Punkten täuschen

MM2 (mod3 piecewise) – verwandtes piecewise-Konzept

MH2 – reverse(in) + c

Regel: out = reverse(in) + c

Params: in ∈ [10..999], c ∈ [-20..20]

Konkurrenten:

MN3 (sumDigits + c) – beide „digit based“

MH5 (Compose digit then affine)

Guardrail: wähle Inputs so, dass reverse nicht gleich bleibt (z.B. 11,22 vermeiden)

MH3 – numberOfDivisors(in) + c

Regel: out = d(in) + c (d = Anzahl Teiler)

Params: in in engem Band (z.B. 10..200), c ∈ [0..20]

Konkurrenten:

MH4 (sumPrimeFactors)

MN1 (Linear) – selten

Guardrail: Inputs so wählen, dass divisor counts variieren (nicht nur Primzahlen)

MH4 – sumPrimeFactors(in) + c

Regel: out = sumPrimeFactors(in) + c
Definition fixieren: mit Wiederholung (multiset) ODER ohne. (Empfehlung: mit Wiederholung.)

Params: in ∈ [10..500], c ∈ [0..20]

Konkurrenten:

MH3 (Divisors)

MN3 (sumDigits + c) – beides „Summen“, selten aber check

MH5 – Compose: Digit-Funktion → Affin

Regel: out = k * f(in) + c, wobei f z.B. sumDigits oder reverse-firstlast ist

Params: k ∈ {2,3,4}, c ∈ [-10..20]

Konkurrenten:

MN3 (sumDigits + c)

MN1 (Linear) – selten

MH2 (reverse + c)

MASTER Mapping Rules (MM1–MM5)
MM1 – p*in + sumDigits(in)

Regel: out = p*in + sumDigits(in)

Params: p ∈ {2,3}, in ∈ [0..2000]

Konkurrenten:

MN1 (Linear)

MN4 (in + sumDigits(in))

MH5 (Compose digit then affine)

MM2 – Piecewise nach in mod 3

Regel: drei Fälle je in mod 3 (klar definierte Ops)

Params: Ops sichtbar machen (mind. eine Multiplikation)

Konkurrenten:

MH1 (Parity piecewise)

MN2 (mod+offset)

M2/M5 (wenn du Master-Sequence-Piecewise als Analog denkst; hier optional)

MM3 – PrimeGap

Regel: out = nextPrime(in) - in

Params: in ∈ [0..500]

Konkurrenten:

MN6 (nextPrime)

MN1 (Linear) – optional

MM4 – sumDigits(in^2)

Regel: out = sumDigits(in^2)

Params: in ∈ [0..200]

Konkurrenten:

MN3 (sumDigits + c)

MH5 (Compose)

MM5 – XOR mask (optional, nur wenn du bitwise willst)

Regel: out = in XOR mask

Params: mask klein (z.B. 1..255), in passend

Konkurrenten:

MN1 (Linear) – praktisch nie

MM2 (piecewise) – praktisch nie

Wenn du keinen Bitwise-Content willst: weglassen (macht UX schwerer).

E) Mapping CompetitorMap (kurz, copy-paste-fähig)

ME1(Add) → [MN1, ME3, ME5]

ME2(Mult) → [MN1, ME3]

ME3(Affin) → [MN1, ME1, ME2]

ME4(Square+ c) → [MN1, ME3]

ME5(Parity piecewise) → [ME1, MN1, MH1]

MN1(Linear) → [ME1, ME2, ME3, MN2, MN3, MN4]

MN2(Mod+ c) → [MN1, MM2]

MN3(sumDigits+c) → [MN4, MH5, MN1]

MN4(in+sumDigits) → [MN3, MN1]

MN5(productDigits) → [MN3, MN1]

MN6(nextPrime) → [MM3, MN1]

MH1(Collatz piecewise) → [ME5, MN1, MM2]

MH2(reverse+c) → [MN3, MH5]

MH3(divisors+c) → [MH4, MN1]

MH4(sumPrimeFactors+c) → [MH3, MN3]

MH5(compose digit→affine) → [MN3, MN1, MH2]

MM1(p*in+sumDigits) → [MN1, MN4, MH5]

MM2(mod3 piecewise) → [MH1, MN2]

MM3(primeGap) → [MN6, MN1]

MM4(sumDigits(in^2)) → [MN3, MH5]

MM5(XOR) → [] (wenn drin, meist keine Konkurrenten nötig)

F) Praktische Hinweise für den Dev (damit’s stabil wird)
F1) „Fit“ muss deterministisch & streng sein

Ein Konkurrent gilt nur als „passt“, wenn:

alle sichtbaren Werte exakt matchen

Parameter gültig im Range sind

bei rekursiv/interleave: genug sichtbare Werte pro Teilfolge vorhanden sind

F2) Cross-Difficulty Checks

Für Performance kannst du entscheiden:

Nur gleiche Difficulty als Konkurrenten prüfen, ODER

gleiche + niedrigere (empfohlen), weil „einfachere“ Regeln oft „mitpassen“.

In den oben genannten Konkurrentenlisten ist das schon „sinnvoll gemischt“, aber du kannst es noch strikter machen.

F3) Debug-Telemetry

Speichere pro generiertem Level:

TemplateId

Params

competitorHits (welche Konkurrenten gepasst hätten)
So findest du schnell die Templates, die zu oft ambiguous werden → dann:

L erhöhen

Parameterbereiche verschieben

Konkurrentenliste erweitern (nur lokal für dieses Template)
# 17) Akzeptanzkriterien (Definition of Done)

## Core

* ✅ Unendliche Level pro Difficulty (Generator)
* ✅ Lücke ist variabel (missingIndex rotiert)
* ✅ Eindeutigkeit: AmbiguityScore==0 für Normal+ (empfohlen) und Hard/Master (Pflicht)
* ✅ Anti-Boring: Kategorien-Rotation + Similarity Score aktiv
* ✅ Choice + Distraktoren funktionieren (wenn Choice genutzt)
* ✅ UI/UX: Feedback korrekt, Lives/Score korrekt



* ✅ Hint-System
* ✅ Mapping Mode
* ✅ Mix Mode
