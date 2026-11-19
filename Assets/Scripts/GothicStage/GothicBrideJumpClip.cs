using UnityEngine;

namespace Aloysius.GothicStage
{
    /// <summary>Non-looping jump key poses for the existing weighted 2D skeleton.</summary>
    public sealed class GothicBrideJumpClip : ScriptableObject
    {
        public GothicBonePose crouch;
        public GothicBonePose takeoff;
        public GothicBonePose rise;
        public GothicBonePose apex;
        public GothicBonePose contact;
        public GothicBonePose landing;
    }
}
