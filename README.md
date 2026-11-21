# Powder Man

A falling-sand sandbox for Android and desktop, made in Unity. Pour sand, water, lava and more onto the screen and watch them fall, flow, burn, melt and explode into each other.

- **Version:** 1.1 (build 2). It lives in `Assets/Scripts/BuildInfo.cs` and shows on the home menu.
- **Android id:** `com.Tony3974.PowderMan`, released on the F-Droid repo at fdroid.tonycad.com.
- **Unity:** 6000.0.85f1, built-in render pipeline, no extra packages.

## Running it

Open the folder in Unity Hub, open `Assets/Scenes/Main.unity` and press Play. The game also starts in any other scene, because `GameBootstrap` adds it if the scene doesn't have one. If the main scene gets deleted, `Tools > Powder Man > Recreate Main Scene` makes it again.

## Playing

The game opens on the **home menu**, with a live demo pouring elements in the background. Before you start it has **Play**, **Load save** (when a save exists) and **Quit**. Once you're playing, the menu has **Resume**, **Save**, **Load save**, **New sandbox** and **Quit**. The version is in the bottom-right corner.

The world fills the screen. Its height is fixed (180 cells) and its width follows the screen shape, so a wide phone gets a wider world. If the window changes shape, the world is resized and what's in it is kept.

There are **two pens**, each with its own element (Sand and Water to start). On a touch screen, the first finger draws with pen 1 and a second finger at the same time draws with pen 2. With a mouse, left draws with pen 1 and right with pen 2. Set a pen to **Eraser** to rub things out.

A slim strip down the left side holds the controls:

- **Pen 1** and **Pen 2**: tap one to open the element picker, then tap an element. Tap outside the picker to close it.
- **- 3 +**: brush size (tap the number to reset it).
- **Pause / Play** and **Step**.
- **Speed** (×0.25 to ×4), **Replace** mode, **Clear** and **Menu**.

The top line shows the element under the mouse, the particle count and the FPS.

Falling powders and liquids shimmer with a heat-haze wobble that fades once they settle. Its strength and how long it lingers are **Fall Distortion** and **Distortion Fade** in the Inspector (0 turns it off). The effect is the shader `Assets/Resources/WorldDistort.shader`.

| Input | Action |
|---|---|
| Touch | First finger draws with pen 1, a second finger with pen 2 |
| Left / right mouse | Draw with pen 1 / pen 2 |
| Mouse wheel, `[` `]` | Brush size |
| Space | Pause / play |
| `.` | Step one tick (while paused) |
| R | Replace mode: draw over things, not just into empty space |
| C | Clear |
| 1–9, Shift+1–9 | Pick the element for pen 1, pen 2 |
| F5 / F9 | Save / load (one slot, in the app's data folder) |
| Esc / Android back | Close the element picker, or go to the home menu (from the menu, quits) |

## Elements

| Element | Kind | What it does |
|---|---|---|
| Wall | solid | Indestructible. Nothing gets through it. |
| Sand | powder | Piles up and sinks through liquids. Lava melts it into Glass. |
| Water | liquid | Puts out fire, boils into Steam, turns lava into Stone. |
| Stone | solid | Lava melts it very slowly. |
| Wood | solid | Catches fire and burns as Ember. |
| Fire | gas | Rises, lights flammable things, fades into Smoke. |
| Ember | solid | Burning wood or plant. Glows, throws off fire, then crumbles. Water puts it out, back to Wood. |
| Oil | liquid | Floats on water and burns fast. |
| Lava | liquid | Slow and hot. Lights things, turns to Stone in water, melts Ice, Snow and Sand. |
| Ice | solid | Slowly freezes the water next to it. Heat melts it. |
| Snow | powder | Light, floats on water and melts in it. |
| Plant | solid | Drinks water and grows. Burns. |
| Acid | liquid | Eats everything except Glass and indestructible things, and gets used up doing it. |
| Gunpowder | powder | Explodes (small) when lit, and sets off chain reactions. |
| TNT | solid | Explodes (big) when lit. |
| Glass | solid | Acid-proof. Made by lava touching sand. |
| Steam | gas | Rises, then condenses into falling Water (rain). |
| Smoke | gas | Rises and fades. |
| Cloner | solid | Copies the first thing that touches it, forever. |
| Void | solid | Deletes anything that touches it. |

## Changing the game

Most changes go in **`Assets/Scripts/Elements.cs`**, where every element and reaction is defined.

**Add an element:** give it an id in `El`, then add it in `DefineElements()`. Element picker buttons follow the order of the `Add` calls.

```csharp
public const byte Mud = 21;   // in El

Add(El.Mud, new Element { Name = "Mud", Kind = Kind.Powder, Color = Hex("#6b4a2b"), Density = 13 });
```

**Add a reaction:** one line in `DefineReactions()`. It works both ways round. `El.Any` matches anything except Empty, itself and indestructible elements. Specific reactions should come before `Any` ones, because the first match wins.

```csharp
React(El.Water, El.Sand, El.Water, El.Mud, 0.01f);   // a, b, a becomes, b becomes, chance per tick
```

The main `Element` settings:

| Field | Meaning |
|---|---|
| `Kind` | `Solid` (never moves), `Powder` (falls and piles), `Liquid` (falls and flows), `Gas` (rises and drifts) |
| `Color`, `Color2` | Each cell gets a random shade between the two |
| `Density` | Heavier powders and liquids sink through lighter liquids and gases |
| `Spread` | How far liquids and gases slide sideways per tick |
| `MoveChance` | Below 1 makes it sluggish (Lava is 0.35) |
| `Heat` | Sets fire to neighbours. Chance per tick = Heat × their Flammability |
| `Flammability`, `BurnsInto` | How easily it catches, and what it becomes |
| `ExplodeRadius` | Explodes instead of burning |
| `LifeMin`, `LifeMax`, `DecaysInto`, `DecayChance` | Disappears after a while (Fire, Smoke, Steam, Ember) |
| `Emits`, `EmitChance` | Spawns something into empty neighbours (Ember throws off Fire) |
| `Flicker` | Re-rolls its shade every tick |
| `Indestructible` | Explosions, Acid and Void can't touch it |
| `HiddenFromToolbar` | Not in the element picker |

Other files:

- **`Assets/Scripts/Simulation.cs`**: the grid and the rules for movement, burning, explosions, reactions, the Cloner, and save/load.
- **`Assets/Scripts/PowderGame.cs`**: rendering, input (touch, mouse, keys), the side strip, the element picker, the home menu and its demo. Grid height (180), tick rate and brush limits are in the Inspector on the `Powder Game` object. Turn off **Fit Width To Screen** there to use a fixed **Grid Width** instead.
- **`Assets/Scripts/BuildInfo.cs`**: the version.
- **`Assets/Editor/AndroidBuild.cs`**: the APK build and icon setup.

## App icon

The icon is a sand stick man running. The PNGs in `Assets/Icons/` come from `Art/make_stickman_icons.py` (the `running()` pose). To change it, edit the pose and run:

```
python3 Art/make_stickman_icons.py Art/IconDrafts/stickman
```

Then copy `2_running.png`, `2_running_background.png` and `2_running_foreground.png` over `AppIcon.png`, `AppIcon_Background.png` and `AppIcon_Foreground.png`, and make `AppIcon_Round.png` (the same image cut to a circle). The other drafts are in `Art/IconDrafts/`.

## Releasing (Android / F-Droid)

1. Bump `Version` and `VersionCode` in `Assets/Scripts/BuildInfo.cs`. The code must go up or phones won't take the update.
2. Build: `Tools > Powder Man > Build APK`, or with Unity closed:
   ```
   Unity -batchmode -nographics -buildTarget Android -projectPath . -executeMethod PowderMan.EditorTools.AndroidBuild.CommandLine -logFile Logs/android-build.log
   ```
   That makes `apks/PowderMan.apk` (IL2CPP, ARM64, landscape, debug-keystore signed).
3. Copy it to `~/APKs/` and run `~/fdroid/release.sh`.
