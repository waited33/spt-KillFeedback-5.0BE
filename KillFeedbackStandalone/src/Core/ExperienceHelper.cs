using System;
using EFT;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KillFeedback.Core
{
    /// <summary>
    /// Reproduces the game's kill-XP maths so the HUD can show how much experience a kill was worth.
    /// <para>
    /// The original mod read these values out of the backend's global configuration through
    /// reflection (<c>KmyTarkovApi</c>'s <c>ExperienceData</c>, which walked
    /// <c>Session.BackEndConfig.Config.Experience.Kill</c>). The same chain is reachable directly
    /// on this client, but <c>IEftSession</c> no longer exposes <c>BackEndConfig</c>: only the
    /// concrete <c>IClientSession</c> does, so the session is resolved once from
    /// <c>TarkovApplication</c> and cast.
    /// </para>
    /// </summary>
    internal static class ExperienceHelper
    {
        private static GlobalConfiguration _config;
        private static bool _resolveFailureLogged;

        /// <summary>True once the backend configuration has been located.</summary>
        public static bool HasSettings
        {
            get
            {
                var settings = KillSettings;

                return settings != null;
            }
        }

        /// <summary>
        /// The kill experience settings, or null while the backend configuration is not available.
        /// </summary>
        public static GlobalConfiguration.ExperienceSettings.KillSettings KillSettings
        {
            get
            {
                try
                {
                    var config = GetConfig();

                    if (config == null)
                    {
                        return null;
                    }

                    var experience = config.Experience;

                    if (experience == null)
                    {
                        return null;
                    }

                    return experience.Kill;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Locates the backend global configuration, caching it once found.
        /// </summary>
        private static GlobalConfiguration GetConfig()
        {
            if (_config != null)
            {
                return _config;
            }

            try
            {
                var application = Object.FindObjectOfType<TarkovApplication>();

                if (application == null)
                {
                    return null;
                }

                var session = application.Session;

                if (session == null)
                {
                    return null;
                }

                // The object behind IEftSession is the concrete client session, which is where the
                // backend configuration lives; the interface is the last resort.
                var response = GetBackEndConfig(session);

                if (response == null)
                {
                    return null;
                }

                var config = response.Config;

                if (config == null)
                {
                    return null;
                }

                _config = config;

                KillFeedbackPlugin.Logger?.LogInfo("Backend configuration located; kill XP is available.");

                return _config;
            }
            catch (Exception exception)
            {
                if (!_resolveFailureLogged)
                {
                    _resolveFailureLogged = true;

                    KillFeedbackPlugin.Logger?.LogWarning(
                        $"Could not read the backend configuration, kill XP will show as 0: {exception.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Reads <c>BackEndConfig</c> off whichever session implementation the client is running.
        /// The property is declared on <c>IClientSession</c>, which <c>IEftSession</c> does not
        /// derive from, so the object is re-typed first.
        /// </summary>
        private static JsonType.GlobalConfigurationResponse GetBackEndConfig(IEftSession session)
        {
            var backendSession = session.TryCast<ClientBackendSession>();

            if (backendSession != null)
            {
                return backendSession.BackEndConfig;
            }

            var emulatorSession = session.TryCast<ClientBackEndEmulator.ClientBackendSessionEmulator>();

            if (emulatorSession != null)
            {
                return emulatorSession.BackEndConfig;
            }

            var clientSession = session.TryCast<IClientSession>();

            return clientSession != null ? clientSession.BackEndConfig : null;
        }

        /// <summary>
        /// Base experience for a kill, before streak and headshot bonuses.
        /// </summary>
        public static int GetBaseExp(int exp, EPlayerSide side, WildSpawnType role, float scavKillExpPenalty,
            bool hasMarkOfUnknown, bool isAI)
        {
            var settings = KillSettings;

            if (settings == null)
            {
                return 0;
            }

            switch (side)
            {
                case EPlayerSide.Usec:
                case EPlayerSide.Bear:
                    return settings.VictimLevelExp;

                case EPlayerSide.Savage:
                    if (exp < 0)
                    {
                        exp = settings.VictimBotLevelExp;
                    }

                    if ((role == WildSpawnType.assault || role == WildSpawnType.marksman || !isAI) &&
                        hasMarkOfUnknown)
                    {
                        exp = (int)Math.Ceiling(exp * scavKillExpPenalty);
                    }

                    return exp;

                default:
                    return 0;
            }
        }

        /// <summary>Experience gained by a headshot, scaled by the game's headshot multiplier.</summary>
        public static int GetHeadExp(int baseExp, EPlayerSide side)
        {
            var settings = KillSettings;

            if (settings == null)
            {
                return 0;
            }

            var multiplier = side != EPlayerSide.Savage ? settings.PmcHeadShotMult : settings.BotHeadShotMult;

            return (int)(baseExp * multiplier);
        }

        /// <summary>Experience gained from a kill streak, scaled by the game's combo bonus.</summary>
        public static int GetStreakExp(int baseExp, int kills)
        {
            var settings = KillSettings;

            if (settings == null)
            {
                return 0;
            }

            return (int)(baseExp * settings.GetKillingBonusPercent(kills) / 100f);
        }
    }
}
