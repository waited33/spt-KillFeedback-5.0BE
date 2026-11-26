using System;

namespace KillFeedback.Models
{
    /// <summary>
    /// The only piece of state the HUD and the kill patch share: the patch raises
    /// <see cref="ShowKill"/>, the HUD view subscribes to it. Everything else lives inside the
    /// views, which are created by the plugin and ticked by the driver.
    /// </summary>
    internal static class KillHUDModel
    {
        /// <summary>Raised when a kill needs to be displayed.</summary>
        public static Action<KillModel> ShowKill;
    }
}
