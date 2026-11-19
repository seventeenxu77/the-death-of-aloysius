using System.Collections.Generic;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    // Small local-space droplets; emission originates from the animated cut, never the background.
    public sealed class SerpentBloodSpray : MonoBehaviour
    {
        struct Drop { public Vector3 p,v;public float age,life,radius;public Color color; }
        readonly List<Drop> drops=new List<Drop>();readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();
        Mesh mesh;Material material;public float gravity=5;
        public void Clear(){drops.Clear();if(mesh)mesh.Clear();}
        public void Initialize(Material source,int order)
        {
            mesh=new Mesh{name="Cut droplets"};mesh.MarkDynamic();gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=gameObject.AddComponent<MeshRenderer>();material=new Material(source);material.SetFloat("_Solid",1);material.SetFloat("_Opacity",1);material.SetFloat("_Exposure",1);
            r.sharedMaterial=material;r.sortingOrder=order;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public void Emit(Vector3 worldPosition,Vector3 worldVelocity,float radius,float life,int seed)
        {
            var c=seed%4==0?new Color(.71f,.11f,.17f,1):new Color(.53f,.055f,.11f,1);
            if(QualitySettings.activeColorSpace==ColorSpace.Linear)c=c.linear;
            drops.Add(new Drop{p=transform.InverseTransformPoint(worldPosition),v=transform.InverseTransformVector(worldVelocity),life=life,radius=radius/Mathf.Max(.001f,transform.lossyScale.x),color=c});
        }
        void Update()
        {
            if(!mesh||Level1TeleportConsole.BlocksGameplayInput)return;
            float dt=Mathf.Min(Time.deltaTime,.05f);var g=transform.InverseTransformVector(Vector3.down*gravity);
            for(int i=drops.Count-1;i>=0;i--){var d=drops[i];d.age+=dt;if(d.age>d.life){drops.RemoveAt(i);continue;}d.v+=g*dt;d.v*=Mathf.Exp(-.3f*dt);d.p+=d.v*dt;drops[i]=d;}
            vertices.Clear();colors.Clear();indices.Clear();
            foreach(var d in drops)
            {
                var direction=d.v.sqrMagnitude>.001f?d.v.normalized:Vector3.down;var normal=new Vector3(-direction.y,direction.x,0);
                float length=d.radius*Mathf.Min(3.5f,1+d.v.magnitude*.4f);var c=d.color;c.a*=1-Mathf.SmoothStep(0,1,(d.age/d.life-.7f)/.3f);
                int k=vertices.Count;vertices.Add(d.p+direction*length);vertices.Add(d.p+normal*d.radius);vertices.Add(d.p-direction*length);vertices.Add(d.p-normal*d.radius);
                for(int j=0;j<4;j++)colors.Add(c);indices.Add(k);indices.Add(k+1);indices.Add(k+2);indices.Add(k);indices.Add(k+2);indices.Add(k+3);
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
