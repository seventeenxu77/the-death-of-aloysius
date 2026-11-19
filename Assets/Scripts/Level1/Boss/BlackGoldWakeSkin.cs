using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    // Direct port of BlackGoldWake_v026/animation.js. Each complete neck has its own skin.
    [ExecuteAlways]
    public sealed class BlackGoldWakeSkin : MonoBehaviour
    {
        [Serializable] public class Head
        {
            public string name;
            public float rigidY;
            public float[] points, drop, eyes;
            public int[] jointIds;
        }
        [Serializable] public class Joint { public string name; public int parent; public float[] rest; }
        [Serializable] public class Skin
        {
            public string name;
            public float[] vertices,uv,weights;
            public int[] joints,indices;
            [NonSerialized] public Mesh mesh;
            [NonSerialized] public Vector3[] output;
            [NonSerialized] public Material material;
            [NonSerialized] public MeshRenderer renderer;
        }
        [Serializable] public class Rig { public Head[] parts; public Joint[] joints; public Skin[] meshes; public int[] bodyJointIds; }
        [Serializable] public class Art { public string name; public Texture2D open,closed; }
        public TextAsset rigAsset;
        public Material skinMaterial;
        public Texture2D bedBackground,bedForeground;
        public Art[] art;
        [Tooltip("苏醒结束后的贝塞尔待机；与 HTML 预览使用相同姿态和蒙皮数据")]
        public SerpentBezierLife bezierLife;
        public bool idleMotion=true;
        [Header("苏醒结束后的待机摆动")]
        [Range(.08f,1.5f),Tooltip("每秒完整左右往返次数。1.35 约为 0.74 秒一个来回；保留贝塞尔姿态、下沉与停顿比例。")]
        public float idleFrequencyHz=1.35f;
        public bool drawBed=true;
        public int sortingOffset;
        public int BoneCount=>rig==null?0:rig.joints.Length;
        [NonSerialized] Rig rig;
        Transform generated;
        Transform[] bones;
        Vector2[] delta;
        float[] angles;
        string overrideHead;
        Vector2[] overridePositions;
        float[] overrideAngles;
        bool roofClip;
        float roofY;
        readonly HashSet<string> hiddenParts=new HashSet<string>();
        public bool IsPartVisible(string name)=>!hiddenParts.Contains(name);
        public void SetPartVisible(string name,bool visible)
        {
            if(visible)hiddenParts.Remove(name);else hiddenParts.Add(name);
            if(!visible&&rig!=null)foreach(var mesh in rig.meshes)if(mesh.name==name&&mesh.renderer)mesh.renderer.enabled=false;
            if(bezierLife)bezierLife.SetPartVisible(name,visible);
        }
        public Head FindHead(string name){EnsureRig();return rig==null?null:Array.Find(rig.parts,p=>p.name==name);}
        public void SetHeadPose(string name,Vector2[] positions,float[] radians,bool clipAtRoof,float worldRoofY)
        {overrideHead=name;overridePositions=positions;overrideAngles=radians;roofClip=clipAtRoof;roofY=worldRoofY;}
        public void ClearHeadPose(){overrideHead=null;overridePositions=null;overrideAngles=null;roofClip=false;}
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        static Vector3 Local(float x,float y)=>new Vector3(x*.01f,-y*.01f,0);
        static float Ease(float x){x=Mathf.Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
        static readonly float[] Lift={0,.12f,.5f,.85f,1,1,1,1,1,1};
        // A half-cycle holds the previous compressed pose, releases upward,
        // sweeps across, then accelerates into a downward accent and settles.
        static Vector2 Sway(float halfCycles)
        {
            int step=Mathf.FloorToInt(halfCycles);float u=halfCycles-step,s=(step&1)==0?1:-1;
            if(u<.18f)return new Vector2(-s,34);
            if(u<.32f){float k=Ease((u-.18f)/.14f);return new Vector2(Mathf.Lerp(-s,-.96f*s,k),Mathf.Lerp(34,-12,k));}
            if(u<.72f){float k=Ease((u-.32f)/.4f);return new Vector2(Mathf.Lerp(-.96f*s,.9f*s,k),Mathf.Lerp(-12,0,k));}
            if(u<.84f){float k=(u-.72f)/.12f;k=k*k*k;return new Vector2(Mathf.Lerp(.9f*s,1.06f*s,k),42*k);}
            if(u<.93f){float k=Ease((u-.84f)/.09f);return new Vector2(Mathf.Lerp(1.06f*s,s,k),Mathf.Lerp(42,34,k));}
            return new Vector2(s,34);
        }
        static float SwayWidth(string name)=>name=="Fang"?70:name=="Stone"?62:name=="Venom"?54:56;
        void OnDisable()=>Clear();
        public void Clear()
        {
            if(bezierLife)bezierLife.Clear();
            if(generated)Release(generated.gameObject);
            foreach(var o in owned)if(o)Release(o);
            owned.Clear();generated=null;rig=null;bones=null;delta=null;angles=null;
        }
        static void Release(UnityEngine.Object o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        Transform Child(string label,Transform parent)
        {
            var t=new GameObject(label){hideFlags=HideFlags.DontSave}.transform;t.SetParent(parent,false);return t;
        }
        Material Draw(string label,Mesh mesh,Texture2D open,Texture2D closed,int order)
        {
            var t=Child(label,generated);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>();
            var mat=new Material(skinMaterial){name=label,hideFlags=HideFlags.DontSave};owned.Add(mat);
            mat.SetTexture("_MainTex",open);mat.SetTexture("_ClosedTex",closed?closed:open);
            renderer.sharedMaterial=mat;renderer.sortingOrder=sortingOffset+order;return mat;
        }
        Mesh MeshAt(string name,Vector3[] vertices,Vector2[] uv,int[] indices)
        {
            var m=new Mesh{name=name,hideFlags=HideFlags.DontSave};m.MarkDynamic();m.vertices=vertices;m.uv=uv;m.triangles=indices;m.RecalculateBounds();owned.Add(m);return m;
        }
        void Bed(string name,Texture2D texture,int order)
        {
            Draw(name,MeshAt(name,new[]{Local(0,0),Local(1536,0),Local(0,1024),Local(1536,1024)},
                new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0)},new[]{0,1,2,1,3,2}),texture,texture,order);
        }
        public void EnsureRig()
        {
            if(rig!=null&&generated&&bones!=null&&bones.Length==rig.joints.Length&&delta!=null&&delta.Length==rig.joints.Length&&angles!=null&&angles.Length==rig.joints.Length)return;
            if(!rigAsset||!skinMaterial)return;
            if(generated||rig!=null)Clear();
            rig=JsonUtility.FromJson<Rig>(rigAsset.text);generated=Child("BlackGold v026 | 46 bones and independent skins",transform);
            delta=new Vector2[rig.joints.Length];angles=new float[rig.joints.Length];bones=new Transform[rig.joints.Length];
            for(int i=0;i<bones.Length;i++)
            {
                var j=rig.joints[i];bones[i]=Child(j.name,j.parent<0?generated:bones[j.parent]);
                var rest=Local(j.rest[0],j.rest[1]);
                bones[i].localPosition=j.parent<0?rest:rest-Local(rig.joints[j.parent].rest[0],rig.joints[j.parent].rest[1]);
            }
            if(drawBed)Bed("Bed background",bedBackground,0);
            foreach(var m in rig.meshes)
            {
                int n=m.vertices.Length/2;m.output=new Vector3[n];var uv=new Vector2[n];
                for(int i=0;i<n;i++){m.output[i]=Local(m.vertices[i*2],m.vertices[i*2+1]);uv[i]=new Vector2(m.uv[i*2],1-m.uv[i*2+1]);}
                m.mesh=MeshAt(m.name,m.output,uv,m.indices);
                var a=Array.Find(art,v=>v.name==m.name);bool body=m.name=="Coils";
                int pi=Array.FindIndex(rig.parts,p=>p.name==m.name);
                m.material=Draw(m.name+" weighted skin",m.mesh,a.open,a.closed,body?1:pi+2);
                m.renderer=generated.GetChild(generated.childCount-1).GetComponent<MeshRenderer>();
                m.material.SetFloat("_Roots",body?0:1);
                if(!body)
                {
                    var h=rig.parts[pi];m.material.SetVector("_Eye0",Eye(h.eyes,0));m.material.SetVector("_Eye1",Eye(h.eyes,4));
                }
                else
                {
                    var front=Draw("Shared coils foreground",m.mesh,a.open,a.open,6);front.SetFloat("_Front",1);
                }
            }
            if(drawBed)Bed("Bed fixed foreground",bedForeground,7);
        }
        static Vector4 Eye(float[] values,int i)=>values.Length>=i+4?new Vector4(values[i],values[i+1],values[i+2],values[i+3]):Vector4.zero;
        static SerpentBedWake.HeadTiming Timing(SerpentBedWake owner,string name)=>name=="Fang"?owner.fang:name=="Venom"?owner.venom:name=="Stone"?owner.stone:owner.timeHead;
        public void Sample(float t,SerpentBedWake owner)
        {
            EnsureRig();if(rig==null)return;
            bool useLife=idleMotion&&bezierLife&&bezierLife.IsConfigured;
            bool inLife=useLife&&t>=owner.Duration;
            foreach(var mesh in rig.meshes)if(mesh.name!="Coils"&&mesh.renderer)mesh.renderer.enabled=!inLife&&IsPartVisible(mesh.name);
            if(inLife)
            {
                bezierLife.Sample(t-owner.Duration,this,overrideHead,overridePositions,overrideAngles,roofClip,roofY);
                return;
            }
            if(bezierLife)bezierLife.Hide();
            Array.Clear(angles,0,angles.Length);
            for(int pi=0;pi<rig.parts.Length;pi++)
            {
                var p=rig.parts[pi];var h=Timing(owner,p.name);float age=t-h.start,progress=Ease(age/Mathf.Max(.1f,h.duration));
                float anticipation=age>0&&age<.4f?Mathf.Sin(age/.4f*Mathf.PI)*6:0;
                float idle=idleMotion&&!useLife?h.idleAmplitude*Ease((age-h.duration)/.7f):0;
                Vector2 swing=Sway(t*Mathf.Max(.01f,idleFrequencyHz)*2+pi*.37f);
                float dx=swing.x*SwayWidth(p.name)*idle,dy=swing.y*idle,roll=swing.x*.065f*idle;
                for(int i=0;i<p.jointIds.Length;i++)
                {
                    delta[p.jointIds[i]]=new Vector2((p.drop[0]*(1-progress)+dx+Mathf.Sin(i*.75f)*12*Mathf.Sin(progress*Mathf.PI))*Lift[i],(p.drop[1]*(1-progress)+anticipation+dy)*Lift[i]);
                    angles[p.jointIds[i]]=roll*.18f*Lift[i];
                }
                var skull=delta[p.jointIds[p.jointIds.Length-1]];
                int last=p.jointIds.Length-1;var pivot=new Vector2(p.points[last*2],p.points[last*2+1]);
                for(int i=0;i<p.jointIds.Length;i++)if(p.points[i*2+1]<=p.rigidY)
                {
                    Vector2 offset=new Vector2(p.points[i*2],p.points[i*2+1])-pivot;
                    delta[p.jointIds[i]]=skull+new Vector2(Mathf.Cos(roll)*offset.x-Mathf.Sin(roll)*offset.y,Mathf.Sin(roll)*offset.x+Mathf.Cos(roll)*offset.y)-offset;
                    angles[p.jointIds[i]]=roll;
                }
                if(p.name==overrideHead&&overridePositions!=null&&overridePositions.Length==p.jointIds.Length)
                    for(int i=0;i<p.jointIds.Length;i++)
                    {
                        delta[p.jointIds[i]]=overridePositions[i]-new Vector2(p.points[i*2],p.points[i*2+1]);
                        angles[p.jointIds[i]]=overrideAngles[i];
                    }
            }
            foreach(int id in rig.bodyJointIds)
            {
                float x=rig.joints[id].rest[0];delta[id]=new Vector2(0,idleMotion&&!useLife?Mathf.Sin(t*.7f+x*.004f)*1.3f*Mathf.Sin(Mathf.PI*x/1536):0);
            }
            for(int i=0;i<bones.Length;i++)
            {
                var j=rig.joints[i];var pos=Local(j.rest[0]+delta[i].x,j.rest[1]+delta[i].y);
                if(j.parent>=0){var parent=rig.joints[j.parent];pos-=Local(parent.rest[0]+delta[j.parent].x,parent.rest[1]+delta[j.parent].y);pos=Quaternion.Euler(0,0,angles[j.parent]*Mathf.Rad2Deg)*pos;}
                bones[i].localPosition=pos;
                bones[i].localRotation=Quaternion.Euler(0,0,-(angles[i]-(j.parent<0?0:angles[j.parent]))*Mathf.Rad2Deg);
            }
            foreach(var m in rig.meshes)
            {
                for(int i=0;i<m.output.Length;i++)
                {
                    float x=m.vertices[i*2],y=m.vertices[i*2+1];Vector2 position=Vector2.zero;
                    for(int k=0;k<2;k++)
                    {
                        int id=m.joints[i*2+k];var rest=rig.joints[id].rest;float dx=x-rest[0],dy=y-rest[1],c=Mathf.Cos(angles[id]),s=Mathf.Sin(angles[id]);
                        position+=new Vector2(rest[0]+delta[id].x+c*dx-s*dy,rest[1]+delta[id].y+s*dx+c*dy)*m.weights[i*2+k];
                    }
                    m.output[i]=Local(position.x,position.y);
                }
                m.mesh.vertices=m.output;m.mesh.RecalculateBounds();
                if(m.name=="Coils")continue;
                var h=Timing(owner,m.name);m.material.SetFloat("_ClosedEye",1-Ease((t-h.start-h.duration-h.eyeDelay)/Mathf.Max(.01f,h.eyeDuration)));
                m.material.SetFloat("_RoofClipEnabled",m.name==overrideHead&&roofClip?1:0);m.material.SetFloat("_RoofY",roofY);
            }
        }
    }
}
