using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Bindito.Core;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.SaveMetadataSystem;
using Timberborn.SaveSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameModes
{
    [Context("Game")]
    public class GameRecordGameConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<GameRecord>().AsSingleton();
            // The in-game Load Game box shows a save's settings too
            containerDefinition.Bind<SaveRecordLabel>().AsSingleton();
        }
    }

    [Context("MainMenu")]
    public class GameRecordMenuConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition) => containerDefinition.Bind<SaveRecordLabel>().AsSingleton();
    }

    /**
     * What a game was created with, for showing it before it's loaded: its
     * mode and game settings, saved with the game and also in the save's
     * metadata, the small part of a save the Load Game box reads when a save
     * is picked. The mods keep their own copies of what they act on; this is
     * only what to show, and what to ask of a save without loading it
     * (ReadFromSave). A save from before it existed has none.
     *
     * A record is the picks by id: the settings' (see Setting.Of), and the
     * mode's (Modes.ModeOf).
     */
    public class GameRecord : ILoadableSingleton, ISaveableSingleton
    {
        private static readonly SingletonKey Key = new("BeaverBuddies.GameRecord");
        internal static readonly ListKey<string> ValuesKey = new("Values");
        /** Its name in the save's metadata. */
        public const string MetadataName = "BeaverBuddies.Game";
        internal static readonly ListKey<string> MetadataKey = new(MetadataName);

        private readonly ISingletonLoader _singletonLoader;

        /** The loaded game's, new or saved (the same as Modes.CurrentRecord). */
        public static IReadOnlyDictionary<string, string> Current => Modes.CurrentRecord;

        public GameRecord(ISingletonLoader singletonLoader) => _singletonLoader = singletonLoader;

        public void Load()
        {
            // A new game's record was made as it started loading, from its picks; a saved game's is
            // what it was saved with (the same as its metadata's, but a mod may have completed it since)
            if (_singletonLoader.TryGetSingleton(Key, out IObjectLoader loader) && loader.Has(ValuesKey))
            {
                Modes.Merge(Parse(loader.Get(ValuesKey)));
            }
        }

        public void Save(ISingletonSaver singletonSaver) => singletonSaver.GetSingleton(Key).Set(ValuesKey, Lines(Current));

        public static List<string> Lines(IReadOnlyDictionary<string, string> values) =>
            values.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value).ToList();

        public static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            var values = new Dictionary<string, string>();
            foreach (string line in lines)
            {
                int split = line.IndexOf('=');
                if (split > 0) values[line.Substring(0, split)] = line.Substring(split + 1);
            }
            return values;
        }

        /** The record in a save's metadata JSON, or null if it has none. */
        public static Dictionary<string, string> FromMetadata(string json)
        {
            JToken lines = JObject.Parse(json)[MetadataName];
            return lines is JArray array ? Parse(array.Select(l => (string)l)) : null;
        }

        /** The record in a save's metadata (a zip of the save), or null if it has none. */
        public static Dictionary<string, string> FromSave(Stream save)
        {
            using var zip = new ZipArchive(save, ZipArchiveMode.Read);
            ZipArchiveEntry entry = zip.GetEntry("save_metadata.json");
            if (entry == null) return null;
            using var reader = new StreamReader(entry.Open());
            return FromMetadata(reader.ReadToEnd());
        }

        /** The record of a save in the repository, read without loading it, or null if it has none. */
        public static Dictionary<string, string> ReadFromSave(GameSaveRepository repository, SaveReference save)
        {
            try
            {
                using Stream stream = repository.OpenSaveWithoutLogging(save);
                return FromSave(stream);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettings] Couldn't read the game settings of {save}: {e.Message}");
                return null;
            }
        }

        /** A record in words, one "Name: value" per line: the game's mode. */
        public static IEnumerable<string> Describe(ILoc loc, IReadOnlyDictionary<string, string> record)
        {
            if (Modes.ModeOf(record) is { } mode) yield return $"{loc.T("GameModes.GameMode")}: {Modes.Label(loc, mode)}";
        }
    }

    // Into the save's metadata too, for the Load Game box
    [HarmonyPatch(typeof(SaveMetadataSerializer), "SaveMetadata")]
    class GameRecordMetadataPatcher
    {
        static void Postfix(IObjectSaver objectSaver)
        {
            if (GameRecord.Current?.Count > 0) objectSaver.Set(GameRecord.MetadataKey, GameRecord.Lines(GameRecord.Current));
        }
    }

    // The record from a save's metadata, without loading the rest of it
    class GameRecordReader : ISaveEntryReader<Dictionary<string, string>>
    {
        public string EntryName => "save_metadata.json";

        public Dictionary<string, string> ReadFromSaveEntryStream(Stream entryStream)
        {
            using var reader = new StreamReader(entryStream);
            return GameRecord.FromMetadata(reader.ReadToEnd());
        }
    }

    /**
     * Under the picked save's list in the Load Game box, above its picture:
     * the game's mode and the settings that aren't their defaults, and all
     * of them on hover.
     */
    public class SaveRecordLabel
    {
        public const string LabelName = "GameModesRecord";

        private readonly ILoc _loc;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private Dictionary<string, string> _shown;

        internal static SaveRecordLabel Instance { get; private set; }

        public SaveRecordLabel(ILoc loc, ITooltipRegistrar tooltipRegistrar)
        {
            _loc = loc;
            _tooltipRegistrar = tooltipRegistrar;
            Instance = this;
        }

        internal void Show(LoadGameBox box)
        {
            Label label = box._root.Q<Label>(LabelName) ?? Add(box._root);
            if (label == null) return;
            _shown = null;
            if (box._saveList.TryGetSelectedSave(out GameSaveItem save))
            {
                try
                {
                    _shown = box._gameSaveDeserializer.ReadFromSaveFile(save.SaveReference, new GameRecordReader());
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameSettings] Couldn't read the game settings of {save.SaveReference}: {e.Message}");
                }
            }
            label.text = _shown?.Count > 0 ? Text(_shown) : "";
            label.ToggleDisplayStyle(!string.IsNullOrEmpty(label.text));
            if (_shown?.Count > 0) Debug.Log($"[GameSettings] Save {save.SaveReference}: {string.Join(";", GameRecord.Lines(_shown))}");
        }

        private string Text(Dictionary<string, string> values)
        {
            string mode = string.Join("\n", GameRecord.Describe(_loc, values));
            string settings = GameSettings.Summary(_loc, values, GameKind.Of(values));
            return string.IsNullOrEmpty(mode) ? settings : mode + "\n" + settings;
        }

        private Label Add(VisualElement root)
        {
            VisualElement thumbnail = root.Q(className: "load-box__thumbnail");
            if (thumbnail?.parent == null) return null;
            var label = new Label { name = LabelName };
            label.AddToClassList("text--default");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.LowerLeft;
            label.style.marginBottom = 6;
            label.style.marginLeft = 4;
            label.style.marginRight = 4;
            // Every setting, not only the changed ones
            _tooltipRegistrar.Register(label, () => _shown == null ? "" : string.Join("\n",
                GameSettings.Describe(_loc, _shown, GameKind.Of(_shown), onlyChanged: false)));
            thumbnail.parent.Insert(thumbnail.parent.IndexOf(thumbnail), label);
            return label;
        }
    }

    [HarmonyPatch(typeof(LoadGameBox), "OnSaveSelectionChanged")]
    class SaveRecordLabelPatcher
    {
        static void Postfix(LoadGameBox __instance) => SaveRecordLabel.Instance?.Show(__instance);
    }
}
