using UnityEngine;

namespace Aloysius.GothicStage
{
    /// <summary>Prone locomotion and hand-constrained ledge mantle on the existing weighted rig.</summary>
    internal sealed class GothicBrideTraversalMotion
    {
        readonly Transform actor;
        readonly Transform[] bones;
        readonly Vector3[] entryPositions;
        readonly Quaternion[] entryRotations;
        readonly Vector3[] entryWorld;
        readonly Quaternion[] entryWorldRotations;
        public GothicBrideTraversalMotion(Transform actor,Transform[] bones)
        {
            this.actor=actor;this.bones=bones;
            entryPositions=new Vector3[bones.Length];entryRotations=new Quaternion[bones.Length];entryWorld=new Vector3[bones.Length];entryWorldRotations=new Quaternion[bones.Length];Capture();
        }
        Transform B(string n){foreach(var b in bones)if(b.name==n)return b;return null;}
        static float S(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        public void Capture(){for(int i=0;i<bones.Length;i++){entryPositions[i]=bones[i].localPosition;entryRotations[i]=bones[i].localRotation;entryWorld[i]=bones[i].position;entryWorldRotations[i]=bones[i].rotation;}}
        int Index(string name){for(int i=0;i<bones.Length;i++)if(bones[i].name==name)return i;return 0;}
        public void Blend(float weight)
        {
            for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=Vector3.Lerp(entryPositions[i],bones[i].localPosition,weight);
                bones[i].localRotation=Quaternion.Slerp(entryRotations[i],bones[i].localRotation,weight);
            }
        }
        static void Solve(Transform a,Transform b,Transform c,Vector3 target,float bend)
        {
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
            Vector2 delta=target-a.position;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
            Vector2 end=(Vector2)a.position+delta.normalized*d;
            float line=Mathf.Atan2(delta.y,delta.x),angle=Mathf.Acos(Mathf.Clamp((l1*l1+d*d-l2*l2)/(2*l1*d),-1,1));
            a.rotation=Quaternion.Euler(0,0,(line+bend*angle-Mathf.Atan2(b.localPosition.y,b.localPosition.x))*Mathf.Rad2Deg);
            Vector2 lower=end-(Vector2)b.position;
            b.rotation=Quaternion.Euler(0,0,(Mathf.Atan2(lower.y,lower.x)-Mathf.Atan2(c.localPosition.y,c.localPosition.x))*Mathf.Rad2Deg);
        }
        void Limb(string side,bool arm,Vector3 target,float bend)
        {Solve(B(side+(arm?"_upperarm":"_thigh")),B(side+(arm?"_forearm":"_shin")),B(side+(arm?"_hand":"_foot")),target,bend);}
        public void LeverGrip(Vector3 wrist,float blend)
        {
            if(blend<=.0001f)return;
            var a=B("near_upperarm");var b=B("near_forearm");var c=B("near_hand");if(!a||!b||!c)return;
            var startA=a.localRotation;var startB=b.localRotation;var hand=c.rotation;
            Solve(a,b,c,wrist,-1);var endA=a.localRotation;var endB=b.localRotation;
            a.localRotation=Quaternion.Slerp(startA,endA,blend);b.localRotation=Quaternion.Slerp(startB,endB,blend);c.rotation=hand;
        }
        void GroundLimb(string side,bool arm,Vector3 target,float bend,float floor)
        {
            var a=B(side+(arm?"_upperarm":"_thigh"));var b=B(side+(arm?"_forearm":"_shin"));var c=B(side+(arm?"_hand":"_foot"));
            Solve(a,b,c,target,bend);
            // A knee/elbow that would intersect the floor abducts to the side.
            // This is skeletal foreshortening of the existing flat meshes, preserving
            // bone lengths instead of folding the knee upward through the torso.
            if(b.position.y>=floor+.025f)return;
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
            Vector3 delta=target-a.position;delta.z=0;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
            Vector3 dir=delta.normalized,end=a.position+dir*d;
            float along=(l1*l1+d*d-l2*l2)/(2*d),radius=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            Vector3 center=a.position+dir*along,perp=new Vector3(-dir.y,dir.x,0);
            if(Mathf.Abs(perp.y)<.001f)return;
            float lateral=Mathf.Clamp((floor+.025f-center.y)/perp.y,-radius,radius);
            float depth=Mathf.Sqrt(Mathf.Max(0,radius*radius-lateral*lateral))*(side=="near"?-1:1);
            Vector3 joint=center+perp*lateral+Vector3.forward*depth;
            a.rotation=Quaternion.FromToRotation(b.localPosition,joint-a.position);
            b.rotation=Quaternion.FromToRotation(c.localPosition,end-b.position);
        }
        public void GroundTransition(float surfaceY,float progress,float entryBlend)
        {
            float t=Mathf.Clamp01(progress);
            Vector2 hip;float pelvisAngle,totalTorso;
            if(t<.30f)
            {float p=S(t/.30f);hip=Vector2.Lerp(new Vector2(0,.8382353f),new Vector2(-.12f,.23f),p);pelvisAngle=0;totalTorso=Mathf.Lerp(0,-78,p);}
            else if(t<.54f)
            {float p=S((t-.30f)/.24f);hip=Vector2.Lerp(new Vector2(-.12f,.23f),new Vector2(-.16f,.34f),p);pelvisAngle=Mathf.Lerp(0,-55,p);totalTorso=Mathf.Lerp(-78,-86,p);}
            else if(t<.75f)
            {float p=S((t-.54f)/.21f);hip=Vector2.Lerp(new Vector2(-.16f,.34f),new Vector2(-.14f,.38f),p);pelvisAngle=Mathf.Lerp(-55,-85,p);totalTorso=Mathf.Lerp(-86,-90,p);}
            else
            {float p=S((t-.75f)/.13f);hip=Vector2.Lerp(new Vector2(-.14f,.38f),new Vector2(-.05f,.18f),p);pelvisAngle=Mathf.Lerp(-85,-90,p);totalTorso=-90;}
            B("pelvis").position=new Vector3(actor.position.x+hip.x,surfaceY+hip.y,actor.position.z);
            B("pelvis").rotation=Quaternion.Euler(0,0,pelvisAngle);
            B("torso").localRotation=Quaternion.Euler(0,0,totalTorso-pelvisAngle);
            B("neck").localRotation=Quaternion.Euler(0,0,Mathf.Lerp(0,70,S(t/.6f)));
            for(int i=0;i<2;i++)
            {
                string side=i==0?"near":"far";
                float footStep=S((t-(i==0?.43f:.58f))/(i==0?.20f:.17f));
                float restX=i==0?-.0117647f:.0117647f,backX=i==0?-.80f:-.84f;
                Vector3 ankle=new Vector3(actor.position.x+Mathf.Lerp(restX,backX,footStep),surfaceY+.0735294f+Mathf.Sin(footStep*Mathf.PI)*.10f,actor.position.z);
                float settle=S((t-.76f)/.12f);
                ankle=Vector3.Lerp(ankle,new Vector3(actor.position.x+(i==0?-.78f:-.80f),surfaceY+.055f,actor.position.z),settle);
                GroundLimb(side,false,ankle,1,surfaceY);B(side+"_foot").rotation=Quaternion.Euler(0,0,-135*footStep);
                Vector3 start=new Vector3(actor.position.x+(i==0?.0518f:.1076f),surfaceY+(i==0?.8088f:.8176f),actor.position.z);
                Vector3 plant=new Vector3(actor.position.x+(i==0?.29f:.34f),surfaceY+.075f,actor.position.z);
                Vector3 hand=Vector3.Lerp(start,plant,S(t/.30f));
                float reach=S((t-(i==0?.88f:.93f))/(i==0?.10f:.07f));
                hand=Vector3.Lerp(hand,new Vector3(actor.position.x+(i==0?.76f:.80f),surfaceY+.055f,actor.position.z),reach);
                GroundLimb(side,true,hand,-1,surfaceY);B(side+"_hand").rotation=Quaternion.Euler(0,0,Mathf.Lerp(0,65,S(t/.3f)));
            }
            Cloth(surfaceY,S((t-.22f)/.66f),0);Blend(entryBlend);KeepFeetAbove(surfaceY);
        }
        void ReleaseArm(string side,Vector3 target,float release,float floor)
        {
            var a=B(side+"_upperarm");var b=B(side+"_forearm");var c=B(side+"_hand");
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
            Vector3 delta=target-a.position;delta.z=0;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
            Vector3 dir=delta.normalized,end=a.position+dir*d,perp=new Vector3(-dir.y,dir.x,0);
            float along=(l1*l1+d*d-l2*l2)/(2*d),radius=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            Vector3 center=a.position+dir*along;float lateral=radius*Mathf.Cos(release*Mathf.PI);
            float depth=radius*Mathf.Sin(release*Mathf.PI)*(side=="near"?-1:1);
            if(center.y+perp.y*lateral<floor+.025f&&Mathf.Abs(perp.y)>.001f)
            {lateral=Mathf.Clamp((floor+.025f-center.y)/perp.y,-radius,radius);depth=Mathf.Sqrt(Mathf.Max(0,radius*radius-lateral*lateral))*(side=="near"?-1:1);}
            Vector3 elbow=center+perp*lateral+Vector3.forward*depth;
            a.rotation=Quaternion.FromToRotation(b.localPosition,elbow-a.position);b.rotation=Quaternion.FromToRotation(c.localPosition,end-b.position);
            c.rotation=Quaternion.Euler(0,0,Mathf.Lerp(-8,65,release));
        }
        public void Crawl(float surfaceY,float phase,float movement,float weight)
        {
            float bob=.008f*Mathf.Sin(phase*Mathf.PI*4)*movement;
            B("pelvis").position=new Vector3(actor.position.x-.05f,surfaceY+.18f+bob,actor.position.z);
            B("pelvis").rotation=Quaternion.Euler(0,0,-90);
            B("torso").localRotation=Quaternion.identity;
            B("neck").localRotation=Quaternion.Euler(0,0,70);
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"near":"far";
                float t=Mathf.Repeat(phase+side*.5f,1);
                float x=t<.6f?Mathf.Lerp(.82f,.604f,t/.6f):Mathf.Lerp(.604f,.82f,S((t-.6f)/.4f));
                float lift=t<.6f?0:Mathf.Sin((t-.6f)/.4f*Mathf.PI)*.055f;
                GroundLimb(prefix,true,new Vector3(actor.position.x+Mathf.Lerp(side==0?.76f:.80f,x,movement),surfaceY+.055f+lift*movement,actor.position.z),-1,surfaceY);
                B(prefix+"_hand").rotation=Quaternion.Euler(0,0,65);
                float legPhase=Mathf.Repeat(t+.5f,1);
                float footX=legPhase<.6f?Mathf.Lerp(-.566f,-.782f,legPhase/.6f):Mathf.Lerp(-.782f,-.566f,S((legPhase-.6f)/.4f));
                float footLift=legPhase<.6f?0:Mathf.Sin((legPhase-.6f)/.4f*Mathf.PI)*.035f;
                GroundLimb(prefix,false,new Vector3(actor.position.x+Mathf.Lerp(side==0?-.78f:-.80f,footX,movement),surfaceY+.055f+footLift*movement,actor.position.z),1,surfaceY);
                B(prefix+"_foot").rotation=Quaternion.Euler(0,0,-135);
            }
            Cloth(surfaceY,1,phase);Blend(S(weight));KeepFeetAbove(surfaceY);
        }
        public void CrawlOnSurface(float phase,float movement,float blend,System.Func<float,float> floor,System.Func<float,float> ceiling)
        {
            float baseY=actor.position.y;
            float Height(float x)=>Mathf.Lerp(baseY,floor(x),blend);
            float Slope(float x)=>Mathf.Atan2(Height(x+.12f)-Height(x-.12f),.24f)*Mathf.Rad2Deg;
            float hipX=actor.position.x-.05f;
            B("pelvis").position=new Vector3(hipX,Height(hipX)+.18f+.008f*Mathf.Sin(phase*Mathf.PI*4)*movement,actor.position.z);
            float hipSlope=Slope(hipX),chestSlope=Slope(hipX+.28f);
            B("pelvis").rotation=Quaternion.Euler(0,0,-90+hipSlope);
            B("torso").localRotation=Quaternion.Euler(0,0,chestSlope-hipSlope);
            B("neck").localRotation=Quaternion.Euler(0,0,70);
            // Lower the head under a low ceiling instead of letting a sprite mask hide it.
            if(B("head").position.y+.19f>ceiling(B("head").position.x))
            {
                float lo=-15,hi=70;
                for(int i=0;i<10;i++)
                {
                    float mid=(lo+hi)*.5f;B("neck").localRotation=Quaternion.Euler(0,0,mid);
                    if(B("head").position.y+.19f>ceiling(B("head").position.x))hi=mid;else lo=mid;
                }
                B("neck").localRotation=Quaternion.Euler(0,0,Mathf.Lerp(70,lo,blend));
            }
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"near":"far";float t=Mathf.Repeat(phase+side*.5f,1);
                float hx=t<.6f?Mathf.Lerp(.82f,.604f,t/.6f):Mathf.Lerp(.604f,.82f,S((t-.6f)/.4f));
                float lift=t<.6f?0:Mathf.Sin((t-.6f)/.4f*Mathf.PI)*.055f;
                float handX=actor.position.x+Mathf.Lerp(side==0?.76f:.80f,hx,movement);
                Vector3 hand=new Vector3(handX,Height(handX)+.055f+lift*movement,actor.position.z);
                GroundLimb(prefix,true,hand,-1,Height(handX));
                GroundLimb(prefix,true,hand,-1,Height(B(prefix+"_forearm").position.x));
                B(prefix+"_hand").rotation=Quaternion.Euler(0,0,65+Slope(handX));
                float legPhase=Mathf.Repeat(t+.5f,1);
                float fx=legPhase<.6f?Mathf.Lerp(-.566f,-.782f,legPhase/.6f):Mathf.Lerp(-.782f,-.566f,S((legPhase-.6f)/.4f));
                float footLift=legPhase<.6f?0:Mathf.Sin((legPhase-.6f)/.4f*Mathf.PI)*.035f;
                float footX=actor.position.x+Mathf.Lerp(side==0?-.78f:-.80f,fx,movement);
                Vector3 foot=new Vector3(footX,Height(footX)+.055f+footLift*movement,actor.position.z);
                GroundLimb(prefix,false,foot,1,Height(footX));
                GroundLimb(prefix,false,foot,1,Height(B(prefix+"_shin").position.x));
                B(prefix+"_foot").rotation=Quaternion.Euler(0,0,-135+Slope(footX));
            }
            LaySurfaceChain(new[]{"hair_root","hair_mid","hair_tip"},Height,.08f);
            LaySurfaceChain(new[]{"veil_root","veil_1","veil_2","veil_tip"},Height,.06f);
            LaySurfaceChain(new[]{"skirt_back_root","skirt_back_mid","skirt_back_tip"},Height,.07f);
            LaySurfaceChain(new[]{"skirt_front_root","skirt_front_mid","skirt_front_tip"},Height,.09f);
            LaySurfaceChain(new[]{"scroll_root","scroll_tip"},Height,.08f);
        }
        void LaySurfaceChain(string[] names,System.Func<float,float> height,float padding)
        {
            for(int i=0;i<names.Length-1;i++)
            {
                var a=B(names[i]);var b=B(names[i+1]);float length=Vector3.Distance(a.position,b.position),dx=-length,dy=0;
                for(int j=0;j<4;j++){dy=Mathf.Clamp(height(a.position.x+dx)+padding-a.position.y,-length*.94f,length*.94f);dx=-Mathf.Sqrt(Mathf.Max(.0001f,length*length-dy*dy));}
                a.rotation=Quaternion.Euler(0,0,(Mathf.Atan2(dy,dx)-Mathf.Atan2(b.localPosition.y,b.localPosition.x))*Mathf.Rad2Deg);
            }
        }
        public void Hang(Vector3 grip,float weight,float reachWeight)
        {
            B("pelvis").rotation=Quaternion.Euler(0,0,-3);
            B("torso").localRotation=Quaternion.Euler(0,0,-3);
            B("neck").localRotation=Quaternion.Euler(0,0,8);
            B("near_thigh").localRotation=Quaternion.Euler(0,0,10);
            B("near_shin").localRotation=Quaternion.Euler(0,0,-18);
            B("far_thigh").localRotation=Quaternion.Euler(0,0,-5);
            B("far_shin").localRotation=Quaternion.Euler(0,0,-12);
            Blend(S(weight));
            Quaternion nearUpper=B("near_upperarm").rotation,nearLower=B("near_forearm").rotation,farUpper=B("far_upperarm").rotation,farLower=B("far_forearm").rotation;
            LockHands(grip,grip+new Vector3(-.10f,.015f,0));
            if(reachWeight<1)
            {
                Quaternion nu=B("near_upperarm").rotation,nl=B("near_forearm").rotation,fu=B("far_upperarm").rotation,fl=B("far_forearm").rotation;
                B("near_upperarm").rotation=Quaternion.Slerp(nearUpper,nu,S(reachWeight));B("near_forearm").rotation=Quaternion.Slerp(nearLower,nl,S(reachWeight));
                B("far_upperarm").rotation=Quaternion.Slerp(farUpper,fu,S(reachWeight));B("far_forearm").rotation=Quaternion.Slerp(farLower,fl,S(reachWeight));
            }
        }
        public void Mantle(Vector3 grip,float t)
        {
            // Shoulder pull, chest press, pelvis transfer, then alternating hand release.
            float rise=t<.35f?Mathf.Lerp(-.84f,-.43f,S(t/.35f)):
                t<.68f?Mathf.Lerp(-.43f,.08f,S((t-.35f)/.33f)):Mathf.Lerp(.08f,.18f,S((t-.68f)/.32f));
            float hipX=t<.68f?Mathf.Lerp(-.18f,-.12f,S(t/.68f)):Mathf.Lerp(-.12f,.32f,S((t-.68f)/.32f));
            float tilt=t<.35f?Mathf.Lerp(-3,-15,S(t/.35f)):Mathf.Lerp(-15,-90,S((t-.35f)/.5f));
            B("pelvis").position=new Vector3(grip.x+hipX,grip.y+rise,actor.position.z);
            B("pelvis").rotation=Quaternion.Euler(0,0,tilt);B("torso").localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-3,0,S(t/.15f)));
            B("neck").localRotation=Quaternion.Euler(0,0,Mathf.Lerp(8,70,S(t)));
            Vector3 near=Vector3.Lerp(grip,grip+new Vector3(1.13f,.055f,0),S((t-.68f)/.32f));
            Vector3 far=Vector3.Lerp(grip+new Vector3(-.10f,.015f,0),grip+new Vector3(1.17f,.055f,0),S((t-.76f)/.24f));
            ReleaseArm("near",near,S((t-.68f)/.32f),grip.y-.8f*(1-S((t-.68f)/.32f)));
            ReleaseArm("far",far,S((t-.76f)/.24f),grip.y-.8f*(1-S((t-.76f)/.24f)));
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"near":"far";float p=S((t-(side==0?.32f:.40f))/(side==0?.68f:.60f));
                int footIndex=Index(prefix+"_foot");
                Vector3 foot=Vector3.Lerp(entryWorld[footIndex],grip+new Vector3(side==0?-.41f:-.43f,.055f,0),p);
                GroundLimb(prefix,false,foot,1,Mathf.Min(grip.y,foot.y-.025f));
                B(prefix+"_foot").rotation=Quaternion.Slerp(entryWorldRotations[footIndex],Quaternion.Euler(0,0,-135),S((t-.45f)/.55f));
            }
            Cloth(grip.y,S((t-.45f)/.55f),0);
        }
        void LockHands(Vector3 near,Vector3 far)
        {
            Limb("near",true,near,1);Limb("far",true,far,1);
            B("near_hand").rotation=Quaternion.Euler(0,0,-8);B("far_hand").rotation=Quaternion.Euler(0,0,-8);
        }
        public void KeepFeetAbove(float floor)
        {
            foreach(string side in new[]{"near","far"})
            {
                var foot=B(side+"_foot");Quaternion rotation=foot.rotation;
                float low=Mathf.Min(foot.TransformPoint(new Vector3(-10,-25,0)/340f).y,foot.TransformPoint(new Vector3(44,-25,0)/340f).y);
                if(low<floor){Vector3 target=foot.position;target.y+=floor-low;GroundLimb(side,false,target,1,floor);foot.rotation=rotation;}
            }
        }
        void Cloth(float floor,float weight,float phase)
        {
            LayChain(new[]{"hair_root","hair_mid","hair_tip"},floor+.08f,weight);
            LayChain(new[]{"veil_root","veil_1","veil_2","veil_tip"},floor+.06f,weight);
            LayChain(new[]{"skirt_back_root","skirt_back_mid","skirt_back_tip"},floor+.07f,weight);
            LayChain(new[]{"skirt_front_root","skirt_front_mid","skirt_front_tip"},floor+.09f,weight);
            LayChain(new[]{"scroll_root","scroll_tip"},floor+.08f,weight);
        }
        void LayChain(string[] names,float y,float weight)
        {
            for(int i=0;i<names.Length-1;i++)
            {
                var a=B(names[i]);var b=B(names[i+1]);float length=Vector3.Distance(a.position,b.position);
                float dy=Mathf.Clamp(y-a.position.y,-length*.94f,length*.3f);
                float dx=-Mathf.Sqrt(Mathf.Max(.0001f,length*length-dy*dy));
                float angle=(Mathf.Atan2(dy,dx)-Mathf.Atan2(b.localPosition.y,b.localPosition.x))*Mathf.Rad2Deg;
                a.rotation=Quaternion.Slerp(a.rotation,Quaternion.Euler(0,0,angle),weight);
            }
        }
    }
}
