using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aloysius.Level1.Boss
{
    // Weighted black-gold skeletal clip, retargeted to the live camera and a locked floor contact.
    public sealed class StoneSlamCombat : MonoBehaviour
    {
        public enum Stage { Idle, Enter, Tracking, Locked, Strike, Impact, Recover, Exit, Defeated, Hanging }
        [Serializable] public class SkinData
        {
            public string name;
            public float[] vertices,uv,weights;
            public int[] joints,indices;
            [NonSerialized] public Mesh mesh;
            [NonSerialized] public Vector3[] output;
        }
        [Serializable] public class RigData { public int fps,frameCount; public float[] rest,poses; public SkinData[] meshes; }
        [Header("黑金剪影骨骼与睁闭眼素材")]
        public TextAsset rigAsset;
        public Material skinMaterial,ringMaterial;
        public SerpentAttack attack;
        public Camera viewCamera;
        [Header("探头与瞄准")]
        [Min(.1f)] public float enterSeconds=.65f;
        [Min(.1f)] public float trackingSeconds=1.72f;
        [Min(.1f)] public float trackingResponse=15;
        [Min(.5f)] public float headWidth=3;
        [Min(.1f),Tooltip("鼻尖进入屏幕上边缘下方的距离")]
        public float visibleDepth=3.8f;
        [Header("蓄力锁定；之后不再追踪")]
        [Min(.1f)] public float warningSeconds=.56f;
        [Min(.05f)] public float strikeSeconds=.22f;
        [Min(.1f),Tooltip("砸地后的总停留时间；末尾保留原来的回弹，再抬头")]
        public float impactSeconds=4f;
        [Min(.1f)] public float recoverSeconds=1.6f;
        [Min(.1f)] public float exitSeconds=.55f;
        [Header("攻击间歇悬挂")]
        [Tooltip("恢复后停在屏幕上方，等待下一次瞄准；取消或离开场景仍正常隐藏")]
        public bool hangBetweenAttacks=true;
        [Min(.1f),Tooltip("悬挂时鼻尖位于屏幕上边缘下方的距离")]
        public float hangingVisibleDepth=3.8f;
        [Min(.1f)] public float headHitRadius=1.05f;
        [Min(.01f)] public float ringWidth=.075f;
        public Color warningColor=new Color(1,.035f,.09f,1);
        [Header("可选特效（本版默认关闭）")]
        public bool showWarningRing=false;
        public bool showImpactPulse=false;
        public int sortingOrder=4;
        [Header("四菱形颈部弱点（原贴图像素坐标）")]
        public Vector2 neckMarkPixels=new Vector2(586,466),neckMarkSizePixels=new Vector2(118,156);
        public Vector2 severCutPixels=new Vector2(440,640);
        [Tooltip("仅在尖刺室落地时使颈部骨骼弯压，让图上的标记落到发射线高度")]
        public Transform markHeightAnchor;
        [Min(0),Tooltip("颈部对齐位移上限，原图坐标单位；0 为不截断，保证标记与刺尖同高")]
        public float maxNeckCompression=0;
        [Header("运行状态（落点锁定后保持不变）")]
        [SerializeField] Stage stage;
        [SerializeField] Vector3 lockedContact;
        [SerializeField] float clock;
        public Stage CurrentStage=>stage;
        public bool Busy=>stage!=Stage.Idle&&stage!=Stage.Defeated&&stage!=Stage.Hanging;
        public bool Dangerous=>stage==Stage.Strike||(stage==Stage.Impact&&clock<.12f);
        public bool TargetLocked=>stage>=Stage.Locked&&stage<=Stage.Exit;
        public Vector3 LockedContact=>lockedContact;
        public SerpentAttack.Phase LegacyPhase=>stage==Stage.Strike?SerpentAttack.Phase.Strike:stage==Stage.Impact?SerpentAttack.Phase.Hold:stage==Stage.Locked?SerpentAttack.Phase.Shake:stage==Stage.Recover||stage==Stage.Exit?SerpentAttack.Phase.Recover:Busy?SerpentAttack.Phase.Windup:SerpentAttack.Phase.Idle;
        RigData rig;
        Transform generated,target,spike;
        Collider2D[] floors;
        Transform[] boneObjects;
        Vector2[] bonePositions;
        float[] angles;
        LineRenderer ring,dustRing;
        Material skin,ringMat,dustMat;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        float baseX,baseY,enterY,floorY,lastClip;
        Vector3 previousNose,nose;
        bool impactSent;
        BoxCollider2D markCollider;Bounds markBounds,previousMarkBounds;
        Vector3 severedNeckStart;
        float markRigidBlend;
        public Vector3 MarkWorld=>markBounds.center;
        public Vector3 CutWorld=>World(SourcePoint(severCutPixels));
        public Vector3 NoseWorld=>nose;
        public Vector3 SourcePixelWorld(Vector2 pixel)=>World(SourcePoint(pixel));
        const float ContactX=674,RestNoseY=694.16f,SourceImpactY=1040;
        float Scale=>headWidth/220f;
        float Top=>viewCamera?viewCamera.transform.position.y+viewCamera.orthographicSize:floorY+10;
        float PeekY=>Mathf.Max(floorY+headWidth*1.6f,Top-visibleDepth);
        float HangingY=>Mathf.Max(floorY+headWidth*1.6f,Top-hangingVisibleDepth);
        public void BeginTracking(Transform follow,float ground,Collider2D[] surfaces,Transform armedSpike)
        {
            if(Busy)return;
            Ensure();if(rig==null)return;
            target=follow;floorY=ground;floors=surfaces;spike=armedSpike;
            if(!viewCamera)viewCamera=Camera.main;
            bool wasHanging=stage==Stage.Hanging&&generated.gameObject.activeSelf;
            if(!wasHanging)baseX=target?target.position.x:attack.startPoint.position.x;
            baseY=enterY=wasHanging?baseY:Top+headWidth*1.4f;clock=0;stage=Stage.Enter;impactSent=false;
            generated.gameObject.SetActive(true);ring.enabled=false;dustRing.enabled=false;
            skin.SetFloat("_CutMode",0);generated.localPosition=Vector3.zero;
            Sample(0);previousNose=nose;previousMarkBounds=markBounds;
        }
        public void Cancel()
        {
            stage=Stage.Idle;clock=0;impactSent=false;target=null;
            if(generated)generated.gameObject.SetActive(false);
            if(generated)generated.localPosition=Vector3.zero;if(skin)skin.SetFloat("_CutMode",0);if(markCollider)markCollider.enabled=false;
            if(ring)ring.enabled=false;if(dustRing)dustRing.enabled=false;
        }
        void OnDisable()=>Cancel();
        void OnDestroy()
        {
            if(generated)Destroy(generated.gameObject);
            foreach(var o in owned)if(o)Destroy(o);
        }
        void Track(float dt)
        {
            if(target)baseX=Mathf.Lerp(baseX,target.position.x,1-Mathf.Exp(-trackingResponse*dt));
        }
        public void Step(float dt)
        {
            if(Level1TeleportConsole.BlocksGameplayInput)return;
            if(stage==Stage.Hanging)
            {
                // Idle is visible but not busy: the campaign cooldown can advance.
                baseY=HangingY;Sample(0);return;
            }
            if(!Busy)return;
            dt=Mathf.Max(0,dt);clock+=dt;previousNose=nose;previousMarkBounds=markBounds;
            switch(stage)
            {
                case Stage.Enter:
                    Track(dt);baseY=Mathf.Lerp(enterY,PeekY,Mathf.SmoothStep(0,1,clock/enterSeconds));Sample(0);
                    if(clock>=enterSeconds)Change(Stage.Tracking);break;
                case Stage.Tracking:
                    Track(dt);baseY=PeekY;Sample(Mathf.Clamp01(clock/trackingSeconds)*1.72f);
                    if(clock>=trackingSeconds){LockContact();Change(Stage.Locked);Sample(1.72f);}break;
                case Stage.Locked:
                    Sample(Mathf.Lerp(1.72f,2.28f,Mathf.Clamp01(clock/warningSeconds)));
                    if(clock>=warningSeconds){Change(Stage.Strike);ring.enabled=false;}break;
                case Stage.Strike:
                    Sample(Mathf.Lerp(2.28f,2.5f,Mathf.Clamp01(clock/strikeSeconds)));
                    if(clock>=strikeSeconds)
                    {
                        Change(Stage.Impact);Sample(2.5f);
                        if(!impactSent){impactSent=true;attack.NotifySkeletalImpact();}
                    }
                    break;
                case Stage.Impact:
                    // Keep the nose planted for the extra time, rather than stretching
                    // the recoil over the entire hold and making the head drift upward.
                    float recoilDuration=Mathf.Min(.85f,impactSeconds);
                    Sample(Mathf.Lerp(2.5f,3.35f,Mathf.Clamp01((clock-impactSeconds+recoilDuration)/recoilDuration)));
                    if(clock>=impactSeconds)Change(Stage.Recover);break;
                case Stage.Recover:
                    Sample(Mathf.Lerp(3.35f,4.95f,Mathf.Clamp01(clock/recoverSeconds)));
                    if(clock>=recoverSeconds){enterY=baseY;Change(Stage.Exit);}break;
                case Stage.Exit:
                    baseY=Mathf.Lerp(enterY,hangBetweenAttacks?HangingY:Top+headWidth*1.4f,Mathf.SmoothStep(0,1,clock/exitSeconds));Sample(4.95f);
                    if(clock>=exitSeconds)
                    {
                        if(hangBetweenAttacks){Change(Stage.Hanging);baseY=HangingY;Sample(0);}
                        else Cancel();
                    }
                    break;
            }
        }
        void Change(Stage next){stage=next;clock=0;}
        void LockContact()
        {
            float y=floorY;
            if(floors!=null)foreach(var c in floors)
            {
                if(!c||!c.enabled||!c.gameObject.activeInHierarchy||c.isTrigger)continue;
                var b=c.bounds;
                if(baseX>=b.min.x&&baseX<=b.max.x&&b.max.y>=y&&b.max.y<baseY-headWidth)y=b.max.y;
            }
            if(spike&&Mathf.Abs(baseX-spike.position.x)<1.1f)y=Mathf.Max(y,spike.position.y+1.4f);
            lockedContact=new Vector3(baseX,y,0);
            if(attack.endPoint)attack.endPoint.position=lockedContact;
        }
        public bool Hits(Vector3 feet)
        {
            if(!Dangerous)return false;
            // Swept head volume includes the final strike sample, so the fast stroke cannot tunnel.
            Vector2 a=previousNose+Vector3.up*headHitRadius,b=nose+Vector3.up*headHitRadius,p=feet+Vector3.up*.8f;
            Vector2 ab=b-a;float f=ab.sqrMagnitude>1e-7f?Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude):0;
            return Vector2.Distance(p,a+ab*f)<=headHitRadius+.25f;
        }
        public bool IntersectsTrap(Bounds area)
        {
            if(rig==null||!Busy||stage<Stage.Strike||stage>Stage.Impact)return false;
            // Only the visible four-diamond patch can kill it, including its swept descent.
            if(area.Intersects(markBounds)||area.Intersects(previousMarkBounds))return true;
            area.Expand(Vector3.Max(markBounds.size,previousMarkBounds.size));
            Vector3 delta=markBounds.center-previousMarkBounds.center;float length=delta.magnitude;
            return length>.0001f&&area.IntersectRay(new Ray(previousMarkBounds.center,delta/length),out float distance)&&distance<=length;
        }
        static readonly float[] SourceJointY={0,160,320,480,650,820};
        Vector2 SourcePoint(Vector2 pixel)
        {
            var v=new Vector2(800+(pixel.x-580)*.72f,-190+pixel.y*.72f);
            if(pixel.y>=820)return SkinPoint(bonePositions.Length-1,v.x,v.y);
            int k=0;while(k<4&&pixel.y>SourceJointY[k+1])k++;
            float w=Mathf.SmoothStep(0,1,(pixel.y-SourceJointY[k])/(SourceJointY[k+1]-SourceJointY[k]));
            return Vector2.LerpUnclamped(SkinPoint(k+1,v.x,v.y),SkinPoint(k+2,v.x,v.y),w);
        }
        void UpdateMark()
        {
            var center=World(SourcePoint(neckMarkPixels));markBounds=new Bounds(center,Vector3.zero);
            for(int i=0;i<4;i++)markBounds.Encapsulate(VisualSourcePoint(neckMarkPixels+Vector2.Scale(neckMarkSizePixels,new Vector2((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f))));
            markBounds.Expand(new Vector3(0,0,.1f));
            if(markCollider){markCollider.transform.position=center;markCollider.size=markBounds.size;markCollider.enabled=stage==Stage.Strike||stage==Stage.Impact;}
        }
        float MarkRigidWeight(Vector2 pixel)
        {
            var d=pixel-neckMarkPixels;
            return markRigidBlend*(1-Mathf.SmoothStep(0,1,(Mathf.Abs(d.x)-neckMarkSizePixels.x*.5f)/35))*(1-Mathf.SmoothStep(0,1,(Mathf.Abs(d.y)-neckMarkSizePixels.y*.5f)/45));
        }
        Vector3 VisualSourcePoint(Vector2 pixel)
        {
            var original=World(SourcePoint(pixel));float weight=MarkRigidWeight(pixel);if(weight<=0)return original;
            var delta=pixel-neckMarkPixels;float a=angles[Mathf.Min(4,angles.Length-1)],c=Mathf.Cos(a),s=Mathf.Sin(a);
            var rigid=World(SourcePoint(neckMarkPixels))+new Vector3(c*delta.x-s*delta.y,-s*delta.x-c*delta.y,0)*(.72f*Scale);
            return Vector3.LerpUnclamped(original,rigid,weight);
        }
        public Transform CaptureDetachedHead(Transform parent,List<UnityEngine.Object> resources,out Vector3 localNose)
        {
            var root=new GameObject("Detached Stone head | exact current attack skin").transform;root.SetParent(parent,false);root.position=Vector3.Lerp(CutWorld,nose,.55f);
            var cutMaterial=new Material(skin);cutMaterial.SetFloat("_CutMode",2);cutMaterial.SetFloat("_CutSourceY",severCutPixels.y);cutMaterial.SetFloat("_ClosedEye",1);resources.Add(cutMaterial);
            foreach(var m in rig.meshes)
            {
                var vertices=new Vector3[m.output.Length];for(int i=0;i<vertices.Length;i++)vertices[i]=root.InverseTransformPoint(generated.TransformPoint(m.output[i]));
                var copy=new Mesh{name="Detached "+m.name};copy.vertices=vertices;copy.uv=m.mesh.uv;copy.triangles=m.indices;copy.RecalculateBounds();resources.Add(copy);
                var part=new GameObject(m.name).transform;part.SetParent(root,false);part.gameObject.AddComponent<MeshFilter>().sharedMesh=copy;
                var r=part.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=cutMaterial;r.sortingOrder=sortingOrder+1;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            localNose=root.InverseTransformPoint(nose);return root;
        }
        public void BeginSevering()
        {
            stage=Stage.Defeated;target=null;severedNeckStart=generated.position;
            skin.SetFloat("_CutMode",1);skin.SetFloat("_CutSourceY",severCutPixels.y);if(markCollider)markCollider.enabled=false;ring.enabled=dustRing.enabled=false;
        }
        public void WithdrawSeveredNeck(float distance){if(generated)generated.position=severedNeckStart+Vector3.up*distance;}
        public void HideSeveredNeck(){if(generated)generated.gameObject.SetActive(false);}
        public void FreezeDefeated(Vector3 contact)
        {
            Ensure();stage=Stage.Defeated;lockedContact=contact;baseX=contact.x;generated.gameObject.SetActive(true);Sample(2.5f);
            ring.enabled=dustRing.enabled=false;
        }
        Transform Child(string label)
        {var t=new GameObject(label).transform;t.SetParent(transform,false);return t;}
        void Ensure()
        {
            if(rig!=null||!rigAsset||!skinMaterial)return;
            rig=JsonUtility.FromJson<RigData>(rigAsset.text);generated=Child("Black gold weighted stone attack");
            skin=new Material(skinMaterial);owned.Add(skin);int count=rig.rest.Length/2;
            bonePositions=new Vector2[count];angles=new float[count];boneObjects=new Transform[count];
            for(int i=0;i<count;i++){boneObjects[i]=new GameObject(i==count-1?"Skull bone":"Neck bone "+i).transform;boneObjects[i].SetParent(generated,false);}
            foreach(var m in rig.meshes)
            {
                var t=new GameObject(m.name).transform;t.SetParent(generated,false);m.mesh=new Mesh{name=m.name};m.mesh.MarkDynamic();owned.Add(m.mesh);
                m.output=new Vector3[m.vertices.Length/2];var uv=new Vector2[m.output.Length];
                for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(m.uv[i*2],1-m.uv[i*2+1]);
                m.mesh.vertices=m.output;m.mesh.uv=uv;m.mesh.triangles=m.indices;
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=m.mesh;var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=skin;renderer.sortingOrder=sortingOrder;
            }
            ringMat=new Material(ringMaterial);owned.Add(ringMat);ring=MakeRing("Head warning circle",ringMat,sortingOrder+2);
            dustMat=new Material(ringMaterial);owned.Add(dustMat);dustRing=MakeRing("Ground impact pulse",dustMat,sortingOrder+1);
            var marker=Child("Four-diamond weak point | follows visible mark");markCollider=marker.gameObject.AddComponent<BoxCollider2D>();markCollider.isTrigger=true;markCollider.enabled=false;
            generated.gameObject.SetActive(false);
        }
        LineRenderer MakeRing(string label,Material material,int order)
        {
            var t=new GameObject(label).transform;t.SetParent(generated,false);var line=t.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.useWorldSpace=true;line.loop=true;line.positionCount=64;line.sortingOrder=order;line.numCornerVertices=3;line.enabled=false;return line;
        }
        Vector2 SkinPoint(int joint,float x,float y)
        {
            float c=Mathf.Cos(angles[joint]),s=Mathf.Sin(angles[joint]);x-=rig.rest[joint*2];y-=rig.rest[joint*2+1];
            return bonePositions[joint]+new Vector2(c*x-s*y,s*x+c*y);
        }
        Vector3 World(Vector2 point)=>new Vector3(baseX+(point.x-ContactX)*Scale,baseY-(point.y-RestNoseY)*Scale,0);
        public void Sample(float clipTime)
        {
            if(rig==null)return;lastClip=clipTime;int count=bonePositions.Length;
            markRigidBlend=0;
            float f=Mathf.Clamp(clipTime*rig.fps,0,rig.frameCount-1);int a=(int)f,b=Mathf.Min(a+1,rig.frameCount-1);float u=f-a;
            for(int k=0;k<count;k++)
            {
                int ia=(a*count+k)*3,ib=(b*count+k)*3;
                bonePositions[k]=new Vector2(Mathf.Lerp(rig.poses[ia],rig.poses[ib],u),Mathf.Lerp(rig.poses[ia+1],rig.poses[ib+1],u));
                angles[k]=Mathf.LerpAngle(rig.poses[ia+2]*Mathf.Rad2Deg,rig.poses[ib+2]*Mathf.Rad2Deg,u)*Mathf.Deg2Rad;
            }
            var sourceNose=SkinPoint(count-1,ContactX,RestNoseY);
            // Extend only the neck to the room floor; the skull keeps its approved proportions.
            float reach=Mathf.Clamp01((sourceNose.y-RestNoseY)/(SourceImpactY-RestNoseY));
            if(TargetLocked||stage==Stage.Defeated)
            {
                float expectedFloor=baseY-(SourceImpactY-RestNoseY)*Scale;
                float correction=-(lockedContact.y-expectedFloor)/Scale*reach;
                for(int k=1;k<count;k++)bonePositions[k].y+=correction*Mathf.Pow((k-1)/(float)(count-2),1.25f);
            }
            if(markHeightAnchor&&reach>0&&TargetLocked)
            {
                float alignment=stage==Stage.Impact?1:stage==Stage.Recover?1-Mathf.SmoothStep(0,1,clock/recoverSeconds):reach;
                float correction=(World(SourcePoint(neckMarkPixels)).y-markHeightAnchor.position.y)/Scale;
                if(maxNeckCompression>0)correction=Mathf.Clamp(correction,-maxNeckCompression,maxNeckCompression);
                correction*=alignment;
                if(count==7)
                {
                    // The mark is between joints 3 and 4, so moving both by the same
                    // amount solves its exact world height. Preserve the skull/floor
                    // contact and fit joint 5 between the compressed neck and skull,
                    // rather than capping the solve and leaving the mark too high.
                    float lowerFraction=Mathf.InverseLerp(bonePositions[4].y,bonePositions[6].y,bonePositions[5].y);
                    bonePositions[2].y+=correction*.28f;
                    bonePositions[3].y+=correction;bonePositions[4].y+=correction;
                    float fitted=Mathf.Lerp(bonePositions[4].y,bonePositions[6].y,lowerFraction);
                    bonePositions[5].y=Mathf.Lerp(bonePositions[5].y,fitted,alignment);
                }
                markRigidBlend=alignment;
            }
            for(int k=0;k<count;k++){boneObjects[k].position=World(bonePositions[k]);boneObjects[k].rotation=Quaternion.Euler(0,0,-angles[k]*Mathf.Rad2Deg);}
            foreach(var m in rig.meshes)
            {
                for(int i=0;i<m.output.Length;i++)
                {
                    float x=m.vertices[i*2],y=m.vertices[i*2+1];var p=SkinPoint(m.joints[i*2],x,y)*m.weights[i*2]+SkinPoint(m.joints[i*2+1],x,y)*m.weights[i*2+1];
                    var world=World(p);
                    if(markRigidBlend>0)
                    {
                        var pixel=new Vector2(m.uv[i*2]*1024,m.uv[i*2+1]*1536);float weight=MarkRigidWeight(pixel);
                        if(weight>0)world=VisualSourcePoint(pixel);
                    }
                    m.output[i]=generated.InverseTransformPoint(world);
                }
                m.mesh.vertices=m.output;m.mesh.RecalculateBounds();
            }
            nose=World(SkinPoint(count-1,ContactX,RestNoseY));if(attack.head)attack.head.position=nose;
            UpdateMark();
            bool closed=(clipTime>.13f&&clipTime<.25f)||(clipTime>1.08f&&clipTime<1.42f)||(clipTime>=2.5f&&clipTime<2.72f);
            skin.SetFloat("_ClosedEye",closed?1:0);
            ring.enabled=showWarningRing&&stage==Stage.Locked;
            if(ring.enabled)
            {
                float pulse=.5f+.5f*Mathf.Sin(clock*Mathf.PI*8),radius=118*(1+.025f*pulse);
                ring.startWidth=ring.endWidth=ringWidth;ringMat.SetColor("_Color",warningColor);
                for(int i=0;i<64;i++){float theta=i*Mathf.PI*2/64;ring.SetPosition(i,World(SkinPoint(count-1,ContactX+Mathf.Cos(theta)*radius,-190+1105*.72f+Mathf.Sin(theta)*radius*.94f)));}
            }
            dustRing.enabled=showImpactPulse&&stage==Stage.Impact&&clock<.4f;
            if(dustRing.enabled)
            {
                dustMat.SetColor("_Color",new Color(.65f,.33f,.12f,Mathf.Clamp01(1-clock/.4f)));
                dustRing.startWidth=dustRing.endWidth=.05f;
                for(int i=0;i<64;i++){float theta=i*Mathf.PI*2/64;dustRing.SetPosition(i,lockedContact+new Vector3(Mathf.Cos(theta)*(.25f+clock*8),Mathf.Sin(theta)*(.06f+clock*.3f)+.03f,0));}
            }
        }
    }
}
