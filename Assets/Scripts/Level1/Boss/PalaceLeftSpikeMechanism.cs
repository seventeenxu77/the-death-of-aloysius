using System;
using UnityEngine;
using Aloysius.GothicStage;

namespace Aloysius.Level1.Boss
{
    [ExecuteAlways]
    public sealed class PalaceLeftSpikeMechanism : MonoBehaviour
    {
        [Serializable] public sealed class Motion
        {
            public int fps;public float duration;public string[] bones;
            public GothicBonePose[] onFrames,offFrames;
            public float[] onAngles,offAngles;
        }
        [Header("横向机关与开关")]
        public Transform outlet,bladeSlide,bladeTip,leverMarker,leverVisual,leverPivot;
        public Transform leverGrip;
        public MeshRenderer bladeRenderer;
        public BoxCollider2D bladeDetection;
        public TextAsset leverMotion;
        [Header("已确认半圆开关与握柄")]
        [Min(.1f)] public float leverHandleLength=.82f;
        [Min(.1f)] public float leverStandDistance=.58f;
        public float leverOffAngle=132,leverOnAngle=100;
        [Min(0)] public float handWristOffsetPixels=19;
        [Tooltip("原动作中手部接触的时间范围，单位为动画秒")]
        public Vector2 gripReachTimes=new Vector2(.35f,1.12f),gripReleaseTimes=new Vector2(2.26f,2.95f);
        [Header("向左射出（世界单位 / 秒）")]
        [Min(.1f)] public float strokeDistance=4.6f;
        [Min(.02f)] public float shootSeconds=.16f;
        [Min(.02f)] public float retractSeconds=.38f;
        [Min(.01f)] public float collisionHeight=.45f;
        [Min(.05f),Tooltip("只有刺尖前段参与击破标记，轴杆碰到脖子不算成功")]
        public float tipHitLength=.65f;
        [Header("拨杆动作与同帧触发")]
        [Min(.1f)] public float actionPlaybackSpeed=1.5f;
        [Min(0),Tooltip("原动作时间轴上的触发秒数；默认位于实际拨动杆柄的中段")]
        public float activateAtClipSeconds=1.52f;
        [Min(.01f)] public float approachTolerance=.025f;
        [Min(.1f)] public float approachTimeout=4;
        [Min(.01f)] public float poseBlendSeconds=.18f;
        [Min(.01f)] public float releaseSeconds=.16f;
        [SerializeField] bool armed;
        [SerializeField,Range(0,1)] float extension;
        [SerializeField,HideInInspector] float authoredBladeLength=4.6f;
        public bool Armed=>armed;
        public bool Operating=>actor!=null;
        public float Extension=>extension;
        public Bounds ActiveBounds=>bladeDetection?bladeDetection.bounds:new Bounds(outlet.position,Vector3.zero);
        Motion motion;
        GothicBride2DWalker actor;
        Action<bool> committed;
        bool posing,wanted,hasCommitted;
        float clock,approachClock,strokeClock,strokeFrom;
        MaterialPropertyBlock properties;
        Vector3 previousTip;
        void Awake(){LoadMotion();Snap(false);}
        void Update(){if(!Application.isPlaying){ApplyBlade();RestLever();}}
        void OnDisable()=>CancelOperation();
        void LoadMotion(){if(motion==null&&leverMotion)motion=JsonUtility.FromJson<Motion>(leverMotion.text);}
        static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        public bool BeginOperation(GothicBride2DWalker character,Action<bool> onCommit)
        {
            if(Operating||!character||character.JumpState!=GothicJumpState.Grounded)return false;
            LoadMotion();if(motion==null||motion.bones.Length!=character.Bones.Length)return false;
            for(int i=0;i<motion.bones.Length;i++)if(motion.bones[i]!=character.Bones[i].name)return false;
            actor=character;committed=onCommit;wanted=!armed;posing=hasCommitted=false;clock=approachClock=0;return true;
        }
        public void StepInteraction(float dt)
        {
            if(!actor)return;
            if(!posing)
            {
                approachClock+=dt;
                // The authored action's lever pivot is 115 pixels to the right of the actor.
                float standX=leverMarker.position.x-leverStandDistance;
                if(approachClock>approachTimeout||standX<actor.LeftLimit||standX>actor.RightLimit){CancelOperation();return;}
                if(Mathf.Abs(actor.transform.position.x-standX)>approachTolerance)
                {actor.AdvanceTowards(standX,dt);return;}
                if(actor.Facing!=1||actor.Turning)
                {
                    var position=actor.transform.position;actor.Advance(1,dt);actor.transform.position=position;return;
                }
                actor.CaptureTraversalPose();posing=true;clock=0;
            }
            clock+=Mathf.Max(0,dt)*actionPlaybackSpeed;
            float f=Mathf.Clamp(clock*motion.fps,0,motion.onFrames.Length-1);int a=Mathf.FloorToInt(f),b=Mathf.Min(a+1,motion.onFrames.Length-1);
            var frames=wanted?motion.onFrames:motion.offFrames;var angles=wanted?motion.onAngles:motion.offAngles;
            actor.PoseLeverAction(frames[a],frames[b],f-a,clock/Mathf.Max(.01f,poseBlendSeconds));
            float authored=-Mathf.Lerp(angles[a],angles[b],f-a)*Mathf.Rad2Deg;
            leverPivot.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(leverOnAngle,leverOffAngle,Mathf.InverseLerp(50,130,authored)));
            if(leverGrip)
            {
                float weight=Ease((clock-gripReachTimes.x)/Mathf.Max(.01f,gripReachTimes.y-gripReachTimes.x))*(1-Ease((clock-gripReleaseTimes.x)/Mathf.Max(.01f,gripReleaseTimes.y-gripReleaseTimes.x)));
                var wrist=leverGrip.position+Vector3.up*(handWristOffsetPixels/actor.Clip.pixelsPerUnit*actor.transform.lossyScale.y);
                actor.ConstrainLeverHand(wrist,weight);
            }
            // Pose, visible handle and trap share this clock and this exact activation frame.
            if(!hasCommitted&&clock>=Mathf.Min(activateAtClipSeconds,motion.duration))
            {hasCommitted=true;SetArmed(wanted);committed?.Invoke(armed);}
            if(clock>=motion.duration)
            {var character=actor;actor=null;committed=null;character.BeginTraversalRelease(releaseSeconds);}
        }
        public void CancelOperation()
        {
            if(actor){var character=actor;actor=null;character.BeginTraversalRelease(releaseSeconds);}
            committed=null;posing=false;RestLever();
        }
        public void SetArmed(bool value)
        {if(armed==value)return;armed=value;strokeFrom=extension;strokeClock=0;}
        public void Snap(bool value)
        {armed=value;extension=value?1:0;strokeFrom=extension;strokeClock=0;RestLever();ApplyBlade();if(bladeTip)previousTip=bladeTip.position;}
        void RestLever(){if(leverPivot)leverPivot.localRotation=Quaternion.Euler(0,0,armed?leverOnAngle:leverOffAngle);}
        public void Step(float dt)
        {
            if(bladeTip)previousTip=bladeTip.position;
            strokeClock+=Mathf.Max(0,dt);
            float t=Mathf.Clamp01(strokeClock/Mathf.Max(.02f,armed?shootSeconds:retractSeconds));
            // Rapid launch, firm stop; withdrawal is slower. The blade translates without stretching.
            float k=armed?1-Mathf.Pow(1-t,3):Ease(t);
            extension=Mathf.Lerp(strokeFrom,armed?1:0,k);ApplyBlade();
        }
        void ApplyBlade()
        {
            if(!outlet||!bladeSlide||!bladeTip)return;
            if(leverMarker&&leverVisual)leverVisual.position=leverMarker.position;
            if(leverGrip)
            {
                leverGrip.localPosition=new Vector3(leverHandleLength,0,0);
                var handle=leverPivot.GetComponentInChildren<MeshRenderer>();if(handle)handle.transform.localScale=new Vector3(leverHandleLength/.82f,1,1);
            }
            // Full-length art is hidden to the right of the slot, then slides out to the left.
            bladeSlide.position=outlet.position+Vector3.right*(strokeDistance*(1-extension));
            bladeTip.position=outlet.position-Vector3.right*(strokeDistance*extension);
            if(bladeRenderer)
            {
                bladeRenderer.transform.localScale=new Vector3(strokeDistance/Mathf.Max(.1f,authoredBladeLength),1,1);
                if(properties==null)properties=new MaterialPropertyBlock();bladeRenderer.GetPropertyBlock(properties);
                properties.SetFloat("_ClipEnabled",1);properties.SetFloat("_ClipRight",outlet.position.x);bladeRenderer.SetPropertyBlock(properties);
            }
            if(bladeDetection)
            {
                bladeDetection.enabled=extension>.01f;
                bladeDetection.transform.position=outlet.position-Vector3.right*(strokeDistance*extension*.5f);
                bladeDetection.size=new Vector2(Mathf.Max(.01f,strokeDistance*extension),collisionHeight);
            }
        }
        public bool HitsStone(SerpentAttack stone)
        {
            if(!armed||extension<=.02f||!bladeDetection||!bladeDetection.enabled||!stone||!stone.Busy)return false;
            var center=bladeTip.position+Vector3.right*(tipHitLength*.5f);
            var area=new Bounds(center,new Vector3(tipHitLength,collisionHeight,.2f));
            area.Encapsulate(new Bounds(previousTip+Vector3.right*(tipHitLength*.5f),new Vector3(tipHitLength,collisionHeight,.2f)));
            if(stone.stoneSlam)return stone.stoneSlam.IntersectsTrap(area);
            return false;
        }
        void OnDrawGizmosSelected()
        {
            if(!outlet)return;Gizmos.color=new Color(1,.7f,.2f);var end=outlet.position-Vector3.right*strokeDistance;
            Gizmos.DrawLine(outlet.position,end);Gizmos.DrawWireCube((outlet.position+end)*.5f,new Vector3(strokeDistance,collisionHeight,.05f));
        }
    }
}
