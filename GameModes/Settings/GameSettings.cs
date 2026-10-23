using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSceneLoading;
using Timberborn.Localization;
using Timberborn.MainMenuPanels;
using Timberborn.NewGameConfigurationSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameModes
{
    /**
     * A mod's heading in the Customize list, and the way to define its
     * settings under it. Every setting made through it starts out applying
     * to the kinds of game the group does (all, unless When says), which the
     * setting's own When can change.
     */
    public class SettingGroup
    {
        public string Heading { get; }
        public Func<GameKind, bool> Applies { get; private set; }

        internal SettingGroup(string heading) => Heading = heading;

        /** The settings made after this are offered for these kinds of game only. */
        public SettingGroup When(Func<GameKind, bool> applies)
        {
            Applies = applies;
            return this;
        }

        public ToggleSetting Toggle(string id, string labelKey, bool @default = true) =>
            GameSettings.Register(new ToggleSetting(id, this, labelKey, @default));

        /** One of a few values, a checkbox each; their loc keys are the label's + "." + the value. */
        public ChoiceSetting Choice(string id, string labelKey, string @default, params string[] values) =>
            GameSettings.Register(new ChoiceSetting(id, this, labelKey, @default, values));

        /** A whole number from min to max in steps of step. */
        public SliderSetting Slider(string id, string labelKey, int min, int max, int step, int @default) =>
            GameSettings.Register(new SliderSetting(id, this, labelKey, min, max, step, @default));

        /**
         * Up to maxItems picks of a few values, each its own row (itemKey names
         * the row and takes its number: "Opponent {0}"; the values' keys are
         * itemKey + "." + the value), with buttons to add one or take one off.
         */
        public ListSetting List(string id, string labelKey, string itemKey, int maxItems, string itemDefault, params string[] values) =>
            GameSettings.Register(new ListSetting(id, this, labelKey, itemKey, maxItems, itemDefault, values));
    }

    /**
     * The settings mods add to new games, and what's been picked for them:
     * each player's picks are remembered, and a new game started on this
     * machine takes them for the mods to read as it loads (Setting.Value).
     * Players who join a game load the host's save, so the host's picks are
     * the game's.
     */
    public static class GameSettings
    {
        public static readonly List<Setting> All = new();

        /** A heading in the Customize list, for the settings of one mod. */
        public static SettingGroup Group(string headingLocKey) => new(headingLocKey);

        internal static T Register<T>(T setting) where T : Setting
        {
            All.RemoveAll(s => s.Id == setting.Id);
            All.Add(setting);
            return setting;
        }

        public static Setting Find(string id) => All.FirstOrDefault(s => s.Id == id);

        // ---- Extra rows

        /**
         * A row a mod puts in the Customize list of its own making, under a heading: Take is
         * given the game's list as the dialog opens and returns the element to show (taking it
         * out of the list if it's there), or null for none.
         */
        public class Row
        {
            public string Group;
            public Func<ScrollView, SettingsUi, VisualElement> Take;
        }

        internal static readonly List<Row> Rows = new();

        public static void AddRow(string groupLocKey, Func<ScrollView, SettingsUi, VisualElement> take) =>
            Rows.Add(new Row { Group = groupLocKey, Take = take });

        // ---- Picks

        private static readonly Dictionary<string, string> Picked = new();

        /** What this player picked last time for a setting, as text: remembered between games. */
        public static string PickedText(string id)
        {
            Setting setting = Find(id);
            if (setting == null) return null;
            if (!Picked.TryGetValue(id, out string text))
            {
                text = PlayerPrefs.GetString(PrefsKey(id), setting.DefaultText);
                Picked[id] = text;
            }
            return setting.IsValid(text) ? text : setting.DefaultText;
        }

        public static void Pick(string id, string text)
        {
            Picked[id] = text;
            PlayerPrefs.SetString(PrefsKey(id), text);
        }

        public static string PrefsKey(string id) => "BeaverBuddies.GameSetting." + id;

        /** Every setting as this player has it picked. */
        public static Dictionary<string, string> Current() => All.ToDictionary(s => s.Id, s => PickedText(s.Id));

        /** The settings a game of this kind has, as this player has them picked. */
        public static Dictionary<string, string> Current(GameKind kind) =>
            All.Where(s => s.AppliesTo(kind)).ToDictionary(s => s.Id, s => PickedText(s.Id));

        // ---- Difficulty presets

        // The difficulty the presets were last applied for, kept between sessions so the picks made after it stay
        private const string PresetDifficultyKey = "BeaverBuddies.GameSettings.PresetDifficulty";
        // Set while the screen puts a difficulty back by itself, which isn't a pick of the player's
        internal static bool Restoring;

        /** The difficulty a game mode is (Easy, Normal or Hard, by its name's last part), or null for a custom one. */
        public static string DifficultyOf(GameModeSpec mode)
        {
            string name = mode?.DisplayNameLocKey ?? "";
            foreach (string difficulty in new[] { "Easy", "Normal", "Hard" }) if (name.EndsWith("." + difficulty)) return difficulty;
            return null;
        }

        /**
         * Sets every setting with presets to its value for a difficulty the player has just picked.
         * Picking the one already applied leaves the settings as they are, so the player's changes
         * to them stay when the screen is opened again.
         */
        internal static void ApplyPresets(string difficulty)
        {
            if (difficulty == null || Restoring || PlayerPrefs.GetString(PresetDifficultyKey, null) == difficulty) return;
            PlayerPrefs.SetString(PresetDifficultyKey, difficulty);
            var set = new List<string>();
            foreach (Setting setting in All)
            {
                if (setting.PresetTexts == null || !setting.PresetTexts.TryGetValue(difficulty, out string text) || !setting.IsValid(text)) continue;
                Pick(setting.Id, text);
                set.Add(setting.Id + "=" + text);
            }
            Debug.Log($"[GameSettings] {difficulty} picked: settings set to its presets: {string.Join(" ", set)}");
        }

        // ---- The new game

        private static Dictionary<string, string> _forGame = new();
        // Set right before a match's game starts: the picks it was agreed on, not this player's
        private static Dictionary<string, string> _agreed;

        /** The next new game takes these picks (a match's, agreed between its players) in place of this player's. */
        public static void StartWith(Dictionary<string, string> values) => _agreed = values;

        // A new game takes this player's picks (or a match's); a saved one keeps its own.
        // Only the settings that apply to the game's kind: the rest, which the Customize
        // list hid, are at their defaults rather than whatever was last picked for another kind of game
        internal static void Starting(bool newGame)
        {
            _forGame = new Dictionary<string, string>();
            if (newGame)
            {
                GameKind kind = GameKind.Loading;
                foreach (Setting setting in All)
                {
                    string text = !setting.AppliesTo(kind) ? setting.DefaultText
                        : _agreed != null && _agreed.TryGetValue(setting.Id, out string agreed) && setting.IsValid(agreed)
                        ? agreed : _agreed != null ? setting.DefaultText : PickedText(setting.Id);
                    _forGame[setting.Id] = text;
                }
            }
            _agreed = null;
            if (newGame && _forGame.Count > 0) Debug.Log("[GameSettings] Starting a game with settings: " + string.Join(";", _forGame.Select(p => p.Key + "=" + p.Value)));
        }

        // The picks for the new game starting on this machine, all of them
        internal static IReadOnlyDictionary<string, string> ForGame => _forGame;

        /** The pick for the new game starting on this machine, or null for a saved game. */
        public static string GameText(string id) => _forGame.TryGetValue(id, out string text) ? text : null;

        // ---- In words

        /**
         * The settings a game has, one "Name: value" each, only those that apply to
         * its kind; with onlyChanged, only those that aren't their default.
         */
        public static List<string> Describe(ILoc loc, IReadOnlyDictionary<string, string> values, GameKind kind, bool onlyChanged)
        {
            var lines = new List<string>();
            if (values == null) return lines;
            foreach (Setting setting in All)
            {
                if (!values.TryGetValue(setting.Id, out string text) || !setting.IsValid(text) || !setting.AppliesTo(kind)) continue;
                if (onlyChanged && text == setting.DefaultText) continue;
                lines.Add($"{loc.T(setting.LabelKey)}: {setting.Words(loc, text)}");
            }
            return lines;
        }

        /** "Game settings: the defaults", or the ones that differ, on a line. */
        public static string Summary(ILoc loc, IReadOnlyDictionary<string, string> values, GameKind kind)
        {
            List<string> changed = Describe(loc, values, kind, onlyChanged: true);
            return changed.Count == 0 ? loc.T("GameModes.Settings.Defaults") : loc.T("GameModes.Settings.Changed", string.Join(", ", changed));
        }
    }

    // Each game starting to load: its settings settle (after the mode's own prefix), and mods are told what it was made with
    [HarmonyPatch]
    class GameSettingsNewGamePatcher
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartNewGame));
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartNewGameInstantly));
        }

        // After the mode's own prefix (normal), before mods that read the new game's settings in theirs (low)
        [HarmonyPriority(Priority.LowerThanNormal)]
        static void Prefix()
        {
            GameSettings.Starting(newGame: true);
            var record = new Dictionary<string, string>(Modes.Record());
            foreach (var pair in GameSettings.ForGame) record[pair.Key] = pair.Value;
            Modes.RaiseGameLoading(record, newGame: true);
        }
    }

    [HarmonyPatch]
    class GameSettingsSaveGamePatcher
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartSaveGame));
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartSaveGameInstantly));
        }

        static void Prefix(GameSceneLoader __instance, SaveReference saveReference)
        {
            GameSettings.Starting(newGame: false);
            Modes.RaiseGameLoading(GameRecord.ReadFromSave(__instance._gameSaveRepository, saveReference), newGame: false);
        }
    }

    // Selecting a difficulty, by a click or as the page opens with one highlighted, sets the settings that
    // have presets to its values, unless they already are its
    [HarmonyPatch(typeof(NewGameModePanel), nameof(NewGameModePanel.SelectModeButton))]
    class DifficultyPresetsPatcher
    {
        static void Postfix(object[] __args) => GameSettings.ApplyPresets(GameSettings.DifficultyOf(__args.OfType<GameModeSpec>().FirstOrDefault()));
    }
}
