using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    // Native port of BezierLife_v010: the same art, arc-length skin and detail cues as the HTML.
    // Sampled by the wake player, rather than an unrelated Update clock or a looping movie.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SerpentBezierLife : MonoBehaviour
    {
        [Serializable] public sealed class Pose { public float[] nodes; public float angle; }
        [Serializable] public sealed class Part
        {
            public string name;
            public float[] origin,eyes,vertices,uv,binding,legacyPoints,legacyWeights;
            public float referenceEndAngle,neckRadius;
            public int[] indices,legacyJoints;
            public Pose[] poses;
        }
        [Serializable] public sealed class Data { public int version; public float frequency; public string tongueGold; public Part[] parts; }
        sealed class RuntimePart
        {
            public Part data;
            public Mesh mesh;
            public Material material;
            public MeshRenderer renderer;
            public Vector3[] vertices;
            public readonly Vector2[] nodes=new Vector2[5],raw=new Vector2[257];
            public readonly float[] lengths=new float[257];
            public readonly Vector3[] curve=new Vector3[193];
            public Transform[] bones;
            public Vector2 end;
            public float headAngle,headCos,headSin;
        }
        struct Micro { public float blink,angle,x,y,sacL,sacR,shake,tongue; }
        public TextAsset profile;
        public Shader lifeShader;
        [Min(0),Tooltip("苏醒姿态平滑接入待机的时间（秒）")]
        public float transitionSeconds=.65f;
        public bool detailsEnabled=true;
        [Range(0,1.5f)] public float detailsStrength=1;
        public bool IsConfigured=>enabled&&profile&&lifeShader;
        [NonSerialized] Data data;
        Transform generated;
        RuntimePart[] parts;
        Mesh tongueMesh;
        Material tongueMaterial;
        Color tongueGold;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<Vector3> tongueVertices=new List<Vector3>();
        readonly List<Color> tongueColors=new List<Color>();
        readonly List<int> tongueIndices=new List<int>();
        float lastTime=float.NaN,overrideStart;
        string previousOverride;
        static Vector3 Local(Vector2 p)=>new Vector3(p.x*.01f,-p.y*.01f,0);
        static float Ease(float x){x=Mathf.Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
        static float Smooth(float a,float b,float x){float z=Mathf.Clamp01((x-a)/(b-a));return z*z*(3-2*z);}
        static Vector2 Rotate(Vector2 p,float c,float s)=>new Vector2(c*p.x-s*p.y,s*p.x+c*p.y);
        static Vector2 Cubic(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        {float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}
        static Transform Child(string name,Transform parent)
        {var t=new GameObject(name){hideFlags=HideFlags.DontSave}.transform;t.SetParent(parent,false);return t;}
        static void Release(UnityEngine.Object o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        void OnDisable()=>Clear();
        public void Clear()
        {
            if(generated)Release(generated.gameObject);
            foreach(var o in owned)if(o)Release(o);
            owned.Clear();generated=null;parts=null;data=null;tongueMesh=null;tongueMaterial=null;
            lastTime=float.NaN;previousOverride=null;
        }
        public void Hide(){if(generated)generated.gameObject.SetActive(false);lastTime=float.NaN;previousOverride=null;}
        public void SetPartVisible(string name,bool visible)
        {if(!visible&&parts!=null)foreach(var p in parts)if(p.data.name==name&&p.renderer)p.renderer.enabled=false;}
        Mesh CreateMesh(string name)
        {var m=new Mesh{name=name,hideFlags=HideFlags.DontSave};m.MarkDynamic();owned.Add(m);return m;}
        Material Draw(BlackGoldWakeSkin owner,string name,Mesh mesh,Texture2D open,Texture2D closed,int order)
        {
            var t=Child(name,generated);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=t.gameObject.AddComponent<MeshRenderer>();r.sortingOrder=owner.sortingOffset+order;
            var m=new Material(lifeShader){name=name,hideFlags=HideFlags.DontSave};owned.Add(m);
            m.SetTexture("_MainTex",open);m.SetTexture("_ClosedTex",closed?closed:open);
            r.sharedMaterial=m;return m;
        }
        void Ensure(BlackGoldWakeSkin owner)
        {
            if(generated&&data!=null&&parts!=null)return;
            Clear();data=JsonUtility.FromJson<Data>(profile.text);
            generated=Child("Bezier Life v010 | four independent curve skins",transform);
            parts=new RuntimePart[data.parts.Length];
            for(int k=0;k<parts.Length;k++)
            {
                var p=data.parts[k];var r=new RuntimePart{data=p,vertices=new Vector3[p.vertices.Length/2]};parts[k]=r;
                var uv=new Vector2[r.vertices.Length];
                for(int i=0;i<uv.Length;i++){r.vertices[i]=Local(new Vector2(p.vertices[i*2],p.vertices[i*2+1]));uv[i]=new Vector2(p.uv[i*2],1-p.uv[i*2+1]);}
                r.mesh=CreateMesh(p.name+" arc-length skin");r.mesh.vertices=r.vertices;r.mesh.uv=uv;r.mesh.triangles=p.indices;r.mesh.RecalculateBounds();
                var art=Array.Find(owner.art,a=>a.name==p.name);
                r.material=Draw(owner,p.name+" independent Bezier skin",r.mesh,art.open,art.closed,k+2);
                r.renderer=generated.GetChild(generated.childCount-1).GetComponent<MeshRenderer>();
                r.material.SetVector("_Eye0",Eye(p.eyes,0));r.material.SetVector("_Eye1",Eye(p.eyes,4));
                r.material.SetFloat("_Fang",p.name=="Fang"?1:0);
                var chain=Child(p.name+" | sampled curve bones",generated);r.bones=new Transform[25];
                for(int i=0;i<r.bones.Length;i++)r.bones[i]=Child("Curve "+i,i==0?chain:r.bones[i-1]);
            }
            ColorUtility.TryParseHtmlString(data.tongueGold,out tongueGold);
            if(QualitySettings.activeColorSpace==ColorSpace.Linear)tongueGold=tongueGold.linear;
            tongueMesh=CreateMesh("Fang forked tongue");
            tongueMaterial=Draw(owner,"Fang tongue | same mouth transform",tongueMesh,null,null,6);
            tongueMaterial.SetFloat("_Solid",1);
            // Same order as the foreground coil, slightly nearer to the camera.
            generated.GetChild(generated.childCount-1).localPosition=new Vector3(0,0,-.001f);
        }
        static Vector4 Eye(float[] a,int k)=>a.Length>=k+4?new Vector4(a[k],a[k+1],a[k+2],a[k+3]):Vector4.zero;
        static Vector2 Node(Pose p,int i)=>new Vector2(p.nodes[i*2],p.nodes[i*2+1]);
        static readonly int[] Sequence={0,1,2,1};
        void BuildCurve(RuntimePart r,float cycle,Micro micro)
        {
            float z=Mathf.Repeat(cycle,1)*4;int segment=Mathf.Min(3,Mathf.FloorToInt(z));
            int a=Sequence[segment],b=Sequence[(segment+1)%4];float hold=a==1?0:.42f;
            float blend=Ease((z-segment-hold)/(1-hold));var pa=r.data.poses[a];var pb=r.data.poses[b];
            for(int i=0;i<5;i++)r.nodes[i]=Vector2.LerpUnclamped(Node(pa,i),Node(pb,i),blend);
            r.nodes[4]+=new Vector2(micro.x,micro.y);r.nodes[3]+=new Vector2(micro.x,micro.y);r.nodes[2].y+=micro.y*.25f;
            var n=r.nodes;var tangent=(n[3]-n[1]).normalized;
            float arm=Mathf.Min(Vector2.Distance(n[2],n[0]),Vector2.Distance(n[4],n[2]))*.32f;
            for(int i=0;i<=128;i++)r.raw[i]=Cubic(n[0],n[1],n[2]-tangent*arm,n[2],i/128f);
            for(int i=1;i<=128;i++)r.raw[128+i]=Cubic(n[2],n[2]+tangent*arm,n[3],n[4],i/128f);
            r.lengths[0]=0;for(int i=1;i<257;i++)r.lengths[i]=r.lengths[i-1]+Vector2.Distance(r.raw[i],r.raw[i-1]);
            float length=r.lengths[256];int k=0;
            for(int i=0;i<193;i++)
            {
                float d=length*i/192;while(k<255&&r.lengths[k+1]<d)k++;
                var p=Vector2.LerpUnclamped(r.raw[k],r.raw[k+1],(d-r.lengths[k])/Mathf.Max(.0001f,r.lengths[k+1]-r.lengths[k]));
                r.curve[i]=new Vector3(p.x,p.y,0);
            }
            for(int i=0;i<193;i++){var d=r.curve[Mathf.Min(192,i+1)]-r.curve[Mathf.Max(0,i-1)];r.curve[i].z=Mathf.Atan2(d.y,d.x);}
            r.end=n[4];r.headAngle=r.curve[192].z-r.data.referenceEndAngle+(Mathf.Lerp(pa.angle,pb.angle,blend)+micro.angle)*Mathf.Deg2Rad;
            r.headCos=Mathf.Cos(r.headAngle);r.headSin=Mathf.Sin(r.headAngle);
        }
        static Vector3 CurveAt(RuntimePart r,float u)
        {
            float z=Mathf.Clamp01(u)*192;int k=Mathf.Min(191,Mathf.FloorToInt(z));float f=z-k;
            var a=r.curve[k];var b=r.curve[k+1];
            return new Vector3(Mathf.Lerp(a.x,b.x,f),Mathf.Lerp(a.y,b.y,f),a.z+Mathf.DeltaAngle(a.z*Mathf.Rad2Deg,b.z*Mathf.Rad2Deg)*Mathf.Deg2Rad*f);
        }
        static float Envelope(float t,float start,float rise,float hold,float fall)
        {float a=t-start;if(a<0||a>=rise+hold+fall)return 0;if(a<rise)return Ease(a/rise);if(a<rise+hold)return 1;return 1-Ease((a-rise-hold)/fall);}
        static float Accents(float t,float[] starts,float rise,float hold,float fall,float offset=0)
        {float m=0;foreach(float s in starts)m=Mathf.Max(m,Envelope(t,s+offset,rise,hold,fall));return m;}
        static float Keyed(float t,float start,Vector2[] keys)
        {
            float a=t-start;if(a<keys[0].x||a>keys[keys.Length-1].x)return 0;
            for(int i=0;i<keys.Length-1;i++)if(a<=keys[i+1].x)return Mathf.LerpUnclamped(keys[i].y,keys[i+1].y,Ease((a-keys[i].x)/(keys[i+1].x-keys[i].x)));
            return 0;
        }
        static readonly float[] StoneBlinks={1.04f,6.18f},FangBlinks={2.65f,2.83f,6.54f},VenomBlinks={3.32f,7.58f},TimeBlinks={4.35f};
        static readonly float[] Tongues={1.55f,1.83f,2.14f,4.62f,4.92f,7.10f,7.39f},Twitches={1.34f,4.40f,6.86f},Sacs={2.03f,2.39f,2.77f,5.57f,5.95f,6.34f};
        static readonly Vector2[] StoneNod={new Vector2(0,0),new Vector2(.15f,-.18f),new Vector2(.29f,1),new Vector2(.51f,1),new Vector2(1.03f,0)};
        static readonly Vector2[] TimeNod={new Vector2(0,0),new Vector2(.4f,1),new Vector2(.64f,1),new Vector2(1.3f,0)};
        Micro Detail(string name,float seconds)
        {
            var m=new Micro();float k=detailsStrength;if(!detailsEnabled||k<.001f)return m;
            float t=Mathf.Repeat(seconds,25f/3);
            switch(name)
            {
                case "Stone":
                    m.blink=Accents(t,StoneBlinks,.055f,.09f,.105f);
                    float nod=Keyed(t,.85f,StoneNod)+Keyed(t,5.99f,StoneNod);m.angle=-8.5f*nod*k;m.y=11*nod*k;break;
                case "Fang":
                    m.blink=Accents(t,FangBlinks,.025f,.025f,.055f);
                    m.tongue=Accents(t,Tongues,.055f,.045f,.11f)*1.12f*Mathf.Min(1.15f,k);
                    float twitch=Accents(t,Twitches,.05f,.04f,.18f);m.angle=2.8f*twitch*k;m.y=3*twitch*k;m.x=1.5f*Mathf.Sin(t*39)*twitch*k;break;
                case "Venom":
                    m.blink=Accents(t,VenomBlinks,.04f,.025f,.09f);
                    m.sacL=Accents(t,Sacs,.11f,.025f,.19f)*k;m.sacR=Accents(t,Sacs,.09f,.025f,.21f,.07f)*.87f*k;
                    float beat=Mathf.Max(m.sacL,m.sacR);m.shake=Mathf.Sin(t*67)*2.1f*beat;m.angle=Mathf.Sin(t*27)*1.2f*beat;m.y=2*beat;break;
                case "Time":
                    m.blink=Accents(t,TimeBlinks,.13f,.09f,.24f);float n=Keyed(t,4.56f,TimeNod);m.angle=1.4f*n*k;m.y=7*n*k;break;
            }
            return m;
        }
        static Vector2 Sac(Vector2 v,Vector2 center,Vector2 radius,float beat,Micro micro,float time,float eyeDistance)
        {
            if(beat<=0)return Vector2.zero;
            float distance=new Vector2((v.x-center.x)/radius.x,(v.y-center.y)/radius.y).magnitude;
            float w=(1-Ease((distance-.65f)/.65f))*Ease((eyeDistance-.8f)/.5f);
            return new Vector2((v.x-center.x)*.29f*beat+Mathf.Sign(center.x-910)*micro.shake,(v.y-center.y)*.24f*beat+Mathf.Sin(time*51+center.x)*1.6f*beat)*w;
        }
        static Vector2 FacialPoint(string name,Vector2 v,Micro m,float time)
        {
            if(name!="Venom"||v.y>235||(m.sacL==0&&m.sacR==0))return v;
            float eye=Mathf.Min(new Vector2((v.x-875)/25,(v.y-139)/20).magnitude,new Vector2((v.x-951)/23,(v.y-139)/20).magnitude);
            return v+Sac(v,new Vector2(837,164),new Vector2(37,34),m.sacL,m,time,eye)+Sac(v,new Vector2(973,163),new Vector2(29,33),m.sacR,m,time,eye);
        }
        static Vector2 HeadPoint(RuntimePart r,Vector2 v)=>r.end+Rotate(v-new Vector2(r.data.origin[0],r.data.origin[1]),r.headCos,r.headSin);
        static Vector2 OverridePoint(Part p,int index,Vector2 source,Vector2[] positions,float[] angles)
        {
            Vector2 output=Vector2.zero;
            for(int j=0;j<2;j++)
            {
                int b=p.legacyJoints[index*2+j];var rest=new Vector2(p.legacyPoints[b*2],p.legacyPoints[b*2+1]);
                output+=(positions[b]+Rotate(source-rest,Mathf.Cos(angles[b]),Mathf.Sin(angles[b])))*p.legacyWeights[index*2+j];
            }
            return output;
        }
        public void Sample(float seconds,BlackGoldWakeSkin owner,string overrideHead,Vector2[] overridePositions,float[] overrideAngles,bool roofClip,float roofY)
        {
            if(!IsConfigured){Hide();return;}Ensure(owner);generated.gameObject.SetActive(true);
            if(float.IsNaN(lastTime)||seconds<lastTime){previousOverride=null;overrideStart=seconds;}
            if(previousOverride!=overrideHead){previousOverride=overrideHead;overrideStart=seconds;}
            lastTime=seconds;
            float entry=transitionSeconds<=0?1:Ease(seconds/transitionSeconds);
            // Enter via the centre-high pose with zero velocity, then continue the approved cycle.
            float cycle=.25f+Mathf.Max(0,seconds-transitionSeconds)*Mathf.Max(.01f,owner.idleFrequencyHz);
            float exposure=owner.skinMaterial.HasProperty("_Exposure")?owner.skinMaterial.GetFloat("_Exposure"):1;
            float attackBlend=Ease((seconds-overrideStart)/.3f);
            RuntimePart fang=null;Micro fangMicro=new Micro();
            foreach(var r in parts)
            {
                r.renderer.enabled=owner.IsPartVisible(r.data.name);if(!r.renderer.enabled)continue;
                var p=r.data;var micro=Detail(p.name,seconds);BuildCurve(r,cycle,micro);
                bool overridden=p.name==overrideHead&&overridePositions!=null&&overrideAngles!=null&&overridePositions.Length==p.legacyPoints.Length/2&&overrideAngles.Length==overridePositions.Length;
                for(int i=0;i<r.vertices.Length;i++)
                {
                    var source=new Vector2(p.vertices[i*2],p.vertices[i*2+1]);int b=i*5;float hw=p.binding[b+4];
                    Vector2 point=HeadPoint(r,FacialPoint(p.name,source,micro,seconds));
                    if(hw<.999f)
                    {
                        var q=CurveAt(r,p.binding[b]);float cross=p.binding[b+1]*p.neckRadius;
                        var tube=new Vector2(q.x-Mathf.Sin(q.z)*cross,q.y+Mathf.Cos(q.z)*cross+p.binding[b+2]);
                        point=Vector2.LerpUnclamped(tube,point,hw);
                    }
                    point=Vector2.LerpUnclamped(source,point,entry);
                    if(overridden)point=Vector2.LerpUnclamped(point,OverridePoint(p,i,source,overridePositions,overrideAngles),attackBlend);
                    r.vertices[i]=Local(point);
                }
                r.mesh.vertices=r.vertices;r.mesh.RecalculateBounds();
                r.material.SetFloat("_ClosedEye",micro.blink*entry);r.material.SetFloat("_IdleBlend",entry);
                r.material.SetFloat("_Details",detailsEnabled?1:0);r.material.SetFloat("_Exposure",exposure);
                r.material.SetFloat("_RoofClipEnabled",overridden&&roofClip?1:0);r.material.SetFloat("_RoofY",roofY);
                for(int i=0;i<25;i++)
                {
                    var q=CurveAt(r,i/24f);var pos=Local(new Vector2(q.x,q.y));
                    r.bones[i].localPosition=i==0?pos:pos-Local(new Vector2(r.curve[(i-1)*8].x,r.curve[(i-1)*8].y));
                }
                if(p.name=="Fang"){fang=r;fangMicro=micro;}
            }
            Tongue(fang,fangMicro,seconds,entry,exposure);
        }
        Vector2 TongueControl(Vector2 anchor,Vector2 p,float amount)=>anchor+(p-anchor)*amount;
        void TongueStroke(RuntimePart r,Vector2 a,Vector2 b,Vector2 c,Vector2 d,float width,Color color,float entry)
        {
            int first=tongueVertices.Count;
            for(int i=0;i<=16;i++)
            {
                float t=i/16f;var p=Cubic(a,b,c,d,t);var next=Cubic(a,b,c,d,Mathf.Min(1,t+.001f));var prev=Cubic(a,b,c,d,Mathf.Max(0,t-.001f));
                var tangent=(next-prev).normalized;var side=new Vector2(-tangent.y,tangent.x)*width*.5f;
                foreach(float sign in Signs)
                {
                    var source=p+side*sign;var point=Vector2.LerpUnclamped(source,HeadPoint(r,source),entry);
                    tongueVertices.Add(Local(point));tongueColors.Add(color);
                }
                if(i>0){int k=first+(i-1)*2;tongueIndices.Add(k);tongueIndices.Add(k+1);tongueIndices.Add(k+2);tongueIndices.Add(k+1);tongueIndices.Add(k+3);tongueIndices.Add(k+2);}
            }
        }
        static readonly float[] Signs={-1,1};
        void Tongue(RuntimePart r,Micro micro,float seconds,float entry,float exposure)
        {
            tongueVertices.Clear();tongueColors.Clear();tongueIndices.Clear();
            if(r!=null&&detailsEnabled&&micro.tongue>=.03f&&entry>0)
            {
                float q=micro.tongue,flutter=Mathf.Sin(seconds*57)*3*q;var a=new Vector2(1138,441);
                Vector2 b=TongueControl(a,new Vector2(1149,446),q),c=TongueControl(a,new Vector2(1155,450),q),d=TongueControl(a,new Vector2(1158,463),q);
                for(int stroke=0;stroke<2;stroke++)
                {
                    var outline=new Color(3/255f,2/255f,2/255f);
                    if(QualitySettings.activeColorSpace==ColorSpace.Linear)outline=outline.linear;
                    var color=stroke==0?outline:tongueGold;color.a*=entry;
                    float width=stroke==0?8:4.5f,fork=stroke==0?6:2.7f;
                    TongueStroke(r,a,b,c,d,width,color,entry);
                    TongueStroke(r,d,TongueControl(a,new Vector2(1152,476),q),TongueControl(a,new Vector2(1146+flutter,490),q),TongueControl(a,new Vector2(1144+flutter,500),q),fork,color,entry);
                    TongueStroke(r,d,TongueControl(a,new Vector2(1164,471),q),TongueControl(a,new Vector2(1169+flutter,485),q),TongueControl(a,new Vector2(1170+flutter,499),q),fork,color,entry);
                }
            }
            tongueMesh.Clear();tongueMesh.SetVertices(tongueVertices);tongueMesh.SetColors(tongueColors);tongueMesh.SetTriangles(tongueIndices,0);tongueMesh.RecalculateBounds();
            tongueMaterial.SetFloat("_Exposure",exposure);
        }
    }
}
