using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bindito.Core;
using Newtonsoft.Json.Linq;
using Timberborn.BlueprintSystem;
using UnityEngine;

namespace GameModes
{
    /**
     * Kinds of map, by what their map or save holds: a mod names a kind and the singletons that make a world one
     * (as they're written in the world: "Archipelago.IslandLayout"). As a game starts loading, its map or save is
     * looked through for them (see LoadingTips), so a game's kinds are known before its blueprints are read, on a
     * guest too, which loads the host's save. A mod that doesn't depend on Game Modes finds Add by reflection.
     */
    public static class MapKinds
    {
        private static readonly Dictionary<string, string[]> Kinds = new();

        // The kinds of the game that loaded last
        private static HashSet<string> _current = new();

        public static void Add(string kind, string[] singletonNames) => Kinds[kind] = singletonNames;

        public static IReadOnlyCollection<string> Current => _current;

        public static bool Is(string kind) => _current.Contains(kind);

        internal static void WorldLoading(string world) =>
            _current = world == null ? new HashSet<string>()
                : new HashSet<string>(Kinds.Where(k => k.Value.Any(s => world.Contains($"\"{s}\""))).Select(k => k.Key));
    }

    /**
     * Patches to a blueprint that apply only in some games: in its game modes, or on its kinds of map (see
     * MapKinds). Each case's Patch is what the blueprint would have in a file of its own, #append and #remove
     * included, laid over it as the game reads it, when the case holds:
     *
     *   "WhenSpec": { "Cases": [ { "MapKinds": [ "Sea" ], "Patch": { "ManufactorySpec": { "ProductionRecipeIds#append": [ "Musket" ] } } } ] }
     *
     * A case with no Modes holds in every mode, one with no MapKinds on every map. A patch can change what a
     * blueprint has, not make a blueprint that isn't there: the game lists every blueprint before any patch.
     * Only games read the cases; the main menu and the map editor have the blueprints as they are.
     */
    public record WhenSpec : ComponentSpec
    {
        [Serialize] public ImmutableArray<WhenCaseSpec> Cases { get; init; }
    }

    // The game reads Modes and MapKinds; Patch it leaves alone, for WhenBlueprints to read from the file
    public record WhenCaseSpec
    {
        [Serialize] public ImmutableArray<string> Modes { get; init; }
        [Serialize] public ImmutableArray<string> MapKinds { get; init; }
    }

    public class WhenBlueprints : IBlueprintModifierProvider
    {
        private const string Key = "WhenSpec";

        private readonly BlueprintFileBundleLoader _bundleLoader;

        public WhenBlueprints(BlueprintFileBundleLoader bundleLoader) => _bundleLoader = bundleLoader;

        public string ModifierName => "Game Modes when";

        public IEnumerable<string> GetModifiers(string blueprintPath)
        {
            BlueprintFileBundle bundle = _bundleLoader.GetBundle(blueprintPath);
            if (bundle == null) yield break;
            // Each file of the blueprint, other mods' patches to it included, in the order the game lays them
            foreach (string json in bundle.Jsons)
            {
                if (!json.Contains("\"" + Key + "\"")) continue;
                foreach (string patch in Patches(json, blueprintPath)) yield return patch;
            }
        }

        private static IEnumerable<string> Patches(string json, string blueprintPath)
        {
            JArray cases;
            try
            {
                cases = JObject.Parse(json)[Key]?["Cases"] as JArray;
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                Debug.LogWarning($"[GameModes] Couldn't read the WhenSpec of {blueprintPath}: {e.Message}");
                yield break;
            }
            if (cases == null) yield break;
            foreach (JToken when in cases)
            {
                if (when["Patch"] is not JObject patch || !Holds(when)) continue;
                Debug.Log($"[GameModes] {blueprintPath}: a WhenSpec case holds");
                yield return patch.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        private static bool Holds(JToken when)
        {
            string[] modes = Names(when["Modes"]), kinds = Names(when["MapKinds"]);
            if (modes.Length > 0 && !modes.Contains(GameModes.Modes.Current)) return false;
            return kinds.Length == 0 || kinds.Any(MapKinds.Is);
        }

        private static string[] Names(JToken token) => token is JArray array ? array.Select(t => (string)t).Where(n => n != null).ToArray() : new string[0];
    }

    [Context("Game")]
    public class WhenConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition) =>
            containerDefinition.MultiBind<IBlueprintModifierProvider>().To<WhenBlueprints>().AsSingleton();
    }
}
