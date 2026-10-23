using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameModes
{
    /** A checkbox. */
    public class ToggleSetting : Setting<bool, ToggleSetting>
    {
        public const string On = "on";
        public const string Off = "off";

        internal ToggleSetting(string id, SettingGroup group, string labelKey, bool @default) : base(id, group, labelKey, @default) { }

        protected override bool TryParse(string text, out bool value)
        {
            value = text == On;
            return text == On || text == Off;
        }

        protected override string Format(bool value) => value ? On : Off;

        public override string Words(ILoc loc, string text) => loc.T(text == On ? "GameModes.Settings.On" : "GameModes.Settings.Off");

        internal override VisualElement CreateRow(SettingsUi ui)
        {
            var rows = new VisualElement();
            Toggle toggle = ui.Row(rows, LabelKey, TooltipKey);
            ui.AddNote(rows, this);
            toggle.SetValueWithoutNotify(Picked);
            toggle.RegisterValueChangedCallback(e => GameSettings.Pick(Id, Format(e.newValue)));
            return rows;
        }
    }

    /** One of a few values, a checkbox each under the setting's name. */
    public class ChoiceSetting : Setting<string, ChoiceSetting>
    {
        // A choice of this many values or fewer puts them side by side
        private const int SideBySide = 3;

        public IReadOnlyList<string> Values { get; }
        private readonly string[] _valueKeys;

        internal ChoiceSetting(string id, SettingGroup group, string labelKey, string @default, string[] values) : base(id, group, labelKey, @default)
        {
            Values = values;
            _valueKeys = values.Select(v => labelKey + "." + v).ToArray();
        }

        /** The loc keys of the values, in order, where they aren't the label's + "." + the value. */
        public ChoiceSetting ValueKeys(params string[] locKeys)
        {
            Array.Copy(locKeys, _valueKeys, Math.Min(locKeys.Length, _valueKeys.Length));
            return this;
        }

        protected override bool TryParse(string text, out string value)
        {
            value = text;
            return text != null && Values.Contains(text);
        }

        protected override string Format(string value) => value;

        public override string Words(ILoc loc, string text)
        {
            int index = Values.ToList().IndexOf(text);
            return index >= 0 ? loc.T(_valueKeys[index]) : text;
        }

        internal override VisualElement CreateRow(SettingsUi ui)
        {
            var rows = new VisualElement();
            var name = SettingsUi.Wrapper(ui.Label(LabelKey));
            ui.Tip(name, TooltipKey);
            rows.Add(name);
            ui.AddNote(rows, this);
            var toggles = new List<(Toggle Toggle, string Value)>();
            void Sync()
            {
                string picked = Picked;
                foreach (var (toggle, value) in toggles) toggle.SetValueWithoutNotify(value == picked);
            }
            // Three or fewer side by side on one line, each with its label; more, a line each
            bool sideBySide = Values.Count <= SideBySide;
            VisualElement line = null;
            if (sideBySide)
            {
                line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.marginLeft = 16;
                rows.Add(line);
            }
            for (int i = 0; i < Values.Count; i++)
            {
                string value = Values[i];
                Toggle toggle = ui.Row(sideBySide ? line : rows, _valueKeys[i], null);
                if (sideBySide)
                {
                    VisualElement cell = toggle.parent;
                    cell.style.flexGrow = 1;
                    cell.style.flexBasis = 0;
                    cell.style.marginRight = 8;
                }
                else toggle.parent.style.marginLeft = 16;
                toggle.RegisterValueChangedCallback(_ =>
                {
                    GameSettings.Pick(Id, value);
                    Sync();
                });
                toggles.Add((toggle, value));
            }
            Sync();
            return rows;
        }
    }

    /** A whole number from Min to Max in steps of Step, on a slider with its value beside it. */
    public class SliderSetting : Setting<int, SliderSetting>
    {
        public int Min { get; }
        public int Max { get; }
        public int Step { get; }
        // How its value reads: this key with the number ("{0} %"), with ".<value>" for a value that has a word of its own
        // ("Fog"), or with ".Min" at its lowest ("No raids")
        public string ValueKey { get; private set; }

        internal SliderSetting(string id, SettingGroup group, string labelKey, int min, int max, int step, int @default) : base(id, group, labelKey, @default)
        {
            Min = min;
            Max = max;
            Step = Math.Max(1, step);
            ValueKey = labelKey + ".Value";
        }

        /** The loc key of how its value reads, where it isn't the label's + ".Value". */
        public SliderSetting ValueText(string locKey)
        {
            ValueKey = locKey;
            return this;
        }

        protected override bool TryParse(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= Min && value <= Max && (value - Min) % Step == 0;

        protected override string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

        public override string Words(ILoc loc, string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? Words(loc, n) : text;

        /** A value in words: the mod's own text for that value where there's one, or at the minimum the one for that, or the value text with the number. */
        public string Words(ILoc loc, int value)
        {
            string own = ValueKey + "." + value;
            string ownText = loc.T(own);
            if (ownText != own) return ownText;
            if (value == Min)
            {
                string key = ValueKey + ".Min";
                string text = loc.T(key);
                if (text != key) return text;
            }
            return loc.T(ValueKey, value);
        }

        internal int Snap(int n) => Mathf.Clamp(Min + Mathf.RoundToInt((n - Min) / (float)Step) * Step, Min, Max);

        internal override VisualElement CreateRow(SettingsUi ui) => ui.Slider(this);
    }

    /** Up to MaxItems picks of a few values, each its own row, saved as the picks joined by commas. */
    public class ListSetting : Setting<string[], ListSetting>
    {
        public int MaxItems { get; }
        public string ItemKey { get; }
        public string ItemDefault { get; }
        public IReadOnlyList<string> Values { get; }
        private readonly string[] _valueKeys;

        internal ListSetting(string id, SettingGroup group, string labelKey, string itemKey, int maxItems, string itemDefault, string[] values)
            : base(id, group, labelKey, Array.Empty<string>())
        {
            MaxItems = maxItems;
            ItemKey = itemKey;
            ItemDefault = itemDefault ?? values[0];
            Values = values;
            _valueKeys = values.Select(v => itemKey + "." + v).ToArray();
        }

        public static string[] Items(string text) => string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Split(',');

        protected override bool TryParse(string text, out string[] value)
        {
            value = Items(text);
            return text != null && value.Length <= MaxItems && value.All(Values.Contains);
        }

        protected override string Format(string[] value) => string.Join(",", value);

        public override string Words(ILoc loc, string text)
        {
            string[] items = Items(text);
            if (items.Length == 0) return loc.T("GameModes.Settings.None");
            return string.Join(", ", items.Select(item => loc.T(_valueKeys[Values.ToList().IndexOf(item)])));
        }

        /**
         * Its name with how many there are and buttons to take one off or add one,
         * then a row per item with its label and a checkbox per value side by
         * side, of which one is ticked.
         */
        internal override VisualElement CreateRow(SettingsUi ui)
        {
            var rows = new VisualElement();
            var count = new Label();
            count.AddToClassList(SettingsUi.LabelClass);
            SettingsUi.Inline(count);
            count.style.minWidth = 18;
            count.style.unityTextAlign = TextAnchor.MiddleCenter;
            var items = new VisualElement();
            var less = SettingsUi.Step("-");
            var more = SettingsUi.Step("+");
            Label title = ui.Label(LabelKey);
            title.style.flexGrow = 1;
            // Its count with a button either side, together at the row's end
            var stepper = SettingsUi.Line(less, count, more);
            var name = SettingsUi.Line(title, stepper);
            name.AddToClassList(SettingsUi.WrapperClass);
            ui.Tip(name, TooltipKey);
            rows.Add(name);
            ui.AddNote(rows, this);
            rows.Add(items);
            List<string> Picks() => Items(PickedText).ToList();
            void Save(List<string> picks) => GameSettings.Pick(Id, Format(picks.ToArray()));
            void Build()
            {
                List<string> picks = Picks();
                count.text = picks.Count.ToString();
                less.SetEnabled(picks.Count > 0);
                more.SetEnabled(picks.Count < MaxItems);
                items.Clear();
                for (int i = 0; i < picks.Count; i++)
                {
                    int index = i;
                    // On one line: the item's label, then each value with its checkbox
                    Label itemName = SettingsUi.Inline(ui.Label(ItemKey, index + 1));
                    itemName.style.minWidth = 140;
                    var row = SettingsUi.Line(itemName);
                    row.style.marginLeft = 16;
                    row.style.marginTop = 2;
                    for (int v = 0; v < Values.Count; v++)
                    {
                        string value = Values[v];
                        var toggle = new Toggle();
                        toggle.AddToClassList(SettingsUi.ToggleClass);
                        toggle.SetValueWithoutNotify(picks[index] == value);
                        Label label = SettingsUi.Inline(ui.Label(_valueKeys[v]));
                        label.style.marginRight = 16;
                        label.RegisterCallback((ClickEvent _) => toggle.value = true);
                        toggle.RegisterValueChangedCallback(_ =>
                        {
                            List<string> now = Picks();
                            if (index < now.Count) now[index] = value;
                            Save(now);
                            Build();
                        });
                        row.Add(SettingsUi.Line(toggle, label));
                    }
                    items.Add(row);
                }
            }
            less.clicked += () =>
            {
                List<string> picks = Picks();
                if (picks.Count == 0) return;
                picks.RemoveAt(picks.Count - 1);
                Save(picks);
                Build();
            };
            more.clicked += () =>
            {
                List<string> picks = Picks();
                if (picks.Count >= MaxItems) return;
                picks.Add(ItemDefault);
                Save(picks);
                Build();
            };
            Build();
            return rows;
        }
    }
}
