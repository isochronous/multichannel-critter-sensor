# Multichannel Critter Sensor

An [Oxygen Not Included](https://www.klei.com/games/oxygen-not-included) mod that adds a **Multichannel Critter Sensor**: a room-based critter/egg counter that outputs over an **Automation Ribbon** and lets you choose exactly which species and egg types to count.

Status: **work in progress**, with custom art: the sprite in `publish/sprite-small.png` (128 px, about the game's native resolution for a one-cell building) is turned into the building's kanim (`src/MultichannelCritterSensor/anim/assets/multichannel_critter_sensor/`) by `common/tools/MakeKanim`, and drives the in-game sprite, construction ghost, and build-menu icon.

## What it does

The sensor counts critters and eggs in its room, like the vanilla Critter Sensor, and writes a 4-bit value to its ribbon output port:

| Bit | Combined-threshold mode | Separate-thresholds mode |
|---|---|---|
| 1 | Green when the combined count passes the threshold | Green when the **critter** count passes its threshold |
| 2 | Off | Green when the **egg** count passes its threshold |
| 3 | One logic tick (100 ms) green pulse each time the tracked count rises | Same |
| 4 | One logic tick (100 ms) green pulse each time the tracked count falls | Same |

- **Species filters.** Ticking *Count Critters* shows the list of discovered baggable species (the same list as the Critter Pick-Up). Ticking *Count Eggs* shows discovered egg types (the incubator's list). Each list has an *All* row; with *All* on, newly discovered species count automatically. Unticking a single row from the *All* state keeps everything else selected.
- **Thresholds** use the vanilla threshold controls (above/below, +/- 1, +/- 10, number field, slider, 0 to 64).
- **Pulses** fire once per 200 ms scan in which the tracked total changed, regardless of how many critters arrived or left in that window. Consecutive pulses are always separated by at least one low tick. A hatch (one egg out, one critter in) leaves the total unchanged and fires nothing. Configuration edits never fire a pulse.
- Connect the output with an Automation Ribbon; a plain automation wire overloads when more than one bit is on, as with the vanilla Ribbon Writer.
- Supports the copy-settings tool; all settings are saved with the building.

Unlocked by **Multiplexing** (two research tiers past the vanilla sensor's Animal Control). Costs 50 kg Refined Metal and 50 kg Plastic. Found under Automation > Sensors, next to the vanilla Critter Sensor.

## Publishing

Steam Workshop item **3804039425**. `publish/content` holds the upload set (DLL, `mod.yaml`, `mod_info.yaml`, `preview.png`, `anim/`) and `publish/workshop-description.txt` the Steam-markup description. Update it by zipping the contents of `publish/content` and running `common/tools/WorkshopUpload update 3804039425 <zip> publish/preview.png`, or with Klei's **Oxygen Not Included Uploader** (Steam Library > Tools), never with steamcmd; see the [oni-mods-common README](https://github.com/isochronous/oni-mods-common#publishing-to-the-steam-workshop) for why. `publish/preview.png` is `publish/logo.jpg` resized to 512x512.

## Building

Requires the .NET SDK (8+). Shared build configuration lives in the [oni-mods-common](https://github.com/isochronous/oni-mods-common) submodule, so clone with `--recurse-submodules` (or run `git submodule update --init`). The game DLLs are referenced directly from the game install; override the path if yours differs:

```
dotnet build src/MultichannelCritterSensor -c Release -p:GameFolder="<path-to>\OxygenNotIncluded"
```

A successful build merges [PLib](https://github.com/peterhaneve/ONIMods/tree/main/PLib) into the DLL and deploys the mod to `Documents\Klei\OxygenNotIncluded\mods\local\MultichannelCritterSensor` (disable with `-p:ModDeployFolder=none`).

## Implementation notes

- `MultichannelCritterSensor` (the building component) walks the room cavity's creature and egg lists every 200 ms, filtering each entry by prefab tag, and sends the packed value through a `RibbonOutputPort`. Pulse bits are cleared from the circuit manager's `onLogicTick` callback, which fires right after the network samples sender values, so each pulse is read by exactly one tick.
- The side screen is built with PLib UI. The three threshold editors are runtime clones of the vanilla `ThresholdSwitchSideScreen` prefab, each targeting a small `IThresholdSwitch` adapter object, so they look and behave exactly like the stock sensor's controls.
- Patch points: `GeneratedBuildings.LoadGeneratedBuildings` (plan screen), `Db.Initialize` (tech), `DetailsScreen.OnPrefabInit` (side screen registration).
