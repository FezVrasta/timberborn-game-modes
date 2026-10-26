using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HarmonyLib;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSceneLoading;
using Timberborn.MapRepositorySystem;
using Timberborn.NewGameConfigurationSystem;
using UnityEngine;

namespace GameModes
{
    /**
     * Loading screen hints that depend on the world being loaded, whatever the mode: a mod says which
     * singletons make a world one of its own (see Modes.AddTipsForWorlds), and as a game starts loading
     * its map or save is looked through for them. The mode's own hints (Modes.AddTips) and these are
     * mixed in the one pool the loading screen picks from (see LoadingBackground).
     */
    public static class LoadingTips
    {
        private class ForWorld
        {
            public string[] Keys, Singletons;
            public Func<string> Picture;
        }

        private static readonly List<ForWorld> ForWorlds = new();

        // The hints that apply to the world about to load
        internal static List<string> NextWorld = new();

        // The picture of the world about to load, over its mode's
        internal static string NextWorldPicture;

        internal static void Add(string[] locKeys, string[] singletons) =>
            ForWorlds.Add(new ForWorld { Keys = locKeys, Singletons = singletons });

        internal static void AddPicture(Func<string> picture, string[] singletons) =>
            ForWorlds.Add(new ForWorld { Keys = new string[0], Singletons = singletons, Picture = picture });

        /** The hints a game in a mode, in the world about to load, can show. */
        internal static List<string> Pool(string mode)
        {
            var pool = new List<string>();
            if (mode != null && Modes.Get(mode) is { } m) pool.AddRange(m.Tips);
            pool.AddRange(NextWorld);
            return pool;
        }

        internal static void WorldLoading(Func<Stream> open)
        {
            NextWorld = new List<string>();
            NextWorldPicture = null;
            if (ForWorlds.Count == 0 || open == null) return;
            try
            {
                using Stream stream = open();
                if (stream == null) return;
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                ZipArchiveEntry entry = zip.GetEntry("world.json");
                if (entry == null) return;
                using var reader = new StreamReader(entry.Open());
                string world = reader.ReadToEnd();
                foreach (ForWorld tips in ForWorlds)
                    if (tips.Singletons.Any(s => world.Contains($"\"{s}\"")))
                    {
                        NextWorld.AddRange(tips.Keys);
                        NextWorldPicture ??= tips.Picture?.Invoke();
                    }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameModes] Couldn't look through the world for its hints: {e.Message}");
            }
        }
    }

    // Each game starting to load: what its map or save holds says which hints it can show
    [HarmonyPatch]
    class WorldTipsPatcher
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartNewGame));
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartNewGameInstantly));
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartSaveGame));
            yield return AccessTools.Method(typeof(GameSceneLoader), nameof(GameSceneLoader.StartSaveGameInstantly));
        }

        static void Prefix(GameSceneLoader __instance, object[] __args)
        {
            SaveReference save = __args.OfType<SaveReference>().FirstOrDefault();
            if (save != null)
            {
                LoadingTips.WorldLoading(() => __instance._gameSaveRepository.OpenSaveWithoutLogging(save));
                return;
            }
            MapFileReference? map = __args.OfType<NewGameConfiguration>().FirstOrDefault()?.MapFileReference
                ?? __args.OfType<MapFileReference>().Cast<MapFileReference?>().FirstOrDefault();
            // The game's own maps have no hints to look for
            if (map is not { Resource: false } reference) { LoadingTips.WorldLoading(null); return; }
            string path = reference.UserFolder
                ? Path.Combine(MapRepository.UserMapsDirectory, reference.Name + ".timber")
                : reference.Path;
            LoadingTips.WorldLoading(() => File.Exists(path) ? File.OpenRead(path) : null);
        }
    }
}
