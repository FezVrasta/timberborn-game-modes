using HarmonyLib;
using Timberborn.ModManagerScene;

namespace GameModes
{
    public class Plugin : IModStarter
    {
        public const string ID = "gamemodes";

        public void StartMod(IModEnvironment modEnvironment) => new Harmony(ID).PatchAll(typeof(Plugin).Assembly);
    }
}
