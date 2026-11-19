using System;
using UnityEngine;

namespace Aloysius.Level1
{
    [DefaultExecutionOrder(850)]
    public sealed class Level1PalaceGuard : MonoBehaviour
    {
        [Header("巡逻区域（相对于初始位置，世界单位）")]
        [SerializeField] Vector2 patrolOffsets=new Vector2(-2.3f,2.3f);
        [SerializeField,Min(.05f)] float moveSpeed=.65f;
        [SerializeField,Min(.1f)] float acceleration=1.8f;
        [SerializeField] Vector2 pauseSeconds=new Vector2(.8f,2.8f);
        [SerializeField,Min(.1f)] float minimumTravel=.7f;
        [SerializeField] bool startWhenPlayerArrives=true;
        [SerializeField] bool initiallyFaceLeft;
        [SerializeField] int randomSeed;
        [Header("行走与转身")]
        [SerializeField,Min(.2f)] float strideLength=.68f;
        [SerializeField,Range(.01f,.2f)] float footLift=.085f;
        [SerializeField,Range(0,30)] float armSwing=18;
        [SerializeField,Range(0,8)] float forwardLean=3;
        [SerializeField,Range(0,.04f)] float bodyBob=.012f;
        [SerializeField,Min(.15f)] float turnSeconds=.38f;
        [SerializeField,Range(0,12)] float clothSway=4;
        [Header("外观及引用")]
        [SerializeField,Range(.7f,1.6f),Tooltip("整体横向比例，1 为原始宽度；不改变身高和巡逻范围。")] float bodyWidth=1.2f;
        [SerializeField,Range(0,1)] float normalExposure=.65f;
        [SerializeField] Level1PalaceCorridor corridor;
        [SerializeField] Level1Lighting lighting;
        [SerializeField] Transform visual;
        [SerializeField] Transform[] bones;
        [SerializeField] SkinnedMeshRenderer[] parts;
        // Bone order is shared with the asset installer. Independent world-space
        // joint poses drive genuine weighted meshes, including three cloth chains.
        static readonly Vector3[] Rest={
            new Vector3(0,.94f),new Vector3(0,1.59f),
            new Vector3(-.07f,1.49f),new Vector3(-.07f,1.17f),new Vector3(-.07f,.84f),
            new Vector3(.035f,1.49f),new Vector3(.035f,1.17f),new Vector3(.035f,.84f),
            new Vector3(0,.94f),new Vector3(0,.51f),new Vector3(0,.08f),
            new Vector3(.018f,.94f),new Vector3(.018f,.51f),new Vector3(.018f,.08f),
            new Vector3(0,.94f),new Vector3(0,.56f),new Vector3(0,.18f),
            new Vector3(.05f,.94f),new Vector3(-.06f,.65f),new Vector3(-.2f,.36f),
            new Vector3(-.10f,.94f),new Vector3(-.32f,.60f),new Vector3(-.55f,.25f),
            new Vector3(-.06f,1.49f),new Vector3(.13f,.96f),new Vector3(.025f,.89f)};
        public static Vector3[] BindPositions=>Rest;
        Vector3 origin;
        System.Random random;
        float target,wait,velocity,phase,blend,turnClock;
        int facing=1,turnFrom=1,turnTo=1;
        bool turning,hasTarget;
        MaterialPropertyBlock block;
        public void Configure(Level1PalaceCorridor hall,Level1Lighting light,Transform body,Transform[] joints,SkinnedMeshRenderer[] meshes)
        {corridor=hall;lighting=light;visual=body;bones=joints;parts=meshes;}
        void Awake()
        {
            origin=transform.position;
            random=new System.Random(randomSeed==0?Environment.TickCount^GetInstanceID():randomSeed);
            facing=initiallyFaceLeft?-1:1;wait=Sample(pauseSeconds);Pose(0);
            visual.localScale=new Vector3(facing*bodyWidth,1,1);
        }
        void OnValidate()
        {
            bodyWidth=Mathf.Clamp(bodyWidth,.7f,1.6f);
            if(visual && !Application.isPlaying)visual.localScale=new Vector3((initiallyFaceLeft?-1:1)*bodyWidth,1,1);
        }
        float Sample(Vector2 range)=>Mathf.Lerp(Mathf.Max(0,Mathf.Min(range.x,range.y)),Mathf.Max(0,Mathf.Max(range.x,range.y)),(float)random.NextDouble());
        void ChooseTarget()
        {
            float left=origin.x+Mathf.Min(patrolOffsets.x,patrolOffsets.y);
            float right=origin.x+Mathf.Max(patrolOffsets.x,patrolOffsets.y);
            float here=transform.position.x;
            float travel=Mathf.Min(minimumTravel,(right-left)*.4f);
            bool goRight=random.NextDouble()>.5;
            if(here+travel>=right)goRight=false;
            else if(here-travel<=left)goRight=true;
            target=goRight?Mathf.Lerp(Mathf.Min(right,here+travel),right,(float)random.NextDouble()):Mathf.Lerp(left,Mathf.Max(left,here-travel),(float)random.NextDouble());
            hasTarget=Mathf.Abs(target-here)>.01f;
        }
        void Update()
        {
            if(!visual || bones==null || bones.Length!=Rest.Length)return;
            float dt=Time.deltaTime;
            if(!turning)visual.localScale=new Vector3(facing*bodyWidth,1,1);
            bool active=!startWhenPlayerArrives || (corridor && corridor.Arrived);
            if(!active)
            {
                velocity=0;blend=Mathf.MoveTowards(blend,0,dt*4);
                Pose(Time.time);return;
            }
            if(turning)
            {
                turnClock+=dt;
                float t=Mathf.Clamp01(turnClock/Mathf.Max(.15f,turnSeconds));
                float width=t<.5f?Mathf.Lerp(1,.035f,Mathf.SmoothStep(0,1,t*2)):Mathf.Lerp(.035f,1,Mathf.SmoothStep(0,1,(t-.5f)*2));
                visual.localScale=new Vector3((t<.5f?turnFrom:turnTo)*width*bodyWidth,1-.02f*Mathf.Sin(t*Mathf.PI),1);
                blend=Mathf.MoveTowards(blend,0,dt*6);velocity=0;Pose(Time.time);
                if(t>=1){turning=false;facing=turnTo;phase=0;}
                return;
            }
            if(!hasTarget)
            {
                wait-=dt;if(wait<=0)ChooseTarget();
            }
            int direction=hasTarget?(target>=transform.position.x?1:-1):0;
            if(direction!=0 && direction!=facing)
            {turning=true;turnClock=0;turnFrom=facing;turnTo=direction;velocity=0;return;}
            float remaining=hasTarget?Mathf.Abs(target-transform.position.x):0;
            float desired=direction*Mathf.Min(moveSpeed,Mathf.Sqrt(2*acceleration*remaining));
            velocity=Mathf.MoveTowards(velocity,desired,acceleration*dt);
            float distance=hasTarget?Mathf.Min(Mathf.Abs(velocity)*dt,remaining):0;
            transform.position+=Vector3.right*direction*distance;
            phase=Mathf.Repeat(phase+distance/(Mathf.Max(.2f,strideLength)*bodyWidth*Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.x))),1);
            blend=Mathf.MoveTowards(blend,distance>.00001f?1:0,dt*5);
            if(hasTarget && remaining-distance<.005f)
            {var p=transform.position;p.x=target;transform.position=p;hasTarget=false;velocity=0;wait=Sample(pauseSeconds);}
            Pose(Time.time);
        }
        void SetBone(int i,Vector3 position,float angle=0)
        {bones[i].localPosition=position;bones[i].localRotation=Quaternion.Euler(0,0,angle);}
        Vector3 BodyPoint(Vector3 point,Vector3 hip,Quaternion lean)=>hip+lean*(point-Rest[0]);
        void Pose(float time)
        {
            if(bones==null || bones.Length!=Rest.Length)return;
            float bob=bodyBob*Mathf.Cos(phase*Mathf.PI*4)*blend+.002f*Mathf.Sin(time*1.8f)*(1-blend);
            Vector3 hip=new Vector3(0,.92f+bob);
            float tilt=-forwardLean*blend;
            Quaternion lean=Quaternion.Euler(0,0,tilt);
            SetBone(0,hip,tilt);
            SetBone(1,BodyPoint(Rest[1],hip,lean),tilt*.35f);
            Arm(2,hip,lean,-Mathf.Cos(phase*Mathf.PI*2)*armSwing*blend);
            Arm(5,hip,lean,Mathf.Cos(phase*Mathf.PI*2)*armSwing*blend);
            Leg(8,hip,phase);Leg(11,hip+Vector3.right*.018f,Mathf.Repeat(phase+.5f,1));
            Cloth(14,hip,time,1f);Cloth(17,hip,time,.75f);Cloth(20,hip,time,1.4f);
            SetBone(23,BodyPoint(Rest[23],hip,lean),tilt);
            SetBone(24,BodyPoint(Rest[24],hip,lean),Mathf.Sin(phase*Mathf.PI*2-.4f)*2*blend);
            SetBone(25,BodyPoint(Rest[25],hip,lean),Mathf.Sin(phase*Mathf.PI*2-.6f)*5*blend+Mathf.Sin(time*1.6f)*.6f);
        }
        void Arm(int first,Vector3 hip,Quaternion lean,float swing)
        {
            Vector3 shoulder=BodyPoint(Rest[first],hip,lean);
            float angle=swing-forwardLean*blend;
            float forearm=angle+8+5*blend;
            Vector3 elbow=shoulder+Quaternion.Euler(0,0,angle)*Vector3.down*.32f;
            Vector3 hand=elbow+Quaternion.Euler(0,0,forearm)*Vector3.down*.33f;
            SetBone(first,shoulder,angle);SetBone(first+1,elbow,forearm);SetBone(first+2,hand,forearm*.4f);
        }
        void Leg(int first,Vector3 hip,float cycle)
        {
            // The stance foot moves backwards at root velocity; swing returns with
            // eased endpoints. Opposite phases give alternating grounded contacts.
            float reach=Mathf.Min(.22f,strideLength*.25f);
            float x,y;
            if(cycle<.5f){x=Mathf.Lerp(reach,-reach,cycle*2);y=0;}
            else{float s=(cycle-.5f)*2;x=Mathf.Lerp(-reach,reach,Mathf.SmoothStep(0,1,s));y=Mathf.Sin(s*Mathf.PI)*footLift;}
            Vector3 ankle=new Vector3(x*blend+(first==11?.018f:0),.08f+y*blend);
            Vector2 delta=ankle-hip;
            float length=Mathf.Clamp(delta.magnitude,.05f,.859f);
            Vector2 unit=delta.normalized;
            Vector2 middle=(Vector2)hip+unit*(length*.5f);
            Vector2 knee=middle+new Vector2(-unit.y,unit.x)*Mathf.Sqrt(Mathf.Max(0,.43f*.43f-length*length*.25f));
            float thigh=Mathf.Atan2(knee.x-hip.x,hip.y-knee.y)*Mathf.Rad2Deg;
            float shin=Mathf.Atan2(ankle.x-knee.x,knee.y-ankle.y)*Mathf.Rad2Deg;
            SetBone(first,hip,thigh);SetBone(first+1,knee,shin);
            SetBone(first+2,ankle,cycle>.5f?Mathf.Sin((cycle-.5f)*Mathf.PI*2)*-7*blend:0);
        }
        void Cloth(int first,Vector3 hip,float time,float strength)
        {
            float turn=turning?Mathf.Sin(Mathf.Clamp01(turnClock/turnSeconds)*Mathf.PI)*-turnFrom*3:0;
            for(int i=0;i<3;i++)
            {
                float sway=(Mathf.Sin(phase*Mathf.PI*2-i*.65f)*clothSway*blend+Mathf.Sin(time*1.5f-i*.6f)*.5f+turn)*strength;
                Vector3 p=Rest[first+i]+hip-Rest[0];
                p.x+=i*.012f*sway;
                SetBone(first+i,p,sway*i*.3f);
            }
        }
        void LateUpdate()
        {
            if(parts==null)return;if(block==null)block=new MaterialPropertyBlock();
            var storm=corridor && corridor.Arrived?corridor.Storm:null;
            float exposure=storm?storm.CharacterExposure(normalExposure):normalExposure;
            float visible=1-(lighting?lighting.PipeVisionBlend:0);
            foreach(var part in parts)
            {
                if(!part)continue;part.GetPropertyBlock(block);
                block.SetFloat("_Exposure",exposure*visible);
                block.SetVector("_Turn",new Vector4(1,1,1,0));
                block.SetFloat("_LightningRim",storm?storm.CharacterRim*visible:0);
                block.SetColor("_LightningTint",storm?storm.Tint:Color.black);
                block.SetVector("_PlayerGlowStrength",Vector4.zero);
                if(storm)storm.ApplyPlayerGlow(block,true,visible);
                part.SetPropertyBlock(block);
            }
        }
        void OnDrawGizmosSelected()
        {
            Vector3 start=Application.isPlaying?origin:transform.position;
            Gizmos.color=new Color(.9f,.7f,.25f);
            Vector3 a=start+Vector3.right*patrolOffsets.x,b=start+Vector3.right*patrolOffsets.y;
            Gizmos.DrawLine(a,b);Gizmos.DrawLine(a,a+Vector3.up*.35f);Gizmos.DrawLine(b,b+Vector3.up*.35f);
        }
    }
}
