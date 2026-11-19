using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Aloysius.GothicStage;
namespace Aloysius.Level1.Boss
{
    // Presentation and opt-in review for the first redesigned room only.
    public sealed class SpikeChamberPresentation:MonoBehaviour
    {
        public SerpentCampaign campaign;
        public Transform cameraFrame,stairBottom,stairTop;
        public SpriteRenderer farSerpent;
        public GothicWalkSurface staircase;
        public Vector2 dimensions;
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();bool review=Array.IndexOf(args,"-verifyspikeroom")>=0;
            if(!review&&Array.IndexOf(args,"-spikeroom")<0)yield break;
            yield return null;yield return null;campaign.Teleport("spike");
            if(review)yield return Review();
        }
        IEnumerator Review()
        {
            const string folder="D:/the death of aloysius/the death of aloysius/ArtSource/SpikeChamber_v002/";
            var checks=new List<string>();campaign.enabled=false;campaign.invulnerable=true;var room=campaign.rooms[1];
            Action<bool,string> check=(ok,name)=>checks.Add((ok?"PASS ":"FAIL ")+name);
            check(campaign.trapStairs.activeInHierarchy,"continuous staircase visible before defeating stone");
            check(campaign.pitPortal.position.x>room.bounds.xMax-1,"door is against right boundary");
            check(campaign.spike.position.x<stairTop.position.x,"spike is left of staircase landing");
            check(Mathf.Abs(campaign.pitPortal.position.y-stairTop.position.y)<.12f,"door threshold aligns with upper walking floor");
            var cam=Camera.main;var view=cam.GetComponent<Level1RoomView>();view.enabled=false;
            cam.transform.position=new Vector3(cameraFrame.position.x,cameraFrame.position.y,-10);cam.orthographicSize=dimensions.y*.5f;cam.aspect=dimensions.x/dimensions.y;
            yield return null;Capture(cam,folder+"RoomOverview.png");
            campaign.actor.RelocateOnRoomFloor(stairBottom.position,true);Physics2D.SyncTransforms();
            float x=stairBottom.position.x;
            for(int i=0;i<1400&&campaign.actor.transform.position.x<stairTop.position.x-.03f;i++)campaign.actor.Advance(1,.016667f);
            check(campaign.actor.transform.position.x>stairTop.position.x-.12f,"normal walk reaches stair top without teleport or jumping");
            check(Mathf.Abs(campaign.actor.transform.position.y-stairTop.position.y)<.15f,"stair support reaches exact gallery height");
            Capture(cam,folder+"StairTop.png");
            for(int i=0;i<1200&&campaign.actor.transform.position.x<campaign.pitPortal.position.x-.1f;i++)campaign.actor.Advance(1,.016667f);
            check(Vector2.Distance(campaign.actor.transform.position,campaign.pitPortal.position)<.3f,"upper landing connects continuously to right-hand door");
            Capture(cam,folder+"RightDoor.png");
            campaign.actor.RelocateOnRoomFloor(campaign.spikeLever.position,true);check(campaign.Interact(),"switch reachable on ground floor");
            check(!campaign.TryDepthEntry(),"lower floor cannot trigger upper door");
            File.WriteAllLines(folder+"Checks.txt",checks);File.WriteAllText(folder+"Result.txt",DateTime.Now.ToString("O")+"\n"+string.Join("\n",checks));
            Application.Quit(checks.Exists(s=>s.StartsWith("FAIL"))?1:0);
        }
        static void Capture(Camera cam,string file)
        {
            var old=cam.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1672,941,24);rt.Create();cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var t=new Texture2D(1672,941,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1672,941),0,0);t.Apply();File.WriteAllBytes(file,t.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(t);
        }
    }
}
