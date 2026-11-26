# Powder Man

A falling-sand sandbox for Android and desktop, made in Unity. Pour sand, water, lava and more onto the screen and watch them fall, flow, burn, melt and explode into each other.

- **Version:** 1.7 (build 8). It lives in `Assets/Scripts/BuildInfo.cs` and shows on the home menu.
- **Android id:** `com.Tony3974.PowderMan`, released on the F-Droid repo at fdroid.tonycad.com.
- **Unity:** 6000.0.85f1, built-in render pipeline, no extra packages.

## Running it

Open the folder in Unity Hub, open `Assets/Scenes/Main.unity` and press Play. The game also starts in any other scene, because `GameBootstrap` adds it if the scene doesn't have one. If the main scene gets deleted, `Tools > Powder Man > Recreate Main Scene` makes it again.

## Playing

The game opens on the **home menu**, with a live demo pouring elements in the background. Before you start it has **Play**, **Load save** (when a save exists), **Help** and **Quit**. Once you're playing, the menu has **Resume**, **Save**, **Load save**, **New sandbox**, **Help** and **Quit**. The version is in the bottom-right corner.

**Help** shows every element in a grid. Tap one to read what it does and what it reacts with. **How to play** at the top explains the controls. When the text is too long for the panel, drag it (or use the mouse wheel) to scroll.

The world fills the screen. Its height is fixed (180 cells) and its width follows the screen shape, so a wide phone gets a wider world. If the window changes shape, the world is resized and what's in it is kept.

There is **one pen with two saved slots, A and B** (Sand and Water to start), so you can flip between two elements fast. **Tap** the other slot to draw with it. **Tap the active slot again** to open the element picker and change what it holds. The active slot has a gold frame and the other is dimmed. Your A, B and active slot are remembered between runs. One finger draws, and extra fingers are ignored. With a mouse, left draws and right switches between A and B. Set a slot to **Eraser** to rub things out.

The element picker has a row of **tabs**: Basic, Earth, Heat, Boom, Life, Sky, Odd, and All (every element at once). It opens on the tab holding the slot's element, and a dot marks the tabs holding A's and B's elements. The Eraser is the last button in every tab.

A slim strip down the left side holds the controls:

- **A** and **B**: tap the other one to switch, tap the active one to pick its element (pick a tab, then an element). Tap outside the picker to close it.
- **- 3 +**: brush size (tap the number to reset it).
- **Pause / Play** and **Step**.
- **Speed** (×0.25 to ×4), **Replace** mode, **Heat view**, **Clear** and **Menu**.

The top line shows the element under the mouse and its temperature, the particle count and the FPS.

The pen sprays powders, liquids and gases as separate grains rather than solid blobs (solids like Wall and Wood still draw solid, and brush size 0 always places its grain). Falling grains drift a little sideways as they drop, so streams spread out into loose dots. How thick the spray is is **Spray Density** in the Inspector, and the drift is each element's `Scatter`.

| Input | Action |
|---|---|
| Touch | One finger draws with the active slot |
| Tap A or B | Switch to it, or if it's already active, change its element |
| Left mouse | Draw with the active slot |
| Right mouse, Tab | Switch between A and B |
| Mouse wheel, `[` `]` | Brush size |
| Space | Pause / play |
| `.` | Step one tick (while paused) |
| R | Replace mode: draw over things, not just into empty space |
| H | Heat view |
| C | Clear |
| 1–9 | Pick one of the first nine Basic elements for the active slot |
| F5 / F9 | Save / load (one slot, in the app's data folder) |
| Esc / Android back | Close the element picker or Help, or go to the home menu (from the menu, quits) |

## Temperature

Every cell has a temperature, air included, and it moves with the cell. Room temperature is 20°.

- Heat flows into whatever is touching, at the speed of the poorer conductor of the two. Metal carries heat fastest, Water and Ice fairly well, and air, Wood, Stone and living things hardly at all. A metal bar with one end in Lava gets hot along its length, and a metal pot over a fire boils its Water.
- Fire (800°), Ember, Burning Coal, Lava (1200°), Burning Thermite and Lightning keep themselves hot. Ice (-15°) and Snow keep themselves cold, so they last in room air until something warm reaches them. Air drifts back to 20°.
- Things change when they get hot or cold enough: Ice and Snow melt, Water and Salt Water boil, Mud and Wet Sponge dry, Clay bakes, Metal, Glass, Sand and Stone melt, and Molten Metal and Molten Glass set again. Melting and boiling are slow: the cell sits at that temperature while it soaks up extra heat (`Latent`), so a pool of Water on a Lava crust steams away over a minute or two, not at once.
- Things that burn catch fire on their own when hot enough (Wood 300°, Gunpowder 200°, Nitro 120°).
- Anything hotter than about 450° glows dark red, then orange, then yellow-white. **Heat view** colours the whole world by temperature: blue is cold, dark is 20°, then purple, red, orange, yellow and white. The Help screen's tag line gives each element's temperatures.
- Freezing Water is still the Ice reaction, not temperature, so a cube of Ice in a pool spreads at the same speed as before.

The heat pass runs every second tick, in `UpdateHeat` in `Simulation.cs`. Saves keep the elements but not their temperatures.

## Electricity

Metal, Molten Metal and Salt Water carry electricity. When Lightning hits one, a flash runs through every conductor touching it, 4 cells a tick, lighting each cell up white as it passes. Each cell it passes warms by 30°, and the cells next to it get shocked: things that burn may catch fire (explosives go off) and Ants and Worms are fried to Ash. Each strike is a separate charge that goes through each cell once, so it always ends, even in moving Salt Water. It's in `Simulation.cs` (`Charge`, `UpdateCharge`), and an element carries it when `Electric` is set.

## Elements

| Element | Kind | What it does |
|---|---|---|
| Wall | solid | Indestructible. Nothing gets through it, and it can hold Antimatter. |
| Sand | powder | Piles up and sinks through liquids. Lava or Lightning turns it into Glass. |
| Water | liquid | Puts out fire, boils into Steam (at 100°), turns lava into Stone. Makes Mud with Dirt and Salt Water with Salt. Grows Plants and Moss, rusts Metal, drowns Ants and Worms. |
| Stone | solid | Lava melts it very slowly, Burning Thermite quickly. Moss creeps over it. |
| Wood | solid | Catches fire and burns as Ember. Fungus eats it. |
| Fire | gas | Rises, lights flammable things, fades into Smoke. Melts Ice and Snow, dries Mud, bakes Clay. |
| Ember | solid | Burning wood or plant. Glows, throws off fire, then crumbles into Ash or Smoke. Water puts it out, back to Wood. |
| Oil | liquid | Floats on water and burns fast. |
| Lava | liquid | Slow and hot. Lights things, turns to Stone in water, melts Ice (sometimes into Obsidian) and Snow, turns Sand into Glass, melts Metal and Glass. |
| Ice | solid | Slowly freezes the water next to it. Heat, Salt and Salt Water melt it. |
| Snow | powder | Light, floats on water and melts in it. |
| Plant | solid | Drinks water and grows, and keeps sprouting on Mud. Burns. Ants, Fungus and Poison Gas kill it. |
| Acid | liquid | Eats everything except Glass, Obsidian and indestructible things (Brick only slowly), and gets used up doing it. Gives off Hydrogen from Metal and fizzes Bubbles in Water. |
| Gunpowder | powder | Explodes (small) when lit, and sets off chain reactions. |
| TNT | solid | Explodes (big) when lit. |
| Glass | solid | Acid-proof. Made by Lava or Lightning hitting Sand. Lava and Thermite melt it into Molten Glass. |
| Steam | gas | Rises, then condenses into falling Water (rain). Clouds gather it. |
| Smoke | gas | Rises and fades. |
| Cloner | solid | Copies the first thing that touches it, forever. |
| Void | solid | Deletes anything that touches it. |
| Dirt | powder | Water turns it into Mud. Seeds sprout on it. |
| Mud | powder | Heavy and slow. Heat dries it back into Dirt. Lets off a little Methane. |
| Clay | solid | Fire or Lava bakes it into Brick. Water slowly softens it into Mud. |
| Brick | solid | Baked Clay. Mostly holds up to Acid. |
| Salt | powder | Melts Ice and Snow. Dissolves in Water into Salt Water. |
| Salt Water | liquid | Heavier than Water, doesn't freeze, leaves Salt when it boils, rusts Metal faster. Carries electricity. |
| Ash | powder | Left by burnt Wood and Plant. Floats on Water. |
| Obsidian | solid | Lava touching Ice. Acid-proof. |
| Metal | solid | Carries heat and electricity. Lava melts it into Molten Metal. Water rusts it. Acid on it gives off Hydrogen. |
| Molten Metal | liquid | Hot, lights things, carries electricity, sets back into Metal below 1150° and glows as it cools. |
| Rust | powder | Wet Metal crumbles into it. |
| Coal | powder | Hard to light, then burns for a long time and leaves Ash. |
| Thermite | powder | Burns white-hot and melts through Metal, Stone, Brick and Glass. Water doesn't stop it. |
| Molten Glass | liquid | Lava melts Glass into it. Sets back into Glass. |
| Seed | powder | Sprouts into a growing Plant on Mud (fast) or Dirt (slow). |
| Vine | solid | Grows down from wherever you put it, up to 40 cells. Burns fast. |
| Moss | solid | Creeps over Stone. Water slowly grows it on Stone. |
| Fungus | solid | Eats Wood and Plant. Lets off Methane. |
| Ant | creature | Walks, climbs walls and digs through soft ground. Eats Seeds and breeds on Plants. Drowns and burns. |
| Worm | creature | Wriggles through Dirt, Mud and Sand. Turns Ash into Dirt. |
| Cloud | gas | Hangs at the top for a couple of minutes, raining. Gathers Steam. |
| Lightning | bolt | Zig-zags down, lights what it hits, fuses Sand into Glass, electrifies Metal and Salt Water. |
| Methane | gas | Rises. Explodes when lit. |
| Hydrogen | gas | Very light. Goes pop when lit. |
| Poison Gas | gas | Kills Plants, Seeds, Vines, Moss, Ants and Worms. Water soaks it up. |
| Bubble | gas | Rises through liquids and pops at the surface. |
| Nitro | liquid | Explodes when lit, or when it lands after falling more than 12 cells. |
| Sponge | solid | Soaks up Water. Heat dries it out. |
| Bouncy Ball | powder | Bounces when it lands, lower each time. |
| Antimatter | powder | Wipes out whatever it touches, and itself. Only indestructible things can hold it. |
| Black Hole | solid | Pulls in everything within 14 cells and deletes it. |
| Portal In / Out | solid | Anything touching an In portal comes out of an Out portal. |

## Changing the game

Most changes go in **`Assets/Scripts/Elements.cs`**, where every element and reaction is defined.

**Add an element:** give it an id in `El`, then add it in `DefineElements()`. It goes in the picker tab named by the `Group(...)` call above it, or set `Category` to put it in another tab (the tabs are listed in `Categories`). Within a tab, buttons follow the order of the `Add` calls. Then write its Help text in `Assets/Scripts/ElementHelp.cs`.

```csharp
public const byte Tar = 59;   // in El, the next free id

Add(El.Tar, new Element { Name = "Tar", Kind = Kind.Liquid, Color = Hex("#1c1a17"), Density = 12, MoveChance = 0.2f, Flammability = 0.1f });
```

**Add a reaction:** one line in `DefineReactions()`. It works both ways round. `El.Any` matches anything except Empty, itself and indestructible elements. Reactions are tried in order and the first one that happens wins, so put specific reactions before `Any` ones.

```csharp
React(El.Tar, El.Sand, El.Tar, El.Stone, 0.01f);   // a, b, a becomes, b becomes, chance per tick
```

The main `Element` settings:

| Field | Meaning |
|---|---|
| `Kind` | `Solid` (never moves), `Powder` (falls and piles), `Liquid` (falls and flows), `Gas` (rises and drifts) |
| `Color`, `Color2` | Each cell gets a random shade between the two |
| `Density` | Heavier powders and liquids sink through lighter liquids and gases |
| `Spread` | How far liquids and gases slide sideways per tick |
| `MoveChance` | Below 1 makes it sluggish (Lava is 0.35) |
| `Scatter` | Chance per tick a falling powder or liquid drifts sideways while in the air (default 0.15, 0 = falls straight) |
| `Heat` | Sets fire to neighbours. Chance per tick = Heat × their Flammability |
| `Flammability`, `BurnsInto` | How easily it catches, and what it becomes |
| `ExplodeRadius` | Explodes instead of burning |
| `LifeMin`, `LifeMax`, `DecaysInto`, `DecayChance`, `DecaysElse` | Disappears after a while (Fire, Smoke, Steam, Ember). Life is at most 255 ticks: set `DecaysElse` to the element itself to make it last longer (Cloud, Molten Metal) |
| `Emits`, `EmitChance` | Spawns something into empty neighbours (Ember throws off Fire) |
| `Flicker` | Re-rolls its shade every tick |
| `Temperature` | What a fresh cell starts at, in °C (default 20) |
| `Hold` | 0 to 1: how hard it pulls itself back to `Temperature` each tick (Fire, Lava, Ice; air is 0.02) |
| `Conductivity` | 0 to 1: how fast heat moves through it (Metal 1, Water 0.5, air 0.02, default 0.2) |
| `HeatsAt`, `HeatsInto`, `HeatsChance`, `HeatsElse` | Hotter than `HeatsAt` it becomes `HeatsInto` (or, if `HeatsChance` fails, `HeatsElse`) |
| `Latent` | Extra heat, in degrees, it soaks up at `HeatsAt` before it changes (uses the cell's life, so not for things that decay) |
| `CoolsAt`, `CoolsInto` | Colder than `CoolsAt` it becomes `CoolsInto` |
| `IgnitesAt` | Things that burn catch fire on their own above this (default 300) |
| `Electric` | Carries electricity (Metal, Molten Metal, Salt Water) |
| `Indestructible` | Explosions, Acid, Void, Antimatter and Black Holes can't touch it |
| `HiddenFromToolbar` | Not in the element picker |
| `Category` | Which picker tab it's in (default: the `Group` it was added under) |

Other files:

- **`Assets/Scripts/Simulation.cs`**: the grid and the rules for movement, burning, explosions, reactions, temperature, electricity, save/load, and the elements that need their own code (Cloner, Bouncy Ball, Nitro, Bubble, Lightning, Black Hole, Portals, Ant, Worm, Vine and sprouting Seeds, in `UpdateSpecial`).
- **`Assets/Scripts/PowderGame.cs`**: rendering (including the glow of hot things and Heat view), input (touch, mouse, keys), the side strip, the element picker and its tabs, the home menu and its demo. Grid height (180), tick rate, brush limits and spray density are in the Inspector on the `Powder Game` object. Turn off **Fit Width To Screen** there to use a fixed **Grid Width** instead.
- **`Assets/Scripts/ElementHelp.cs`**: the Help screen text for each element. Update it when you change what an element does.
- **`Assets/Scripts/BuildInfo.cs`**: the version.
- **`plan.md`**: ideas, what's done, and what still needs checking.
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
