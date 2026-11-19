using UnityEngine;

namespace Aloysius.SunCourt
{
    /// <summary>Opt-in and art controls for the Sun Court's screen-space paper finish.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [AddComponentMenu("Sun Court/Paper Fresco Camera")]
    public sealed class PaperFrescoCamera : MonoBehaviour
    {
        [Header("Overall finish")]
        [Range(0f, 1f)] public float effectStrength = 1f;

        [Header("Thin painted contour")]
        [Range(0f, 1f)] public float inkStrength = .30f;
        [Range(.5f, 3f)] public float lineWidth = 1f;
        [Tooltip("Depth discontinuity in world units. Raise this to omit small architectural seams.")]
        [Range(.01f, .5f)] public float depthThreshold = .08f;
        public Color inkColor = new Color(.33f, .20f, .14f, 1f);

        [Header("Stable paper grain")]
        [Range(0f, .15f)] public float paperStrength = .035f;
        [Tooltip("Scale of screen-fixed paper fibres; 1 is a fine paper surface at native resolution.")]
        [Range(.5f, 3f)] public float paperScale = 1f;

        [Header("Printed color range")]
        [Tooltip("Gently brings very dark and bright values into a printed color range. No RGB posterization.")]
        [Range(0f, 1f)] public float paletteCompression = .18f;
        [Range(0f, 1f)] public float warmth = .10f;
    }
}
