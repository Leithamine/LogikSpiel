# MathCross Auswertungsregel

Für **mehrteilige Gleichungen** in MathCross (z. B. `A op1 B op2 C = D`) gilt als feste Spielregel:

- **Auswertung strikt links nach rechts**.
- Es gibt **keine** Priorisierung nach „Punkt vor Strich“.

Beispiele:

- `2 + 3 × 4` wird als `(2 + 3) × 4 = 20` ausgewertet.
- `20 - 6 ÷ 2` wird als `(20 - 6) ÷ 2 = 7` ausgewertet.

Diese Regel wird im Generator (`MathCrossGeneratorService.Evaluate`) konsistent umgesetzt.


## Division in Master

Fachentscheidung für den `master`-Modus:

- **Nicht-ganzzahlige Divisionsergebnisse sind erlaubt**.
- Zugelassen sind Ergebnisse mit **maximal einer Nachkommastelle** (z. B. `5 ÷ 2 = 2.5`).
- Ergebnisse mit mehr als einer Nachkommastelle (z. B. `1 ÷ 3`) werden verworfen, um Anzeige/Validierung konsistent zu halten.

In Modi ohne Dezimalzahlen bleiben nur ganzzahlige Divisionsergebnisse gültig.
