# Hello Mode

A sample mod for [Game Modes](../../README.md): it adds one game mode, **Hello**, with one game setting, and greets you when a game in that mode starts. It's small on purpose, to show the pieces a mod needs and nothing more.

## What it does

- A **Hello** entry in the Game Mode column of the New Game screens, with a line about it under the difficulty's own.
- A **Greeting** setting (Hello, Hi or Hey) under a "Hello Mode" heading in Customize. It only shows for games started in the Hello mode.
- A sunrise picture behind the New Game screens while Hello is picked, and behind the loading screen of a Hello game (new or saved).
- When a Hello game has loaded, a notification greets the beavers with the picked word. A saved Hello game greets the same way when it's loaded again.

## How it's put together

| File | What it's for |
|---|---|
| `manifest.json` | Requires `gamemodes` (at least the version with the APIs used here) and Harmony |
| `Plugin.cs` | Adds the mode and the setting when the game starts, and binds the greeter |
| `Greeter.cs` | A service that reads the loaded game's mode and setting |
| `Hello.jpg` | The picture behind the screens, copied next to the mod's DLL by the build |
| `Localizations/enUS_HelloMode.csv` | The text: mode name and line, heading, setting and its values |
| `HelloMode.csproj` | References `GameModes.dll` from the Mods folder, and installs the mod there |

### Adding the mode

```csharp
Modes.Add(Mode, "HelloMode.GameMode.Hello");
Modes.Describe(Mode, "HelloMode.GameMode.Hello.Description");
```

`Modes.Add` takes an id (yours alone, so prefix it with the mod's) and the loc key of its name. A mode is offered for games played alone and with others (`alone` and `withOthers` say otherwise). Game Modes shows the column once two modes are offered, and remembers the player's pick. `Describe` is the line under the pick; `DescribeAlone` gives a different one for a game played alone.

### Adding a game setting

```csharp
SettingGroup rules = GameSettings.Group("HelloMode.Settings").When(kind => kind.Is(Mode));
Greeting = rules.Choice("hellomode.greeting", "HelloMode.Settings.Greeting", "hello", "hello", "hi", "hey");
```

`GameSettings.Group` is the mod's heading in the Customize list (a loc key), and `When` says which games its settings are for: here only those in the Hello mode (`GameKind` also says alone or with others). The group makes the setting: `Choice(id, labelKey, default, values...)`. Its text keys follow from the label's: `HelloMode.Settings.Greeting` is the name, `.Tooltip` what hovering it says, and `.hello`, `.hi` and `.hey` the values. There's `Toggle`, `Slider` and `List` as well, and fluent calls (`Tip`, `Small`, `Presets` and others) to change the defaults. See [Game Settings](../../README.md#game-settings).

Players' picks are remembered between games and saved with each game, so you never write any saving code.

### Adding a picture

```csharp
string picture = Path.Combine(modEnvironment.ModPath, "Hello.jpg");
Modes.SetBackground(Mode, menu: () => picture, loading: () => picture);
```

`menu` is the picture behind the New Game screens while the mode is picked there, and `loading` the one behind the loading screen of a game in the mode, new or saved. Each is a function returning the path of an image file (a JPG or PNG), asked as the screen shows, so a mod can change its picture with the date or one of its settings, or return null for none (the game's own picture stays). Either can be left out. Game Modes reads the file, covers the screen with it and puts what was there back when the mode is no longer picked.

### Reading them

```csharp
if (Modes.Current != Plugin.Mode) return;            // the loaded game's mode
string greeting = Plugin.Greeting.Current;           // its pick for the setting
```

`Modes.Current` and `setting.Current` come from the game's record (its mode and settings, saved with it), and they're there from the first moment a game loads, new or saved. Use `setting.Value` where only a new game's pick matters.

## Building

1. Build and install Game Modes first: `dotnet build GameModes/GameModes.csproj` in the repo root. It installs itself into `Documents/Timberborn/Mods/GameModes`.
2. Copy `env.props.*-template` to `env.props` here and point it at Timberborn and Harmony, as for Game Modes.
3. `dotnet build Samples/HelloMode/HelloMode.csproj`. It installs into `Documents/Timberborn/Mods/HelloMode`; turn it on in the game's mod list, next to Game Modes.

`GameModesDll` overrides where the build looks for `GameModes.dll`.

## Trying it

Start a new game, pick **Hello** in the Game Mode column, open **Customize** to choose the greeting, and start. The notification shows when the game has loaded.
