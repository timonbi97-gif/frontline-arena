# Frontline Arena

Ein Wellen-Shooter in der Ego-Perspektive, gebaut mit Unity 6 (6000.5.7f1). Er läuft direkt im Browser über GitHub Pages.

**▶ Spielen:** https://timonbi97-gif.github.io/frontline-arena/

## Features

- **Große Karte (400 × 400 m)** mit 5 Zonen: Stadt mit Hochhäusern, Ruinen, Park, Parkplatz und Platz; Wald mit See und Hütten; Industriegebiet mit Containern, Tanks und Lagerhallen; Wüste mit Bunkern, Kakteen und Sandsack-Stellungen; Farm mit Feldern, Scheunen und Silos
- **4 Gegnertypen:**
  - Sprinter: schnell, Nahkampf
  - Soldat: schießt aus der Distanz und weicht seitlich aus
  - Koloss: langsam und viel Leben
  - Boss: alle 5 Wellen; Salven, Stampfangriff, ruft Verstärkung
- **Gegner-KI** mit A*-Wegfindung um Gebäude herum und Sichtlinien-Prüfung
- **5 Waffen:**
  - Pistole und Sturmgewehr von Anfang an
  - Schrotflinte, Scharfschützengewehr (mit Zielfernrohr) und Raketenwerfer (Flächenschaden) liegen in Verstecken auf der Karte
- **Pickups:** Gesundheit, Rüstung, Munition; auf der Karte und als Beute von Gegnern
- **Kampfsystem:** Kopfschüsse, Combo-Multiplikator, Highscore (wird im Browser gespeichert)
- **HUD:** Minikarte, große Karte (M), Trefferanzeige, Schadensrichtung
- **3 Schwierigkeitsstufen**, einstellbare Maus-Empfindlichkeit und Lautstärke
- Alle Modelle, Effekte und Sounds werden per Code erzeugt; es gibt keine externen Assets

## Steuerung

| Taste | Aktion |
|---|---|
| WASD | Bewegen |
| Maus | Zielen |
| Linksklick | Schießen |
| Rechtsklick | Zielen / Zoom |
| Shift | Sprinten |
| Leertaste | Springen |
| R | Nachladen |
| 1–5 / Mausrad | Waffe wechseln |
| Q | Letzte Waffe |
| M / Tab | Karte |
| Esc / P | Pause |

## Projektaufbau

- `Assets/Scripts/`: der gesamte Spielcode (`GameManager`, `World`, `Player`, `Weapons`, `Enemy`, …)
- `Assets/Editor/BuildScript.cs`: erzeugt Szene und Materialien und baut WebGL nach `docs/`
- `Assets/WebGLTemplates/Frontline/`: HTML-Seite für den Browser
- `docs/`: der fertige WebGL-Build, den GitHub Pages ausliefert

## Selbst bauen

Im Unity-Editor: Menü **Frontline → Build WebGL**. Oder per Kommandozeile:

```
Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildWebGL -quit
```

Danach `docs/` committen und pushen. GitHub Pages aktualisiert sich automatisch.
