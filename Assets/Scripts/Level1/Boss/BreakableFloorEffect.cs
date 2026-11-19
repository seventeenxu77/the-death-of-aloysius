using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    [ExecuteAlways]
    public sealed class BreakableFloorEffect : MonoBehaviour
    {
        public Material floorMaterial,debrisMaterial,dustMaterial;
        public BoxCollider2D entrance;
        [Header("破坏判定与通道 / 世界单位")]
        [Min(.2f)] public float hitWidth=2.4f;
        [Min(.05f)] public float hitHeight=.5f;
        [Min(.2f)] public float patchWidth=4.4f;
        [Header("完整地板层")]
        public bool fullFloorMode;
        public Vector2 floorWorldSpan;
        public float floorTop=2.5f,floorBottom=-3.04f;
        public int floorSortingOrder=-29;
        public int rimSortingOrder=32,debrisSortingOrder=31,dustSortingOrder=36;
        [Min(.2f)] public float entranceWidth=1.7f;
        [Header("动画 / 秒")]
        [Min(.05f)] public float collapseSeconds=.105f;
        [Min(.1f)] public float enterDelay=.85f;
        [Min(.1f)] public float effectSeconds=4.5f;
        [Range(0,2)] public float dustOpacity=1;
        [Range(0,2)] public float debrisAmount=1;
        public int sortingOrder=5;
        public bool Broken {get;private set;}
        public bool PassageOpen=>Broken&&age>=enterDelay;
        public float Age=>age;
        float age;
        Transform generated;
        Material floor,frontFloor;
        Mesh debris,dust;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> indices=new List<int>();
        static readonly Color[] StoneColors={new Color(.38f,.20f,.21f),new Color(.53f,.31f,.22f),new Color(.31f,.13f,.19f),new Color(.70f,.51f,.25f),new Color(.22f,.11f,.15f)};
        static float Random(int i){double x=Math.Sin(i*127.1+311.7)*43758.5453;return (float)(x-Math.Floor(x));}
        static float Ease(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
        float Scale=>patchWidth/380;
        Vector3 Point(float x,float y)=>new Vector3((x-263)*Scale,-(y-837)*Scale,0);
        void OnEnable(){Ensure();Sample();}
        void OnDisable(){Clear();}
        void OnValidate(){if(entrance){entrance.size=new Vector2(entranceWidth,.65f);entrance.isTrigger=true;} }
        void Update()
        {
            if(!Application.isPlaying||!Broken)return;
            if(Level1TeleportConsole.BlocksGameplayInput)return;
            age=Mathf.Min(effectSeconds,age+Time.deltaTime);Sample();
        }
        void Clear()
        {
            if(generated)Release(generated.gameObject);
            foreach(var o in owned)if(o)Release(o);
            owned.Clear();generated=null;floor=null;frontFloor=null;
        }
        static void Release(UnityEngine.Object o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        Mesh MakeMesh(string label){var m=new Mesh{name=label,hideFlags=HideFlags.DontSave};m.MarkDynamic();owned.Add(m);return m;}
        void Renderer(string label,Mesh mesh,Material material,int order)
        {
            var t=new GameObject(label){hideFlags=HideFlags.DontSave}.transform;t.SetParent(generated,false);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=order;
        }
        void Ensure()
        {
            if(generated||!floorMaterial||!debrisMaterial||!dustMaterial)return;
            generated=new GameObject("Cracked tile, broken opening and debris"){hideFlags=HideFlags.DontSave}.transform;generated.SetParent(transform,false);
            var tile=MakeMesh("Localized floor patch");
            if(fullFloorMode)
            {
                // The entire room floor is one layer. Align its authored weak tile to the existing hatch.
                float left=floorWorldSpan.x-transform.position.x,right=floorWorldSpan.y-transform.position.x,u=285f/1536;
                tile.vertices=new[]{new Vector3(left,floorTop),new Vector3(0,floorTop),new Vector3(right,floorTop),new Vector3(left,floorBottom),new Vector3(0,floorBottom),new Vector3(right,floorBottom)};
                tile.uv=new[]{new Vector2(0,0),new Vector2(u,0),new Vector2(1,0),new Vector2(0,1),new Vector2(u,1),new Vector2(1,1)};
                tile.triangles=new[]{0,1,3,1,4,3,1,2,4,2,5,4};
            }
            else
            {
                tile.vertices=new[]{Point(70,777),Point(450,777),Point(70,883),Point(450,883)};
                tile.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};tile.triangles=new[]{0,1,2,1,3,2};
            }
            tile.RecalculateBounds();
            floor=new Material(floorMaterial){hideFlags=HideFlags.DontSave};owned.Add(floor);Renderer(fullFloorMode?"Complete room floor | intact / broken":"Intact tile / shattered hole",tile,floor,fullFloorMode?floorSortingOrder:sortingOrder);
            if(fullFloorMode)
            {
                floor.SetFloat("_LayerMode",0);
                frontFloor=new Material(floorMaterial){name="Near floor lip and slab face",hideFlags=HideFlags.DontSave};
                frontFloor.SetFloat("_LayerMode",1);owned.Add(frontFloor);
                Renderer("Foreground floor lip | occludes descending body",tile,frontFloor,rimSortingOrder);
            }
            debris=MakeMesh("Flying and falling angular stone fragments");dust=MakeMesh("Rising dust");
            Renderer("Debris | behind near lip",debris,debrisMaterial,fullFloorMode?debrisSortingOrder:sortingOrder+1);
            Renderer("Dust | foreground",dust,dustMaterial,fullFloorMode?dustSortingOrder:sortingOrder+2);
        }
        public bool TryBreak(Vector3 contact)
        {
            if(Broken||Mathf.Abs(contact.x-transform.position.x)>hitWidth*.5f||Mathf.Abs(contact.y-transform.position.y)>hitHeight)return false;
            Broken=true;age=0;Ensure();Sample();return true;
        }
        public bool CanEnter(Vector3 feet)
        {
            return PassageOpen&&Mathf.Abs(feet.x-transform.position.x)<entranceWidth*.5f&&feet.y<=transform.position.y+.18f&&feet.y>=transform.position.y-.5f;
        }
        public void ResetFloor(){Broken=false;age=0;Ensure();Sample();}
        void BeginMesh(){vertices.Clear();uv.Clear();colors.Clear();indices.Clear();}
        void AddVertex(float x,float y,Vector2 tex,Color color){vertices.Add(Point(x,y));uv.Add(tex);colors.Add(color);}
        void Rock(float x,float y,float size,float angle,int seed,float alpha)
        {
            int start=vertices.Count;float c=Mathf.Cos(angle),s=Mathf.Sin(angle);Color color=StoneColors[seed%StoneColors.Length];color.a=alpha;
            Vector2[] pts={new Vector2(-1,-.28f),new Vector2(-.42f,-.75f),new Vector2(.58f,-.52f),new Vector2(1,.08f),new Vector2(.32f,.59f),new Vector2(-.6f,.48f)};
            for(int i=0;i<6;i++)
            {
                float px=pts[i].x*size*(.8f+Random(seed+i)*.3f),py=pts[i].y*size;
                var tint=i<3?Color.Lerp(color,new Color(.76f,.58f,.3f,alpha),.3f):color;tint.a=alpha;
                AddVertex(x+c*px-s*py,y+s*px+c*py,Vector2.zero,tint);
            }
            for(int i=1;i<5;i++){indices.Add(start);indices.Add(start+i);indices.Add(start+i+1);}
        }
        void Cloud(float x,float y,float rx,float ry,Color color)
        {
            int n=vertices.Count;
            AddVertex(x-rx,y-ry,new Vector2(0,0),color);AddVertex(x+rx,y-ry,new Vector2(1,0),color);
            AddVertex(x-rx,y+ry,new Vector2(0,1),color);AddVertex(x+rx,y+ry,new Vector2(1,1),color);
            indices.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
        }
        void EndMesh(Mesh mesh){mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();}
        void Sample()
        {
            if(entrance)entrance.enabled=PassageOpen;
            if(!floor)return;floor.SetFloat("_Reveal",Broken?Ease(age/collapseSeconds):0);
            if(frontFloor)frontFloor.SetFloat("_Reveal",Broken?Ease(age/collapseSeconds):0);
            BeginMesh();
            if(Broken)
            {
                for(int i=0;i<Mathf.RoundToInt(39*debrisAmount);i++)
                {
                    float a=age-Random(i+600)*.07f;if(a<0)continue;
                    float ox=263+(Random(i+87)-.5f)*160,oy=837+(Random(i+888)-.5f)*23;
                    float vx=(Random(i+1)-.5f)*410,vy=-75-Random(i+5)*195,flight=-2*vy/490,restX=ox+vx*flight;
                    float x,y,angle,alpha=1;
                    if(a<flight){x=ox+vx*a;y=oy+vy*a+245*a*a;angle=a*(Random(i+77)-.5f)*16;}
                    else
                    {
                        if(restX>175&&restX<345)continue;
                        float land=a-flight;x=restX+vx*.13f*(1-Mathf.Exp(-land*7));y=oy-Mathf.Abs(Mathf.Sin(land*15))*7*Mathf.Exp(-land*10);
                        angle=flight*(Random(i+77)-.5f)*16;alpha=1-Ease((age-3.8f)/.7f);
                    }
                    if(y<=878&&alpha>0)Rock(x,y,2+Random(i+40)*7,angle,i,alpha);
                }
                // Large broken tile chips lift briefly, then collapse into the void.
                for(int i=0;i<13;i++)
                {
                    float a=age-i*.003f;if(a<0||a>.8f)continue;
                    float x=167+Random(i+20)*181,y=813+Random(i+80)*47;
                    x+=(x-263)*1.2f*a;y+=(-90-Random(i+9)*135)*a+380*a*a;
                    if(y<873)Rock(x,y,10+Random(i+220)*12,(Random(i+18)-.5f)*7*a,i,1-Ease((a-.45f)/.35f));
                }
            }
            EndMesh(debris);BeginMesh();
            if(Broken)for(int i=0;i<33;i++)
            {
                float a=age-Random(i+400)*.12f,life=1.65f+Random(i+300)*1.25f;if(a<0||a>life)continue;
                float side=i%2==0?-1:1,x=263+(Random(i+211)-.5f)*115+side*(18+Random(i+110)*55)*(1-Mathf.Exp(-a*2));
                float y=841-(20+Random(i+320)*52)*(1-Mathf.Exp(-a*2.7f))-(i%4==0?a*17:0);
                float r=(8+Random(i+78)*16)*(1+.7f*a),alpha=Mathf.Clamp01(a/.075f)*Mathf.Pow(Mathf.Max(0,1-a/life),1.6f)*.4f*dustOpacity;
                Cloud(x,y,r*1.5f,r*.68f,new Color(.56f,.43f,.29f,alpha));
            }
            EndMesh(dust);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.yellow;Gizmos.DrawWireCube(transform.position,new Vector3(hitWidth,hitHeight*2,.1f));
            Gizmos.color=Color.cyan;Gizmos.DrawWireCube(transform.position,new Vector3(entranceWidth,.65f,.1f));
        }
    }
}
