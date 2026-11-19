using UnityEngine;

namespace Aloysius.Level1.Boss
{
    // Saved Transform bones drive native SkinnedMeshRenderers. No pose-writing Update.
    public sealed class SerpentEditableRig : MonoBehaviour
    {
        public Transform[] bones;
        public string[] jointNames;
        public Vector3[] restLocalPositions;
        public bool showBones=true;
        public Transform FindBone(string name)
        {
            if(bones==null||jointNames==null)return null;
            for(int i=0;i<Mathf.Min(bones.Length,jointNames.Length);i++)if(jointNames[i]==name)return bones[i];
            return null;
        }
        public void RestoreRest()
        {
            if(bones==null||restLocalPositions==null)return;
            for(int i=0;i<Mathf.Min(bones.Length,restLocalPositions.Length);i++)if(bones[i])
            {bones[i].localPosition=restLocalPositions[i];bones[i].localRotation=Quaternion.identity;bones[i].localScale=Vector3.one;}
        }
    }
}
