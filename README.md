# Frontline Arena

Ein realistischer Wellen-Shooter in der Ego-Perspektive, gebaut mit Unity 6 (6000.5.7f1). Er läuft direkt im Browser über GitHub Pages.

**▶ Spielen:** https://timonbi97-gif.github.io/frontline-arena/

## Features

- **26 Waffen in 8 Klassen.** Jede hat ein eigenes Modell, eigene Werte und eigenen Sound.
- **Echte Visiere:**
  - Kimme und Korn
  - Rotpunkt (offener Ring)
  - Holo-Visier
  - ACOG 4x mit Chevron
  - Zielfernrohr mit Mil-Dot-Absehen
  
  Durch alle Visiere sieht man hindurch.
- **Waffenkammer im Hauptmenü** für Haupt- und Sekundärwaffe. Weitere Waffen liegen auf der Karte, und Gegner lassen ihre Waffe fallen. Aufnehmen mit **E**.
- **Feuermodi:** Einzel, Auto, Feuerstoß sowie Pump- und Repetierwaffen. Dazu Magazinwechsel-Animation, Patronenhülsen und Mündungsrauch.
- **Große Karte (400 × 400 m):**
  - Stadt mit Ziegel- und Glasfassaden, Ampeln, Zebrastreifen, ausgebrannten Autos und Rauchsäulen
  - Wald mit See
  - Industriegebiet
  - Wüste
  - Farm
  - Berge am Horizont
- **Realistische Grafik:**
  - prozedurale Texturen mit Normal-Maps
  - Wolkenhimmel
  - Spiegelungen
  - Bloom, ACES-Tonemapping und Vignette
- **Gegner:**
  - Soldaten mit Tarnuniform, Helm und echter Waffe
  - Feuerstöße mit Reaktionszeit, Nachladen und Granaten
  - 4 Typen: Stürmer, Schütze, MG-Schütze, Juggernaut
- **Sound:** Hall im Freien, Kugelpfeifen, Schritte, Wind und entferntes Gefecht
- **Komfort:** Lebensregeneration, Ducken, Granaten, 3 Schwierigkeitsstufen

## Waffen

| Klasse | Waffen |
|---|---|
| Pistolen | P9, M1911, Desert Eagle, Revolver .357, G18 (Auto) |
| Maschinenpistolen | MP5, UMP-45, Vector, P90 |
| Sturmgewehre | M4A1, AK-47, G36C, SCAR-H (ACOG), AUG A3 (ACOG), FAMAS (Feuerstoß) |
| Maschinengewehre | M249, PKM |
| Schrotflinten | M870 (Pump), Benelli M4, Doppelflinte |
| Präzisionsgewehre | SVD Dragunov, M14 EBR |
| Scharfschützengewehre | AWM (Repetierer), Barrett M82 .50 |
| Werfer | RPG-7, M32 Granatwerfer |

## Steuerung

| Taste | Aktion |
|---|---|
| WASD | Bewegen |
| Maus | Zielen |
| Linksklick | Schießen |
| Rechtsklick | Visier / Zielfernrohr |
| Shift | Sprinten |
| Leertaste | Springen |
| C / Strg | Ducken |
| R | Nachladen |
| 1 / 2 / 3 | Hauptwaffe / Pistole / Werfer |
| E | Waffe aufnehmen |
| G | Granate werfen |
| Q | Letzte Waffe |
| M / Tab | Karte |
| Esc / P | Pause |

## Projektaufbau

- `Assets/Scripts/Arsenal.cs`: alle Waffen mit ihren Werten
- `Assets/Scripts/GunBuilder.cs`: baut die Waffenmodelle (Gehäuse, Lauf, Magazin, Schaft, Visier)
- `Assets/Scripts/World.cs`: Kartengenerator
- `Assets/Scripts/TexGen.cs`: prozedurale Texturen und Meshes
- `Assets/Shaders/`: Triplanar-Shader, Himmel, Post-Processing
- `Assets/Editor/BuildScript.cs`: erzeugt Szene und Materialien und baut WebGL nach `docs/`
- `docs/`: der fertige WebGL-Build, den GitHub Pages ausliefert

## Selbst bauen

Im Unity-Editor: Menü **Frontline → Build WebGL**. Oder per Kommandozeile:

```
Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildWebGL -quit
```

Danach `docs/` committen und pushen.
