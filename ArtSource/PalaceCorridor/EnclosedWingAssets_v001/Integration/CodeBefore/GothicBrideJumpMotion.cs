using UnityEngine;

namespace Aloysius.GothicStage
{
    public enum GothicJumpState { Grounded, Anticipation, Airborne, Landing }

    /// <summary>One-shot skeletal jump and a deterministic vertical arc over the room floor.</summary>
    internal sealed class GothicBrideJumpMotion
    {
        readonly Transform actor;
        readonly Transform[] bones;
        readonly GothicBrideClip clip;
        readonly int[] thighs = new int[2], shins = new int[2], feet = new int[2];
        readonly float roomFloorY;
        float floorY, flightOriginY, launchVelocity;
        Collider2D[] surfaces;
        Collider2D support;
        bool fallingOnly;
        GothicBonePose entry;
        float clock, queued, height, gravity, anticipation, landing, flightDuration;
        public GothicJumpState State { get; private set; }
        public bool Active => State != GothicJumpState.Grounded;
        public float Height => Mathf.Max(0f, actor.position.y - floorY);
        public float VerticalSpeed => State == GothicJumpState.Airborne ? launchVelocity-gravity*clock : 0f;
        public float GroundY => floorY;
        public int Count { get; private set; }

        public GothicBrideJumpMotion(Transform body, Transform[] joints, GothicBrideClip motion, float ground)
        {
            actor = body; bones = joints; clip = motion; floorY = roomFloorY = ground;
            gravity=14f;landing=.18f;flightDuration=.756f;
            for (int side = 0; side < 2; side++)
            {
                string prefix = side == 0 ? "near_" : "far_";
                thighs[side] = Find(prefix + "thigh");
                shins[side] = Find(prefix + "shin");
                feet[side] = Find(prefix + "foot");
            }
        }
        public void SetSurfaces(Collider2D[] value)=>surfaces=value;
        static bool Available(Collider2D c)=>c && c.enabled && c.gameObject.activeInHierarchy;
        static bool Over(float x,Bounds b)=>x>=b.min.x-.06f && x<=b.max.x+.06f;

        void CheckSupport()
        {
            if(State==GothicJumpState.Airborne)return;
            Physics2D.SyncTransforms();
            if(support && Available(support) && Over(actor.position.x,support.bounds))
            {
                floorY=support.bounds.max.y;
                var p=actor.position;p.y=floorY;actor.position=p;return;
            }
            if(floorY>roomFloorY+.001f || actor.position.y>floorY+.003f)
            {
                support=null;floorY=roomFloorY;flightOriginY=actor.position.y;
                launchVelocity=clock=0;fallingOnly=true;State=GothicJumpState.Airborne;
            }
        }

        int Find(string name)
        {
            for (int i = 0; i < bones.Length; i++) if (bones[i].name == name) return i;
            return -1;
        }

        public void QueueInput(bool pressed, float dt, float turnWait)
        {
            queued = Mathf.Max(0f, queued - dt);
            if (pressed && clip.jump) queued = .14f + turnWait;
        }

        public void TryBegin(float jumpHeight, float fallGravity, float windupTime, float landTime)
        {
            if (Active || queued <= 0f || !clip.jump) return;
            queued = clock = 0f;
            height = Mathf.Max(.05f, jumpHeight); gravity = Mathf.Max(.1f, fallGravity);
            anticipation = Mathf.Max(.01f, windupTime); landing = Mathf.Max(.01f, landTime);
            flightDuration = 2f * Mathf.Sqrt(2f * height / gravity);
            launchVelocity=Mathf.Sqrt(2f*height*gravity);flightOriginY=floorY;fallingOnly=false;
            entry = new GothicBonePose { x = new float[bones.Length], y = new float[bones.Length], angle = new float[bones.Length] };
            for (int i = 0; i < bones.Length; i++)
            {
                entry.x[i] = bones[i].localPosition.x * clip.pixelsPerUnit;
                entry.y[i] = (i == 0 ? clip.floor : 0f) - bones[i].localPosition.y * clip.pixelsPerUnit;
                entry.angle[i] = -Mathf.DeltaAngle(0f, bones[i].localEulerAngles.z) * Mathf.Deg2Rad;
            }
            State = GothicJumpState.Anticipation;
        }

        public void Advance(float dt,float previousX)
        {
            CheckSupport();
            if (!Active) return;
            float previousY=actor.position.y;
            clock += dt;
            // Carry time across state boundaries, so 30/60/120 Hz give the same arc.
            if (State == GothicJumpState.Anticipation && clock >= anticipation)
            {
                clock -= anticipation; State = GothicJumpState.Airborne; Count++;support=null;
            }
            if(State==GothicJumpState.Airborne)
            {
                float nextY=flightOriginY+launchVelocity*clock-.5f*gravity*clock*clock;
                float landingY=roomFloorY;Collider2D landedOn=null;
                if(VerticalSpeed<=0 && surfaces!=null)
                    foreach(var surface in surfaces)
                    {
                        if(!Available(surface))continue;
                        Bounds b=surface.bounds;float top=b.max.y;
                        if(top<landingY || previousY<top-.001f || nextY>top)continue;
                        float fraction=Mathf.Clamp01((previousY-top)/Mathf.Max(.000001f,previousY-nextY));
                        if(!Over(Mathf.Lerp(previousX,actor.position.x,fraction),b))continue;
                        landingY=top;landedOn=surface;
                    }
                var position=actor.position;
                if(VerticalSpeed<=0 && nextY<=landingY)
                {
                    float fraction=Mathf.Clamp01((previousY-landingY)/Mathf.Max(.000001f,previousY-nextY));
                    clock=dt*(1-fraction);floorY=landingY;support=landedOn;
                    position.y=landingY;State=GothicJumpState.Landing;
                }
                else position.y=nextY;
                actor.position=position;
            }
            if (State == GothicJumpState.Landing && clock >= landing)
            {
                clock = 0f; State = GothicJumpState.Grounded;
            }
            if(State!=GothicJumpState.Airborne){var p=actor.position;p.y=floorY;actor.position=p;}
        }

        public void ApplyPose()
        {
            if (!Active || !clip.jump) return;
            GothicBrideJumpClip motion = clip.jump;
            if (State == GothicJumpState.Anticipation)
            {
                float t = Mathf.Clamp01(clock / anticipation);
                if (t < .65f) Mix(entry, motion.crouch, Smooth(t / .65f));
                else Mix(motion.crouch, motion.takeoff, Smooth((t - .65f) / .35f));
                GroundFeet(Smooth(t / .25f));
            }
            else if (State == GothicJumpState.Airborne)
            {
                float t = fallingOnly?Mathf.Clamp01(.5f+clock/.6f):Mathf.Clamp01(clock / flightDuration);
                if (t < .22f) Mix(motion.takeoff, motion.rise, Smooth(t / .22f));
                else if (t < .50f) Mix(motion.rise, motion.apex, Smooth((t - .22f) / .28f));
                else Mix(motion.apex, motion.contact, Smooth((t - .50f) / .42f));
            }
            else
            {
                float t = Mathf.Clamp01(clock / landing);
                if (t < .28f)
                {
                    Mix(motion.contact, motion.landing, Smooth(t / .28f));
                    GroundFeet(1f);
                }
                else
                {
                    float weight = 1f - Smooth((t - .28f) / .72f);
                    // The existing walker already supplied this frame's locomotion pose.
                    // Recover into that live pose rather than restarting a walk cycle.
                    BlendOverCurrent(motion.landing, weight);
                    GroundFeet(weight);
                }
            }
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        Vector3 Position(GothicBonePose pose, int i)
        { return new Vector3(pose.x[i] / clip.pixelsPerUnit, ((i == 0 ? clip.floor : 0f) - pose.y[i]) / clip.pixelsPerUnit, 0f); }

        void Mix(GothicBonePose a, GothicBonePose b, float t)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(Position(a, i), Position(b, i), t);
                float angle = Mathf.LerpAngle(a.angle[i] * Mathf.Rad2Deg, b.angle[i] * Mathf.Rad2Deg, t);
                bones[i].localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }

        void BlendOverCurrent(GothicBonePose pose, float weight)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(bones[i].localPosition, Position(pose, i), weight);
                float angle = Mathf.LerpAngle(bones[i].localEulerAngles.z, -pose.angle[i] * Mathf.Rad2Deg, weight);
                bones[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void GroundFeet(float weight)
        {
            for (int side = 0; side < 2; side++)
            {
                if (thighs[side] < 0 || shins[side] < 0 || feet[side] < 0) continue;
                Transform hip = bones[thighs[side]], knee = bones[shins[side]], foot = bones[feet[side]];
                Quaternion footRotation = foot.rotation;
                float sole = Mathf.Min(foot.TransformPoint(new Vector3(-10f, -25f, 0f) / clip.pixelsPerUnit).y,
                    foot.TransformPoint(new Vector3(44f, -25f, 0f) / clip.pixelsPerUnit).y);
                // Keep planted contacts from sinking during recovery blends; only
                // fade the constraint for a foot already lifting into its next step.
                Vector2 target = foot.position;
                target.y += (floorY - sole) * (sole < floorY ? 1f : weight);
                Vector2 delta = target - (Vector2)hip.position;
                float upper = Vector3.Distance(hip.position, knee.position), lower = Vector3.Distance(knee.position, foot.position);
                float distance = Mathf.Clamp(delta.magnitude, .0001f, upper + lower - .00001f);
                float line = Mathf.Atan2(delta.y, delta.x);
                float a = Mathf.Acos(Mathf.Clamp((upper * upper + distance * distance - lower * lower) / (2f * upper * distance), -1f, 1f));
                float b = Mathf.Acos(Mathf.Clamp((lower * lower + distance * distance - upper * upper) / (2f * lower * distance), -1f, 1f));
                hip.rotation = Quaternion.Euler(0, 0, (line + a) * Mathf.Rad2Deg + 90f);
                knee.rotation = Quaternion.Euler(0, 0, (line - b) * Mathf.Rad2Deg + 90f);
                foot.rotation = footRotation;
            }
        }
    }
}
