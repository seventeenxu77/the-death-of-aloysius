using UnityEngine;

namespace Aloysius.Level1
{
    [ExecuteAlways]
    public sealed class Level1BedCurtains : MonoBehaviour
    {
        [Header("床帘开合（运行中也可调整）")]
        public bool open;
        [Range(0,1)] public float opening;
        [Min(.1f)] public float duration = 2.4f;
        public AnimationCurve easing = AnimationCurve.EaseInOut(0,0,1,1);
        [Header("突然揭幕：快速拉开，布料滞后与回摆")]
        [Min(.05f)] public float snapOpenDuration=.38f;
        [Min(0)] public float snapHemDelay=.07f;
        [Min(.05f)] public float snapSettleDuration=.5f;
        [Range(0,.5f)] public float snapRecoil=.12f;
        [Header("左右独立帘布")]
        public MeshFilter left;
        public MeshFilter right;
        public Rect leftUV;
        public Rect rightUV;
        [Header("床局部坐标：帘顶固定，内缘向两侧收拢")]
        public float leftAnchor = 1.90f;
        public float rightAnchor = 13.45f;
        public float top = -2.25f;
        public float height = 7.40f;
        public float closedWidth = 5.85f;
        public float gatheredWidth = 1.40f;
        public float hemLift = .18f;
        [Range(8,80)] public int segments = 48;
        Mesh leftMesh, rightMesh;
        float clock;
        bool geometryDirty=true;
        float lastOpening=-1;
        bool snapping;
        public bool FullyOpen => open && !snapping && opening >= .999f;
        float snapClock,snapFrom;
        public void Toggle() { snapping=false;clock=opening;open = !open; }
        public void SnapOpen()
        {
            if(snapping)return;
            open=true;snapFrom=opening;snapClock=0;snapping=true;geometryDirty=true;
        }
        public void ResetClosed()
        {snapping=false;open=false;opening=clock=0;snapClock=0;geometryDirty=true;Rebuild();}
        void OnEnable() { clock = opening; Rebuild(); }
        void OnValidate() { clock = opening; geometryDirty=true; }
        void Update()
        {
            if (Application.isPlaying)
            {
                if(snapping)
                {
                    snapClock+=Time.deltaTime;opening=SnapAmount(0);clock=opening;geometryDirty=true;
                    if(snapClock>=Mathf.Max(.05f,snapOpenDuration)+snapHemDelay+Mathf.Max(.05f,snapSettleDuration))
                    {snapping=false;opening=clock=1;}
                }
                else
                {
                    clock = Mathf.MoveTowards(clock, open ? 1 : 0, Time.deltaTime / Mathf.Max(.1f,duration));
                    opening = Mathf.Clamp01(easing.Evaluate(clock));
                }
            }
            if(geometryDirty || !Mathf.Approximately(lastOpening,opening))Rebuild();
        }
        public void Rebuild()
        {
            if (!left || !right) return;
            Make(ref leftMesh,left,leftUV,true);
            Make(ref rightMesh,right,rightUV,false);
            geometryDirty=false;lastOpening=opening;
        }
        float SnapAmount(float row)
        {
            float t=Mathf.Clamp01((snapClock-row*snapHemDelay)/Mathf.Max(.05f,snapOpenDuration));
            return Mathf.Lerp(snapFrom,1,1-Mathf.Pow(1-t,3));
        }
        void Make(ref Mesh mesh, MeshFilter filter, Rect uv, bool isLeft)
        {
            if (!mesh) { mesh = new Mesh { name = isLeft ? "Left gathered curtain" : "Right gathered curtain", hideFlags = HideFlags.HideAndDontSave }; mesh.MarkDynamic(); }
            int rows = Mathf.Max(8,segments);
            var v = new Vector3[(rows+1)*2]; var tex = new Vector2[v.Length]; var colors = new Color[v.Length]; var tri = new int[rows*6];
            float h = height - hemLift*opening;
            for (int i=0;i<=rows;i++)
            {
                float t=(float)i/rows;
                float localOpening=snapping?SnapAmount(t):opening;
                float w=Mathf.Lerp(closedWidth,gatheredWidth,localOpening)+Mathf.Sin(t*Mathf.PI)*.22f*localOpening;
                if(snapping)
                {
                    float settle=Mathf.Clamp01((snapClock-Mathf.Max(.05f,snapOpenDuration)-snapHemDelay)/Mathf.Max(.05f,snapSettleDuration));
                    // The upper attachment stays fixed; the gathered cloth and hem
                    // overshoot once, then settle rather than snapping into a still pose.
                    w+=snapRecoil*Mathf.Sin(settle*Mathf.PI*3)*Mathf.Pow(1-settle,2)*t;
                }
                float x=isLeft?leftAnchor:rightAnchor-w;
                v[2*i]=new Vector3(x,top-h*t,0); v[2*i+1]=new Vector3(x+w,top-h*t,0);
                tex[2*i]=new Vector2(uv.xMin,Mathf.Lerp(uv.yMax,uv.yMin,t)); tex[2*i+1]=new Vector2(uv.xMax,tex[2*i].y);
                colors[2*i]=colors[2*i+1]=Color.white;
                if(i<rows) { int k=i*6,a=i*2;tri[k]=a;tri[k+1]=a+2;tri[k+2]=a+1;tri[k+3]=a+1;tri[k+4]=a+2;tri[k+5]=a+3; }
            }
            mesh.Clear();mesh.vertices=v;mesh.uv=tex;mesh.colors=colors;mesh.triangles=tri;mesh.RecalculateBounds();filter.sharedMesh=mesh;
        }
        void OnDisable()
        {
            if(left)left.sharedMesh=null;if(right)right.sharedMesh=null;
            if(Application.isPlaying){Destroy(leftMesh);Destroy(rightMesh);}else{DestroyImmediate(leftMesh);DestroyImmediate(rightMesh);}
        }
    }
}
