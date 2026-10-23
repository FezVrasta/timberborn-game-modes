using HarmonyLib;
using Timberborn.ModManagerScene;

namespace GameModes
{
    public class Plugin : IModStarter
    {
        public const string ID = "gamemodes";

        public void StartMod(IModEnvironment modEnvironment)
        {
            new Harmony(ID).PatchAll(typeof(Plugin).Assembly);
            // The loading screen of the game that starts loading gets its mode's picture (see Backgrounds)
            Modes.GameLoading += (record, _) => Backgrounds.NextLoadMode = Modes.ModeOf(record);
        }
    }
}
