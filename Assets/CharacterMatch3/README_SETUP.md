# Character Match-3 Setup

This folder contains a self-contained portrait-mobile match-3 game module.

## Character PNGs

Place five transparent PNG character-head images in:

`Assets/char/`

Expected filenames are:

- `cat.png`
- `bunny.png`
- `dino.png`
- `penguin.png`
- `bear.png`

The generator also matches the existing `Assets/Char/` folder and filename casing on Windows, but the lowercase names above are the recommended portable layout.

## First Setup

In Unity, run:

`Character Match-3 > Setup Complete Game`

The command is safe to run more than once. It creates or updates folders, the character catalog, scoring config, 50 level assets, prefabs, placeholder materials, scenes, build settings, and validation logs.

## Playing A Level

Open:

`Assets/CharacterMatch3/Scenes/Boot.unity`

Press Play. The Boot scene loads the level map. Level 1 is unlocked by default; winning unlocks the next level.

To test a specific level, select a `Level_###.asset` and use:

`Character Match-3 > Level Tools > Play Selected Level`

## Editing Levels

Level assets live in:

`Assets/CharacterMatch3/Levels/`

Each `LevelDefinition` exposes board size, active-cell mask, move limit, available characters, seed, goals, blockers, locks, companions, exits, pre-placed pieces, star thresholds, tutorials, difficulty label, theme ID, and reshuffle settings.

After editing, use:

`Character Match-3 > Level Tools > Validate Selected Level`

or:

`Character Match-3 > Level Tools > Validate All 50 Levels`

## Board Previews

Select a level and run:

`Character Match-3 > Level Tools > Generate Board Preview`

Preview PNGs are saved under:

`Assets/CharacterMatch3/Art/Previews/`

## Replacing Placeholder Art And Audio

Normal pieces use the sprites in `CharacterCatalog.asset`. Special pieces, blockers, exits, and UI panels use simple generated UI placeholders so the game is readable immediately.

Audio is managed by `AudioManager`. Assign clips for swap, invalid swap, match, cascade, line clear, burst, rainbow, blocker break, token delivered, win, lose, and button click. The game runs correctly with all fields empty.

## Building For Android And iOS

1. Run `Setup Complete Game`.
2. Open `File > Build Profiles`.
3. Select Android or iOS.
4. Confirm the scenes are listed in this order:
   - `Boot`
   - `LevelMap`
   - `Gameplay`
5. Use portrait orientation. The setup command configures portrait defaults.
6. Build normally for the selected platform.

## Known Limitations

- Visuals and audio are polished placeholders, not final production art.
- The difficulty analyzer is heuristic; it flags suspicious levels but does not mathematically prove solvability.
- Gravity is vertical within board columns and respects inactive cells/crates as fixed blockers.
- Tutorial highlights use a lightweight overlay and forced-swap gate; they are intentionally data-driven so they can be refined per level.
