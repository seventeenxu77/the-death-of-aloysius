using System.Collections.Generic;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    [DefaultExecutionOrder(100)]
    public sealed class StoneSeveringSequence : MonoBehaviour
    {
        public SerpentCampaign campaign;public StoneSlamCombat slam;public StoneCeilingBreach breach;
        public BlackGoldWakeSkin farSkin;public SeveredSerpentWhip whip;public Material bloodMaterial;
        [Header("断头掉落")]
        [Min(.1f)] public float headGravity=14;
        public Vector2 headVelocity=new Vector2(-.65f,-1);
        public float headAngularVelocity=-75;
        public float landedAngle=-85;
        [Range(0,.5f)] public float bounce=.12f;
        [Header("断颈收回与远景切换 / 秒")]
        [Min(.1f)] public float nearWithdrawSeconds=.85f;
        [Min(.1f)] public float farWithdrawSeconds=.9f;
        [Min(.05f)] public float stumpAppearSeconds=.35f;
        [SerializeField] bool severed;
        [SerializeField] float elapsed;
        Transform effects,droppedHead;Vector3 localNose,velocity;float angularVelocity,nearDistance;
        Vector3[] contacts;float tumbleAngle;
        bool landed;SerpentBloodSpray burst;
        readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
        public bool Severed=>severed;
        public void BeginSevering()
        {
            if(severed||!slam||!farSkin||!whip)return;
            severed=true;elapsed=0;landed=false;
            effects=new GameObject("Stone death | detached current skin and cut spray").transform;effects.SetParent(campaign.rooms[1].root,false);
            var cut=slam.CutWorld;droppedHead=slam.CaptureDetachedHead(effects,resources,out localNose);
            velocity=headVelocity;angularVelocity=headAngularVelocity;
            tumbleAngle=0;
            var pixels=new[]{new Vector2(405,1228),new Vector2(285,1192),new Vector2(475,1200),new Vector2(215,1070),new Vector2(560,950),new Vector2(590,800),new Vector2(610,650),new Vector2(260,650)};
            contacts=new Vector3[pixels.Length];for(int i=0;i<pixels.Length;i++)contacts[i]=droppedHead.InverseTransformPoint(slam.SourcePixelWorld(pixels[i]));
            var camera=slam.viewCamera?slam.viewCamera:Camera.main;
            nearDistance=camera?Mathf.Max(6,camera.transform.position.y+camera.orthographicSize-cut.y+2):14;
            slam.BeginSevering();if(breach)breach.BeginWithdrawal(farWithdrawSeconds);
            var bloodObject=new GameObject("Near cut blood").transform;bloodObject.SetParent(effects,false);burst=bloodObject.gameObject.AddComponent<SerpentBloodSpray>();burst.gravity=headGravity*.6f;burst.Initialize(bloodMaterial,slam.sortingOrder+2);
            for(int i=0;i<28;i++)
            {
                float a=i*2.399963f;var v=new Vector3(Mathf.Cos(a)*(1+i%4*.45f),.3f+Mathf.Sin(a)*1.4f,0);
                burst.Emit(cut+Vector3.right*((i%7-3)*.18f),v,.025f+i%3*.012f,.8f+i%5*.16f,i);
            }
        }
        void Update()
        {
            if(!severed||Level1TeleportConsole.BlocksGameplayInput)return;
            float dt=Mathf.Min(Time.deltaTime,.05f);elapsed+=dt;
            if(droppedHead&&!landed)
            {
                velocity+=Vector3.down*(headGravity*dt);droppedHead.position+=velocity*dt;
                tumbleAngle=Mathf.MoveTowards(tumbleAngle,landedAngle,Mathf.Abs(angularVelocity)*dt);droppedHead.rotation=Quaternion.Euler(0,0,tumbleAngle);
                float floor=campaign.rooms[1].floor;float bottom=droppedHead.TransformPoint(localNose).y;
                foreach(var point in contacts)bottom=Mathf.Min(bottom,droppedHead.TransformPoint(point).y);
                if(bottom<floor)
                {
                    droppedHead.position+=Vector3.up*(floor-bottom);velocity.x*=.7f;if(velocity.y<0)velocity.y=-velocity.y*bounce;
                    if(velocity.y<.65f&&Mathf.Abs(tumbleAngle-landedAngle)<1){landed=true;velocity=Vector3.zero;}
                }
            }
            if(elapsed<nearWithdrawSeconds)slam.WithdrawSeveredNeck(nearDistance*Mathf.SmoothStep(0,1,elapsed/nearWithdrawSeconds));
            else slam.HideSeveredNeck();
            if(elapsed>=farWithdrawSeconds)
            {
                farSkin.SetPartVisible("Stone",false);
                whip.Sample(elapsed-farWithdrawSeconds,Mathf.SmoothStep(0,1,(elapsed-farWithdrawSeconds)/stumpAppearSeconds));
            }
        }
        public void ResetSequence()
        {
            severed=false;elapsed=0;if(effects)Destroy(effects.gameObject);effects=null;droppedHead=null;
            foreach(var r in resources)if(r)Destroy(r);resources.Clear();
            if(whip)whip.Hide();if(farSkin)farSkin.SetPartVisible("Stone",true);if(slam)slam.Cancel();
        }
        void OnDestroy(){foreach(var r in resources)if(r)Destroy(r);}
    }
}
