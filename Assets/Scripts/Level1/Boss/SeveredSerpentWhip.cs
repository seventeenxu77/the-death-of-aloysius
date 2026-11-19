using System;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    // Native port of the approved StoneSeveredWhip_v001 preview; fixed root and delayed tip.
    public sealed class SeveredSerpentWhip : MonoBehaviour
    {
        [Serializable] public sealed class Data { public int width,height;public float[] chain,vertices,binding,wound,cutAxis;public int[] indices;public float cutWidth; }
        public TextAsset rigAsset;public Texture2D neckTexture;public Material skinMaterial;
        [Min(.0001f)] public float sourcePixelScale=.005f;
        [Min(.05f)] public float frequencyHz=.78f;
        [Range(0,1.5f)] public float amplitude=1;
        [Range(0,1)] public float exposure=.28f;
        public int sortingOrder=-37;
        [Min(0)] public float bloodPerSecond=24;
        [Min(.1f)] public float bloodGravity=2.2f;
        Data data;Transform visual;Mesh mesh;Material material;SerpentBloodSpray blood;
        Vector2[] rest,posed;Vector3[] output;float[] lengths,restAngles,jointAngles,phi,cos,sin;Transform[] bones;
        float lastTime=-1,emissionClock;int emissionIndex;Vector3 previousWound;
        static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        static float Mean(float a,float b)=>a+Mathf.DeltaAngle(a*Mathf.Rad2Deg,b*Mathf.Rad2Deg)*Mathf.Deg2Rad*.5f;
        static float Random(int i){float f=Mathf.Sin(i*127.1f+311.7f)*43758.5453f;return f-Mathf.Floor(f);}
        static Transform Child(string n,Transform p){var t=new GameObject(n).transform;t.SetParent(p,false);return t;}
        Vector3 Local(Vector2 p)=>new Vector3((p.x-rest[0].x)*sourcePixelScale,-(p.y-rest[0].y)*sourcePixelScale,0);
        Vector2 Point(Vector2 v,int k){var d=v-rest[k];return posed[k]+new Vector2(cos[k]*d.x-sin[k]*d.y,sin[k]*d.x+cos[k]*d.y);}
        void Ensure()
        {
            if(data!=null&&visual)return;data=JsonUtility.FromJson<Data>(rigAsset.text);int n=data.chain.Length/2;
            rest=new Vector2[n];posed=new Vector2[n];bones=new Transform[n];lengths=new float[n-1];restAngles=new float[n-1];jointAngles=new float[n];phi=new float[n-1];cos=new float[n];sin=new float[n];
            visual=Child("Approved severed neck | 65 connected joints",transform);
            for(int i=0;i<n;i++){rest[i]=new Vector2(data.chain[i*2],data.chain[i*2+1]);bones[i]=Child("Whip bone "+i,i==0?visual:bones[i-1]);}
            for(int i=0;i<n-1;i++){var d=rest[i+1]-rest[i];lengths[i]=d.magnitude;restAngles[i]=Mathf.Atan2(d.y,d.x);}
            for(int i=0;i<n;i++)jointAngles[i]=i==0?restAngles[0]:i==n-1?restAngles[n-2]:Mean(restAngles[i-1],restAngles[i]);
            mesh=new Mesh{name="Approved severed neck skin"};mesh.MarkDynamic();output=new Vector3[data.vertices.Length/2];var uv=new Vector2[output.Length];
            for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(data.vertices[i*2]/data.width,1-data.vertices[i*2+1]/data.height);
            mesh.vertices=output;mesh.uv=uv;mesh.triangles=data.indices;
            var t=Child("Independent black-gold cut skin",visual);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();
            material=new Material(skinMaterial);material.SetTexture("_MainTex",neckTexture);material.SetFloat("_Exposure",exposure);r.sharedMaterial=material;r.sortingOrder=sortingOrder;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            blood=Child("Blood from moving wound",visual).gameObject.AddComponent<SerpentBloodSpray>();blood.gravity=bloodGravity;blood.Initialize(skinMaterial,sortingOrder+1);
        }
        public void Hide(){if(blood)blood.Clear();if(visual)visual.gameObject.SetActive(false);lastTime=-1;emissionClock=0;}
        public void Sample(float time,float opacity)
        {
            Ensure();visual.gameObject.SetActive(true);float gain=Ease(time/.5f);posed[0]=rest[0];int n=rest.Length;
            for(int i=0;i<n-1;i++)
            {
                float u=(i+.5f)/(n-1),phase=2*Mathf.PI*frequencyHz*(time-u*.39f/frequencyHz);
                float wave=Mathf.Sin(phase)+.24f*Mathf.Sin(2*phase+.7f)+.11f*Mathf.Sin(phase*.617f+u*3.1f);
                float strength=Ease(u/.28f)*(.17f+.62f*u),tip=Ease((u-.6f)/.4f)*.13f*Mathf.Pow(Mathf.Sin(phase-.3f),3);
                phi[i]=restAngles[i]+amplitude*gain*(strength*wave+tip);
                posed[i+1]=posed[i]+new Vector2(Mathf.Cos(phi[i]),Mathf.Sin(phi[i]))*lengths[i];
            }
            for(int i=0;i<n;i++){float a=i==0?phi[0]:i==n-1?phi[n-2]:Mean(phi[i-1],phi[i]);cos[i]=Mathf.Cos(a-jointAngles[i]);sin[i]=Mathf.Sin(a-jointAngles[i]);}
            for(int i=0;i<output.Length;i++){int k=(int)data.binding[i*2];float w=data.binding[i*2+1];var p=new Vector2(data.vertices[i*2],data.vertices[i*2+1]);output[i]=Local(Vector2.LerpUnclamped(Point(p,k),Point(p,k+1),w));}
            mesh.vertices=output;mesh.RecalculateBounds();material.SetFloat("_Opacity",opacity);material.SetFloat("_Exposure",exposure);
            for(int i=0;i<n;i++){bones[i].position=transform.TransformPoint(Local(posed[i]));float a=i==0?phi[0]:phi[Mathf.Min(n-2,i-1)];bones[i].rotation=transform.rotation*Quaternion.Euler(0,0,-a*Mathf.Rad2Deg);}
            var wound=transform.TransformPoint(Local(Point(new Vector2(data.wound[0],data.wound[1]),n-1)));
            float dt=lastTime<0?0:Mathf.Max(0,time-lastTime);var velocity=dt>.0001f?(wound-previousWound)/dt:Vector3.zero;lastTime=time;previousWound=wound;
            emissionClock+=dt*bloodPerSecond*opacity;
            while(emissionClock>=1)
            {
                emissionClock--;int seed=++emissionIndex;float offset=(Random(seed+19)-.5f)*data.cutWidth*.67f;
                var p=new Vector2(data.wound[0]+data.cutAxis[0]*offset,data.wound[1]+data.cutAxis[1]*offset);
                var at=transform.TransformPoint(Local(Point(p,n-1)));var direction=transform.TransformDirection(new Vector3(Mathf.Cos(phi[n-2]),-Mathf.Sin(phi[n-2]),0));
                var v=velocity*.68f+direction*(.45f+Random(seed+67)*.9f)+new Vector3((Random(seed+23)-.5f)*.7f,(Random(seed+83)-.5f)*.5f);
                blood.Emit(at,v,.012f+Random(seed+53)*.024f,.85f+Random(seed+47)*1.35f,seed);
            }
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
