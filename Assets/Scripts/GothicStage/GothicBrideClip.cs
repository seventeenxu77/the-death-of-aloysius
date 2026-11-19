using System;
using UnityEngine;

namespace Aloysius.GothicStage
{
    [Serializable]
    public sealed class GothicBonePose
    {
        public float[] x;
        public float[] y;
        public float[] angle;
    }

    public sealed class GothicBrideClip : ScriptableObject
    {
        public float cycleSeconds = 1.16f;
        public float stridePixels = 360f;
        public float pixelsPerUnit = 340f;
        public float floor = 630f;
        public GothicBonePose bind;
        public GothicBonePose[] frames;
        [Tooltip("Optional independent running cycle, phase aligned with this walk.")]
        public GothicBrideClip sprint;
        [Tooltip("Anticipation, takeoff, flight and landing poses for this skeleton.")]
        public GothicBrideJumpClip jump;
    }
}
