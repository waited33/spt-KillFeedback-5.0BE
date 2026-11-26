using UnityEngine;
using KillFeedback.Models;

namespace KillFeedback.Core
{
    /// <summary>
    /// Ticks the HUD once per frame and keeps track of the local player.
    /// <para>
    /// This is the standalone replacement for the update pump that <c>GamePanelHUDCore</c> used to
    /// provide. It lives on the plugin's own (DontDestroyOnLoad) GameObject.
    /// </para>
    /// <para>
    /// It also owns the lifetime of the HUD. The overlay cannot be created in <c>Load()</c>: that
    /// runs before the first scene exists and the overlay does not survive the scene swap that
    /// follows. The first tick builds it, and any later disappearance (a scene change, or the
    /// game cleaning up) is repaired by rebuilding it.
    /// </para>
    /// </summary>
    public class KillFeedbackDriver : MonoBehaviour
    {
        private const float CheckInterval = 1f;
        private const float RebuildCooldown = 3f;
        private const int MaxLoggedAttempts = 5;

        private float _nextCheck;
        private float _nextRebuild;
        private int _attempts;
        private bool _ticking;

        private void Update()
        {
            if (!_ticking)
            {
                _ticking = true;

                KillFeedbackPlugin.Logger?.LogInfo(
                    $"KillFeedbackDriver is ticking (scene '{gameObject.scene.name}').");
            }

            KillFeedbackRuntime.RefreshPlayer();

            KillFeedbackRuntime.UpdateManager.Update();

            // Deferred from the settings panel: rebuilding the Configuration Manager list has to
            // happen outside its own drawing pass.
            KillFeedbackPlugin.RefreshPanelIfRequested();

            if (Time.unscaledTime < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + CheckInterval;

            if (KillFeedbackPlugin.HudReady || Time.unscaledTime < _nextRebuild)
            {
                return;
            }

            _nextRebuild = Time.unscaledTime + RebuildCooldown;
            _attempts++;

            if (_attempts > 1 && _attempts <= MaxLoggedAttempts)
            {
                KillFeedbackPlugin.Logger?.LogWarning(
                    "The kill HUD is missing (root=" +
                    (KillFeedbackPlugin.HudRoot != null ? "alive" : "gone") +
                    ", wired=" + (KillHUDModel.ShowKill != null ? "yes" : "no") +
                    "); rebuilding it (attempt " + _attempts + ").");
            }

            KillFeedbackPlugin.EnsureHud();
        }
    }
}
