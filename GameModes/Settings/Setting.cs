using System;
using System.Collections.Generic;
using Timberborn.Localization;
using UnityEngine.UIElements;

namespace GameModes
{
    /**
     * A rule of a game, picked on the New Game screens' Customize list as the
     * game is created and saved with it. Define one through a SettingGroup
     * (GameSettings.Group), which gives it its heading:
     *
     *     static readonly SettingGroup Rules = GameSettings.Group("MyMod.Settings");
     *     public static readonly SliderSetting Raids = Rules.Slider("mymod.raids", "MyMod.Settings.Raids", 0, 20, 1, 7)
     *         .When(kind => kind.Is(MyMode));
     *
     * and read it as the game loads, typed: Raids.Value. Its text keys follow
     * from the one given (label, label + ".Tooltip", label + ".Value" for a
     * slider's value, label + "." + value for a choice's), each changed with
     * the fluent calls.
     *
     * Each player's pick is remembered between games. A new game takes it
     * (Value), whether the list was opened or not, and saves it with the
     * game (Current): a game joined or loaded keeps the one it was made with.
     */
    public abstract class Setting
    {
        public string Id { get; }
        // The heading it goes under, a loc key: one per mod
        public string Group { get; }
        // Loc keys: its name, and what hovering it says (missing for none)
        public string LabelKey { get; protected set; }
        public string TooltipKey { get; protected set; }
        // A line of small print right under its name ("still in beta"), or null for none
        public string NoteKey { get; protected set; }

        private Func<GameKind, bool> _applies;
        internal Dictionary<string, string> PresetTexts;

        protected Setting(string id, SettingGroup group, string labelKey)
        {
            Id = id;
            Group = group.Heading;
            LabelKey = labelKey;
            TooltipKey = labelKey + ".Tooltip";
            _applies = group.Applies;
        }

        /** Whether the setting is offered for, and saved with, a game of this kind. */
        public bool AppliesTo(GameKind kind) => _applies == null || _applies(kind);

        public void SetApplies(Func<GameKind, bool> applies) => _applies = applies;
        public void SetTooltip(string key) => TooltipKey = key;
        public void SetNote(string key) => NoteKey = key;

        /** What each of the game's difficulties sets it to, as text, for a setting found by its id (the typed Presets is for ones defined here). */
        public void SetPresetTexts(string easy, string normal, string hard) =>
            PresetTexts = new Dictionary<string, string> { ["Easy"] = easy, ["Normal"] = normal, ["Hard"] = hard };

        /** The value as it's saved and picked, as text. */
        public abstract string DefaultText { get; }
        public abstract bool IsValid(string text);
        /** A value in words, as the Customize list and the Load Game box name it. */
        public abstract string Words(ILoc loc, string text);

        // The setting's row in the Customize dialog
        internal abstract VisualElement CreateRow(SettingsUi ui);

        /** The pick the new game loading on this machine was started with, or null for a saved game. */
        public string NewGameText => GameSettings.GameText(Id);
        /** What this player has picked right now, for the next game. */
        public string PickedText => GameSettings.PickedText(Id);
    }

    /**
     * A setting of one type, T, with its fluent definition: each call returns
     * the setting itself, so a definition reads as one expression.
     */
    public abstract class Setting<T, TSelf> : Setting where TSelf : Setting<T, TSelf>
    {
        public T Default { get; private set; }

        protected Setting(string id, SettingGroup group, string labelKey, T @default) : base(id, group, labelKey)
        {
            Default = @default;
        }

        protected abstract bool TryParse(string text, out T value);
        protected abstract string Format(T value);

        public sealed override string DefaultText => Format(Default);
        public sealed override bool IsValid(string text) => TryParse(text, out _);

        private T Parsed(string text) => text != null && TryParse(text, out T value) ? value : Default;

        /** The value for the new game loading: its pick, or the default (for a saved game, which keeps its own copy). */
        public T Value => Parsed(NewGameText);

        /** What this player has picked right now. */
        public T Picked => Parsed(PickedText);

        /** Whether the new game loading has a pick for it (a saved game doesn't, it has its own copy), and what it is. */
        public bool TryGetNewGameValue(out T value)
        {
            value = Value;
            return NewGameText != null && IsValid(NewGameText);
        }

        /** The value in a game's record (see GameRecord), the default where it has none. */
        public T Of(IReadOnlyDictionary<string, string> record) =>
            record != null && record.TryGetValue(Id, out string text) ? Parsed(text) : Default;

        /** The value of the game loaded, new or saved. */
        public T Current => Of(GameRecord.Current);

        protected TSelf Self => (TSelf)this;

        public TSelf WithDefault(T value)
        {
            Default = value;
            return Self;
        }

        /** The loc key of what hovering the setting says, where it isn't the label's + ".Tooltip". */
        public TSelf Tip(string locKey)
        {
            SetTooltip(locKey);
            return Self;
        }

        /** Small print under the setting's name. */
        public TSelf Small(string locKey)
        {
            SetNote(locKey);
            return Self;
        }

        /** The kinds of game it's offered for and saved with, where it isn't every kind (or its group's). */
        public TSelf When(Func<GameKind, bool> applies)
        {
            SetApplies(applies);
            return Self;
        }

        /** What each of the game's difficulties sets it to when the player picks that difficulty. */
        public TSelf Presets(T easy, T normal, T hard)
        {
            PresetTexts = new Dictionary<string, string> { ["Easy"] = Format(easy), ["Normal"] = Format(normal), ["Hard"] = Format(hard) };
            return Self;
        }
    }
}
