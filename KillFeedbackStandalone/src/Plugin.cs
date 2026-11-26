using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using KillFeedback.Core;
using KillFeedback.Models;
using KillFeedback.Utils;
using KillFeedback.Views;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KillFeedback
{
    /// <summary>
    /// Standalone port of kmyuhkyuk's GamePanelHUD kill feedback for the 5.0 client.
    /// <para>
    /// The original shipped as one of nine HUD modules that all depended on the
    /// <c>GamePanelHUDCore</c> plugin and on <c>KmyTarkovApi</c> (a reflection layer written for
    /// the Mono build). Neither exists here, so this plugin carries its own copies of the few
    /// things it used to borrow: the per-frame update pump, the HUD canvas, the localization
    /// helpers and the experience maths.
    /// </para>
    /// </summary>
    [BepInPlugin(Guid, PluginName, Version)]
    public sealed class KillFeedbackPlugin : BasePlugin
    {
        public const string Guid = "com.killfeedback.standalone";
        public const string PluginName = "KillFeedback-5.0BE";
        public const string Version = "5.0.0";

        private const string BundleFileName = "gamepanelkillhud.bundle";
        private const string BundleResourceName = "KillFeedback.gamepanelkillhud.bundle";

        /// <summary>Root of the HUD, kept alive across scenes.</summary>
        internal static GameObject HudRoot;

        internal static ManualLogSource Logger;

        private static KillFeedbackPlugin _instance;

        private AssetBundle _bundle;

        private static bool _panelRefreshRequested;

        /// <summary>True once the HUD exists and is wired up to receive kills.</summary>
        internal static bool HudReady => HudRoot != null && KillHUDModel.ShowKill != null;

        /// <summary>
        /// Asks for the Configuration Manager window to be rebuilt on the next frame, which is how
        /// a language change becomes visible without closing and reopening the window.
        /// </summary>
        internal static void RequestPanelRefresh()
        {
            _panelRefreshRequested = true;
        }

        /// <summary>
        /// Rebuilds the settings list of the Configuration Manager when a language change is
        /// pending. Called from the driver: the request is always made from inside the manager's
        /// own drawing pass, where rebuilding its list would corrupt the iteration in progress.
        /// </summary>
        internal static void RefreshPanelIfRequested()
        {
            if (!_panelRefreshRequested)
            {
                return;
            }

            _panelRefreshRequested = false;

            try
            {
                var loader = IL2CPPChainloader.Instance;

                if (loader == null)
                {
                    return;
                }

                foreach (var pair in loader.Plugins)
                {
                    var plugin = pair.Value?.Instance;

                    // The manager is not referenced by this assembly, so it is matched by name and
                    // its list is rebuilt through reflection.
                    if (plugin == null ||
                        plugin.GetType().FullName != "ConfigurationManager.ConfigurationManager")
                    {
                        continue;
                    }

                    var rebuild = plugin.GetType().GetMethod("BuildSettingList",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    rebuild?.Invoke(plugin, null);

                    Logger?.LogInfo("Settings panel rebuilt for the new language.");

                    return;
                }
            }
            catch (Exception exception)
            {
                Logger?.LogWarning(
                    $"Could not rebuild the settings panel; reopen the window to see the new labels: {exception.Message}");
            }
        }

        /// <summary>
        /// Rebuilds the HUD when it went missing, which happens when the game unloads the scene
        /// the HUD was created in. Called by the driver, never from the render loop directly.
        /// </summary>
        internal static void EnsureHud()
        {
            var instance = _instance;

            if (instance == null)
            {
                return;
            }

            try
            {
                instance.BuildHud();
            }
            catch (Exception exception)
            {
                Logger?.LogError($"Could not rebuild the kill HUD: {exception}");
            }
        }

        public override void Load()
        {
            Logger = Log;
            _instance = this;

            Logger.LogInfo($"{PluginName} {Version} loading");

            try
            {
                SettingsModel.Create(Config);
            }
            catch (Exception exception)
            {
                // A single unsupported setting type aborts the whole plugin, which then silently
                // disappears from the Configuration Manager. Keep it loud instead.
                Logger.LogError($"Could not create the settings; defaults will be used: {exception}");

                SettingsModel.CreateDefaults();
            }

            // The HUD views are ordinary MonoBehaviours compiled into this assembly. IL2CPP does
            // not know about them until they are injected, and AddComponent would throw otherwise.
            RegisterBehaviour<KillFeedbackBehaviour>();
            RegisterBehaviour<KillFeedbackDriver>();
            RegisterBehaviour<KillHUDView>();
            RegisterBehaviour<KillUIView>();
            RegisterBehaviour<ExpUIView>();

            try
            {
                new Harmony(Guid).PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not apply the kill patch; the HUD will not see any kills: {exception}");
            }

            AddComponent<KillFeedbackDriver>();

            // Built here so the HUD exists from the first frame, and built again by the driver if
            // the game destroys it while swapping scenes (Load runs before the first scene exists,
            // so that is a real possibility on this client).
            try
            {
                BuildHud();
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not build the kill HUD: {exception}");
            }
        }

        /// <summary>Injects a MonoBehaviour into IL2CPP, tolerating an already-injected type.</summary>
        private static void RegisterBehaviour<T>() where T : MonoBehaviour
        {
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<T>();
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"IL2CPP type injection for {typeof(T).Name} reported: {exception.Message}");
            }
        }

        /// <summary>
        /// Creates the overlay canvas and instantiates the prefabs out of the bundle.
        /// </summary>
        private void BuildHud()
        {
            if (HudRoot != null)
            {
                Object.Destroy(HudRoot);
                HudRoot = null;
            }

            var bundle = _bundle != null ? _bundle : (_bundle = LoadBundle());

            if (bundle == null)
            {
                return;
            }

            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);

            var assets = bundle.LoadAllAssets();

            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];

                if (asset == null)
                {
                    continue;
                }

                var prefab = asset.TryCast<GameObject>();

                if (prefab != null && !prefabs.ContainsKey(prefab.name))
                {
                    prefabs[prefab.name] = prefab;
                }
            }

            if (!prefabs.TryGetValue("GamePanelKillHUD", out var hudPrefab))
            {
                Logger.LogError($"'{BundleFileName}' does not contain the 'GamePanelKillHUD' prefab.");

                return;
            }

            HudRoot = new GameObject("KillFeedbackHUD");

            var canvas = HudRoot.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SettingsModel.CanvasSortingOrder.Value;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                              AdditionalCanvasShaderChannels.Normal |
                                              AdditionalCanvasShaderChannels.Tangent;

            HudRoot.AddComponent<CanvasScaler>();

            Object.DontDestroyOnLoad(HudRoot);

            var hud = Object.Instantiate(hudPrefab, HudRoot.transform);

            hud.name = "GamePanelKillHUD";
            hud.transform.localPosition = Vector3.zero;

            // The prefab carries its own Canvas. Nesting one render mode inside another is not
            // supported and can silently hide everything, so the HUD renders on our canvas only.
            var innerCanvas = hud.GetComponent<Canvas>();

            if (innerCanvas != null)
            {
                Object.Destroy(innerCanvas);
            }

            var innerScaler = hud.GetComponent<CanvasScaler>();

            if (innerScaler != null)
            {
                Object.Destroy(innerScaler);
            }

            // The prefab's own scripts (KillHUDView, KillUIView, ExpUIView, KillHUDController) are
            // not part of the game, so Unity drops them while loading and the serialized
            // references they held are gone. The components are re-added here and find their
            // children by name instead.
            var killPanel = hud.transform.Find("KillPanel");

            if (killPanel == null)
            {
                Logger.LogError("The kill HUD prefab is missing its 'KillPanel' child.");

                return;
            }

            var exp = killPanel.Find("Exp");

            if (exp != null && exp.GetComponent<ExpUIView>() == null)
            {
                exp.gameObject.AddComponent<ExpUIView>();
            }

            var hudView = killPanel.gameObject.AddComponent<KillHUDView>();

            hudView.Initialize();

            var missingFonts = FontHelper.CountWithoutFont(hud, out var textCount);

            Logger.LogInfo(
                $"Kill HUD built (scene '{HudRoot.scene.name}', ready={HudReady}, texts={textCount}, " +
                $"without font={missingFonts}, screen={Screen.width}x{Screen.height})");
        }

        /// <summary>
        /// Loads the prefab bundle, preferring the copy next to the DLL and falling back to the
        /// copy embedded in this assembly.
        /// </summary>
        private AssetBundle LoadBundle()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                            ?? BepInEx.Paths.PluginPath;

            var bundlePath = Path.Combine(directory, BundleFileName);

            try
            {
                if (!File.Exists(bundlePath))
                {
                    ExtractEmbeddedBundle(bundlePath);
                }

                var bundle = AssetBundle.LoadFromFile(bundlePath);

                if (bundle == null)
                {
                    Logger.LogError($"Could not load '{bundlePath}'.");
                }

                return bundle;
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not load '{bundlePath}': {exception}");

                return null;
            }
        }

        private static void ExtractEmbeddedBundle(string destination)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(BundleResourceName))
            {
                if (stream == null)
                {
                    Logger.LogError(
                        $"'{BundleFileName}' is missing next to the DLL and is not embedded in it either.");

                    return;
                }

                var directory = Path.GetDirectoryName(destination);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var file = File.Create(destination))
                {
                    stream.CopyTo(file);
                }
            }

            Logger.LogInfo($"Extracted the embedded bundle to '{destination}'.");
        }
    }
}
