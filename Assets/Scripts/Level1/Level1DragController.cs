using UnityEngine;
using Aloysius.GothicStage;

namespace Aloysius.Level1
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(GothicBride2DWalker))]
    public sealed class Level1DragController : MonoBehaviour
    {
        [SerializeField] Level1Draggable[] objects;
        [Header("拖动移动")]
        [Tooltip("人物与物品一起移动的最高速度，单位：Unity 单位/秒。脚步频率随移动距离自动匹配。")]
        [SerializeField, Min(0f)] float dragSpeed = .38f;
        [Tooltip("拖动启动、停止和反向时的加减速度，单位：Unity 单位/秒²。数值越大，响应越快。")]
        [SerializeField, Min(.01f)] float dragAcceleration = 1.6f;
        [Header("抓取与松手")]
        [Tooltip("到达物品侧边后，蹲下并抓住物品的过渡时间，单位：秒。")]
        [SerializeField, Min(.01f)] float grabDuration = .32f;
        [Tooltip("松手并恢复站姿的过渡时间，单位：秒。")]
        [SerializeField, Min(.01f)] float releaseDuration = .25f;
        [Tooltip("人物站位到物品抓取边缘的水平距离。过大会使手臂无法够到物品。")]
        [SerializeField, Min(0f)] float standOffDistance = .40f;
        GothicBride2DWalker walker;
        Level1PlayerContactBody body;
        Level1TraversalController traversal;
        Level1Draggable held;
        int side;
        float weight;
        bool aligned,releasing,blockedUntilRelease,jumpAfterRelease;
        public bool ExternalControl { get; set; }
        public event System.Action ResetPerformed;
        public bool IsDragging => held && aligned && !releasing && weight>=1;
        public float PoseWeight => weight;
        public Level1Draggable HeldObject => held;
        public bool IsNavigating => held && !aligned && !releasing;
        public void Configure(Level1Draggable[] targets)
        {
            objects=targets;walker=GetComponent<GothicBride2DWalker>();body=GetComponent<Level1PlayerContactBody>();
            traversal=GetComponent<Level1TraversalController>();
            walker.ExternalControl=true;
        }
        void Start()=>Configure(objects);
        void OnEnable(){walker=GetComponent<GothicBride2DWalker>();walker.ExternalControl=true;}
        void OnDisable(){if(held)held.Highlight.Held=false;held=null;weight=0;if(walker)walker.ExternalControl=false;}
        void Update()
        {
            if(Level1TeleportConsole.BlocksGameplayInput)return;
            if(ExternalControl)return;
            int direction=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            Tick(direction,Time.deltaTime,Input.GetKey(KeyCode.E),Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift),Input.GetKeyDown(KeyCode.Space),Input.GetKeyDown(KeyCode.R),Input.GetKey(KeyCode.C),Input.GetKey(KeyCode.W),Input.GetKeyDown(KeyCode.S));
        }
        public void CancelForTeleport()
        {
            if(held && held.Highlight)held.Highlight.Held=false;
            held=null;weight=0;aligned=releasing=jumpAfterRelease=false;blockedUntilRelease=false;
            if(traversal)traversal.ResetTraversal();
            ExternalControl=false;
            if(walker)walker.ExternalControl=true;
        }
        public void Tick(int direction,float dt,bool grab,bool sprint=false,bool jump=false,bool reset=false,bool crawl=false,bool climb=false,bool drop=false)
        {
            dt=Mathf.Clamp(dt,0,.05f);
            if(reset)
            {
                if(objects!=null)foreach(var item in objects)if(item)item.ResetPosition();
                held=null;weight=0;aligned=releasing=jumpAfterRelease=false;blockedUntilRelease=grab;
                if(traversal)traversal.ResetTraversal();
                walker.ResetActor();ResetPerformed?.Invoke();return;
            }
            if(!held && traversal && traversal.Tick(direction,dt,grab,jump,crawl,climb,drop))return;
            if(!grab)blockedUntilRelease=false;
            if(!held && grab && !blockedUntilRelease && walker.JumpState==GothicJumpState.Grounded && !walker.Turning)
            {
                body.FitToPose();Physics2D.SyncTransforms();
                float closest=float.PositiveInfinity;
                if(objects!=null)foreach(var item in objects)
                {
                    if(!item || !item.isActiveAndEnabled || !item.CompareTag("Draggable"))continue;
                    item.Highlight.Evaluate(0);
                    if(!item.Highlight.IsContacting)continue;
                    for(int end=-1;end<=1;end+=2)
                    {
                        float standX=item.Grip(end).x+end*standOffDistance;
                        if(standX<walker.LeftLimit || standX>walker.RightLimit)continue;
                        float distance=Mathf.Abs(standX-transform.position.x);
                        if(distance>=closest)continue;
                        closest=distance;held=item;side=end;
                    }
                }
                if(held){aligned=releasing=false;weight=0;held.Highlight.Held=true;}
            }
            if(!held){walker.Advance(direction,dt,sprint,jump);if(traversal)traversal.TryCatch(grab);return;}
            if(!held.isActiveAndEnabled || !held.GetComponent<Collider2D>().enabled){held.Highlight.Held=false;held=null;weight=0;walker.Advance(0,dt);return;}
            if(!grab||jump){releasing=true;blockedUntilRelease=grab;jumpAfterRelease|=jump;}
            if(releasing && !aligned)
            {
                held.Highlight.Held=false;held=null;weight=0;
                walker.Advance(direction,dt,sprint,jumpAfterRelease);jumpAfterRelease=false;return;
            }
            Vector3 grip=held.Grip(side);
            if(releasing)
            {
                weight=Mathf.MoveTowards(weight,0,dt/Mathf.Max(.01f,releaseDuration));
                walker.AdvanceDrag(0,dt,weight,grip,transform.position.x,transform.position.x,dragSpeed,dragAcceleration);
                if(weight==0){held.Highlight.Held=false;held=null;aligned=false;walker.Advance(0,dt,false,jumpAfterRelease);jumpAfterRelease=false;}
                return;
            }
            if(!aligned)
            {
                float target=grip.x+side*standOffDistance;
                float dx=target-transform.position.x;
                if(Mathf.Abs(dx)>.001f || Mathf.Abs(walker.Speed)>.001f || walker.JumpState!=GothicJumpState.Grounded)
                {
                    walker.AdvanceTowards(target,dt);return;
                }
                if(walker.Facing!=-side || walker.Turning){walker.Advance(-side,dt);return;}
                if(walker.WalkBlend>.001f){walker.Advance(0,dt);return;}
                aligned=true;
            }
            weight=Mathf.MoveTowards(weight,1,dt/Mathf.Max(.01f,grabDuration));
            float min=transform.position.x+held.LeftBound-held.Bounds.min.x;
            float max=transform.position.x+held.RightBound-held.Bounds.max.x;
            if(weight<1)min=max=transform.position.x;
            float delta=walker.AdvanceDrag(weight>=1?direction:0,dt,weight,grip,min,max,dragSpeed,dragAcceleration);
            held.transform.position+=Vector3.right*delta;
        }
    }
}
