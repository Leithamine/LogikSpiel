# MathCross – Produktentscheidungen (separat von Bugfixes)

> Zweck: Dieses Dokument hält **Produktentscheidungen** fest und trennt sie explizit von technischen Bugfixes.

## J1 – Gewinnlogik definieren

### Entscheidung
- **Primäre Gewinnlogik:** Exakte Zielanordnung (Token-genau) gemäß generiertem Puzzle.
- **Begründung:**
  - Das aktuelle Puzzle-Design arbeitet mit konkreten Zielzellen (`Solution`) und geplanter Schwierigkeitskurve.
  - Mehrdeutige mathematisch korrekte Alternativen würden je nach Gleichung unvorhersehbar mehrere Lösungen zulassen.
  - Level-Design, Hint-System und Progression bleiben nur mit eindeutiger Zielanordnung konsistent.

### Produktregel
- Ein Level gilt als gelöst, wenn alle editierbaren Zellen die generierten Sollwerte treffen.
- Eingabe-Normalisierung (Operator-/Zahlzeichen) bleibt erlaubt, aber semantisch muss die Zielanordnung erfüllt sein.

---

## J2 – Master-Schwierigkeit schärfen

### Zielbild
`master` soll sich klar von `hard` unterscheiden – nicht nur durch Dezimalwerte, sondern auch durch Komplexität und Lesbarkeit.

### Maßnahmen (Roadmap)
1. **Längere Gleichungen pilotieren**
   - Optionaler Modus mit 9er-Länge (`A op1 B op2 C op3 D = E`) für späte Master-Level.
2. **Operatorlogik erweitern (kontrolliert)**
   - Begrenzte Operator-Pools pro Levelphase (z. B. früh ohne `÷`, später mit `÷` + Dezimalregel).
3. **Visuelle Kennzeichnung verstärken**
   - Master-Header/Icon-Farbe stärker differenzieren.
   - Dezimal-/Operator-Hinweis in der UI sichtbar machen.

### Akzeptanzkriterium
- Spieler erkennen ohne Erklärung, dass `master` anspruchsvoller und „anders“ ist als `hard`.

---

## J3 – Operator-Token-System vereinheitlichen

### Entscheidung
Es gibt eine **kanonische Operatorsprache** über alle Schichten:
- `+`, `−`, `×`, `÷`, `=`

### Regelwerk
- **Generator** produziert ausschließlich kanonische Tokens.
- **UI** zeigt kanonische Tokens.
- **Validierung** akzeptiert Aliase (z. B. `-`, `x`, `*`, `/`, `:`), normalisiert aber intern auf die kanonische Form.

### DoD
- Kein Layer verwendet ein abweichendes internes Operatoralphabet.
- Alias-Verarbeitung passiert zentral (kein dupliziertes Mapping).

---

## J4 – UX-Backlog (priorisiert)

### P1 – Undo
- Letzte Eingabe rückgängig machen (zellbasiert, optional mit Stack > 1).
- Wichtig für Fehlertoleranz auf Mobile.

### P1 – Responsive Zellgröße
- Zellgröße dynamisch abhängig von Screenbreite, Orientierung und Gleichungslänge.
- Ziel: keine abgeschnittenen Boards bei kleineren Geräten.

### P2 – Bessere Kandidatenlogik
- Kandidaten kontextsensitiver:
  - Zahlenbereich je Schwierigkeit,
  - plausiblere Distraktoren,
  - weniger Wiederholungen.

### Optional spätere UX-Features
- Langdruck für Schnelllöschen.
- Soft-Highlight für betroffene Gleichungen bei Zellfokus.
- Hints mit abgestufter Stärke (statt nur Full-Cell-Reveal).

---

## Umsetzungsprinzip

- Produktentscheidungen aus diesem Dokument werden in eigenen Paketen umgesetzt,
  **getrennt** von Stabilitäts-/Bugfix-Paketen.
