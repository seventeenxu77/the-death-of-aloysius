using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Aloysius.Level1.Boss
{
    // Opt-in executable integration rehearsal; never runs during normal play.
    public sealed class SerpentChapterVerification:MonoBehaviour
    {
        const string Folder="D:/the death of aloysius/the death of aloysius/ArtSource/LevelTask_20261116/Verification/";
        readonly List<string> checks=new List<string>();
        readonly List<string> errors=new List<string>();
        SerpentCampaign c;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Environment.GetCommandLineArgs().Contains("-verifychapter"))new GameObject("Opt-in chapter verifier").AddComponent<SerpentChapterVerification>();}
        void Awake(){Application.logMessageReceived+=OnLog;Directory.CreateDirectory(Folder);}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(Folder+"Checks.txt",checks);if(!ok)Debug.LogWarning("[Verification] "+label);}
        void Place(Vector3 p){c.actor.RelocateOnRoomFloor(p,true);Physics2D.SyncTransforms();}
        void FinishAttack(SerpentAttack a){for(int i=0;i<1000&&a.Busy&&a.State!=SerpentAttack.Phase.Hold;i++)a.Step(.0166667f);}
        IEnumerator WaitTransition(){float end=Time.realtimeSinceStartup+12;while(c.Transitioning&&Time.realtimeSinceStartup<end)yield return null;Check(!c.Transitioning,"transition coroutine completes");}
        IEnumerator Start()
        {
            yield return null;yield return null;c=FindObjectOfType<SerpentCampaign>();
            if(!c){Check(false,"campaign exists");Done();yield break;}
            Time.timeScale=4;c.invulnerable=true;
            Check(c.rooms.Length==6,"six connected campaign spaces");
            Check(c.wake.bones.Length==66,"66 actual wake bones");
            Check(c.wake.clip.frames.Length==451,"451 frames from restored v016 motion");
            Check(c.wake.clip.frames[0].phases[0]<.1f&&c.wake.clip.frames[450].phases.All(p=>p>.99f),"sleep-to-front head texture phases retained");
            var tongues=c.wake.bones.Where(b=>b.name.Contains("Tongue")).ToArray();
            Check(tongues.Length>=3,"independent tongue bone chain retained");
            c.Teleport("wake");float limit=Time.realtimeSinceStartup+15;
            while(c.CurrentBeat==SerpentCampaign.Beat.Awakening&&Time.realtimeSinceStartup<limit)yield return null;
            Check(c.CurrentBeat==SerpentCampaign.Beat.CrackedFloor,"curtains -> full wake -> battle");
            Check(c.fallingRocks.gameObject.activeSelf,"right entry blocked by falling rocks");
            c.enabled=false;yield return Capture("01_Bed_Awake",c.route.bedroomOverviewCenter.position,7.3f);
            foreach(var a in new[]{c.stone,c.venom,c.fang,c.timeHead}.Concat(c.stairHeads))a.enabled=false;
            Place(c.hatch.position+Vector3.right*3);c.TickWorld(10);FinishAttack(c.stone);Check(c.crackedFloor.gameObject.activeSelf,"wrong stone aim cannot open floor");c.stone.Cancel();
            Place(c.hatch.position);c.TickWorld(10);FinishAttack(c.stone);Check(!c.crackedFloor.gameObject.activeSelf,"stone impact opens cracked floor");c.TickWorld(0);yield return WaitTransition();
            Check(c.CurrentBeat==SerpentCampaign.Beat.StoneTrap,"vertical shaft lands in lower room");Check(Mathf.Abs(c.actor.transform.position.x-c.hatch.position.x)<.1f,"shaft is vertically aligned");
            float jumpStart=c.actor.transform.position.y,maxY=jumpStart;c.actor.Advance(0,.0167f,false,true);for(int j=0;j<100;j++){c.actor.Advance(0,.0167f);maxY=Mathf.Max(maxY,c.actor.transform.position.y);}Check(maxY-jumpStart>=.86f,"actual jump clears .7m stairs and .85m fang tiers");
            Place(new Vector3(c.spike.position.x,c.rooms[1].floor));c.TickWorld(10);FinishAttack(c.stone);Check(!c.StoneDefeated,"unarmed spike cannot kill stone");c.stone.Cancel();
            Place(c.spikeLever.position);Check(c.Interact(),"spike lever interaction");Place(new Vector3(c.spike.position.x,c.rooms[1].floor));c.TickWorld(10);FinishAttack(c.stone);Check(c.StoneDefeated,"armed spike kills correctly aligned stone");
            Check(c.trapStairs.activeSelf,"stone death unlocks climb route");yield return Capture("02_SpikeRoom",new Vector3(c.rooms[1].bounds.center.x,c.rooms[1].floor+4.2f,-10),5.8f);
            Place(c.pitPortal.position);Check(c.TryDepthEntry(),"upper platform W enters depth passage");yield return WaitTransition();Check(c.CurrentBeat==SerpentCampaign.Beat.FangStairs,"depth route reaches fang chamber");
            Check(Mathf.Abs(c.actor.transform.localScale.x-1)<.1f,"depth transition restores actor scale");
            var a0=c.stairHeads[0];a0.gameObject.SetActive(true);a0.Begin(a0.endPoint.position);FinishAttack(a0);a0.Step(.01f);Physics2D.SyncTransforms();Check(a0.standingSurface.enabled,"extended fang becomes standing surface");
            float top=a0.standingSurface.bounds.max.y;Place(new Vector3(a0.standingSurface.bounds.center.x,top));c.actor.Advance(0,.02f);Check(Mathf.Abs(c.actor.GroundY-top)<.05f,"walker stands on snake neck collider");
            a0.Cancel();Check(!a0.standingSurface.enabled,"retracted fang cannot leave invisible support");
            a0.gameObject.SetActive(false);var nextFang=c.stairHeads[1];nextFang.gameObject.SetActive(true);nextFang.Begin(nextFang.endPoint.position);FinishAttack(nextFang);nextFang.Step(.01f);
            Check(nextFang.head.localScale.y<0,"left-facing fang remains upright");yield return Capture("03_FangTower",new Vector3(c.rooms[2].bounds.center.x,c.rooms[2].floor+4,-10),5.9f);
            Place(c.rooms[2].depthEntry.position);Check(c.TryDepthEntry(),"high return passage accepts player");yield return WaitTransition();Check(c.CurrentBeat==SerpentCampaign.Beat.ReturnBedroom,"route loops to original bedroom");
            var liftCollider=c.rooms[3].platforms[0];Physics2D.SyncTransforms();
            Place(new Vector3(c.lift.position.x,c.rooms[3].floor));
            c.actor.Advance(0,.0167f,false,true);for(int j=0;j<100;j++)c.actor.Advance(0,.0167f);
            Check(Mathf.Abs(c.actor.GroundY-liftCollider.bounds.max.y)<.03f,"actual jump lands on bedroom lift");
            Check(c.Interact(),"bedroom lift lever reachable while standing on platform");
            float liftStart=c.actor.transform.position.y;
            for(int j=0;j<120;j++){c.TickWorld(.0167f);c.actor.Advance(0,.0167f);}
            Check(Mathf.Abs(c.actor.transform.position.y-liftStart-c.liftHeight)<.05f,"standing player rides actual lift collider to full height");
            // A low stream must miss the high chains.
            Place(new Vector3(-1.2f,c.rooms[3].floor));c.venom.Cancel();c.TickWorld(10);FinishAttack(c.venom);c.TickWorld(.2f);Check(!c.FangDefeated,"low venom cannot cut high chain");c.venom.Cancel();
            Place(new Vector3(-1.2f,c.chains.position.y-.8f));c.TickWorld(10);FinishAttack(c.venom);Check(Mathf.Abs(c.venom.startPoint.position.y-c.venom.endPoint.position.y)<.001f,"venom trajectory is horizontal");
            c.TickWorld(c.chainCorrosionSeconds+.1f);yield return new WaitForSeconds(1.4f);Check(c.FangDefeated,"corroded chains drop blade and kill fang");
            yield return Capture("04_BedroomLoop",new Vector3(0,c.rooms[3].floor+2.3f,-10),3.5f);
            Place(c.underGate.position);c.TickWorld(.01f);yield return WaitTransition();Check(c.CurrentBeat==SerpentCampaign.Beat.Arsenal,"under-gate route enters arsenal");
            Place(c.rooms[4].spawn.position);c.venom.Cancel();c.TickWorld(10);FinishAttack(c.venom);c.TickWorld(c.chainCorrosionSeconds+.1f);Check(!c.ironGate.gameObject.activeSelf,"horizontal venom corrodes iron gate");
            Place(c.cannon.position);Check(!c.Interact(),"cannot fire unloaded cannon");Place(c.cannonBall.position);Check(c.Interact(),"stone cannonball loads");Place(c.cannon.position);Check(c.Interact(),"loaded cannon fires");Check(Vector2.Distance(c.venom.head.position,c.cannonLanding.position+new Vector3(1.5f,1.4f))<.1f,"cannon shoots the visible venom head on balcony");yield return WaitTransition();Check(c.VenomDefeated,"cannon projectile defeats venom");
            Check(c.Interact(),"empty cannon accepts player after venom dies");yield return WaitTransition();Check(Vector2.Distance(c.actor.transform.position,c.cannonLanding.position)<.1f,"player cannon arc lands on balcony");
            yield return Capture("05_Arsenal",new Vector3(c.rooms[4].bounds.center.x,c.rooms[4].floor+4,-10),6.5f);
            Place(c.ladder.position);Check(c.TryLadder(),"balcony ladder begins final depth transition");yield return WaitTransition();Check(c.CurrentBeat==SerpentCampaign.Beat.TimeCourt,"ladder reaches final time room");Place(c.controller.position);Check(c.Interact(),"controller becomes bait");c.TickWorld(10);yield return WaitTransition();
            // A control attempt away from the correct depth/position must not win.
            Check(!c.TryTimeTrap(),"time trap rejects wrong position or depth");
            for(int i=0;i<4;i++)
            {
                if(i>0){Place(c.controller.position);Check(c.Interact(),"rebait controller "+i);c.TickWorld(10);yield return WaitTransition();}
                for(int k=0;k<500;k++)
                {
                    float dx=c.timeTraps[i].position.x-c.controlledHead.position.x;
                    float z=(c.controlledHead.position.y-c.rooms[5].floor-1.3f)/3;
                    c.MoveControlled(Mathf.Clamp(dx*3,-1,1),Mathf.Clamp((c.trapDepths[i]-z)*4,-1,1),.02f);
                }
                if(i==0)Check(Vector3.Distance(c.timeHead.neck.GetPosition(15),c.controlledHead.position)<.001f,"controlled time head remains connected to neck while moving");
                Check(c.TryTimeTrap(),"controlled head triggers seal "+(i+1));yield return new WaitForSeconds(.5f);
            }
            Check(c.CurrentBeat==SerpentCampaign.Beat.Victory,"all four time seals complete encounter");yield return Capture("06_TimeCourt",new Vector3(c.rooms[5].bounds.center.x,c.rooms[5].floor+4,-10),6.2f);
            c.SuspendForOriginalRoute();Check(c.route.enabled,"GM return restores original route");
            Check(c.actor.StandingSurfaces!=null,"original standing surfaces restored");
            c.RestartChapter();yield return null;Check(Mathf.Abs(c.actor.RoomFloorY-c.route.bedroomSpawn.position.y)<.01f,"restart restores bedroom floor after final underground room");
            Done();
        }
        IEnumerator Capture(string name,Vector3 position,float size)
        {
            var cam=Camera.main;var view=cam.GetComponent<Level1RoomView>();view.enabled=false;cam.transform.position=new Vector3(position.x,position.y,-10);cam.orthographicSize=size;
            yield return null;
            // Render explicitly: a hidden Windows player may skip its swap-chain backbuffer.
            var old=cam.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);
            rt.Create();cam.targetTexture=rt;cam.aspect=1.6f;cam.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();
            var pixels=tex.GetPixels32();int lit=pixels.Count(p=>p.r>12||p.g>12||p.b>12);Check(lit>pixels.Length/100,"render contains visible scene: "+name);
            File.WriteAllBytes(Folder+name+".png",tex.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(tex);view.enabled=true;
        }
        void Done()
        {
            File.WriteAllLines(Folder+"RuntimeErrors.txt",errors);File.WriteAllLines(Folder+"Events.txt",c?c.Events:new List<string>());
            int fails=checks.Count(s=>s.StartsWith("FAIL"));File.WriteAllText(Folder+"Result.txt",DateTime.Now.ToString("O")+"\nChecks="+checks.Count+" Failures="+fails+" RuntimeErrors="+errors.Count);
            Application.Quit(fails+errors.Count==0?0:1);
        }
    }
}
