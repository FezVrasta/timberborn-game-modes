using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Timberborn.GameSceneLoading;
using Timberborn.Localization;
using UnityEngine;

namespace GameModes
{
    /** A way to play a new game that a mod adds, picked on the New Game screens. */
    public class GameMode
    {
        public string Id;
        public string LabelLocKey;
        public string DescriptionLocKey;
        // Its line in a game played alone, where it isn't the one it has with others
        public string AloneDescriptionLocKey;
        // Where it's offered: alone (the game's own Start), with others (a mod's own way to start one), or both
        public bool Alone = true;
        public bool WithOthers = true;
        // The picture behind the New Game screens while it's picked, and behind the loading screen of a game in it,
        // each the path of an image file, or null for none (see Modes.SetBackground)
        public Func<string> MenuPicture;
        public Func<string> LoadingPicture;

        public string Label(ILoc loc) => loc.T(LabelLocKey);

        public string DescriptionKey(bool alone) => alone && AloneDescriptionLocKey != null ? AloneDescriptionLocKey : DescriptionLocKey;
    }

    /**
     * The game modes mods add, and the one each new game is started in.
     * Classic is the game as it comes, and the only mode until a mod adds
     * one: then the New Game screens' last page has a column to pick one
     * (see GameModeColumn), and the pick is handed to the mods as the game
     * loads (Game, GameAlone) and recorded with it (Record).
     *
     * A mod that starts games with others (a multiplayer mod) says when the
     * page is for one (PageAlone), and starts it with StartWith.
     */
    public static class Modes
    {
        public const string Classic = "gamemodes.classic";
        // The record's keys, in the save's metadata
        public const string RecordKey = "bb.gamemode";
        public const string AloneKey = "bb.alone";
        private const string On = "on";

        public static readonly List<GameMode> All = new()
        {
            new GameMode
            {
                Id = Classic, LabelLocKey = "GameModes.Classic", DescriptionLocKey = "GameModes.Classic.Description", WithOthers = false,
            },
        };

        // Whether the New Game screens are making a game played alone; a multiplayer mod says when they aren't
        public static Func<bool> PageAlone = () => true;

        // Told when a mode is picked
        public static event Action Picked;

        /**
         * Told as each game starts loading, with its record (the mode's keys and the settings'; see
         * GameRecord) for a new game, or the record in its save for a saved one (null if it has none).
         * Mods that act on how a game was made, before its services are put together, listen here.
         */
        public static event Action<IReadOnlyDictionary<string, string>, bool> GameLoading;

        internal static void RaiseGameLoading(IReadOnlyDictionary<string, string> record, bool newGame)
        {
            _currentRecord = record != null ? new Dictionary<string, string>(record) : new Dictionary<string, string>();
            GameLoading?.Invoke(_currentRecord, newGame);
        }

        /**
         * The record of the game loading, and then loaded (see GameRecord): its mode and settings, there
         * from the first moment of loading, a new game's picks or a save's own. A save from before records
         * has none, until a mod completes it (Complete).
         */
        // Never changed once it's in place: a completed record is a new one that replaces it, so threads
        // that read it (the server, say) always see a whole one
        private static volatile Dictionary<string, string> _currentRecord = new();
        public static IReadOnlyDictionary<string, string> CurrentRecord => _currentRecord;

        /** The loaded game's mode, or null for a game started without one. */
        public static string Current => ModeOf(CurrentRecord);

        /** Whether the loaded game is played alone. */
        public static bool CurrentAlone => IsAlone(CurrentRecord);

        /**
         * Puts a value in the loaded game's record where it has none: a mod that kept a setting's value
         * with the game itself before records existed hands it over as that older save loads.
         */
        public static void Complete(string id, string text)
        {
            if (!_currentRecord.ContainsKey(id)) Merge(new[] { new KeyValuePair<string, string>(id, text) });
        }

        // The record with these added (they win over what's there)
        internal static void Merge(IEnumerable<KeyValuePair<string, string>> values)
        {
            var merged = new Dictionary<string, string>(_currentRecord);
            foreach (var pair in values) merged[pair.Key] = pair.Value;
            _currentRecord = merged;
        }

        public static void Add(string id, string labelLocKey, bool alone = true, bool withOthers = true)
        {
            if (All.Any(m => m.Id == id)) return;
            All.Add(new GameMode { Id = id, LabelLocKey = labelLocKey, Alone = alone, WithOthers = withOthers });
        }

        /**
         * The pictures a game mode has behind the screens: the New Game screens while it's the mode picked, and the
         * loading screen of a game in it, new or saved. Each is a function returning the path of an image file
         * (a JPG or PNG in the mod's folder) when the screen shows, or null for none, so a mod can change its
         * picture with the date or its settings. Without one, the game's own picture stays.
         */
        public static void SetBackground(string id, Func<string> menu = null, Func<string> loading = null)
        {
            if (Get(id) is not { } mode) return;
            mode.MenuPicture = menu;
            mode.LoadingPicture = loading;
        }

        public static GameMode Get(string id) => All.FirstOrDefault(m => m.Id == id);

        public static void Describe(string id, string descriptionLocKey)
        {
            if (Get(id) is { } mode) mode.DescriptionLocKey = descriptionLocKey;
        }

        public static void DescribeAlone(string id, string descriptionLocKey)
        {
            if (Get(id) is { } mode) mode.AloneDescriptionLocKey = descriptionLocKey;
        }

        /** The modes offered for a game alone or with others, in the order they were added. */
        public static List<GameMode> For(bool alone) => All.Where(m => alone ? m.Alone : m.WithOthers).ToList();

        // A mode's name, or its id for one no mod has added
        public static string Label(ILoc loc, string id) => Get(id)?.Label(loc) ?? id;

        private static readonly Dictionary<bool, string> PickedFor = new();

        /** What this player picked last time for a game alone or with others, or the first mode offered. */
        public static string PickedMode(bool alone)
        {
            List<GameMode> offered = For(alone);
            if (offered.Count == 0) return null;
            if (!PickedFor.TryGetValue(alone, out string id))
            {
                id = PlayerPrefs.GetString(PrefsKey(alone), null);
                PickedFor[alone] = id;
            }
            return offered.Any(m => m.Id == id) ? id : offered[0].Id;
        }

        public static void Pick(string id, bool alone)
        {
            PickedFor[alone] = id;
            PlayerPrefs.SetString(PrefsKey(alone), id);
            Picked?.Invoke();
        }

        private static string PrefsKey(bool alone) => "GameModes.Picked." + (alone ? "Alone" : "WithOthers");

        // The mode for the game starting on this machine, set right before it starts
        private static string _armed;
        private static bool _armedAlone;

        /** The new game about to start is played in this mode, alone or with others. */
        public static void StartWith(string mode, bool alone)
        {
            _armed = mode;
            _armedAlone = alone;
            Debug.Log($"[GameModes] Starting a game in {mode}, {(alone ? "alone" : "with others")}");
        }

        /** The mode of the new game loading on this machine, or null for a saved game or one started without one. */
        public static string Game { get; private set; }
        /** Whether it's played alone. */
        public static bool GameAlone { get; private set; } = true;

        internal static void Starting(bool newGame)
        {
            Game = newGame ? _armed : null;
            GameAlone = !newGame || _armed == null || _armedAlone;
            _armed = null;
        }

        /** The new game's mode as it's recorded with it, nothing for one without. */
        public static Dictionary<string, string> Record()
        {
            var record = new Dictionary<string, string>();
            if (Game == null) return record;
            record[RecordKey] = Game;
            if (GameAlone) record[AloneKey] = On;
            return record;
        }

        // A record's mode, or null for a game without one
        public static string ModeOf(IReadOnlyDictionary<string, string> record) =>
            record != null && record.TryGetValue(RecordKey, out string mode) ? mode : null;

        // Whether a record's game is played alone (one without a mode is)
        public static bool IsAlone(IReadOnlyDictionary<string, string> record) =>
            ModeOf(record) == null || record.ContainsKey(AloneKey);
    }

    [HarmonyPatch]
    class ModesStartingPatcher
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartNewGame));
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartNewGameInstantly));
        }

        static void Prefix() => Modes.Starting(newGame: true);
    }

    [HarmonyPatch]
    class ModesSaveStartingPatcher
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartSaveGame));
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartSaveGameInstantly));
        }

        static void Prefix() => Modes.Starting(newGame: false);
    }
}
