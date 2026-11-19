using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aloysius.Level1.Boss
{
    // The v023 preview's 70-bone translation skin, not a movie or a whole-picture dissolve.
    [ExecuteAlways]
    public sealed class SerpentBedWake : MonoBehaviour
    {
        [Serializable] public sealed class HeadTiming
        {
            [Min(0)] public float start;
            [Min(.1f)] public float duration=2.8f;
            [Min(0)] public float turnOffset;
            [Min(.1f)] public float turnDuration=1.6f;
            [Min(0)] public float eyeDelay=.05f;
            [Min(.01f)] public float eyeDuration=.18f;
            [Min(0)] public float idleAmplitude=1;
            public HeadTiming(float start,float offset,float turn){this.start=start;turnOffset=offset;turnDuration=turn;}
        }
        [Serializable] public sealed class PartArt {public Texture2D body;public Texture2D[] faces;}
        [Serializable] public sealed class Rig {public Part[] parts;}
        [Serializable] public sealed class Part
        {
            public string name;
            public float[] vertices,weights,startBones,endBones,headRects;
            public int[] indices;
            [NonSerialized] public Vector2[] currentBones;
            [NonSerialized] public Transform[] boneObjects;
            [NonSerialized] public Vector3[] deformed;
            [NonSerialized] public Mesh mesh,headMesh;
            [NonSerialized] public Vector3[] headVertices;
            [NonSerialized] public Material faceMaterial;
        }
        [Header("黑金四头蛇（优先使用）")]
        public BlackGoldWakeSkin blackGoldSkin;
        [Header("旧版美术备份")]
        public TextAsset rigData;
        public Material material;
        public Texture2D bedBackground,bedForeground;
        public PartArt[] art;
        [Header("从音乐开始计时，单位：秒")]
        public HeadTiming fang=new HeadTiming(1,.5f,1.6f);
        public HeadTiming venom=new HeadTiming(5.5f,.45f,1.6f);
        public HeadTiming stone=new HeadTiming(12,.4f,1.7f);
        public HeadTiming timeHead=new HeadTiming(17,0,1);
        [Min(20)] public float duration=20;
        [Header("窗帘完全拉开后同步播放")]
        public AudioSource music;
        public bool playFullMusic=true;
        [Min(.1f),Tooltip("仅在关闭完整播放后使用")] public float musicSeconds=25;
        public double MusicDuration=>music&&music.clip?(playFullMusic?music.clip.length:Math.Min(musicSeconds,music.clip.length)):0;
        [SerializeField,Range(0,25)] float previewTime;
        public bool Playing {get;private set;}
        public bool Finished {get;private set;}
        public float TimeSeconds {get;private set;}
        public float Duration => Mathf.Max(duration,End(fang),End(venom),End(stone),End(timeHead));
        Rig rig;
        float sampledAt=float.NaN;
        Transform generated;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        double startedAt;
        bool musicPending;
        float End(HeadTiming h)=>blackGoldSkin?h.start+h.duration+h.eyeDelay+h.eyeDuration:h.start+Mathf.Max(h.duration,h.turnOffset+h.turnDuration);
        static float Ease(float f){f=Mathf.Clamp01(f);return f*f*f*(10+f*(-15+6*f));}
        static Vector3 Local(float x,float y)=>new Vector3(x*.01f,-y*.01f,0);
        HeadTiming Timing(string name)=>name=="Fang"?fang:name=="Venom"?venom:name=="Stone"?stone:timeHead;
        float Amount(HeadTiming h,float t)=>Ease((t-h.start)/Mathf.Max(.1f,h.duration));
        void OnEnable(){EnsureRig();Sample(Application.isPlaying?0:previewTime);}
        void OnDisable(){StopMusic();Playing=false;Clear();}
        void OnValidate(){previewTime=Mathf.Clamp(previewTime,0,Duration);sampledAt=float.NaN;}
        void Update()
        {
            if(!Application.isPlaying){Sample(previewTime);return;}
            if(Playing||(Finished&&blackGoldSkin))
            {
                float elapsed=Mathf.Max(0,(float)(AudioSettings.dspTime-startedAt));
                Sample(blackGoldSkin?elapsed:Mathf.Min(elapsed,Duration));
                if(elapsed>=Duration){Playing=false;Finished=true;}
            }
            if(musicPending && AudioSettings.dspTime-startedAt>=MusicDuration)StopMusic();
        }
        public void Play()
        {
            StopMusic();Sample(0);Finished=false;Playing=true;
            // Both animation and audio use this DSP clock; head cues do not drift with frame drops.
            startedAt=AudioSettings.dspTime+.08;
            if(music&&music.clip){music.pitch=1;music.time=0;music.PlayScheduled(startedAt);if(!playFullMusic)music.SetScheduledEndTime(startedAt+MusicDuration);musicPending=true;}
        }
        public void ResetPose(){StopMusic();Playing=Finished=false;previewTime=0;Sample(0);}
        public void StopMusic(){if(music)music.Stop();musicPending=false;}
        void Clear()
        {
            if(blackGoldSkin)blackGoldSkin.Clear();
            if(generated){if(Application.isPlaying)Destroy(generated.gameObject);else DestroyImmediate(generated.gameObject);}
            foreach(var o in owned)if(o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
            owned.Clear();generated=null;rig=null;sampledAt=float.NaN;
        }
        Transform Child(string label,Transform parent)
        {
            var go=new GameObject(label){hideFlags=HideFlags.DontSave};go.transform.SetParent(parent,false);return go.transform;
        }
        Mesh MakeMesh(string label,Vector3[] vertices,Vector2[] uv,int[] indices)
        {
            var m=new Mesh{name=label,hideFlags=HideFlags.DontSave};m.MarkDynamic();m.vertices=vertices;m.uv=uv;m.triangles=indices;m.RecalculateBounds();owned.Add(m);return m;
        }
        Material Draw(string label,Mesh mesh,Texture2D texture,int order)
        {
            var t=Child(label,generated);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=t.gameObject.AddComponent<MeshRenderer>();var mat=new Material(material){name=label,hideFlags=HideFlags.DontSave};
            mat.SetTexture("_MainTex",texture);mat.SetTexture("_NextTex",texture);owned.Add(mat);r.sharedMaterial=mat;r.sortingOrder=order;return mat;
        }
        static readonly int[] QuadIndices={0,1,2,1,3,2};
        static readonly Vector2[] QuadUV={new Vector2(0,1),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0)};
        static Vector3[] Quad(float x,float y,float w,float h)=>new[]{Local(x,y),Local(x+w,y),Local(x,y+h),Local(x+w,y+h)};
        void EnsureRig()
        {
            if(blackGoldSkin){blackGoldSkin.EnsureRig();return;}
            if(rig!=null||!rigData||!material||art==null)return;
            rig=JsonUtility.FromJson<Rig>(rigData.text);generated=Child("v023 generated skin and bones",transform);
            Draw("Bed background",MakeMesh("Bed",Quad(0,0,1536,1024),QuadUV,QuadIndices),bedBackground,0);
            int order=1;
            for(int n=0;n<rig.parts.Length;n++)
            {
                var p=rig.parts[n];int count=p.vertices.Length/2,bones=p.startBones.Length/2;
                p.deformed=new Vector3[count];var uv=new Vector2[count];
                for(int i=0;i<count;i++){float x=p.vertices[2*i],y=p.vertices[2*i+1];p.deformed[i]=Local(x,y);uv[i]=new Vector2(x/1536f,1-y/1024f);}
                p.mesh=MakeMesh(p.name,p.deformed,uv,p.indices);Draw(p.name+" weighted skin",p.mesh,art[n].body,order++);
                p.currentBones=new Vector2[bones];p.boneObjects=new Transform[bones];var chain=Child(p.name+" bones",generated);
                for(int k=0;k<bones;k++)p.boneObjects[k]=Child("Bone "+k,chain);
                if(p.headRects.Length>0)
                {
                    p.headVertices=new Vector3[4];p.headMesh=MakeMesh(p.name+" face",p.headVertices,QuadUV,QuadIndices);
                    p.faceMaterial=Draw(p.name+" turning frames",p.headMesh,art[n].faces[0],order++);
                }
            }
            Draw("Stationary bed foreground",MakeMesh("Bed foreground",Quad(0,0,1536,1024),QuadUV,QuadIndices),bedForeground,order);
        }
        public void Sample(float seconds)
        {
            if(blackGoldSkin){TimeSeconds=Mathf.Clamp(seconds,0,Duration);blackGoldSkin.Sample(Mathf.Max(0,seconds),this);return;}
            EnsureRig();if(rig==null)return;TimeSeconds=Mathf.Clamp(seconds,0,Duration);
            if(sampledAt==TimeSeconds)return;sampledAt=TimeSeconds;
            for(int n=0;n<rig.parts.Length;n++)
            {
                var p=rig.parts[n];bool neck=p.headRects.Length>0;var h=Timing(p.name);
                float t=TimeSeconds-(p.name=="Tail"?.3f:0),a=neck?Amount(h,t):(Amount(fang,t)+Amount(venom,t)+Amount(stone,t))/3;
                int count=p.currentBones.Length;float local=(t-h.start)/Mathf.Max(.1f,h.duration);
                float anticipation=local>0&&local<.16f?-Mathf.Sin(local/.16f*Mathf.PI)*5:0;
                for(int k=0;k<count;k++)
                {
                    float q=k/(float)(count-1),delay=(1-q)*.06f,f=neck?Ease((local-delay)/(1-delay)):a;
                    var pos=new Vector2(Mathf.Lerp(p.startBones[k*2],p.endBones[k*2],f),Mathf.Lerp(p.startBones[k*2+1],p.endBones[k*2+1],f)+(neck?anticipation*q:0));
                    p.currentBones[k]=pos;p.boneObjects[k].localPosition=Local(pos.x,pos.y);
                }
                float bx=0,by=0,bw=0,bh=0;var b=p.headRects;
                if(neck){bx=Mathf.Lerp(b[0],b[4],a);by=Mathf.Lerp(b[1],b[5],a);bw=Mathf.Lerp(b[2],b[6],a);bh=Mathf.Lerp(b[3],b[7],a);}
                for(int i=0;i<p.deformed.Length;i++)
                {
                    float x=p.vertices[i*2],y=p.vertices[i*2+1],dx=0,dy=0;
                    for(int k=0;k<count;k++){float w=p.weights[i*count+k];dx+=w*(p.currentBones[k].x-p.endBones[k*2]);dy+=w*(p.currentBones[k].y-p.endBones[k*2+1]);}
                    float px=x+dx,py=y+dy;
                    if(neck)
                    {
                        float weight=Mathf.Clamp01((x-b[4]+30)/60)*Mathf.Clamp01((b[4]+b[6]+30-x)/60)*Mathf.Clamp01((y-b[5]+50)/60)*Mathf.Clamp01((b[5]+b[7]+70-y)/120);
                        px=Mathf.Lerp(px,bx+(x-b[4])*bw/b[6],weight);py=Mathf.Lerp(py,by+(y-b[5])*bh/b[7],weight);
                    }
                    else{float pin=1-Ease((x-440)/40)*Ease((1090-x)/40)*Ease((y-745)/40);px=x+dx*pin;py=y+dy*pin;}
                    p.deformed[i]=Local(px,py);
                }
                p.mesh.vertices=p.deformed;p.mesh.RecalculateBounds();
                if(!neck)continue;
                int last=count-1;bx+=p.currentBones[last].x-Mathf.Lerp(p.startBones[last*2],p.endBones[last*2],a);by+=p.currentBones[last].y-Mathf.Lerp(p.startBones[last*2+1],p.endBones[last*2+1],a);
                p.headVertices[0]=Local(bx,by);p.headVertices[1]=Local(bx+bw,by);p.headVertices[2]=Local(bx,by+bh);p.headVertices[3]=Local(bx+bw,by+bh);
                p.headMesh.vertices=p.headVertices;p.headMesh.RecalculateBounds();
                var faces=art[n].faces;float frame=Ease((t-h.start-h.turnOffset)/Mathf.Max(.1f,h.turnDuration))*(faces.Length-1);int index=Mathf.Min(faces.Length-2,(int)frame);
                p.faceMaterial.SetTexture("_MainTex",faces[index]);p.faceMaterial.SetTexture("_NextTex",faces[index+1]);p.faceMaterial.SetFloat("_FrameMix",frame-index);
            }
        }
    }
}
