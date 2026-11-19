using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    [ExecuteAlways,DefaultExecutionOrder(-20)]
    public sealed class StoneCeilingBreach : MonoBehaviour
    {
        public SerpentCampaign campaign;
        public FarSerpentPresentation presentation;
        public Transform roofOpening;
        public Material stoneMaterial,dustMaterial;
        [Header("进入尖刺大厅后自动演出 / 秒")]
        [Min(0)] public float entryDelay=.6f;
        [Min(.1f)] public float retractSeconds=.5f,aimSeconds=.75f,pressSeconds=.35f,punchSeconds=.45f,settleSeconds=.4f;
        [Header("骨骼伸展 / 世界单位")]
        public float headAboveRoof=2.4f;
        public float skullBelowRoof=2f;
        public float headTurnDegrees=75;
        [Range(0,1)] public float neckRotationWeight=.12f;
        [Header("破口与碎屑")]
        public Vector2 openingSize=new Vector2(3.1f,.9f);
        [Range(0,1)] public float dustOpacity=.28f;
        [Range(0,6)] public float previewTime;
        public float ContactTime=>retractSeconds+aimSeconds+pressSeconds;
        public float HoldTime=>ContactTime+punchSeconds+settleSeconds;
        public bool Ready=>started&&clock>=HoldTime;
        public float CurrentTime=>Application.isPlaying?clock:previewTime;
        bool started;
        float clock;
        bool forcedPreview;
        float forcedTime;
        Vector2[] positions;
        float[] angles;
        bool withdrawing;float withdrawClock,withdrawDuration;
        Vector2[] withdrawPositions;float[] withdrawAngles;
        Transform generated;
        Mesh hole,rim,particles,dust;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<Vector3> v=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> idx=new List<int>();
        static float Ease(float f){f=Mathf.Clamp01(f);return f*f*(3-2*f);}
        static float Random(int i){double x=Math.Sin(i*127.1+311.7)*43758.5453;return (float)(x-Math.Floor(x));}
        void Update()
        {
            if(Application.isPlaying)
            {
                if(!started&&campaign&&campaign.RoomIndex==1&&campaign.CurrentBeat==SerpentCampaign.Beat.StoneTrap){started=true;clock=-entryDelay;}
                if(started&&!Level1TeleportConsole.BlocksGameplayInput)clock+=Time.deltaTime;
                if(withdrawing&&!Level1TeleportConsole.BlocksGameplayInput)withdrawClock+=Time.deltaTime;
            }
            PreparePose();
        }
        void OnDisable(){if(presentation&&presentation.skin)presentation.skin.ClearHeadPose();Clear();}
        public void ResetBreach(){withdrawing=false;withdrawClock=0;started=false;clock=-entryDelay;forcedPreview=false;if(presentation&&presentation.skin)presentation.skin.ClearHeadPose();PreparePose();}
        public void BeginWithdrawal(float duration)
        {
            PreparePose();if(positions==null)return;
            withdrawPositions=(Vector2[])positions.Clone();withdrawAngles=(float[])angles.Clone();
            withdrawDuration=Mathf.Max(.1f,duration);withdrawClock=0;withdrawing=true;
        }
        public void PreviewAt(float time){forcedPreview=true;forcedTime=time;PreparePose();if(presentation)presentation.Refresh();}
        public void EndPreview(){forcedPreview=false;PreparePose();if(presentation)presentation.Refresh();}
        [ContextMenu("预览：头部已穿过屋顶")]
        void PreviewHold(){previewTime=HoldTime+.5f;PreparePose();if(presentation)presentation.Refresh();}
        public void PreparePose()
        {
            if(!presentation||!presentation.skin||!roofOpening)return;
            var skin=presentation.skin;var head=skin.FindHead("Stone");if(head==null)return;
            float time=forcedPreview?forcedTime:CurrentTime;
            EnsureEffects();
            if(withdrawing&&withdrawPositions!=null)
            {
                float k=Ease(withdrawClock/withdrawDuration);var rootPoint=new Vector2(head.points[0],head.points[1]);
                for(int i=0;i<positions.Length;i++)
                {
                    float u=i/(float)(positions.Length-1);var target=rootPoint+new Vector2(0,330*u);
                    positions[i]=Vector2.Lerp(withdrawPositions[i],target,k);angles[i]=Mathf.Lerp(withdrawAngles[i],0,k);
                }
                skin.SetHeadPose("Stone",positions,angles,true,roofOpening.position.y);Effects(Mathf.Max(0,time-ContactTime));return;
            }
            if(time<=0){skin.ClearHeadPose();Effects(-1);return;}
            if(positions==null||positions.Length!=head.jointIds.Length){positions=new Vector2[head.jointIds.Length];angles=new float[positions.Length];}
            Vector3 root=skin.transform.TransformPoint(new Vector3(head.points[4]*.01f,-head.points[5]*.01f));
            float aim=Ease((time-retractSeconds)/aimSeconds),punch=Ease((time-ContactTime)/punchSeconds);
            float skullY=roofOpening.position.y+Mathf.Lerp(-skullBelowRoof,headAboveRoof,punch);
            float turn=headTurnDegrees*Mathf.Deg2Rad*aim;
            float shake=time>=retractSeconds+aimSeconds&&time<ContactTime?Mathf.Sin(time*117)*.025f:0;
            for(int i=0;i<positions.Length;i++)
            {
                Vector2 rest=new Vector2(head.points[i*2],head.points[i*2+1]);
                if(i<2){positions[i]=rest;angles[i]=0;continue;}
                float u=(i-2)/(float)(positions.Length-3);
                var target=new Vector3(Mathf.Lerp(root.x,roofOpening.position.x-.2f,Ease(u))+Mathf.Sin(u*Mathf.PI*2)*.45f*(1-u),Mathf.Lerp(root.y,skullY,u));
                var local=skin.transform.InverseTransformPoint(target);var point=new Vector2(local.x*100,-local.y*100);
                positions[i]=Vector2.Lerp(rest,point,aim);
                if(time<retractSeconds)positions[i].y+=Mathf.Sin(Mathf.PI*time/retractSeconds)*22*u;
                if(shake!=0)positions[i].x+=shake/skin.transform.lossyScale.x*100*u;
                // Keep the thick neck's cross-section while the rigid skull tips upward.
                angles[i]=i==positions.Length-1?turn:turn*neckRotationWeight*Ease((i-3)/6f);
            }
            skin.SetHeadPose("Stone",positions,angles,time>=retractSeconds+aimSeconds,roofOpening.position.y);
            Effects(time-ContactTime);
        }
        void Clear()
        {
            if(generated)Release(generated.gameObject);
            foreach(var o in owned)if(o)Release(o);owned.Clear();generated=null;
        }
        static void Release(UnityEngine.Object o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        Mesh Draw(string name,Material material,int order)
        {
            var t=new GameObject(name){hideFlags=HideFlags.DontSave}.transform;t.SetParent(generated,false);
            var m=new Mesh{name=name,hideFlags=HideFlags.DontSave};m.MarkDynamic();owned.Add(m);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=m;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=order;return m;
        }
        void EnsureEffects()
        {
            if(generated||!stoneMaterial||!dustMaterial)return;
            generated=new GameObject("Ceiling impact | rear void, front lip, falling stone and dust"){hideFlags=HideFlags.DontSave}.transform;generated.SetParent(roofOpening,false);
            hole=Draw("Rear opening and fractured stone",stoneMaterial,-44);rim=Draw("Near jagged rim | overlaps extended neck",stoneMaterial,-33);
            particles=Draw("Falling ceiling fragments",stoneMaterial,-31);dust=Draw("Ceiling dust",dustMaterial,-31);
        }
        void Begin(){v.Clear();colors.Clear();uv.Clear();idx.Clear();}
        int Vertex(Vector3 p,Color c,Vector2 tex){int n=v.Count;v.Add(p);colors.Add(c);uv.Add(tex);return n;}
        void Tri(int a,int b,int c){idx.Add(a);idx.Add(b);idx.Add(c);}
        void End(Mesh m){m.Clear();m.SetVertices(v);m.SetColors(colors);m.SetUVs(0,uv);m.SetTriangles(idx,0);m.RecalculateBounds();}
        Vector3 Edge(int i,bool outer)
        {
            float a=i*Mathf.PI*2/24,r=.89f+Random(i+26)*.2f;if(outer)r+=.13f+Random(i+62)*.13f;
            return new Vector3(Mathf.Cos(a)*openingSize.x*.5f*r,Mathf.Sin(a)*openingSize.y*.5f*r);
        }
        void RimSegment(int i)
        {
            int next=(i+1)%24;Color inner=new Color(.17f,.105f,.085f),outer=new Color(.36f,.24f,.12f);
            int a=Vertex(Edge(i,false),inner,Vector2.zero),b=Vertex(Edge(next,false),inner,Vector2.zero),c=Vertex(Edge(i,true),outer,Vector2.zero),d=Vertex(Edge(next,true),outer,Vector2.zero);
            Tri(a,b,c);Tri(b,d,c);
        }
        void Chip(Vector3 p,float size,float angle,int seed,float alpha)
        {
            Color color=new Color(.29f+Random(seed)*.1f,.18f,.11f,alpha);int n=v.Count;
            for(int k=0;k<5;k++){float a=angle+k*Mathf.PI*2/5,r=size*(.7f+Random(seed+k)*.45f);Vertex(p+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.7f),color,Vector2.zero);}
            Tri(n,n+1,n+2);Tri(n,n+2,n+3);Tri(n,n+3,n+4);
        }
        void Cloud(Vector3 p,float size,float alpha)
        {
            var c=new Color(.34f,.26f,.21f,alpha);int n=v.Count;
            Vertex(p+new Vector3(-size,-size*.65f),c,new Vector2(0,0));Vertex(p+new Vector3(size,-size*.65f),c,new Vector2(1,0));
            Vertex(p+new Vector3(-size,size*.65f),c,new Vector2(0,1));Vertex(p+new Vector3(size,size*.65f),c,new Vector2(1,1));Tri(n,n+1,n+2);Tri(n+1,n+3,n+2);
        }
        void Effects(float age)
        {
            if(!generated)return;
            Begin();if(age>=0)
            {
                int center=Vertex(Vector3.zero,new Color(.008f,.005f,.012f),Vector2.zero);
                for(int i=0;i<24;i++){int a=Vertex(Edge(i,false),new Color(.008f,.005f,.012f),Vector2.zero),b=Vertex(Edge((i+1)%24,false),new Color(.008f,.005f,.012f),Vector2.zero);Tri(center,a,b);RimSegment(i);}
            }
            End(hole);Begin();if(age>=0)for(int i=12;i<24;i++)RimSegment(i);End(rim);
            Begin();if(age>=0&&age<2.8f)for(int i=0;i<31;i++)
            {
                float a=age-Random(i+40)*.1f;if(a<0)continue;
                float vx=(Random(i+2)-.5f)*4,vy=1+Random(i+9)*2;
                Chip(new Vector3((Random(i+16)-.5f)*openingSize.x+vx*a,vy*a-4.9f*a*a),.04f+Random(i+32)*.11f,a*(Random(i+44)-.5f)*16,i,1-Ease((a-1.5f)/1.1f));
            }
            End(particles);Begin();if(age>=0&&age<3)for(int i=0;i<22;i++)
            {
                float a=age-Random(i+66)*.13f,life=1.5f+Random(i+71);if(a<0||a>life)continue;
                var p=new Vector3((Random(i+6)-.5f)*openingSize.x+(Random(i+19)-.5f)*a,-.12f-a*.45f);
                Cloud(p,(.16f+Random(i+82)*.3f)*(1+a),Mathf.Clamp01(a/.08f)*Mathf.Pow(1-a/life,1.7f)*dustOpacity);
            }
            End(dust);
        }
    }
}
