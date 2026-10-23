using System;
using System.Collections.Generic;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameModes
{
    /**
     * What a setting's row is built from: the Customize list's own look (a
     * checkbox and its label as the game's Drought and Badtide rows, its
     * slider row from the Settings menu), made once here so every setting
     * type, and any extra row a mod adds, looks like the rest.
     */
    public class SettingsUi
    {
        internal const string WrapperClass = "new-game-mode-panel__setting-wrapper";
        internal const string ToggleClass = "new-game-mode-panel__setting-toggle";
        internal const string LabelClass = "new-game-mode-panel__setting-label";

        private readonly ILoc _loc;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly VisualElementLoader _visualElementLoader;

        internal SettingsUi(ILoc loc, ITooltipRegistrar tooltipRegistrar, VisualElementLoader visualElementLoader)
        {
            _loc = loc;
            _tooltipRegistrar = tooltipRegistrar;
            _visualElementLoader = visualElementLoader;
        }

        public ILoc Loc => _loc;

        // ---- Text and hover

        /** What hovering the element says: the loc key's text, where there is one. */
        public void Tip(VisualElement target, string locKey)
        {
            if (string.IsNullOrEmpty(locKey) || _loc.T(locKey) == locKey) return;
            WrappedTooltip.Register(_tooltipRegistrar, target, () => _loc.T(locKey));
        }

        /** A label in the list's style, with its number for one that takes one ("Opponent {0}"). */
        public Label Label(string locKey, int? number = null)
        {
            var label = new Label(number is { } n ? _loc.T(locKey, n) : _loc.T(locKey));
            label.AddToClassList(LabelClass);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;
            return label;
        }

        /** A setting's small print under its name: smaller and dimmer than the rows, wrapping, outside any row's wrapper so it never reads as part of a value. */
        internal void AddNote(VisualElement rows, Setting setting)
        {
            if (string.IsNullOrEmpty(setting.NoteKey)) return;
            Label note = Label(setting.NoteKey);
            note.name = "GameModes.Setting.Note." + setting.Id;
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.fontSize = 12;
            note.style.unityFontStyleAndWeight = FontStyle.Italic;
            note.style.opacity = 0.75f;
            note.style.marginLeft = 4;
            note.style.marginTop = 0;
            note.style.marginBottom = 4;
            rows.Add(note);
        }

        // ---- Rows

        /** A checkbox and its label, as the game's Drought and Badtide rows. */
        public Toggle Row(VisualElement rows, string labelLocKey, string tooltipLocKey)
        {
            var toggle = new Toggle();
            toggle.AddToClassList(ToggleClass);
            Label label = Label(labelLocKey);
            // The label ticks the box too
            label.RegisterCallback((ClickEvent _) => toggle.value = !toggle.value);
            VisualElement wrapper = Wrapper(toggle, label);
            Tip(wrapper, tooltipLocKey);
            rows.Add(wrapper);
            return toggle;
        }

        internal static VisualElement Wrapper(params VisualElement[] children)
        {
            var wrapper = new VisualElement();
            wrapper.AddToClassList(WrapperClass);
            foreach (VisualElement child in children) wrapper.Add(child);
            return wrapper;
        }

        // Elements side by side, centred on the line
        internal static VisualElement Line(params VisualElement[] children)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            foreach (VisualElement child in children) line.Add(child);
            return line;
        }

        // A label only as wide as its words, not the rows' column
        internal static Label Inline(Label label)
        {
            label.style.width = StyleKeyword.Auto;
            label.style.flexGrow = 0;
            label.style.flexShrink = 0;
            return label;
        }

        // A small button for a list's count, in the menus' style
        internal static Button Step(string text)
        {
            var button = new NineSliceButton { text = text, focusable = false };
            button.AddToClassList("menu-button");
            button.AddToClassList("menu-button--medium");
            button.style.width = 32;
            button.style.minWidth = 32;
            button.style.height = 26;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }

        // ---- Slider

        /**
         * A slider: the game's own slider row from its Settings menu (camera
         * speeds and the like), with the setting's name and value, snapping
         * to its steps. Where that row can't be had, one drawn by hand.
         */
        internal VisualElement Slider(SliderSetting setting)
        {
            int current = setting.Snap(setting.Picked);
            VisualElement row = GameSlider();
            if (row != null)
            {
                var name = row.Query<Label>().Where(l => l.name != "Value").First();
                if (name != null) name.text = _loc.T(setting.LabelKey);
                Label value = row.Q<Label>("Value");
                if (value != null)
                {
                    // Room between the slider and what it's at
                    value.style.marginLeft = 12;
                    value.style.minWidth = 56;
                }
                var slider = row.Q<Slider>("Slider");
                slider.lowValue = setting.Min;
                slider.highValue = setting.Max;
                slider.SetValueWithoutNotify(current);
                if (value != null) value.text = setting.Words(_loc, current);
                slider.RegisterValueChangedCallback(e =>
                {
                    int n = setting.Snap(Mathf.RoundToInt(e.newValue));
                    if (!Mathf.Approximately(n, e.newValue)) slider.SetValueWithoutNotify(n);
                    if (value != null) value.text = setting.Words(_loc, n);
                    GameSettings.Pick(setting.Id, n.ToString(System.Globalization.CultureInfo.InvariantCulture));
                });
                Tip(row, setting.TooltipKey);
                return WithNote(row, setting);
            }
            return WithNote(DrawnSlider(setting, current), setting);
        }

        private VisualElement WithNote(VisualElement row, Setting setting)
        {
            var rows = new VisualElement();
            rows.Add(row);
            AddNote(rows, setting);
            return rows;
        }

        // The Settings menu's slider row, taken out of a fresh copy of its box with the styles that dress it
        private VisualElement GameSlider()
        {
            try
            {
                VisualElement box = _visualElementLoader.LoadVisualElement("Options/SettingsBox");
                VisualElement row = box.Q<VisualElement>("EdgePanCameraSpeed");
                if (row?.Q<Slider>("Slider") == null) return null;
                var sheets = new List<StyleSheet>();
                for (VisualElement e = row; e != null; e = e.parent)
                {
                    for (int i = 0; i < e.styleSheets.count; i++) sheets.Add(e.styleSheets[i]);
                }
                row.RemoveFromHierarchy();
                foreach (StyleSheet sheet in sheets) row.styleSheets.Add(sheet);
                row.name = null;
                return row;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettings] No game slider to copy: {e.Message}");
                return null;
            }
        }

        // Drawn by hand, where the game's own slider row can't be had
        private VisualElement DrawnSlider(SliderSetting setting, int current)
        {
            var rows = new VisualElement();
            var name = Wrapper(Label(setting.LabelKey));
            Tip(name, setting.TooltipKey);
            rows.Add(name);
            var value = Inline(new Label());
            value.AddToClassList(LabelClass);
            value.style.minWidth = 64;
            value.style.marginLeft = 8;
            var slider = new SliderInt(setting.Min, setting.Max) { pageSize = setting.Step, focusable = false };
            slider.style.flexGrow = 1;
            slider.style.minWidth = 160;
            Style(slider);
            void Show(int n) => value.text = setting.Words(_loc, n);
            slider.SetValueWithoutNotify(current);
            Show(current);
            slider.RegisterValueChangedCallback(e =>
            {
                int n = setting.Snap(e.newValue);
                if (n != e.newValue) slider.SetValueWithoutNotify(n);
                Show(n);
                GameSettings.Pick(setting.Id, n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            });
            var line = Line(slider, value);
            line.style.marginLeft = 16;
            line.style.marginRight = 8;
            rows.Add(line);
            return rows;
        }

        // The game has no style for Unity's own sliders (they draw as nothing): a thin track, the
        // part up to the value filled, and a round handle, in the panel's colours
        private static readonly Color Track = new(0.1f, 0.16f, 0.15f), Fill = new(0.85f, 0.72f, 0.42f), Handle = new(0.98f, 0.9f, 0.68f);

        private static void Style(SliderInt slider)
        {
            slider.style.height = 22;
            slider.style.marginTop = 2;
            slider.style.marginBottom = 2;
            VisualElement input = slider.Q(className: "unity-base-slider__input");
            if (input != null)
            {
                input.style.flexGrow = 1;
                input.style.height = 22;
            }
            VisualElement container = slider.Q(className: "unity-base-slider__drag-container");
            if (container != null)
            {
                container.style.flexGrow = 1;
                container.style.height = 22;
            }
            VisualElement tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.position = Position.Absolute;
                tracker.style.left = 0;
                tracker.style.right = 0;
                tracker.style.top = 8;
                tracker.style.height = 6;
                tracker.style.backgroundColor = Track;
                Round(tracker, 3);
                tracker.style.borderTopWidth = tracker.style.borderBottomWidth = tracker.style.borderLeftWidth = tracker.style.borderRightWidth = 1;
                tracker.style.borderTopColor = tracker.style.borderBottomColor = tracker.style.borderLeftColor = tracker.style.borderRightColor = new Color(0.5f, 0.43f, 0.27f);
            }
            var filled = new VisualElement { pickingMode = PickingMode.Ignore };
            filled.style.position = Position.Absolute;
            filled.style.left = 0;
            filled.style.top = 8;
            filled.style.height = 6;
            filled.style.backgroundColor = Fill;
            Round(filled, 3);
            container?.Insert(1, filled);
            VisualElement dragger = slider.Q(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.position = Position.Absolute;
                dragger.style.width = 16;
                dragger.style.height = 16;
                dragger.style.top = 3;
                dragger.style.backgroundColor = Handle;
                Round(dragger, 8);
                dragger.style.borderTopWidth = dragger.style.borderBottomWidth = dragger.style.borderLeftWidth = dragger.style.borderRightWidth = 1;
                dragger.style.borderTopColor = dragger.style.borderBottomColor = dragger.style.borderLeftColor = dragger.style.borderRightColor = new Color(0.4f, 0.32f, 0.18f);
                // The filled part up to the value, as a share of the track
                void Follow(int value) => filled.style.width = Length.Percent(100f * (value - slider.lowValue) / Mathf.Max(1, slider.highValue - slider.lowValue));
                Follow(slider.value);
                slider.RegisterValueChangedCallback(e => Follow(e.newValue));
                slider.RegisterCallback<AttachToPanelEvent>(_ => Follow(slider.value));
            }
        }

        private static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }
    }
}
