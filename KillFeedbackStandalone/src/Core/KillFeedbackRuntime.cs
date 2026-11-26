using System;
using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace KillFeedback.Core
{
    /// <summary>
    /// Base class for anything that wants to be ticked once per frame by the HUD.
    /// <para>
    /// The original mod relied on <c>GamePanelHUDCore</c>'s <c>UpdateManger</c>, which lived in a
    /// separate plugin. The standalone port has no such plugin, so the manager lives here and is
    /// driven by <c>KillFeedbackDriver</c>. A base class is used rather than an interface because
    /// these types are injected into IL2CPP, which only understands the class hierarchy.
    /// </para>
    /// </summary>
    public class KillFeedbackBehaviour : MonoBehaviour
    {
        /// <summary>Called once per frame while the behaviour is registered.</summary>
        public virtual void CustomUpdate()
        {
        }
    }

    /// <summary>Minimal per-frame update fan-out that survives elements destroying themselves.</summary>
    internal sealed class UpdateManager
    {
        private readonly List<KillFeedbackBehaviour> _updates = new List<KillFeedbackBehaviour>();
        private readonly List<KillFeedbackBehaviour> _removeUpdates = new List<KillFeedbackBehaviour>();

        public void Register(KillFeedbackBehaviour update)
        {
            if (update == null || _updates.Contains(update))
            {
                return;
            }

            _updates.Add(update);
        }

        public void Remove(KillFeedbackBehaviour update)
        {
            if (update != null && !_removeUpdates.Contains(update))
            {
                _removeUpdates.Add(update);
            }
        }

        public void Update()
        {
            // Destroyed elements unregister themselves from OnDestroy, which can happen in the
            // middle of a tick; the removals are therefore applied at the start of the next one.
            if (_removeUpdates.Count > 0)
            {
                for (var i = 0; i < _removeUpdates.Count; i++)
                {
                    _updates.Remove(_removeUpdates[i]);
                }

                _removeUpdates.Clear();
            }

            var count = _updates.Count;

            for (var i = 0; i < count && i < _updates.Count; i++)
            {
                _updates[i].CustomUpdate();
            }
        }
    }

    /// <summary>
    /// Per-raid state the HUD needs: the local player and the raid's kill counter.
    /// </summary>
    internal static class KillFeedbackRuntime
    {
        private static readonly UpdateManager Manager = new UpdateManager();

        /// <summary>The local player, or null while not in a raid.</summary>
        public static Player YourPlayer { get; private set; }

        /// <summary>Number of kills made this raid; drives the streak bonus.</summary>
        public static int KillCount;

        public static UpdateManager UpdateManager => Manager;

        public static bool HasPlayer => YourPlayer != null;

        /// <summary>Refreshes <see cref="YourPlayer"/> from the current game world.</summary>
        public static void RefreshPlayer()
        {
            var player = FindLocalPlayer();

            if (player == null)
            {
                if (YourPlayer != null)
                {
                    ResetRaid();
                }

                return;
            }

            YourPlayer = player;
        }

        private static Player FindLocalPlayer()
        {
            try
            {
                var world = Comfort.Common.Singleton<GameWorld>.Instance;

                if (world != null)
                {
                    var mainPlayer = world.MainPlayer;

                    if (mainPlayer != null)
                    {
                        return mainPlayer;
                    }
                }
            }
            catch (Exception)
            {
                // Singleton<GameWorld> is not available in every game state; fall through.
            }

            try
            {
                return GamePlayerOwner.MyPlayer;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void ResetRaid()
        {
            YourPlayer = null;
            KillCount = 0;
        }
    }
}
