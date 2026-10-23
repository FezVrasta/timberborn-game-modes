using System.IO;
using Bindito.Core;
using GameModes;
using Timberborn.ModManagerScene;

namespace HelloMode
{
    /**
     * The mod's start: it adds the Hello game mode to Game Modes, with a picture and one game setting. Nothing
     * else is needed for them to show up on the New Game screens and the loading screen.
     */
    public class Plugin : IModStarter
    {
        public const string Mode = "hellomode.hello";

        // How the game greets: one of a few values, read back as a string (see Greeter)
        public static ChoiceSetting Greeting { get; private set; }

        public void StartMod(IModEnvironment modEnvironment)
        {
            // A game mode: its id and the loc key of its name, then the line shown under it when it's picked
            Modes.Add(Mode, "HelloMode.GameMode.Hello");
            Modes.Describe(Mode, "HelloMode.GameMode.Hello.Description");

            // A picture behind the New Game screens while the mode is picked, and behind the loading screen of a
            // game in it: the path of an image file, given when the screen shows (a function, so it can change)
            string picture = Path.Combine(modEnvironment.ModPath, "Hello.jpg");
            Modes.SetBackground(Mode, menu: () => picture, loading: () => picture);

            // A game setting, under a heading of the mod's own in the Customize list (a loc key). It's only
            // offered in games started in the Hello mode
            SettingGroup rules = GameSettings.Group("HelloMode.Settings").When(kind => kind.Is(Mode));
            Greeting = rules.Choice("hellomode.greeting", "HelloMode.Settings.Greeting", "hello", "hello", "hi", "hey");
        }
    }

    // The greeting itself: a service that exists in games, and reads the mode and setting of the one that loaded
    [Context("Game")]
    public class HelloConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition) => containerDefinition.Bind<Greeter>().AsSingleton();
    }
}
