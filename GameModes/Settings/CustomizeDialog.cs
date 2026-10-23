using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using HarmonyLib;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.MainMenuPanels;
using Timberborn.NewGameConfigurationSystem;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameModes
{
    [Context("MainMenu")]
    public class CustomizeDialogConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<CustomizeDialog>().AsSingleton();
        }
    }

    /**
     * The New Game screens' Customize list in a dialog of its own, instead of
     * in place of the difficulty's description, with the mods' game settings
     * at its end, built like its rows: a bold heading per mod, a checkbox per
     * toggle, and a checkbox per value of a choice, under its name. Only the
     * settings that apply to the game being made show. Closing it puts the
     * list back, hidden, where the game keeps reading it from.
     */
    public class CustomizeDialog
    {
        public const string DialogName = "GameModesCustomize";
        public const string RulesName = "GameModesSettings";

        private readonly SettingsUi _ui;
        private readonly ILoc _loc;
        private readonly DialogBoxShower _dialogBoxShower;
        private bool _open;
        private string _shown;
        // The difficulty the Customize list started from, and its rows as they were then
        private GameModeSpec _base;
        private Button _baseButton;
        private Dictionary<string, (string Label, string Value)> _baseRows;

        internal static CustomizeDialog Instance { get; private set; }

        public CustomizeDialog(ILoc loc, ITooltipRegistrar tooltipRegistrar, DialogBoxShower dialogBoxShower, VisualElementLoader visualElementLoader)
        {
            _loc = loc;
            _ui = new SettingsUi(loc, tooltipRegistrar, visualElementLoader);
            _dialogBoxShower = dialogBoxShower;
            Instance = this;
            // The settings shown depend on the mode
            Modes.Picked -= Refresh;
            Modes.Picked += Refresh;
        }

        private static List<Setting> Shown(GameKind kind) => GameSettings.All.Where(s => s.AppliesTo(kind)).ToList();

        internal void OpenCustomize(NewGameModePanel panel)
        {
            ScrollView list = panel._root.Q<ScrollView>("CustomModeSettings");
            VisualElement home = list?.parent;
            if (home == null || _open) return;
            _open = true;
            // From a difficulty, the list starts from it; on Custom already, it has what was set.
            // The page stays on the difficulty meanwhile: only OK with a change makes it Custom
            if (panel._predefinedGameMode != null)
            {
                _base = panel._predefinedGameMode;
                _baseButton = panel._selectedModeButton;
                panel._customNewGameModeController.Initialize(panel._root, _base, panel.UpdateNextButton);
                _baseRows = null;
            }
            int index = home.IndexOf(list);
            var content = new VisualElement { name = DialogName };
            // The New Game screens' styles, for the list's classes
            for (VisualElement e = list; e != null; e = e.parent)
            {
                for (int i = 0; i < e.styleSheets.count; i++) content.styleSheets.Add(e.styleSheets[i]);
            }
            // The rows mods bring themselves, taken out of the game's list (and out of the settings
            // from the last time it opened) to go under their headings
            var extra = GameSettings.Rows.Select(row => (row.Group, Element: row.Take(list, _ui))).Where(r => r.Element != null).ToList();
            list.Q(RulesName)?.RemoveFromHierarchy();
            List<Setting> rules = Shown(GameKind.OnPage);
            if (rules.Count > 0 || extra.Count > 0) list.Add(Rules(rules, extra));
            home.Remove(list);
            content.Add(list);
            list.style.height = 520;
            list.ToggleDisplayStyle(true);
            _baseRows ??= Rows(list);
            _dialogBoxShower.Create().SetMaxWidth(760).AddContent(content).SetConfirmButton(() =>
            {
                // Back where the game reads it, hidden
                list.RemoveFromHierarchy();
                home.Insert(Math.Min(index, home.childCount), list);
                list.ToggleDisplayStyle(false);
                _open = false;
                ShowCustomized(panel, list);
            }).Show();
            Debug.Log("[GameSettings] Customize dialog open");
        }

        /**
         * After the dialog: with nothing changed, the difficulty it started from
         * (picked again if the game was on Custom); otherwise Custom, with the
         * details saying what's changed from it.
         */
        private void ShowCustomized(NewGameModePanel panel, ScrollView list)
        {
            var changed = new List<string>();
            foreach (var row in Rows(list))
            {
                if (_baseRows != null && _baseRows.TryGetValue(row.Key, out var before) && before.Value == row.Value.Value) continue;
                changed.Add($"{row.Value.Label}: {row.Value.Value}");
            }
            if (changed.Count == 0 && _base != null && _baseButton != null)
            {
                Debug.Log($"[GameSettings] Customize closed with nothing changed: back to {_base.DisplayNameLocKey}");
                GameSettings.Restoring = true;
                try { if (panel._predefinedGameMode == null) panel.OnPredefinedModeButtonClicked(_baseButton, _base); }
                finally { GameSettings.Restoring = false; }
                return;
            }
            Debug.Log($"[GameSettings] Customize closed with {changed.Count} changed");
            // Custom, as the game's own Customize button makes it
            panel.SelectModeButton(null, null);
            string from = _baseButton != null ? $" ({_baseButton.text})" : "";
            panel._modeDescription.text = _loc.T("NewGameConfigurationPanel.Custom") + from + "\n\n" + string.Join("\n", changed);
            panel._modeDescription.ToggleDisplayStyle(true);
            panel._customizeButton.ToggleDisplayStyle(true);
            panel._tutorialToggleController.ShowMainToggle();
        }

        /**
         * The Customize list's rows as they read: each row's label and the rest of
         * it in words ("9 age 10 % - 70 %"), a ticked box as On or Off. Not the
         * Tutorial box, nor the mods' settings, which are their own.
         */
        private Dictionary<string, (string Label, string Value)> Rows(ScrollView list)
        {
            var rows = new Dictionary<string, (string, string)>();
            list.Query(className: SettingsUi.WrapperClass).ForEach(wrapper =>
            {
                if (string.IsNullOrEmpty(wrapper.name) || wrapper.name.StartsWith("TutorialToggle")) return;
                for (VisualElement e = wrapper.parent; e != null && e != list; e = e.parent) if (e.name == RulesName) return;
                string label = null;
                var parts = new List<string>();
                foreach (VisualElement child in wrapper.Children())
                {
                    switch (child)
                    {
                        case Toggle toggle:
                            parts.Add(_loc.T(toggle.value ? "GameModes.Settings.On" : "GameModes.Settings.Off"));
                            break;
                        case IntegerField field:
                            parts.Add(field.value.ToString());
                            break;
                        case Label text when label == null && text.ClassListContains(SettingsUi.LabelClass):
                            label = text.text;
                            break;
                        case Label text:
                            parts.Add(text.text);
                            break;
                    }
                }
                if (label != null) rows[wrapper.name] = (label, string.Join(" ", parts));
            });
            return rows;
        }

        // The settings under a heading per mod, a mod's own rows first under its heading
        private VisualElement Rules(List<Setting> settings, List<(string Group, VisualElement Element)> extra)
        {
            var rules = new VisualElement { name = RulesName };
            var order = settings.Select(s => s.Group).Distinct().ToList();
            // A heading only a mod's own rows are under goes first
            foreach (string group in extra.Select(r => r.Group).Distinct().Reverse()) if (!order.Contains(group)) order.Insert(0, group);
            foreach (string group in order)
            {
                var heading = _ui.Label(group);
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                heading.style.marginTop = 12;
                rules.Add(heading);
                foreach (var row in extra.Where(r => r.Group == group)) rules.Add(row.Element);
                foreach (Setting setting in settings.Where(s => s.Group == group)) rules.Add(setting.CreateRow(_ui));
            }
            return rules;
        }

        // Which settings the game being made gets, in the log as it changes
        internal void Refresh()
        {
            GameKind kind = GameKind.OnPage;
            string ids = $"{kind.Key}: {string.Join(" ", Shown(kind).Select(s => s.Id))}";
            if (ids == _shown) return;
            _shown = ids;
            Debug.Log($"[GameSettings] Shown for {ids}");
        }
    }

    // The page's Customize button opens the dialog in place of the game's own handler, which would make the
    // game Custom right away and start the list over from a difficulty there may not be
    [HarmonyPatch(typeof(NewGameModePanel), nameof(NewGameModePanel.OnCustomizeButtonClicked))]
    class CustomizeDialogPatcher
    {
        static bool Prefix(NewGameModePanel __instance)
        {
            if (CustomizeDialog.Instance == null) return true;
            CustomizeDialog.Instance.OpenCustomize(__instance);
            return false;
        }
    }

    // As the page shows and whenever what it makes may have changed
    [HarmonyPatch(typeof(NewGameModePanel), nameof(NewGameModePanel.UpdateNextButton))]
    class CustomizeDialogRefreshPatcher
    {
        static void Postfix() => CustomizeDialog.Instance?.Refresh();
    }
}
