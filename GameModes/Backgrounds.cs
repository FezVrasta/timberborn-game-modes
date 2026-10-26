using System;
using System.Collections.Generic;
using System.IO;
using Bindito.Core;
using Timberborn.Localization;
using Timberborn.MainMenuPanels;
using Timberborn.SceneLoading;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameModes
{
    /**
     * The pictures game modes put behind the screens (see Modes.SetBackground). The New Game screens and the
     * loading screen draw theirs on an element named TitleScreen, so a mode's picture goes there as an inline
     * style, over the game's, and what was there comes back when the screen no longer shows the mode.
     *
     * A texture is read from its file rather than through the asset loader, which throws mod textures away on
     * every scene load, in the middle of the loading screen showing it.
     */
    public static class Backgrounds
    {
        private const string BackgroundName = "TitleScreen";

        private static readonly Dictionary<string, Texture2D> Textures = new();

        // The mode of the game about to load, from the call that starts loading it (see Plugin)
        internal static string NextLoadMode;

        /** The picture behind the loading screen of a game in a mode, as a file's path, or null. */
        public static string LoadingPicture(string mode) => mode == null ? null : Modes.Get(mode)?.LoadingPicture?.Invoke();

        /** The texture of an image file, kept for the rest of the session. */
        public static Texture2D Picture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Textures.TryGetValue(path, out Texture2D had) && had) return had;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[GameModes] No picture {path}");
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.Compress(true);
            // Every scene load unloads what nothing holds, and only this holds it
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return Textures[path] = texture;
        }

        /** An image file behind an element drawn as a screen's background, or the game's own again for null. */
        public static void Show(VisualElement background, string path)
        {
            if (background == null) return;
            Texture2D texture = Picture(path);
            if (texture)
            {
                background.style.backgroundImage = new StyleBackground(texture);
                background.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }
            else
            {
                background.style.backgroundImage = StyleKeyword.Null;
                background.style.backgroundSize = StyleKeyword.Null;
            }
        }

        /** The element a screen draws its picture on, above an element of it. */
        public static VisualElement BackgroundAbove(VisualElement element)
        {
            while (element != null && element.name != BackgroundName) element = element.parent;
            return element;
        }

        /** The element a screen draws its picture on, in its root. */
        public static VisualElement BackgroundIn(VisualElement root) => root?.Q<VisualElement>(BackgroundName);
    }

    [Context("MainMenu")]
    public class MenuBackgroundsConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<NewGameBackground>().AsSingleton();
            containerDefinition.Bind<LoadingBackground>().AsSingleton();
        }
    }

    [Context("Game")]
    public class GameBackgroundsConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition) => containerDefinition.Bind<LoadingBackground>().AsSingleton();
    }

    /**
     * A mode's picture behind the New Game screens while it's the mode picked there (for the game the
     * page is making: alone or with others), and back to what was there when another is picked or the screen
     * goes. Looked at a few times a second: the pick and the screen come and go through the game
     * and mods, with no event for both.
     */
    public class NewGameBackground : IUpdatableSingleton
    {
        private const float Every = 0.2f;
        private readonly NewGameModePanel _newGame;
        private float _next;
        private string _shown;
        // What the element had before a mode's picture went on it
        private StyleBackground _image;
        private StyleBackgroundSize _size;

        public NewGameBackground(NewGameModePanel newGame) => _newGame = newGame;

        public void UpdateSingleton()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Every;
            VisualElement root = _newGame._root;
            bool open = root?.panel != null && root.resolvedStyle.display == DisplayStyle.Flex;
            string path = open ? Modes.Get(GameKind.OnPage.Mode)?.MenuPicture?.Invoke() : null;
            if (path == _shown) return;
            VisualElement background = Backgrounds.BackgroundAbove(root);
            if (background == null) return;
            if (path != null)
            {
                if (_shown == null)
                {
                    _image = background.style.backgroundImage;
                    _size = background.style.backgroundSize;
                }
                Backgrounds.Show(background, path);
            }
            else
            {
                background.style.backgroundImage = _image;
                background.style.backgroundSize = _size;
            }
            _shown = path;
        }
    }

    /**
     * Every load shows the loading screen: only the one started for a game in a mode with a picture gets it. The
     * screen outlives the scenes, so each scene that can start a load listens to it while it's up.
     */
    public class LoadingBackground : ILoadableSingleton, IUnloadableSingleton
    {
        private readonly LoadingScreen _loadingScreen;
        private readonly ILoc _loc;

        public LoadingBackground(LoadingScreen loadingScreen, ILoc loc)
        {
            _loadingScreen = loadingScreen;
            _loc = loc;
        }

        public void Load() => _loadingScreen.LoadingScreenEnabled += OnLoadingScreenEnabled;

        public void Unload() => _loadingScreen.LoadingScreenEnabled -= OnLoadingScreenEnabled;

        private void OnLoadingScreenEnabled(object sender, EventArgs e)
        {
            string mode = Backgrounds.NextLoadMode;
            Backgrounds.NextLoadMode = null;
            ShowTip(mode);
            string path = Backgrounds.LoadingPicture(mode);
            // Other games keep what the game just set: clearing it would drop the game's own picture
            if (path == null) return;
            Debug.Log($"[GameModes] Loading screen: the picture of {mode}");
            Backgrounds.Show(Backgrounds.BackgroundIn(_loadingScreen._root), path);
        }

        // Half the time, a hint of the mode or of the world takes the place of the game's
        private void ShowTip(string mode)
        {
            var tips = LoadingTips.Pool(mode);
            LoadingTips.NextWorld = new List<string>();
            if (tips.Count == 0 || UnityEngine.Random.value >= 0.5f) return;
            var label = _loadingScreen._root.Q<Label>("TipText");
            var wrapper = _loadingScreen._root.Q<VisualElement>("TipWrapper");
            if (label == null || wrapper == null) return;
            label.text = _loc.T(tips[UnityEngine.Random.Range(0, tips.Count)]);
            wrapper.style.display = DisplayStyle.Flex;
        }
    }
}
