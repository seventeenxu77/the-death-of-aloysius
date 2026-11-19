using UnityEngine;
using Aloysius.GothicStage;

namespace Aloysius.Level1
{
    public enum Level1TraversalState { Normal, Lowering, Crawling, Rising, Catching, Hanging, Pulling, InsidePipe }
    public sealed class Level1TraversalController : MonoBehaviour
    {
        [SerializeField] Transform gripEdge;
        [SerializeField] SpriteRenderer pipeRenderer;
        [SerializeField,Min(.1f)] float crawlSpeed=.28f;
        [SerializeField,Min(.1f)] float pullDuration=1.9f;
        [SerializeField,Min(.05f)] float catchRadius=.20f;
        [Header("管内路径与阻挡")]
        [SerializeField, Tooltip("管底曲线：横轴为贴图从左到右的比例，纵轴为从下到上的比例。")]
        AnimationCurve pipeFloor = new AnimationCurve(new Keyframe(.08f,.326f),new Keyframe(.2f,.331f),new Keyframe(.26f,.32f),new Keyframe(.36f,.25f),new Keyframe(.48f,.224f),new Keyframe(.58f,.22f),new Keyframe(.72f,.26f),new Keyframe(.84f,.31f),new Keyframe(.94f,.29f));
        [SerializeField, Tooltip("管顶曲线，与管底使用相同的贴图比例坐标。用于检测头部空间。")]
        AnimationCurve pipeCeiling = new AnimationCurve(new Keyframe(.08f,.76f),new Keyframe(.2f,.70f),new Keyframe(.3f,.69f),new Keyframe(.42f,.63f),new Keyframe(.55f,.555f),new Keyframe(.7f,.60f),new Keyframe(.8f,.72f),new Keyframe(.9f,.78f),new Keyframe(.94f,.77f));
        [SerializeField, Range(.6f,.98f), Tooltip("右侧阻挡位置，占管道贴图宽度的比例。手伸到这里就停止。")]
        float pipeEnd = .94f;
        [SerializeField, Min(.02f), Tooltip("为人物前方手掌留出的距离，单位：世界单位。")]
        float pipeFrontClearance = .98f;
        [SerializeField, Min(.01f), Tooltip("攀入后适应弯曲内壁的时间，单位：秒。")]
        float pipeEntryBlendDuration = .4f;
        [SerializeField, Min(.01f)] float pipeCrawlAcceleration = 1.2f;
        float pipeU,pipeEntryClock;
        System.Func<float,float> floorSampler,ceilingSampler;
        GothicBride2DWalker walker;
        float clock,phase,moving,surfaceY,crawlVelocity;
        int look,originalPipeOrder;
        Vector3 catchStart;
        bool catchBlocked;
        public Level1TraversalState State { get; private set; }
        public Transform GripEdge=>gripEdge;
        public SpriteRenderer PipeRenderer=>pipeRenderer;
        public float PipePathPosition=>pipeU;
        public float Progress=>State==Level1TraversalState.Pulling?Mathf.Clamp01(clock/pullDuration):0;
        public void Configure(Transform grip,SpriteRenderer pipe)
        {gripEdge=grip;pipeRenderer=pipe;walker=GetComponent<GothicBride2DWalker>();originalPipeOrder=pipe?pipe.sortingOrder:9;}
        void Awake(){walker=GetComponent<GothicBride2DWalker>();originalPipeOrder=pipeRenderer?pipeRenderer.sortingOrder:9;}
        void OnDisable()=>ResetTraversal();
        public void ResetTraversal()
        {State=Level1TraversalState.Normal;clock=phase=moving=crawlVelocity=pipeEntryClock=0;catchBlocked=false;if(pipeRenderer)pipeRenderer.sortingOrder=originalPipeOrder;}
        public void TryCatch(bool grab)
        {
            if(!grab){catchBlocked=false;return;}
            if(catchBlocked||State!=Level1TraversalState.Normal||!gripEdge||walker.Facing!=1||walker.JumpState!=GothicJumpState.Airborne)return;
            Transform hand=null,nearShoulder=null,farShoulder=null;
            foreach(var b in walker.Bones){if(b.name=="near_hand")hand=b;else if(b.name=="near_upperarm")nearShoulder=b;else if(b.name=="far_upperarm")farShoulder=b;}
            bool handsNear=hand&&Vector2.Distance(hand.TransformPoint(new Vector3(.025f,-.025f,0)),gripEdge.position)<=catchRadius;
            bool armsReach=nearShoulder&&farShoulder&&Vector2.Distance(nearShoulder.position,gripEdge.position)<.47f
                &&Vector2.Distance(farShoulder.position,gripEdge.position+new Vector3(-.10f,.015f,0))<.47f;
            if(armsReach&&(handsNear||gripEdge.position.y>nearShoulder.position.y))BeginHang();
        }
        public void BeginHang()
        {walker.CaptureTraversalPose();catchStart=transform.position;clock=0;State=Level1TraversalState.Catching;look=1;}
        public bool Tick(int direction,float dt,bool grab,bool jump,bool crawl,bool climb,bool drop)
        {
            dt=Mathf.Clamp(dt,0,.05f);
            if(!grab)catchBlocked=false;
            if(State==Level1TraversalState.Normal)
            {
                if(!crawl||walker.JumpState!=GothicJumpState.Grounded||walker.Turning)return false;
                look=walker.Facing;surfaceY=walker.GroundY;walker.CaptureTraversalPose();clock=phase=moving=crawlVelocity=0;State=Level1TraversalState.Lowering;
            }
            clock+=dt;
            if((State==Level1TraversalState.Catching||State==Level1TraversalState.Hanging||State==Level1TraversalState.Pulling||State==Level1TraversalState.InsidePipe)&&(!gripEdge||!gripEdge.gameObject.activeInHierarchy||!pipeRenderer||!pipeRenderer.enabled||(drop&&State!=Level1TraversalState.InsidePipe)))
            {Drop();return true;}
            if(State==Level1TraversalState.InsidePipe){AdvanceInPipe(direction,dt);return true;}
            if(State==Level1TraversalState.Lowering)
            {
                float progress=Mathf.Clamp01(clock/1.4f);
                walker.PoseGroundTransition(surfaceY,progress,Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/.14f)),look);
                if(clock>=1.4f){State=Level1TraversalState.Crawling;clock=0;}
            }
            else if(State==Level1TraversalState.Crawling)
            {
                if(!crawl||jump)
                {walker.CaptureTraversalPose();State=Level1TraversalState.Rising;clock=0;return true;}
                float min=walker.LeftLimit;
                float max=walker.RightLimit;
                crawlVelocity=Mathf.MoveTowards(crawlVelocity,direction*crawlSpeed,dt*1.2f);
                var p=transform.position;float before=p.x;p.x=Mathf.Clamp(p.x+crawlVelocity*dt,min,max);transform.position=p;
                float delta=p.x-before;phase=Mathf.Repeat(phase+delta*look/.36f,1);moving=Mathf.MoveTowards(moving,Mathf.Abs(delta)>.00001f?1:0,dt*6);
                walker.PoseCrawl(surfaceY,phase,moving,1,look);
                if(!walker.HasStandingSupport(surfaceY,p.x)){ResetTraversal();walker.ResumeAfterTraversal();return true;}
            }
            else if(State==Level1TraversalState.Rising)
            {
                walker.PoseGroundTransition(surfaceY,1-Mathf.Clamp01(clock/1.35f),Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/.14f)),look);
                if(clock>=1.35f){State=Level1TraversalState.Normal;walker.ResumeAfterTraversal();}
            }
            else if(State==Level1TraversalState.Catching)
            {
                var grip=gripEdge.position;float t=Mathf.Clamp01((clock-.12f)/.26f);
                Vector3 target=new Vector3(grip.x-.18f,grip.y-1.6782353f,catchStart.z);
                transform.position=Vector3.Lerp(catchStart,target,Mathf.SmoothStep(0,1,t));
                walker.PoseHang(grip,t,Mathf.Clamp01(clock/.12f));
                if(t>=1){State=Level1TraversalState.Hanging;clock=0;}
            }
            else if(State==Level1TraversalState.Hanging)
            {
                var grip=gripEdge.position;transform.position=new Vector3(grip.x-.18f,grip.y-1.6782353f,transform.position.z);
                walker.PoseHang(grip,1);
                if(jump||climb){walker.CaptureTraversalPose();State=Level1TraversalState.Pulling;clock=0;if(pipeRenderer)pipeRenderer.sortingOrder=40;}
            }
            else if(State==Level1TraversalState.Pulling)
            {
                float t=Mathf.Clamp01(clock/pullDuration);var grip=gripEdge.position;
                transform.position=new Vector3(grip.x-.18f,grip.y-1.6782353f,transform.position.z);
                walker.PoseMantle(grip,t);
                if(t>=1)
                {
                    surfaceY=grip.y;transform.position=new Vector3(grip.x+.37f,surfaceY,transform.position.z);
                    phase=moving=crawlVelocity=pipeEntryClock=0;State=Level1TraversalState.InsidePipe;clock=0;
                    pipeU=UAtX(transform.position.x);floorSampler=PipeFloorAtX;ceilingSampler=PipeCeilingAtX;
                    walker.PoseCrawl(surfaceY,0,0,1,1);
                }
            }
            return true;
        }

        // The actor is kinematic and moved through Transform. Constrain its path here,
        // rather than relying on a trigger collider to stop direct Transform writes.
        Vector3 RawPipePoint(float u,AnimationCurve curve)
        {
            var sprite=pipeRenderer.sprite;var rect=sprite.rect;
            Vector3 local=new Vector3((u*rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,(curve.Evaluate(u)*rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0);
            return pipeRenderer.transform.TransformPoint(local);
        }
        float UAtX(float x)
        {
            float lo=0,hi=1;
            for(int i=0;i<16;i++){float mid=(lo+hi)*.5f;if(RawPipePoint(mid,pipeFloor).x<x)lo=mid;else hi=mid;}
            return (lo+hi)*.5f;
        }
        float FloorOffset=>gripEdge.position.y-RawPipePoint(UAtX(gripEdge.position.x),pipeFloor).y;
        public float PipeFloorAtX(float x)=>RawPipePoint(UAtX(x),pipeFloor).y+FloorOffset;
        public float PipeCeilingAtX(float x)=>RawPipePoint(UAtX(x),pipeCeiling).y-.025f;
        void PoseInPipe()
        {
            var p=RawPipePoint(pipeU,pipeFloor);
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(pipeEntryClock/Mathf.Max(.01f,pipeEntryBlendDuration)));
            p.y=Mathf.Lerp(gripEdge.position.y,p.y+FloorOffset,blend);p.z=transform.position.z;transform.position=p;
            if(floorSampler==null){floorSampler=PipeFloorAtX;ceilingSampler=PipeCeilingAtX;}
            walker.PosePipeCrawl(phase,moving,blend,floorSampler,ceilingSampler);
        }
        bool FitsPipe()
        {
            foreach(var b in walker.Bones)
            {
                float radius=b.name=="head"?.18f:b.name=="pelvis"?.08f:b.name=="torso"?.055f:0;
                if(radius==0||b.position.x<gripEdge.position.x)continue;
                if(b.position.y+radius>PipeCeilingAtX(b.position.x)+.002f||b.position.y-radius<PipeFloorAtX(b.position.x)-.002f)return false;
            }
            return true;
        }
        void AdvanceInPipe(int direction,float dt)
        {
            float entry=UAtX(gripEdge.position.x+.37f);
            float end=Mathf.Max(entry,UAtX(RawPipePoint(pipeEnd,pipeFloor).x-pipeFrontClearance));
            pipeU=Mathf.Clamp(pipeU,entry,end);
            float before=pipeU,oldPhase=phase;
            pipeEntryClock+=dt;
            crawlVelocity=Mathf.MoveTowards(crawlVelocity,direction*crawlSpeed,Mathf.Max(.01f,pipeCrawlAcceleration)*dt);
            float span=Mathf.Max(.001f,Vector2.Distance(RawPipePoint(pipeU-.001f,pipeFloor),RawPipePoint(pipeU+.001f,pipeFloor))/.002f);
            pipeU=Mathf.Clamp(pipeU+crawlVelocity*dt/span,entry,end);
            float travel=Vector2.Distance(RawPipePoint(before,pipeFloor),RawPipePoint(pipeU,pipeFloor))*Mathf.Sign(pipeU-before);
            phase=Mathf.Repeat(phase+travel/.36f,1);moving=Mathf.MoveTowards(moving,Mathf.Abs(travel)>.00001f?1:0,dt*6);
            PoseInPipe();
            if(pipeEntryClock>=pipeEntryBlendDuration&&!FitsPipe())
            {pipeU=before;phase=oldPhase;moving=0;crawlVelocity=0;PoseInPipe();}
            if(Mathf.Abs(pipeU-before)<.000001f)crawlVelocity=0;
            // Only the left mouth leads out; S/Space/C cannot drop through the shell.
            if(direction<0&&pipeU<=entry+.00002f){BeginHang();}
        }
        void OnDrawGizmosSelected()
        {
            if(!pipeRenderer||!pipeRenderer.sprite||!gripEdge||pipeFloor==null||pipeCeiling==null)return;
            float start=UAtX(gripEdge.position.x);
            Vector3 lastFloor=RawPipePoint(start,pipeFloor)+Vector3.up*FloorOffset;
            Vector3 lastCeiling=RawPipePoint(start,pipeCeiling)-Vector3.up*.025f;
            for(int i=1;i<=64;i++)
            {
                float u=Mathf.Lerp(start,pipeEnd,i/64f);
                Vector3 floor=RawPipePoint(u,pipeFloor)+Vector3.up*FloorOffset;
                Vector3 ceiling=RawPipePoint(u,pipeCeiling)-Vector3.up*.025f;
                Gizmos.color=Color.green;Gizmos.DrawLine(lastFloor,floor);
                Gizmos.color=Color.cyan;Gizmos.DrawLine(lastCeiling,ceiling);
                lastFloor=floor;lastCeiling=ceiling;
            }
            Gizmos.color=Color.red;Gizmos.DrawLine(lastFloor,lastCeiling);
        }
        void Drop()
        {
            // The root is a floor reference during the mantle; derive it from the
            // actual pelvis before restoring gravity to avoid a positional jump.
            Transform pelvis=null;foreach(var b in walker.Bones)if(b.name=="pelvis"){pelvis=b;break;}
            Vector3 p=transform.position;if(pelvis){p.x=pelvis.position.x;p.y=Mathf.Max(walker.RoomFloorY,pelvis.position.y-.8382353f);}transform.position=p;
            ResetTraversal();catchBlocked=true;walker.ResumeAfterTraversal();walker.Advance(0,0);
        }
    }
}
