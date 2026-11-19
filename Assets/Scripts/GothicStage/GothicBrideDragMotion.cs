using UnityEngine;

namespace Aloysius.GothicStage
{
    /// <summary>Fixed-length limbs, alternating planted steps, and two hands constrained to a grip.</summary>
    internal sealed class GothicBrideDragMotion
    {
        readonly Transform actor;
        readonly Transform[] bones;
        readonly Transform pelvis, torso, neck;
        readonly Vector3[] positions;
        readonly Quaternion[] rotations;
        public GothicBrideDragMotion(Transform actor, Transform[] bones)
        {
            this.actor=actor;this.bones=bones;
            pelvis=Find("pelvis");torso=Find("torso");neck=Find("neck");
            positions=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];
        }
        Transform Find(string name) { foreach(var b in bones)if(b.name==name)return b;return null; }
        static float Smooth(float t) => Mathf.SmoothStep(0,1,Mathf.Clamp01(t));

        public void Apply(float weight,float phase,float stepping,Vector3 grip,int facing)
        {
            if(weight<=0)return;
            for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
            float bob=.008f*Mathf.Cos(phase*Mathf.PI*4)*stepping;
            pelvis.localPosition+=new Vector3(-.04f,-.32f+bob,0);
            torso.localRotation=Quaternion.Euler(0,0,-48f);
            neck.localRotation=Quaternion.Euler(0,0,28f);
            // Presentation mirrors the skinned mesh in the shader, not the bones.
            // Solve in the unmirrored skeleton's world space.
            grip.x=actor.position.x+(grip.x-actor.position.x)*facing;
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"near_":"far_";
                float t=Mathf.Repeat(phase+side*.5f,1);
                float x=t<.62f?Mathf.Lerp(.22f,-.22f,t/.62f):Mathf.Lerp(-.22f,.22f,Smooth((t-.62f)/.38f));
                float lift=t<.62f?0:Mathf.Sin((t-.62f)/.38f*Mathf.PI)*.055f;
                Vector3 ankle=actor.position+new Vector3(Mathf.Lerp(side==0?-.09f:.09f,x,stepping),25f/340f+lift*stepping,0);
                Solve(Find(prefix+"thigh"),Find(prefix+"shin"),Find(prefix+"foot"),ankle,1);
                Find(prefix+"foot").rotation=Quaternion.identity;
                Vector3 hand=grip+new Vector3(side==0?0:.035f,side==0?0:.018f,0);
                Solve(Find(prefix+"upperarm"),Find(prefix+"forearm"),Find(prefix+"hand"),hand,-1);
                Find(prefix+"hand").rotation=Quaternion.Euler(0,0,-12);
            }
            // Counter-rotate hanging attachments: bending the back must not make cloth rigid.
            Find("hair_root").localRotation*=Quaternion.Euler(0,0,24f);
            Find("veil_root").localRotation*=Quaternion.Euler(0,0,20f);
            Find("scroll_root").localRotation*=Quaternion.Euler(0,0,48f);
            for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=Vector3.Lerp(positions[i],bones[i].localPosition,weight);
                bones[i].localRotation=Quaternion.Slerp(rotations[i],bones[i].localRotation,weight);
            }
            // Interpolated joint rotations can put soles slightly below the floor
            // while bending/recovering. Correct contacts without stretching bones.
            foreach(string prefix in new[]{"near_","far_"})
            {
                var foot=Find(prefix+"foot");Quaternion rotation=foot.rotation;
                float sole=Mathf.Min(foot.TransformPoint(new Vector3(-10,-25,0)/340f).y,foot.TransformPoint(new Vector3(44,-25,0)/340f).y);
                if(sole<actor.position.y)
                {
                    Vector3 target=foot.position;target.y+=actor.position.y-sole;
                    Solve(Find(prefix+"thigh"),Find(prefix+"shin"),foot,target,1);foot.rotation=rotation;
                }
            }
        }

        static void Solve(Transform a,Transform b,Transform c,Vector3 target,float bend)
        {
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
            Vector2 delta=target-a.position;
            float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.00001f,l1+l2-.00001f);
            float line=Mathf.Atan2(delta.y,delta.x);
            float angle=Mathf.Acos(Mathf.Clamp((l1*l1+d*d-l2*l2)/(2*l1*d),-1,1));
            float local1=Mathf.Atan2(b.localPosition.y,b.localPosition.x);
            a.rotation=Quaternion.Euler(0,0,(line+bend*angle-local1)*Mathf.Rad2Deg);
            Vector2 lower=target-b.position;
            float local2=Mathf.Atan2(c.localPosition.y,c.localPosition.x);
            b.rotation=Quaternion.Euler(0,0,(Mathf.Atan2(lower.y,lower.x)-local2)*Mathf.Rad2Deg);
        }
    }
}
