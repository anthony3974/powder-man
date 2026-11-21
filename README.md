# Powder Man

A falling-sand sandbox. Open `Assets/Scenes/Main.unity` and press Play.

## Controls

| Input | Action |
|---|---|
| Left mouse / touch | Draw selected element |
| Right mouse | Erase |
| Mouse wheel, `[` `]` | Brush size |
| Space | Pause / play |
| `.` | Step one tick (while paused) |
| R | Replace mode (draw over things, not just empty space) |
| C | Clear |
| 1–9 | Pick element |
| F5 / F9 | Save / load (one slot) |

## Where to change things

- **`Assets/Scripts/Elements.cs`** is where most changes go: every element and every reaction is defined there.
  - New element: add an id to `El`, then `Add(El.X, new Element { ... })` in `DefineElements()`.
  - New reaction: `React(a, b, aBecomes, bBecomes, chance)` in `DefineReactions()`. It works both ways round. `El.Any` is a wildcard.
  - Burning is automatic: anything with `Heat` sets fire to neighbours with `Flammability`. `ExplodeRadius` makes it explode instead.
- **`Assets/Scripts/Simulation.cs`**: the movement rules (powder, liquid, gas), burning, explosions, the cloner.
- **`Assets/Scripts/PowderGame.cs`**: drawing, input, the toolbar. Grid size and tick rate are in the Inspector on the `Powder Game` object.
- `Tools > Powder Man > Recreate Main Scene` rebuilds the scene if it gets deleted.

## Releasing (Android / F-Droid)

1. Bump `Version` and `VersionCode` in `Assets/Scripts/BuildInfo.cs`. The code must go up or phones won't take the update. The home menu shows both.
2. `Tools > Powder Man > Build APK` (or batch: `Unity -batchmode -nographics -buildTarget Android -projectPath . -executeMethod PowderMan.EditorTools.AndroidBuild.CommandLine -logFile Logs/android-build.log`) makes `apks/PowderMan.apk` (`com.Tony3974.PowderMan`).
3. Copy it to `~/APKs/` and run `~/fdroid/release.sh`.
