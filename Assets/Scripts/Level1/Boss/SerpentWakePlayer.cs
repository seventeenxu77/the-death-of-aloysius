using UnityEngine;
namespace Aloysius.Level1.Boss
{
    public sealed class SerpentWakePlayer : MonoBehaviour
    {
        public SerpentBedWake bedWake;
        public SerpentWakeData clip;
        public Transform[] bones;
        public SkinnedMeshRenderer[] heads;
        public Renderer tongue;
        [Min(.01f)] public float playbackSpeed=1;
        public bool playOnStart;
        [Tooltip("床内排布的头部比例补偿，避免整体铺床缩放压扁面部")]
        public Vector2 headProportion=new Vector2(.76f,1.24f);
        [SerializeField] float time;
        bool legacyPlaying;
        public bool Playing {get=>bedWake?bedWake.Playing:legacyPlaying;private set=>legacyPlaying=value;}
        public bool Finished => bedWake?bedWake.Finished:clip && time>=clip.duration;
        MaterialPropertyBlock properties;
        void Start(){Sample(0);if(playOnStart)Play();}
        public void Play(){if(bedWake){bedWake.Play();return;}time=0;Playing=true;Sample(0);}
        public void ResetPose(){if(bedWake){bedWake.ResetPose();return;}Playing=false;time=0;Sample(0);}
        void Update(){if(bedWake)return;if(Playing){time=Mathf.Min(clip.duration,time+Time.deltaTime*playbackSpeed);Sample(time);if(Finished)Playing=false;}}
        public void Sample(float seconds)
        {
            if(bedWake){bedWake.Sample(seconds);return;}
            if(!clip||clip.frames==null||clip.frames.Length==0)return;
            time=Mathf.Clamp(seconds,0,clip.duration);
            float f=time*clip.fps;int a=Mathf.Min((int)f,clip.frames.Length-1),b=Mathf.Min(a+1,clip.frames.Length-1);float t=f-a;
            var x=clip.frames[a];var y=clip.frames[b];
            for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=new Vector3(Mathf.Lerp(x.positions[i*2],y.positions[i*2],t),Mathf.Lerp(x.positions[i*2+1],y.positions[i*2+1],t),0);
                bones[i].localRotation=Quaternion.Euler(0,0,Mathf.LerpAngle(x.angles[i],y.angles[i],t));
                if(bones[i].name.EndsWith("_Head"))bones[i].localScale=new Vector3(headProportion.x,headProportion.y,1);
            }
            if(properties==null)properties=new MaterialPropertyBlock();
            for(int i=0;i<heads.Length;i++){heads[i].GetPropertyBlock(properties);properties.SetFloat("_Phase",Mathf.Lerp(x.phases[i],y.phases[i],t));heads[i].SetPropertyBlock(properties);}
            if(tongue)tongue.enabled=Mathf.Lerp(x.phases[2],y.phases[2],t)>=.8f;
        }
    }
}
