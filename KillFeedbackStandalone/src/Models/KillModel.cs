using EFT;

namespace KillFeedback.Models
{
    /// <summary>Everything the HUD needs to render one kill line.</summary>
    internal struct KillModel
    {
        /// <summary>Nickname of the killed player.</summary>
        public string PlayerName;

        /// <summary>Short name of the weapon used, e.g. "AK-104".</summary>
        public string WeaponName;

        /// <summary>Distance between the shooter and the victim, in metres.</summary>
        public float Distance;

        /// <summary>Level of the killed player.</summary>
        public int Level;

        /// <summary>The victim's own experience, used as the scav XP base.</summary>
        public int Exp;

        /// <summary>Number of kills in the current streak.</summary>
        public int KillCount;

        /// <summary>Which side the victim belonged to.</summary>
        public EPlayerSide Side;

        /// <summary>Body part that took the fatal hit.</summary>
        public EBodyPart Part;

        /// <summary>Role of the victim, used to colour bosses and followers differently.</summary>
        public WildSpawnType Role;

        /// <summary>Experience penalty applied when killing an unknown scav.</summary>
        public float ScavKillExpPenalty;

        /// <summary>True when the victim was wearing the mark of unknown.</summary>
        public bool HasMarkOfUnknown;

        /// <summary>True when the victim was an AI rather than a human player.</summary>
        public bool IsAI;

        /// <summary>True for the synthetic kill produced by the "Test Kill" button.</summary>
        public bool IsTest;
    }
}
