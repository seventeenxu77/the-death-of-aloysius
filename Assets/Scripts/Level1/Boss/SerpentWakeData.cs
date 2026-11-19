using System;
using UnityEngine;
namespace Aloysius.Level1.Boss
{
    public sealed class SerpentWakeData : ScriptableObject
    {
        public int fps=30;
        public float duration=15;
        public WakeFrame[] frames;
    }
    [Serializable] public class WakeFrame { public float[] angles,positions,phases; }
    [Serializable] public class WakeJoint { public string name; public int parent; public float x,y; }
    [Serializable] public class WakeMesh { public string name,texture; public int part; public float[] vertices,uv,weights; public int[] joints,indices; }
    [Serializable] public class WakeImport { public int width,height,fps; public float duration; public WakeJoint[] joints; public WakeMesh[] meshes; public WakeFrame[] frames; }
}
