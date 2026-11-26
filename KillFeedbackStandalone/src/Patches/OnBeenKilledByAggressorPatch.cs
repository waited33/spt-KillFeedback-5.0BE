using System;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using KillFeedback.Core;
using KillFeedback.Models;
using UnityEngine;

namespace KillFeedback.Patches
{
    /// <summary>
    /// Watches every death in the raid and feeds the kills made by the local player to the HUD.
    /// <para>
    /// The original mod hooked the same method through <c>KmyTarkovApi</c>'s
    /// <c>_PlayerHelper.OnBeenKilledByAggressor</c>. On this client the method grew an
    /// <c>EDamageType</c> parameter, which is why the signature below is spelled out in full
    /// instead of being looked up by name.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Player), "OnBeenKilledByAggressor",
        new[] { typeof(IPlayer), typeof(DamageInfo), typeof(EBodyPart), typeof(EDamageType) })]
    internal static class OnBeenKilledByAggressorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, IPlayer __0, DamageInfo __1, EBodyPart __2)
        {
            try
            {
                HandleKill(__instance, __0, __1, __2);
            }
            catch (Exception exception)
            {
                // A HUD must never take the game down with it.
                KillFeedbackPlugin.Logger?.LogError($"Kill feedback failed: {exception}");
            }
        }

        private static void HandleKill(Player victim, IPlayer aggressor, DamageInfo damageInfo, EBodyPart bodyPart)
        {
            if (victim == null || aggressor == null)
            {
                return;
            }

            var yourPlayer = KillFeedbackRuntime.YourPlayer;

            if (yourPlayer == null)
            {
                return;
            }

            // The aggressor arrives as IPlayer; compare the underlying object so the check does
            // not depend on which wrapper type the game handed us.
            if (!(aggressor is Il2CppObjectBase aggressorObject) ||
                aggressorObject.Pointer != yourPlayer.Pointer)
            {
                return;
            }

            var profile = victim.Profile;

            if (profile == null)
            {
                return;
            }

            var info = profile.Info;

            if (info == null)
            {
                return;
            }

            var settings = info.Settings;

            var hasMarkOfUnknown = aggressor.HasMarkOfUnknown(out var markOfUnknown);

            // ShortName is a localization key on this build ("<templateId> ShortName"), not a
            // display string, so it has to go through the game's own resolver before it reaches
            // the HUD.
            var weaponName = ResolveWeaponName(damageInfo, out var rawWeaponName);

            var distance = Vector3.Distance(yourPlayer.Position, victim.Position);

            var killModel = new KillModel
            {
                PlayerName = info.Nickname,
                WeaponName = weaponName,
                Part = bodyPart,
                Distance = distance,
                Level = info.Level,
                Side = info.Side,
                Exp = settings != null ? settings.Experience : -1,
                Role = settings != null ? settings.Role : WildSpawnType.assault,
                KillCount = KillFeedbackRuntime.KillCount++,
                ScavKillExpPenalty = markOfUnknown != null ? markOfUnknown.ScavKillExpPenalty : 0f,
                HasMarkOfUnknown = hasMarkOfUnknown,
                IsAI = victim.IsAI,
                IsTest = false
            };

            // One line per kill: the only way to tell what the game actually handed over for the
            // weapon, which is the one field the HUD cannot fake.
            KillFeedbackPlugin.Logger?.LogInfo(
                $"Kill captured: name='{killModel.PlayerName}', weapon='{weaponName}' (key='{rawWeaponName}'), " +
                $"part={bodyPart}, distance={distance:F1}m, level={killModel.Level}, " +
                $"side={killModel.Side}, isAI={killModel.IsAI}, exp={killModel.Exp}");

            KillHUDModel.ShowKill?.Invoke(killModel);
        }

        /// <summary>
        /// Turns the weapon carried by the damage info into something a player can read.
        /// <para>
        /// <c>Item.ShortName</c> and <c>Item.Name</c> both return the localization key on this
        /// build (<c>"&lt;templateId&gt; ShortName"</c>), so the game's own
        /// <c>LocalizationExtensions</c> helpers are asked first; they know how the item keys are
        /// stored and return the translated text. Each step is guarded because a missing template
        /// must never take a kill down with it.
        /// </para>
        /// </summary>
        /// <param name="rawWeaponName">The untouched key, reported in the log for diagnosis.</param>
        private static string ResolveWeaponName(DamageInfo damageInfo, out string rawWeaponName)
        {
            rawWeaponName = null;

            try
            {
                var weapon = damageInfo.Weapon;

                if (weapon == null)
                {
                    return null;
                }

                rawWeaponName = SafeRead(() => weapon.ShortName);

                var fullName = SafeRead(() => weapon.Name);

                var candidates = new[]
                {
                    SafeRead(() => LocalizationExtensions.LocalizedShortName(weapon)),
                    SafeRead(() => LocalizationExtensions.LocalizedName(weapon)),
                    LocalizedHelper.Localized(rawWeaponName),
                    LocalizedHelper.Localized(fullName)
                };

                foreach (var candidate in candidates)
                {
                    if (LocalizedHelper.IsResolved(candidate))
                    {
                        return candidate;
                    }
                }

                KillFeedbackPlugin.Logger?.LogWarning(
                    $"Weapon name could not be localized (key '{rawWeaponName}'); hiding the bracket.");

                return null;
            }
            catch (Exception exception)
            {
                KillFeedbackPlugin.Logger?.LogWarning(
                    $"Could not read the weapon of a kill: {exception.Message}");

                return null;
            }
        }

        /// <summary>Runs a lookup that is allowed to fail; a broken template is not fatal.</summary>
        private static string SafeRead(Func<string> read)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
