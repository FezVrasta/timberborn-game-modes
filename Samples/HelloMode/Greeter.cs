using GameModes;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace HelloMode
{
    /**
     * Greets the player when a game in the Hello mode has loaded. Modes.Current is the loaded game's mode
     * (new or saved), and Plugin.Greeting.Current its pick for the setting: both come from the game's
     * record, which is saved with it, so a saved game greets the way it was made to.
     */
    public class Greeter : IPostLoadableSingleton
    {
        private readonly QuickNotificationService _notifications;

        public Greeter(QuickNotificationService notifications) => _notifications = notifications;

        public void PostLoad()
        {
            if (Modes.Current != Plugin.Mode) return;
            string greeting = Plugin.Greeting.Current + ", beavers!";
            Debug.Log($"[HelloMode] {greeting}");
            _notifications.SendNotification(greeting);
        }
    }
}
