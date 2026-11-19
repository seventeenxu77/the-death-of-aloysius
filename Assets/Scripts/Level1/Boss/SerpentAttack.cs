using System;
using UnityEngine;
namespace Aloysius.Level1.Boss
{
    // A reusable attack, independent of the campaign. Markers stay editable in the scene.
    public sealed class SerpentAttack : MonoBehaviour
    {
        public enum Kind { Stone, Venom, Fang, Time }
        public enum Phase { Idle, Windup, Shake, Strike, Hold, Recover }
        public Kind kind;
        public StoneSlamCombat stoneSlam;
        public Transform startPoint,endPoint,head,neckRoot,mouth,sacLeft,sacRight;
        public LineRenderer neck,jet,warning;
        public BoxCollider2D standingSurface;
        [Header("动画参数 / 秒、世界单位")]
        [Min(.05f)] public float windupSeconds=.65f,shakeSeconds=.45f,strikeSeconds=.19f,holdSeconds=2.8f,recoverSeconds=1.1f;
        [Min(0)] public float windupDistance=.7f,shakeDistance=.07f;
        [Range(-180,180)] public float spriteForwardAngle;
        public bool autoOrient=true;
        [Min(.1f)] public float hitRadius=.8f,neckWidth=.7f,jetLength=18f;
        [Min(0)] public float sacInflation=.32f;
        public AnimationCurve lungeCurve=new AnimationCurve(new Keyframe(0,0,0,0),new Keyframe(1,1,3,3));
        [SerializeField] Phase phase;
        float clock;Vector3 from,to,back;Vector3 originalScale,sacL,sacR;bool initialized;
        public Phase State=>stoneSlam?stoneSlam.LegacyPhase:phase;
        public bool Busy=>stoneSlam?stoneSlam.Busy:phase!=Phase.Idle;
        public bool Dangerous=>stoneSlam?stoneSlam.Dangerous:phase==Phase.Strike;
        public event Action<SerpentAttack> Impact;
        public event Action<SerpentAttack> Spray;
        void Awake()=>Initialize();
        void Initialize(){if(initialized)return;initialized=true;if(!head)head=transform;originalScale=head.localScale;sacL=sacLeft?sacLeft.localScale:Vector3.one;sacR=sacRight?sacRight.localScale:Vector3.one;}
        public void Begin(Vector3 target)
        {
            Initialize();if(Busy)return;
            if(stoneSlam){stoneSlam.BeginTracking(null,target.y,null,null);return;}
            from=startPoint?startPoint.position:head.position;to=endPoint?endPoint.position:target;
            if(kind==Kind.Venom)to.y=from.y;
            Vector3 dir=(to-from).normalized;if(dir.sqrMagnitude<.01f)dir=Vector3.right;
            back=from-dir*windupDistance;clock=0;phase=Phase.Windup;
            if(autoOrient)head.rotation=Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg-spriteForwardAngle);
            head.localScale=new Vector3(originalScale.x,originalScale.y*((kind==Kind.Fang||kind==Kind.Venom)&&dir.x<-.5f?-1:1),originalScale.z);
            if(standingSurface)standingSurface.enabled=false;
            if(warning){warning.enabled=true;warning.positionCount=2;warning.SetPosition(0,from);warning.SetPosition(1,to);}
        }
        [ContextMenu("预览一次攻击（运行时）")]
        public void PreviewAttack(){if(Application.isPlaying)Begin(endPoint?endPoint.position:transform.position+Vector3.right*5);}
        public void Cancel(){if(stoneSlam)stoneSlam.Cancel();phase=Phase.Idle;clock=0;if(jet)jet.enabled=false;if(warning)warning.enabled=false;if(standingSurface)standingSurface.enabled=false;if(head&&startPoint)head.position=startPoint.position;}
        void Update()=>Step(Time.deltaTime);
        void LateUpdate()=>RefreshNeck();
        public void RefreshNeck()
        {
            if(stoneSlam)return;
            if(!neck||!neckRoot||!head)return;
            neck.positionCount=16;neck.startWidth=neck.endWidth=neckWidth;
            for(int i=0;i<16;i++){float u=i/15f;var p=Vector3.Lerp(neckRoot.position,head.position,u);p.y+=Mathf.Sin(u*Mathf.PI)*(.35f+Mathf.Sin(clock*2)*.08f);neck.SetPosition(i,p);}
        }
        public void Step(float dt)
        {
            if(stoneSlam){stoneSlam.Step(dt);return;}
            Initialize();if(!Busy)return;clock+=dt;
            float duration=Duration(phase);float t=Mathf.Clamp01(clock/duration);
            if(phase==Phase.Windup)head.position=Vector3.Lerp(from,back,Mathf.SmoothStep(0,1,t));
            if(phase==Phase.Shake)head.position=back+new Vector3(Mathf.Sin(clock*83),Mathf.Sin(clock*67),0)*shakeDistance*(.3f+.7f*t);
            if(phase==Phase.Strike)head.position=Vector3.Lerp(back,kind==Kind.Venom?from:to,lungeCurve.Evaluate(t));
            if(phase==Phase.Hold)head.position=kind==Kind.Venom?from:to;
            if(phase==Phase.Recover)head.position=Vector3.Lerp(kind==Kind.Venom?from:to,from,Mathf.SmoothStep(0,1,t));
            float swell=phase==Phase.Windup?t:phase==Phase.Shake?1:phase==Phase.Strike?1-t:0;
            if(sacLeft)sacLeft.localScale=sacL*(1+swell*sacInflation);
            if(sacRight)sacRight.localScale=sacR*(1+swell*sacInflation);
            RefreshNeck();
            if(jet&&jet.enabled){var a=mouth?mouth.position:head.position;var d=(to-from).normalized;jet.SetPosition(0,a);jet.SetPosition(1,a+d*jetLength);jet.startWidth=.16f+Mathf.Sin(clock*39)*.04f;}
            if(standingSurface){standingSurface.enabled=kind==Kind.Fang&&phase==Phase.Hold;standingSurface.transform.position=new Vector3((head.position.x+(neckRoot?neckRoot.position.x:from.x))*.5f,head.position.y+.28f,0);standingSurface.size=new Vector2(Mathf.Abs(head.position.x-(neckRoot?neckRoot.position.x:from.x))+.6f,.3f);}
            if(t<1)return;
            clock=0;
            switch(phase)
            {
                case Phase.Windup:phase=Phase.Shake;break;
                case Phase.Shake:phase=Phase.Strike;if(warning)warning.enabled=false;if(kind==Kind.Venom){if(jet){jet.positionCount=2;jet.enabled=true;}Spray?.Invoke(this);}break;
                case Phase.Strike:phase=Phase.Hold;Impact?.Invoke(this);break;
                case Phase.Hold:phase=Phase.Recover;if(jet)jet.enabled=false;break;
                default:Cancel();break;
            }
        }
        float Duration(Phase p)=>Mathf.Max(.01f,p==Phase.Windup?windupSeconds:p==Phase.Shake?shakeSeconds:p==Phase.Strike?strikeSeconds:p==Phase.Hold?holdSeconds:recoverSeconds);
        public bool Hits(Vector3 feet)
        {
            if(stoneSlam)return stoneSlam.Hits(feet);
            if(!Dangerous)return false;
            if(kind==Kind.Venom){float direction=Mathf.Sign(to.x-from.x);return Mathf.Abs(feet.y+.8f-from.y)<.6f&&(feet.x-from.x)*direction>0&&(feet.x-from.x)*direction<jetLength;}
            return Vector2.Distance(head.position,feet+Vector3.up*.6f)<hitRadius;
        }
        public void NotifySkeletalImpact(){Impact?.Invoke(this);}
        void OnDrawGizmosSelected(){if(startPoint&&endPoint){Gizmos.color=Color.yellow;Gizmos.DrawLine(startPoint.position,endPoint.position);Gizmos.DrawWireSphere(endPoint.position,hitRadius);}}
    }
}
