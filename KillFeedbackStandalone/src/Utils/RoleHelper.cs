using EFT;

namespace KillFeedback.Utils
{
    /// <summary>Helpers for the victim's scav role.</summary>
    internal static class RoleHelper
    {
        /// <summary>True for scavs that act as a boss or as a boss' follower.</summary>
        public static bool IsBossOrFollower(this WildSpawnType role)
        {
            return IsBoss(role) || IsFollower(role);
        }

        /// <summary>True for boss roles.</summary>
        public static bool IsBoss(this WildSpawnType role)
        {
            // The game keeps the classification in a global extension class; IL2CPP interop
            // exposes it as plain statics rather than as extension methods.
            return WildSpawnTypeExtension.IsBoss(role);
        }

        /// <summary>True for boss follower roles.</summary>
        public static bool IsFollower(this WildSpawnType role)
        {
            return WildSpawnTypeExtension.IsFollower(role);
        }

        /// <summary>The localization key naming this scav role.</summary>
        public static string GetScavRoleKey(this WildSpawnType role)
        {
            return WildSpawnTypeExtension.GetScavRoleKey(role);
        }
    }
}
