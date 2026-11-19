using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Aloysius.GothicStage;
namespace Aloysius.Level1.Boss
{
    [Serializable] public class PalaceRoom
    {
        public string id;
        public Transform root,spawn,depthEntry,depthEnd;
        public Rect bounds;
        public float floor,cameraSize=4.8f;
        public Collider2D[] platforms;
    }
    [DefaultExecutionOrder(-200)]
    public sealed class SerpentCampaign:MonoBehaviour
    {
        public enum Beat { Dormant, Awakening, CrackedFloor, StoneTrap, FangStairs, ReturnBedroom, Arsenal, TimeCourt, Victory }
        [Header("角色与既有场景")]
        public GothicBride2DWalker actor;
        public Level1RoyalBedchamber route;
        public Level1DragController drag;
        public SerpentWakePlayer wake;
        public PalaceRoom[] rooms;
        public SerpentAttack stone,venom,fang,timeHead;
        public SerpentAttack[] stairHeads;
        [Header("寝宫：封门与垂直洞")]
        public Transform fallingRocks,crackedFloor,hatch,shaftLanding,leftLight;
        [Header("尖刺与远景入口")]
        public Transform spike,spikeLever,pitPortal;
        public GameObject trapStairs;
        [Header("卧室闸门")]
        public Transform lift,liftLever,chains,blade,underGate;
        public GameObject gateExit;
        [Header("炮台")]
        public Transform ironGate,cannon,cannonBall,cannonLanding,ladder;
        public GameObject projectile;
        [Header("时间之蛇：可控制的诱饵")]
        public Transform controller,controlledHead;
        public Transform[] timeTraps;
        public Transform[] timeWeights;
        public float[] trapDepths;
        [Header("可调节演出与难度")]
        [Min(.1f)]public float attackInterval=3.8f,interactRadius=1.15f,respawnImmunity=2f;
        [Min(.1f)]public float liftHeight=1.6f,liftSeconds=1.6f,chainCorrosionSeconds=1.3f;
        [Min(.1f)]public float depthTravelSeconds=2.3f,fadeSeconds=.45f;
        [Min(.1f)]public float possessionSeconds=18f,controlledSpeed=3.3f;
        [Range(.2f,1)]public float farScale=.55f;
        public bool invulnerable;
        [SerializeField] Beat beat;
        [SerializeField] int roomIndex;
        [SerializeField] bool cracked,spikeArmed,stoneDead,liftRaised,fangDead,ironOpen,ballLoaded,venomDead,baitPlaced,possessed;
        [SerializeField] int timeSeals;
        [SerializeField] float corrosion,ironCorrosion,controlDepth;
        public Beat CurrentBeat=>beat;
        public bool OwnsCamera=>beat!=Beat.Dormant&&beat!=Beat.Awakening;
        public bool Transitioning=>transitioning;
        public int RoomIndex=>roomIndex;
        public bool StoneDefeated=>stoneDead;
        public bool FangDefeated=>fangDead;
        public bool VenomDefeated=>venomDead;
        public int TimeSeals=>timeSeals;
        bool transitioning,ballistic,initialized;
        float attackClock,immunity,wakeClock,liftClock,black,possessClock,stairClock;
        int stairIndex,timeAttack;
        Vector3 actorScale,rocksRest,spikeRest,liftRest,bladeRest,ironRest,controllerRest,originalPosition;
        Vector3 controllerScale;
        Vector3[] weightsRest;
        Collider2D[] originalSurfaces;
        int[] playerOrders;
        Vector3 cameraVelocity;
        MaterialPropertyBlock playerLight;
        readonly List<string> events=new List<string>();
        public IReadOnlyList<string> Events=>events;
        void Start()
        {
            Initialize();stone.Impact+=StoneImpact;venom.Spray+=OnSpray;
            var gm=FindObjectOfType<Level1TeleportConsole>();
            if(gm&&!gm.Commands.TryGet("boss",out _))gm.RegisterCommand("boss","boss status|reset|god","Boss 关卡状态、重置和无敌",args=>{
                if(args.Length!=1)return GMCommandResult.Error("boss status|reset|god");
                if(args[0]=="god"){invulnerable=!invulnerable;return GMCommandResult.Ok("无敌="+invulnerable);}
                if(args[0]=="reset"){RestartChapter();return GMCommandResult.Ok("已重置 Boss 关卡");}
                return GMCommandResult.Ok($"{beat}; room={rooms[roomIndex].id}; stone={stoneDead}; fang={fangDead}; venom={venomDead}; time={timeSeals}/4");
            });
            foreach(string arg in Environment.GetCommandLineArgs())if(arg=="-boss")StartCoroutine(StartBossAfterRoute());
        }
        IEnumerator StartBossAfterRoute(){yield return null;yield return null;Teleport("wake");}
        public void Initialize()
        {
            if(initialized)return;initialized=true;actorScale=actor.transform.localScale;originalPosition=actor.transform.position;
            rocksRest=fallingRocks.position;spikeRest=spike.position;liftRest=lift.position;bladeRest=blade.position;ironRest=ironGate.position;controllerRest=controller.position;
            controllerScale=controller.localScale;weightsRest=new Vector3[timeWeights.Length];for(int i=0;i<weightsRest.Length;i++)weightsRest[i]=timeWeights[i].position;
            originalSurfaces=actor.StandingSurfaces;
            playerOrders=new int[actor.Parts.Length];for(int i=0;i<playerOrders.Length;i++)playerOrders[i]=actor.Parts[i].sortingOrder;
            fallingRocks.gameObject.SetActive(false);gateExit.SetActive(false);trapStairs.SetActive(false);leftLight.gameObject.SetActive(false);
            foreach(var a in AllAttacks())a.gameObject.SetActive(false);
            rooms[3].root.gameObject.SetActive(false);
            UpdateTimeSeals();
        }
        IEnumerable<SerpentAttack> AllAttacks(){yield return stone;yield return venom;yield return fang;yield return timeHead;foreach(var a in stairHeads)yield return a;}
        void Log(string s){events.Add(s);Debug.Log("[SerpentCampaign] "+s);}
        void Update()
        {
            if(Level1TeleportConsole.BlocksGameplayInput)return;
            float dt=Mathf.Min(Time.deltaTime,.05f);immunity=Mathf.Max(0,immunity-dt);
            if(beat==Beat.Dormant){if(route.curtains.open&&Vector2.Distance(actor.transform.position,route.bedRevealCenter.position)<5)BeginWake();return;}
            if(beat==Beat.Awakening)
            {
                wakeClock+=dt;drag.ExternalControl=true;actor.Advance(0,dt);
                if(wakeClock>1.1f&&!fallingRocks.gameObject.activeSelf){fallingRocks.gameObject.SetActive(true);StartCoroutine(DropObject(fallingRocks,rocksRest+Vector3.up*7,rocksRest,.65f));}
                if(wake.Finished){beat=Beat.CrackedFloor;TakeControl(0,false);leftLight.gameObject.SetActive(true);Log("苏醒结束，右入口已封闭，左侧裂缝可击破");}return;
            }
            if(Input.GetKeyDown(KeyCode.R)){Respawn();return;}
            if(transitioning)return;
            if(possessed){ControlTime(dt);return;}
            int direction=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            actor.Advance(direction,dt,Input.GetKey(KeyCode.LeftShift),Input.GetKeyDown(KeyCode.Space));
            if(Input.GetKeyDown(KeyCode.E))Interact();
            if(Input.GetKey(KeyCode.W))TryDepthEntry();
            TickWorld(dt);
        }
        public void BeginWake()
        {
            if(beat!=Beat.Dormant)return;beat=Beat.Awakening;wakeClock=0;drag.CancelForTeleport();drag.ExternalControl=true;actor.ExternalControl=true;
            route.curtains.SnapOpen();wake.Play();Log("四头蛇苏醒：转头、吐信、毒囊、尾巴轨道开始");
        }
        public void TickWorld(float dt)
        {
            if(transitioning)return;
            var p=actor.transform.position;attackClock-=dt;
            if(beat==Beat.CrackedFloor)
            {
                if(!cracked&&p.x<hatch.position.x+5&&attackClock<=0&&!stone.Busy)AimStone(p,false);
                if(cracked&&Mathf.Abs(p.x-hatch.position.x)<1.05f){StartCoroutine(FallShaft());return;}
            }
            if(beat==Beat.StoneTrap)
            {
                if(!stoneDead&&attackClock<=0&&!stone.Busy)AimStone(p,true);
            }
            if(beat==Beat.FangStairs)
            {
                bool busy=false;foreach(var h in stairHeads)busy|=h.Busy;
                stairClock-=dt;
                if(!busy&&stairClock<=0)
                {
                    foreach(var h in stairHeads)h.gameObject.SetActive(false);
                    stairIndex=Mathf.Clamp(Mathf.FloorToInt((p.y-rooms[2].floor)/.85f),0,stairHeads.Length-1);
                    var a=stairHeads[stairIndex];a.gameObject.SetActive(true);a.Begin(a.endPoint.position);stairClock=.4f;
                }
            }
            if(beat==Beat.ReturnBedroom)
            {
                if((liftRaised&&liftClock<liftSeconds)||(!liftRaised&&liftClock>0))
                {
                    // GothicBrideJumpMotion follows its actual support collider on
                    // the next Advance. Do not carry a player standing below it.
                    liftClock=Mathf.MoveTowards(liftClock,liftRaised?liftSeconds:0,dt);lift.position=liftRest+Vector3.up*(liftHeight*Mathf.SmoothStep(0,1,liftClock/liftSeconds));
                }
                if(!fangDead&&attackClock<=0&&!venom.Busy)AimVenom(p);
                if(venom.State==SerpentAttack.Phase.Hold&&!fangDead&&liftRaised&&JetTouches(chains.position))
                {corrosion+=dt;Tint(chains,new Color(.35f,.48f,.11f));if(corrosion>=chainCorrosionSeconds)StartCoroutine(DropGate());}
                if(fangDead&&Mathf.Abs(p.x-underGate.position.x)<.65f&&Mathf.Abs(p.y-underGate.position.y)<1.4f){StartCoroutine(Transition(4,false));return;}
            }
            if(beat==Beat.Arsenal)
            {
                if(!venomDead&&attackClock<=0&&!venom.Busy)AimVenom(p);
                if(!ironOpen&&venom.State==SerpentAttack.Phase.Hold&&JetTouches(ironGate.position+Vector3.up*.8f))
                {ironCorrosion+=dt;Tint(ironGate,new Color(.32f,.4f,.15f));if(ironCorrosion>=chainCorrosionSeconds){ironOpen=true;ironGate.gameObject.SetActive(false);Log("毒液腐蚀铁门，炮台通路打开");}}
                actor.SetMovementBounds(rooms[4].bounds.xMin,ironOpen?rooms[4].bounds.xMax:ironGate.position.x-.8f);
                if(Input.GetKey(KeyCode.W))TryLadder();
            }
            if(beat==Beat.TimeCourt&&!possessed)
            {
                if(attackClock<=0&&!timeHead.Busy)
                {
                    if(baitPlaced){StartCoroutine(BiteController());return;}
                    AimTime(p);
                }
            }
            foreach(var a in AllAttacks())if(a.gameObject.activeInHierarchy&&a.Hits(p)&&immunity<=0&&!invulnerable){Respawn();break;}
            if(p.y<rooms[roomIndex].floor-3)Respawn();
        }
        void AimStone(Vector3 player,bool pit)
        {
            stone.gameObject.SetActive(true);float y=rooms[pit?1:0].floor;
            stone.startPoint.position=new Vector3(player.x,y+9,0);
            bool hitSpike=pit&&spikeArmed&&Mathf.Abs(player.x-spike.position.x)<1.1f;
            stone.endPoint.position=new Vector3(player.x,hitSpike?spike.position.y+1.4f:y+.25f,0);
            stone.neckRoot.position=stone.startPoint.position+Vector3.up*4;
            stone.Begin(stone.endPoint.position);attackClock=attackInterval;
        }
        public void StoneImpact(SerpentAttack attack)
        {
            if(beat==Beat.CrackedFloor&&!cracked&&Mathf.Abs(attack.head.position.x-hatch.position.x)<1.2f)
            {cracked=true;crackedFloor.gameObject.SetActive(false);Log("巨石砸碎裂缝地板，垂直洞开启");}
            else if(beat==Beat.StoneTrap&&spikeArmed&&!stoneDead&&Mathf.Abs(attack.head.position.x-spike.position.x)<1.1f)
            {stoneDead=true;attack.Cancel();attack.head.position=spike.position+Vector3.up*1.2f;attack.enabled=false;trapStairs.SetActive(true);Log("巨石头被尖刺刺穿，二层远景入口可达");}
        }
        void AimVenom(Vector3 player)
        {
            venom.gameObject.SetActive(true);float x=rooms[roomIndex].bounds.xMin-.7f;
            venom.startPoint.position=new Vector3(x,player.y+.8f,0);venom.endPoint.position=new Vector3(x+18,player.y+.8f,0);venom.neckRoot.position=venom.startPoint.position-Vector3.right*3;
            if(beat==Beat.Arsenal&&ironOpen)PositionVenomOnBalcony();
            venom.Begin(venom.endPoint.position);attackClock=attackInterval;
        }
        void PositionVenomOnBalcony()
        {
            venom.startPoint.position=cannonLanding.position+new Vector3(1.5f,1.4f);
            venom.endPoint.position=venom.startPoint.position-Vector3.right*18;
            venom.neckRoot.position=venom.startPoint.position+new Vector3(4,1.5f);
        }
        void OnSpray(SerpentAttack a){Log("毒囊蓄力完成，水平喷射：y="+a.startPoint.position.y.ToString("F2"));}
        bool JetTouches(Vector3 point)
        {return point.x>venom.startPoint.position.x&&point.x<venom.startPoint.position.x+venom.jetLength&&Mathf.Abs(point.y-venom.startPoint.position.y)<.6f;}
        public bool Interact()
        {
            if(transitioning)return false;
            if(beat==Beat.StoneTrap&&Near(spikeLever))
            {spikeArmed=!spikeArmed;spike.position=spikeRest+Vector3.up*(spikeArmed?1.15f:0);Tint(spikeLever,spikeArmed?new Color(1,.85f,.45f):Color.white);Log("尖刺开关="+spikeArmed);return true;}
            if(beat==Beat.ReturnBedroom&&Near(liftLever))
            {liftRaised=!liftRaised;Tint(liftLever,liftRaised?new Color(1,.85f,.45f):Color.white);Log("卧室平台抬升="+liftRaised);return true;}
            if(beat==Beat.Arsenal&&ironOpen&&!ballLoaded&&Near(cannonBall))
            {ballLoaded=true;cannonBall.gameObject.SetActive(false);Log("巨石炮弹已装入炮膛");return true;}
            if(beat==Beat.Arsenal&&Near(cannon))
            {if(ballLoaded&&!venomDead){StartCoroutine(FireCannon());return true;}if(venomDead){StartCoroutine(LaunchPlayer());return true;}}
            if(beat==Beat.TimeCourt&&Near(controller)&&!possessed)
            {baitPlaced=true;attackClock=.4f;Log("时间控制器已启动为诱饵，等待蛇咬住");return true;}
            return false;
        }
        bool Near(Transform t)=>t&&Vector2.Distance(actor.transform.position,t.position)<interactRadius;
        public bool TryDepthEntry()
        {
            if(transitioning)return false;
            if(beat==Beat.StoneTrap&&stoneDead&&Vector2.Distance(actor.transform.position,pitPortal.position)<1.3f)
            {StartCoroutine(Transition(2,true));return true;}
            if(beat==Beat.FangStairs&&Vector2.Distance(actor.transform.position,rooms[2].depthEntry.position)<1.3f)
            {StartCoroutine(Transition(3,true));return true;}
            return false;
        }
        void TakeControl(int index,bool relocate)
        {
            roomIndex=index;route.enabled=false;drag.CancelForTeleport();drag.ExternalControl=true;actor.ExternalControl=true;
            var r=rooms[index];actor.transform.localScale=actorScale;
            actor.ConfigurePlatforms(r.platforms);
            if(relocate)actor.transform.position=r.spawn.position;
            actor.ConfigureAdventureFloor(r.floor);actor.SetMovementBounds(r.bounds.xMin,r.bounds.xMax);
            if(relocate)actor.RelocateOnRoomFloor(r.spawn.position,true);
            foreach(var a in AllAttacks()){a.Cancel();a.gameObject.SetActive(false);}
            rooms[3].root.gameObject.SetActive(index==3);
            if(index==2){stairClock=0;stairIndex=0;}
            if(index==3&&!fangDead){fang.gameObject.SetActive(true);fang.head.position=underGate.position+Vector3.up*1.6f;fang.RefreshNeck();fang.enabled=false;}
            attackClock=2;immunity=respawnImmunity;
        }
        IEnumerator FallShaft()
        {
            transitioning=true;stone.Cancel();stone.gameObject.SetActive(false);var from=actor.transform.position;
            float t=0;while(t<1){t+=Time.deltaTime/1.1f;actor.PoseCrawl(from.y,0,0,.2f,actor.Facing);actor.transform.position=Vector3.Lerp(from,shaftLanding.position,Mathf.Clamp01(t*t));yield return null;}
            beat=Beat.StoneTrap;TakeControl(1,true);transitioning=false;Log("通过垂直洞落到地下尖刺室");
        }
        IEnumerator Transition(int destination,bool depth)
        {
            transitioning=true;foreach(var a in AllAttacks())a.Cancel();var r=rooms[roomIndex];Vector3 from=actor.transform.position;
            if(depth&&r.depthEnd)
            {
                float t=0;while(t<1){t+=Time.deltaTime/depthTravelSeconds;float u=Mathf.SmoothStep(0,1,t);actor.transform.position=Vector3.Lerp(from,r.depthEnd.position,u);actor.transform.localScale=actorScale*Mathf.Lerp(1,farScale,u);actor.PoseDepthWalk(t*2,1,-1);SetPlayerOrder(-8);yield return null;}
            }
            yield return Fade(1);TakeControl(destination,true);SetPlayerOrder(0);
            beat=destination==2?Beat.FangStairs:destination==3?Beat.ReturnBedroom:destination==4?Beat.Arsenal:Beat.TimeCourt;
            yield return Fade(0);transitioning=false;Log("进入 "+rooms[destination].id);
        }
        IEnumerator Fade(float target){float start=black;for(float t=0;t<1;t+=Time.deltaTime/Mathf.Max(.05f,fadeSeconds)){black=Mathf.Lerp(start,target,t);yield return null;}black=target;}
        IEnumerator DropObject(Transform item,Vector3 from,Vector3 to,float seconds)
        {for(float t=0;t<1;t+=Time.deltaTime/seconds){item.position=Vector3.Lerp(from,to,t*t);yield return null;}item.position=to;}
        IEnumerator DropGate()
        {
            fangDead=true;chains.gameObject.SetActive(false);venom.Cancel();Log("锁链腐蚀断裂，闸刀落下");
            yield return DropObject(blade,blade.position,bladeRest-Vector3.up*3.1f,.36f);
            fang.head.rotation=Quaternion.Euler(0,0,-75);yield return DropObject(fang.head,fang.head.position,underGate.position-Vector3.up*.4f,.6f);
            fang.gameObject.SetActive(false);gateExit.SetActive(true);Log("突刺头死亡，闸门下方打开");
        }
        IEnumerator FireCannon()
        {
            transitioning=true;venom.Cancel();venom.gameObject.SetActive(true);PositionVenomOnBalcony();
            venom.Begin(venom.endPoint.position);venom.Step(.001f);venom.Cancel();
            projectile.SetActive(true);Vector3 from=cannon.position+new Vector3(.8f,1.1f),to=venom.head.position;
            for(float t=0;t<1;t+=Time.deltaTime/.7f){projectile.transform.position=Vector3.Lerp(from,to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*1.2f;yield return null;}
            projectile.transform.position=to;venom.head.localScale=new Vector3(1.15f,-.75f,1);yield return new WaitForSeconds(.12f);
            projectile.SetActive(false);venomDead=true;yield return DropObject(venom.head,to,to-Vector3.up*4,.5f);venom.gameObject.SetActive(false);transitioning=false;Log("炮弹命中毒液头；炮台现在可发射玩家");
        }
        IEnumerator LaunchPlayer()
        {
            transitioning=true;ballistic=true;var from=cannon.position;for(float t=0;t<1;t+=Time.deltaTime/1.8f){actor.transform.position=Vector3.Lerp(from,cannonLanding.position,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*4;actor.PoseCrawl(actor.transform.position.y,t,0,.6f,1);yield return null;}
            actor.RelocateOnRoomFloor(cannonLanding.position,true);ballistic=false;transitioning=false;Log("玩家抵达毒液平台，W 攀梯");
        }
        IEnumerator ClimbToFinal()
        {
            transitioning=true;var from=actor.transform.position;for(float t=0;t<1;t+=Time.deltaTime/1.6f){actor.transform.position=from+Vector3.up*(t*3);actor.PoseHang(actor.transform.position+Vector3.up*1.6f,1);yield return null;}
            yield return Transition(5,true);
        }
        public bool TryLadder()
        {
            var p=actor.transform.position;
            if(transitioning||beat!=Beat.Arsenal||!venomDead||p.y<cannonLanding.position.y-.3f||Mathf.Abs(p.x-ladder.position.x)>=1.2f)return false;
            StartCoroutine(ClimbToFinal());return true;
        }
        void AimTime(Vector3 player)
        {
            timeHead.gameObject.SetActive(true);timeHead.Cancel();timeHead.kind=SerpentAttack.Kind.Time;timeAttack=(timeAttack+1)%(Mathf.Max(1,5-timeSeals));
            float baseY=rooms[5].floor;
            if(timeAttack==0){timeHead.startPoint.position=new Vector3(player.x,baseY+8);timeHead.endPoint.position=player+Vector3.up*.5f;}
            else if(timeAttack==1){timeHead.startPoint.position=new Vector3(rooms[5].bounds.xMin,player.y+.8f);timeHead.endPoint.position=player+Vector3.right*2;}
            else if(timeAttack==2){timeHead.startPoint.position=new Vector3(rooms[5].bounds.xMax,player.y+.8f);timeHead.endPoint.position=player-Vector3.right*2;}
            else if(timeAttack==3){timeHead.startPoint.position=rooms[5].depthEnd.position;timeHead.endPoint.position=player+Vector3.up;}
            else{timeHead.kind=SerpentAttack.Kind.Venom;timeHead.startPoint.position=new Vector3(rooms[5].bounds.xMin,player.y+.8f);timeHead.endPoint.position=new Vector3(rooms[5].bounds.xMax,player.y+.8f);}
            timeHead.neckRoot.position=rooms[5].depthEnd.position+Vector3.up*2;timeHead.Begin(player);attackClock=attackInterval;
        }
        IEnumerator BiteController()
        {
            transitioning=true;timeHead.Cancel();timeHead.gameObject.SetActive(true);var from=rooms[5].depthEnd.position;
            timeHead.head.rotation=Quaternion.identity;timeHead.head.localScale=Vector3.one;
            for(float t=0;t<1;t+=Time.deltaTime/.8f){timeHead.head.position=Vector3.Lerp(from,controller.position+Vector3.up*.8f,t);timeHead.RefreshNeck();yield return null;}
            possessed=true;baitPlaced=false;possessClock=possessionSeconds;controlDepth=0;controlledHead=timeHead.head;
            controller.SetParent(controlledHead,true);transitioning=false;Log("时间蛇咬住控制器：A/D 移动，W/S 深度，E 引落重锤");
        }
        void ControlTime(float dt)
        {
            possessClock-=dt;
            float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            float z=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            MoveControlled(x,z,dt);
            if(Input.GetKeyDown(KeyCode.E))TryTimeTrap();
            if(possessClock<=0)ReleaseTime();
        }
        public void MoveControlled(float x,float depth,float dt)
        {
            if(!possessed)return;controlDepth=Mathf.Clamp01(controlDepth+depth*dt*.5f);
            var r=rooms[5];var p=controlledHead.position;p.x=Mathf.Clamp(p.x+x*controlledSpeed*dt,r.bounds.xMin+1,r.bounds.xMax-1);p.y=r.floor+1.3f+controlDepth*3;
            controlledHead.position=p;controlledHead.localScale=Vector3.one*Mathf.Lerp(1,.6f,controlDepth);timeHead.RefreshNeck();
        }
        public bool TryTimeTrap()
        {
            if(!possessed||timeSeals>=timeTraps.Length)return false;
            var t=timeTraps[timeSeals];if(Mathf.Abs(controlledHead.position.x-t.position.x)>1.2f||Mathf.Abs(controlDepth-trapDepths[timeSeals])>.2f)return false;
            var weight=timeWeights[timeSeals];timeSeals++;UpdateTimeSeals();Log("时间蛇攻击能力已剥离 "+timeSeals+"/4");StartCoroutine(CrushTimeHead(weight,t.position));
            return true;
        }
        void UpdateTimeSeals(){for(int i=0;i<timeTraps.Length;i++)Tint(timeTraps[i],i==timeSeals?new Color(1,.92f,.45f):new Color(.25f,.2f,.23f));}
        IEnumerator CrushTimeHead(Transform weight,Vector3 target)
        {
            possessed=false;transitioning=true;
            yield return DropObject(weight,weight.position,target+Vector3.up*.3f,.35f);
            timeHead.head.localScale=new Vector3(1.1f,.28f,1);yield return new WaitForSeconds(.1f);ReleaseTime();transitioning=false;
            if(timeSeals>=timeTraps.Length){beat=Beat.Victory;timeHead.gameObject.SetActive(false);Log("四头蛇战斗完成");}
        }
        void ReleaseTime(){possessed=false;controller.SetParent(rooms[5].root,true);controller.position=controllerRest;controller.localScale=controllerScale;timeHead.Cancel();timeHead.head.localScale=Vector3.one;attackClock=2.5f;}
        public void Respawn()
        {
            if(beat==Beat.Dormant)return;StopAllCoroutines();transitioning=ballistic=false;black=0;if(possessed||controller.parent==timeHead.head)ReleaseTime();
            projectile.SetActive(false);
            if(fangDead){chains.gameObject.SetActive(false);blade.position=bladeRest-Vector3.up*3.1f;gateExit.SetActive(true);}
            TakeControl(roomIndex,true);immunity=respawnImmunity;Log("检查点复位："+rooms[roomIndex].id);
        }
        public bool Teleport(string id)
        {
            Initialize();int index=Array.FindIndex(rooms,r=>r.id==id);if(index<0&&id!="wake")return false;
            StopAllCoroutines();transitioning=false;black=0;
            if(id=="wake"){beat=Beat.Dormant;actor.ConfigurePlatforms(originalSurfaces);actor.ConfigureAdventureFloor(originalPosition.y);route.enabled=true;route.TeleportForDebug("bed",null,out _);actor.RelocateOnRoomFloor(route.bedRevealCenter.position);BeginWake();return true;}
            beat=index==0?Beat.CrackedFloor:index==1?Beat.StoneTrap:index==2?Beat.FangStairs:index==3?Beat.ReturnBedroom:index==4?Beat.Arsenal:Beat.TimeCourt;
            TakeControl(index,true);return true;
        }
        public void SuspendForOriginalRoute()
        {
            StopAllCoroutines();transitioning=possessed=false;black=0;beat=Beat.Dormant;route.enabled=true;
            actor.transform.localScale=actorScale;actor.ConfigurePlatforms(originalSurfaces);actor.ConfigureAdventureFloor(originalPosition.y);SetPlayerOrder(0);
            foreach(var a in AllAttacks()){a.Cancel();a.gameObject.SetActive(false);}rooms[3].root.gameObject.SetActive(false);drag.ExternalControl=false;
        }
        public void RestartChapter()
        {
            if(possessed)ReleaseTime();
            StopAllCoroutines();transitioning=possessed=false;black=0;cracked=spikeArmed=stoneDead=liftRaised=fangDead=ironOpen=ballLoaded=venomDead=baitPlaced=false;
            timeSeals=0;corrosion=ironCorrosion=liftClock=0;fallingRocks.position=rocksRest;spike.position=spikeRest;lift.position=liftRest;blade.position=bladeRest;ironGate.position=ironRest;
            crackedFloor.gameObject.SetActive(true);chains.gameObject.SetActive(true);ironGate.gameObject.SetActive(true);cannonBall.gameObject.SetActive(true);gateExit.SetActive(false);trapStairs.SetActive(false);
            fallingRocks.gameObject.SetActive(false);for(int i=0;i<timeWeights.Length;i++)timeWeights[i].position=weightsRest[i];
            foreach(var a in AllAttacks())a.enabled=true;route.curtains.ResetClosed();wake.ResetPose();beat=Beat.Dormant;Teleport("wake");
        }
        void SetPlayerOrder(int offset){for(int i=0;i<actor.Parts.Length;i++)actor.Parts[i].sortingOrder=playerOrders[i]+offset;}
        static void Tint(Transform root,Color color){foreach(var r in root.GetComponentsInChildren<SpriteRenderer>())r.color=color;}
        public bool FrameCamera(Camera cam,float aspect)
        {
            if(!OwnsCamera)return false;ApplyCharacterLight();var r=rooms[roomIndex];float size=r.cameraSize;
            var target=actor.transform.position+new Vector3(0,2.1f,-10);
            if(beat==Beat.TimeCourt||beat==Beat.Victory){target=new Vector3(r.bounds.center.x,r.floor+3.8f,-10);size=5.4f;}
            float half=Mathf.Min(size*aspect,r.bounds.width*.5f);target.x=Mathf.Clamp(target.x,r.bounds.xMin+half,r.bounds.xMax-half);
            if(!ballistic)target.y=Mathf.Max(target.y,r.floor+size-.7f);
            cam.transform.position=Vector3.SmoothDamp(cam.transform.position,target,ref cameraVelocity,.28f);cam.orthographicSize=Mathf.Lerp(cam.orthographicSize,size,1-Mathf.Exp(-Time.deltaTime*3));return true;
        }
        void LateUpdate()
        {
            if(beat!=Beat.Dormant)ApplyCharacterLight();
        }
        void ApplyCharacterLight(){if(playerLight==null)playerLight=new MaterialPropertyBlock();foreach(var p in actor.Parts){p.GetPropertyBlock(playerLight);playerLight.SetFloat("_Exposure",.8f);playerLight.SetFloat("_LightningRim",.1f);p.SetPropertyBlock(playerLight);}}
        void OnGUI(){if(black<=0)return;var c=GUI.color;GUI.depth=-9999;GUI.color=new Color(0,0,0,black);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=c;}
    }
}
