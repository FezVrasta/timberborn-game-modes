# Game Modes

![Game Modes](Media/cover.png)

A game mode picker for Timberborn's New Game screens, for mods that add ways to play.

On its own it changes nothing: with no mod adding a mode, New Game is the game's own. [BeaverBuddies Co-Op/PvP Edition](https://github.com/FezVrasta/BeaverBuddies-CoOp-PvP-Edition) and [Timber Empires](https://steamcommunity.com/sharedfiles/filedetails/?id=3814012422) add theirs.

## For mod authors

Depend on it in your manifest (`{ "Id": "gamemodes" }`), reference `GameModes.dll` and register your modes when the game starts:

```csharp
GameModes.Modes.Add("mymod.race", "MyMod.GameMode.Race", alone: true, withOthers: false);
GameModes.Modes.Describe("mymod.race", "MyMod.GameMode.Race.Description");
GameModes.Modes.DescribeAlone("mymod.race", "MyMod.GameMode.Race.Description.Alone");
```

A mode shows on the page when it's offered for what the page is making (a game alone, or with others, see `PageAlone`), and the column shows once there are two to pick from. The new game's mode is read while it loads:

| | |
|---|---|
| `Modes.Game` | the mode the new game loading was started in, null for a saved game |
| `Modes.GameAlone` | whether it's played alone |
| `Modes.Record()` | the mode as it's saved with the game (`bb.gamemode`, `bb.alone`) |
| `Modes.ModeOf(record)` | a saved game's mode |
| `Modes.PickedMode(alone)` | what the player has picked on the page |
| `Modes.Picked` | event, a pick was made |

A mod that starts games with others (multiplayer) sets `Modes.PageAlone` to say when the page is for one, and starts it with `Modes.StartWith(mode, alone: false)`. `GameModeColumn.Column` and `GameModeColumn.Button` build more columns that look like the difficulties'.

`Classic` (`gamemodes.classic`) is the game as it comes, offered for games alone.

## Building

Copy `GameModes/env.props.*-template` to `GameModes/env.props` and point it at Timberborn and Harmony, then `dotnet build GameModes/GameModes.csproj`. It installs into `Documents/Timberborn/Mods/GameModes`.

## License

GPL-3.0, see [LICENSE](LICENSE).
