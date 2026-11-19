using UnityEngine;

namespace Aloysius.Level1.Boss
{
    [ExecuteAlways]
    public sealed class FarSerpentPresentation : MonoBehaviour
    {
        public BlackGoldWakeSkin skin;
        public SerpentBedWake timingSource;
        [Min(20)] public float awakePoseTime=25;
        public bool animate=true;
        public StoneCeilingBreach ceilingBreach;
        void OnEnable()=>Refresh();
        void Update()=>Refresh();
        public void Refresh()
        {
            if(skin&&timingSource){if(ceilingBreach)ceilingBreach.PreparePose();skin.Sample(awakePoseTime+(Application.isPlaying&&animate?Time.time:0),timingSource);}
        }
        public void SampleForPreview(float motionTime)
        {if(skin&&timingSource){if(ceilingBreach)ceilingBreach.PreparePose();skin.Sample(awakePoseTime+motionTime,timingSource);}}
    }
}
