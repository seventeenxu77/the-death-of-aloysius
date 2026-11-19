using UnityEngine;

namespace Aloysius.GothicStage
{
    /// <summary>Native Unity bones and weighted meshes, sampled from the approved 2D rig.</summary>
    public sealed class GothicBride2DWalker : MonoBehaviour
    {
        [SerializeField] GothicBrideClip clip;
        [SerializeField] Transform visual;
        [SerializeField] Transform[] bones;
        [SerializeField] SkinnedMeshRenderer[] parts;
        [SerializeField] float leftLimit = -6.25f;
        [SerializeField] float rightLimit = 6.25f;
        [SerializeField] float acceleration = 4.2f;
        [SerializeField] float turnDuration = .34f;
        [Header("Jump")]
        [SerializeField, Min(.05f)] float jumpHeight = 1f;
        [SerializeField, Min(.1f)] float jumpGravity = 14f;
        [SerializeField, Min(.01f)] float jumpAnticipation = .10f;
        [SerializeField, Min(.01f)] float jumpLanding = .18f;
        [Header("One-way standing surfaces")]
        [SerializeField] Collider2D[] oneWaySurfaces;
        bool navigating;
        float navigationTarget;
        float velocity, phase, walkBlend, runBlend, turnClock, turnStartBlend;
        int facing = 1, turnTarget = 1, turnFrom = 1, priorInput;
        bool turning;
        Vector3 initialPosition;
        GothicBrideJumpMotion jumpMotion;
        GothicBrideDragMotion dragMotion;
        GothicBrideTraversalMotion traversalMotion;
        GothicBrideTraversalMotion exitPoseBlend;
        public bool SpecialPoseActive { get; private set; }
        float dragPhase, dragStepBlend;
        float releaseBlendRemaining, releaseBlendDuration;
        float clothFloor;
        bool clothFloorActive;
        MaterialPropertyBlock presentation;
        Transform[] attachmentRoots;
        float[] attachmentDelays;
        float[] renderedWidths;
        static readonly int TurnProperty = Shader.PropertyToID("_Turn");
        public float BodyWidth { get; private set; } = 1f;
        public float BodySquash { get; private set; } = 1f;
        public Vector3 TraversalFocusPosition
        {
            get
            {
                foreach(var bone in bones)
                    if(bone.name=="pelvis")
                    {
                        Vector3 p=transform.InverseTransformPoint(bone.position);
                        p.x*=BodyWidth;p.y*=BodySquash;
                        return transform.TransformPoint(p)-Vector3.up*.18f;
                    }
                return transform.position;
            }
        }
        public float TurnProgress => Mathf.Clamp01(turnClock / turnDuration);
        public float TurnDuration => turnDuration;
        public bool ExternalControl { get; set; }
        public int Facing => facing;
        public float LeftLimit => leftLimit;
        public float RightLimit => rightLimit;
        public void SetMovementBounds(float left, float right) { leftLimit = left; rightLimit = Mathf.Max(left, right); }
        // Same-elevation room transition: clear velocity and special poses, retain the original R reset point.
        public void RelocateOnRoomFloor(Vector3 destination, bool preserveHeight = false)
        {
            ResetActor();
            if(!preserveHeight)destination.y = initialPosition.y;
            transform.position = destination;
            if(preserveHeight && jumpMotion!=null)jumpMotion.PlaceOnSurface(destination);
        }
        public float Phase => phase;
        public float Speed => velocity;
        public bool Turning => turning;
        public float WalkBlend => walkBlend;
        public float WalkSpeed => clip.stridePixels / clip.pixelsPerUnit / clip.cycleSeconds;
        public float RunSpeed => clip && clip.sprint ? clip.sprint.stridePixels / clip.sprint.pixelsPerUnit / clip.sprint.cycleSeconds : WalkSpeed;
        public float RunBlend => runBlend;
        public Transform[] Bones => bones;
        public SkinnedMeshRenderer[] Parts => parts;
        public GothicBrideClip Clip => clip;
        public GothicJumpState JumpState => jumpMotion != null ? jumpMotion.State : GothicJumpState.Grounded;
        public float HeightAboveGround => jumpMotion != null ? jumpMotion.Height : 0f;
        public float VerticalSpeed => jumpMotion != null ? jumpMotion.VerticalSpeed : 0f;
        public int JumpCount => jumpMotion != null ? jumpMotion.Count : 0;
        public float GroundY => jumpMotion != null ? jumpMotion.GroundY : initialPosition.y;
        public float RoomFloorY => initialPosition.y;
        public bool HasStandingSupport(float y,float x)
        {
            if(Mathf.Abs(y-initialPosition.y)<.02f)return true;
            Physics2D.SyncTransforms();
            if(oneWaySurfaces!=null)foreach(var c in oneWaySurfaces)
                                if(c && c.enabled && c.gameObject.activeInHierarchy && x>=c.bounds.min.x-.06f && x<=c.bounds.max.x+.06f)
                {
                    var profile=c.GetComponent<GothicWalkSurface>();float top=c.bounds.max.y;
                    if(profile&&!profile.TryHeight(x,out top))continue;
                    if(Mathf.Abs(top-y)<.02f)return true;
                }
            return false;
        }
        public void ConfigurePlatforms(Collider2D[] surfaces)
        { oneWaySurfaces=surfaces; if(jumpMotion!=null)jumpMotion.SetSurfaces(surfaces); }

        public void AdvanceTowards(float targetX, float dt)
        {
            navigating=true;navigationTarget=Mathf.Clamp(targetX,leftLimit,rightLimit);
            float dx=navigationTarget-transform.position.x;
            Advance(Mathf.Abs(dx)<.001f?0:dx>0?1:-1,dt);
            navigating=false;
        }

        public void Configure(GothicBrideClip motion, Transform body, Transform[] joints, SkinnedMeshRenderer[] meshes)
        {
            clip = motion; visual = body; bones = joints; parts = meshes;
            initialPosition = transform.position;
            ResetActor();
        }

        void Awake()
        {
            initialPosition = transform.position;
            ResetActor();
        }

        void Update()
        {
            if(Aloysius.Level1.Level1TeleportConsole.BlocksGameplayInput)return;
            if (ExternalControl) return;
            if (Input.GetKeyDown(KeyCode.R)) ResetActor();
            int direction = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                          - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Advance(direction, Time.deltaTime, sprint, Input.GetKeyDown(KeyCode.Space));
            if (direction != priorInput)
            {
                Debug.Log($"[Bride2D] Input={direction} X={transform.position.x:F3} Facing={facing}");
                priorInput = direction;
            }
        }

        public void ResetActor()
        {
            if (!clip || !visual || bones == null) return;
            transform.position = initialPosition;
            velocity = phase = walkBlend = runBlend = turnClock = 0f;
            facing = turnTarget = turnFrom = 1; turning = false;
            jumpMotion = new GothicBrideJumpMotion(transform, bones, clip, initialPosition.y);
            jumpMotion.SetSurfaces(oneWaySurfaces);
            dragMotion = null; dragPhase = dragStepBlend = 0f;
            traversalMotion=null;exitPoseBlend=null;SpecialPoseActive=false;releaseBlendRemaining=0;
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            ApplyPose(0f, 0f);
            Present(1f, 1f, false, 0f);
        }

        public void Advance(int direction, float dt, bool sprint = false, bool jumpPressed = false)
        {
            SpecialPoseActive=false;
            if (!clip || !visual || bones == null) return;
            dt = Mathf.Clamp(dt, 0f, .05f);
            direction = Mathf.Clamp(direction, -1, 1);
            if (jumpMotion == null) jumpMotion = new GothicBrideJumpMotion(transform, bones, clip, initialPosition.y);
            bool wantsTurn = direction != 0 && direction != facing && !jumpMotion.Active;
            float turnWait = turning ? Mathf.Max(0f, turnDuration - turnClock) : wantsTurn ? turnDuration : 0f;
            jumpMotion.QueueInput(jumpPressed, dt, turnWait);
            if (!turning && wantsTurn)
            {
                turning = true; turnClock = 0f; turnTarget = direction;
                turnFrom = facing; turnStartBlend = walkBlend;
            }
            if (turning)
            {
                // The ground pivot stays fixed. Close the feet, narrow around the
                // vertical body axis, cross the thin profile, then unfold and settle.
                turnClock += dt;
                float t = Mathf.Clamp01(turnClock / turnDuration);
                velocity = Mathf.MoveTowards(velocity, 0f, acceleration * 2f * dt);
                walkBlend = turnStartBlend * (1f - Smooth(t / .38f));
                ApplyPose(phase, walkBlend);
                visual.localPosition = Vector3.zero;
                visual.localScale = Vector3.one;
                float width = TurnWidth(t, turnFrom);
                float squash = 1f - .025f * Smooth(t / .18f) * (1f - Smooth((t - .55f) / .45f));
                if (t >= .45f) facing = turnTarget;
                Present(width, squash, true, t);
                if (t >= 1f)
                {
                    turning = false; phase = 0f; velocity = 0f; walkBlend = 0f; runBlend = 0f;
                    facing = turnTarget;
                    Present(facing, 1f, false, 0f);
                }
                return;
            }
            jumpMotion.TryBegin(jumpHeight, jumpGravity, jumpAnticipation, jumpLanding);
            bool wantsRun = sprint && clip.sprint && direction != 0;
            // Air steering can reverse travel while the character finishes the jump.
            // The established body turn plays on the ground, without freezing gravity.
            float desired = direction == facing || jumpMotion.Active ? direction * (wantsRun ? RunSpeed : WalkSpeed) : 0f;
            if(navigating)desired=direction*Mathf.Min(WalkSpeed,Mathf.Sqrt(2f*acceleration*Mathf.Abs(navigationTarget-transform.position.x)));
            float rate = wantsRun || runBlend > .001f ? acceleration * 1.65f : acceleration;
            if (JumpState == GothicJumpState.Airborne) rate *= .55f;
            velocity = Mathf.MoveTowards(velocity, desired, rate * dt);
            float oldX = transform.position.x;
            Vector3 next = transform.position;
            next.x = Mathf.Clamp(next.x + velocity * dt, leftLimit, rightLimit);
            if(navigating && (navigationTarget-oldX)*(navigationTarget-next.x)<=0)
            { next.x=navigationTarget;velocity=0; }
            transform.position = next;
            float distance = Mathf.Abs(next.x - oldX);
            if (distance < .000001f && direction != 0) velocity = 0f;
            // Walk and run share foot-contact phase. Keep phase through Shift changes,
            // and derive cadence from actual travel and the blended stride length.
            runBlend = clip.sprint ? Mathf.InverseLerp(WalkSpeed, RunSpeed, Mathf.Abs(velocity)) : 0f;
            float stride = Mathf.Lerp(clip.stridePixels / clip.pixelsPerUnit,
                clip.sprint ? clip.sprint.stridePixels / clip.sprint.pixelsPerUnit : clip.stridePixels / clip.pixelsPerUnit, runBlend);
            phase = Mathf.Repeat(phase + distance / stride, 1f);
            float targetBlend = Mathf.Abs(velocity) > .008f ? 1f : 0f;
            walkBlend = Mathf.MoveTowards(walkBlend, targetBlend, dt * (targetBlend > walkBlend ? 8f : 6f));
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            ApplyPose(phase, walkBlend);
            jumpMotion.Advance(dt,oldX);
            jumpMotion.ApplyPose();
            if(releaseBlendRemaining>0 && traversalMotion!=null)
            {
                releaseBlendRemaining=Mathf.Max(0,releaseBlendRemaining-dt);
                traversalMotion.Blend(Smooth(1-releaseBlendRemaining/releaseBlendDuration));
                SpecialPoseActive=releaseBlendRemaining>0;
            }
            Present(facing, 1f, false, 0f);
        }

        /// <summary>Grounded interaction: keep facing the grip while stepping in either direction.</summary>
        public float AdvanceDrag(int direction, float dt, float weight, Vector3 grip, float minX, float maxX, float dragSpeed, float dragAcceleration)
        {
            dt=Mathf.Clamp(dt,0,.05f);
            if(dragMotion==null)dragMotion=new GothicBrideDragMotion(transform,bones);
            velocity=Mathf.MoveTowards(velocity,Mathf.Clamp(direction,-1,1)*Mathf.Max(0f,dragSpeed),Mathf.Max(.01f,dragAcceleration)*dt);
            float oldX=transform.position.x;
            Vector3 p=transform.position;
            p.x=Mathf.Clamp(p.x+velocity*dt,Mathf.Max(leftLimit,minX),Mathf.Min(rightLimit,maxX));
            transform.position=p;float delta=p.x-oldX;
            if(Mathf.Abs(delta)<.000001f)velocity=0;
            // A planted foot traverses .44 units during 62% of the cycle.
            // Match phase to travel so its world-space contact does not slide.
            dragPhase=Mathf.Repeat(dragPhase+delta*facing/(.44f/.62f),1);
            dragStepBlend=Mathf.MoveTowards(dragStepBlend,Mathf.Abs(velocity)>.005f?1:0,dt*6);
            runBlend=walkBlend=0;
            ApplyPose(0,0);
            dragMotion.Apply(Mathf.SmoothStep(0,1,weight),dragPhase,dragStepBlend,grip+Vector3.right*delta,facing);
            Present(facing,1,false,0);
            return delta;
        }

        public void CaptureTraversalPose()
        {
            if(traversalMotion==null)traversalMotion=new GothicBrideTraversalMotion(transform,bones);
            traversalMotion.Capture();
        }
        void PrepareTraversal()
        {
            if(traversalMotion==null)traversalMotion=new GothicBrideTraversalMotion(transform,bones);
            clothFloorActive=false;SpecialPoseActive=true;velocity=walkBlend=runBlend=0;turning=false;
            visual.localPosition=Vector3.zero;visual.localScale=Vector3.one;ApplyPose(0,0);
        }
        public void PoseCrawl(float surface,float cycle,float moving,float blend,int look)
        {PrepareTraversal();clothFloor=surface;clothFloorActive=true;facing=look;traversalMotion.Crawl(surface,cycle,moving,blend);Present(facing,1,false,0);}
        public void PosePipeCrawl(float cycle,float moving,float blend,System.Func<float,float> floor,System.Func<float,float> ceiling)
        {PrepareTraversal();facing=1;traversalMotion.CrawlOnSurface(cycle,moving,blend,floor,ceiling);Present(1,1,false,0);}
        public void BeginPipeExitTurn()
        {
            exitPoseBlend=new GothicBrideTraversalMotion(transform,bones);
            turnFrom=1;
        }
        public void PosePipeExitTurn(float progress,float cycle,System.Func<float,float> floor,System.Func<float,float> ceiling)
        {
            PrepareTraversal();
            float width=TurnWidth(progress,1),pivot=transform.position.x;
            float WorldX(float x)=>pivot+(x-pivot)*width;
            traversalMotion.CrawlOnSurface(cycle,0,1,x=>floor(WorldX(x)),x=>ceiling(WorldX(x)));
            exitPoseBlend?.Blend(Smooth(progress/.18f));
            facing=progress<.45f?1:-1;
            Present(width,1,true,progress);
        }
        public void PosePipeBackOut(float cycle,float movement,System.Func<float,float> floor,System.Func<float,float> ceiling)
        {
            PrepareTraversal();facing=-1;
            float pivot=transform.position.x;
            traversalMotion.CrawlOnSurface(cycle,movement,1,x=>floor(2*pivot-x),x=>ceiling(2*pivot-x));
            Present(-1,1,false,0);
        }
        public void BeginPipeLowering(Vector3 grip)
        {
            // The shader mirrors the canonical skeleton. Rebase both coordinates
            // while preserving every currently displayed joint in world space.
            Vector3 old=transform.position;
            Vector3 next=new Vector3(grip.x+.18f,grip.y-1.6782353f,old.z);
            transform.position=next;
            Vector3 delta=next-old;
            bones[0].position+=new Vector3(delta.x,-delta.y,-delta.z);
            exitPoseBlend=new GothicBrideTraversalMotion(transform,bones);
            // Mantle needs the hanging endpoint as its captured foot pose. This
            // pose is authored but never presented before the reverse playback.
            PrepareTraversal();facing=-1;
            Vector3 canonicalGrip=grip;canonicalGrip.x=2*transform.position.x-grip.x;
            traversalMotion.Hang(canonicalGrip,1,1);
            traversalMotion.Capture();
            PosePipeLowering(grip,0,0);
        }
        public void PosePipeLowering(Vector3 grip,float progress,float entryBlend)
        {
            PrepareTraversal();facing=-1;
            Vector3 canonicalGrip=grip;canonicalGrip.x=2*transform.position.x-grip.x;
            traversalMotion.Mantle(canonicalGrip,1-Mathf.Clamp01(progress));
            exitPoseBlend?.Blend(Smooth(entryBlend));
            Present(-1,1,false,0);
        }
        public void PoseGroundTransition(float surface,float progress,float entryBlend,int look)
        {PrepareTraversal();clothFloor=surface;clothFloorActive=true;facing=look;traversalMotion.GroundTransition(surface,progress,entryBlend);Present(facing,1,false,0);}
        public void PoseHang(Vector3 grip,float blend,float reachWeight=1)
        {PrepareTraversal();facing=1;traversalMotion.Hang(grip,blend,reachWeight);Present(1,1,false,0);}
        public void PoseMantle(Vector3 grip,float progress)
        {PrepareTraversal();facing=1;traversalMotion.Mantle(grip,progress);Present(1,1,false,0);}
        public void PoseRecover(float blend)
        {PrepareTraversal();traversalMotion.Blend(Mathf.SmoothStep(0,1,blend));traversalMotion.KeepFeetAbove(GroundY);Present(facing,1,false,0);}
        public void BeginTraversalRelease(float duration)
        {
            CaptureTraversalPose();
            ResumeAfterTraversal();
            clothFloorActive=false;
            releaseBlendRemaining=releaseBlendDuration=Mathf.Max(.01f,duration);
        }
        public void ResumeAfterTraversal()
        {
            SpecialPoseActive=false;velocity=walkBlend=runBlend=0;
            jumpMotion=new GothicBrideJumpMotion(transform,bones,clip,initialPosition.y);jumpMotion.SetSurfaces(oneWaySurfaces);
        }

        public void PreviewPose(float cyclePhase, float sprintBlend = 0f)
        {
            phase = Mathf.Repeat(cyclePhase, 1f);
            runBlend = clip.sprint ? Mathf.Clamp01(sprintBlend) : 0f;
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            ApplyPose(phase, 1f);
            Present(1f, 1f, false, 0f);
        }

        // Perspective passages own the screen-space path; retain the same weighted walking rig.
        public void PoseDepthWalk(float cyclePhase, float movementBlend, int look)
        {
            phase = Mathf.Repeat(cyclePhase, 1f);
            walkBlend = Mathf.Clamp01(movementBlend); runBlend = 0;
            facing = look < 0 ? -1 : 1;
            ApplyPose(phase, walkBlend);
            visual.localPosition = Vector3.zero; visual.localScale = Vector3.one;
            Present(facing, 1f, false, 0f);
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        static float TurnWidth(float t, int from)
        {
            float rotation = Smooth((t - .12f) / .66f);
            float width = Mathf.Cos(rotation * Mathf.PI);
            // Cross the edge in one brief moment without disappearing completely.
            float sign = t < .45f ? 1f : -1f;
            width = sign * Mathf.Max(.025f, Mathf.Abs(width));
            if (t > .78f) width = -(1f + .025f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .78f) / .22f)));
            return from * width;
        }

        public float PartWidth(string partName)
        {
            if (renderedWidths == null) return BodyWidth;
            for (int i = 0; i < parts.Length; i++) if (parts[i].name == partName) return renderedWidths[i];
            return BodyWidth;
        }

        void PreparePresentation()
        {
            if (presentation != null) return;
            presentation = new MaterialPropertyBlock();
            attachmentRoots = new Transform[parts.Length];
            attachmentDelays = new float[parts.Length];
            renderedWidths = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string attachment = null;
                switch (parts[i].name)
                {
                    case "veil": attachment = "veil_root"; attachmentDelays[i] = .14f; break;
                    case "skirt_back": attachment = "skirt_back_root"; attachmentDelays[i] = .18f; break;
                    case "skirt_front": attachment = "skirt_front_root"; attachmentDelays[i] = .055f; break;
                    case "hair": attachment = "hair_root"; attachmentDelays[i] = .10f; break;
                    case "scroll": attachment = "scroll_root"; attachmentDelays[i] = .12f; break;
                }
                if (attachment != null)
                    foreach (Transform bone in bones) if (bone.name == attachment) { attachmentRoots[i] = bone; break; }
            }
        }

        void Present(float bodyWidth, float squash, bool isTurn, float t)
        {
            PreparePresentation();
            BodyWidth = bodyWidth; BodySquash = squash;
            for (int i = 0; i < parts.Length; i++)
            {
                float partWidth = bodyWidth;
                if (isTurn && attachmentRoots[i])
                {
                    float delay = attachmentDelays[i];
                    partWidth = TurnWidth(Mathf.Clamp01((t - delay) / (1f - delay)), turnFrom);
                }
                float anchor = attachmentRoots[i] ? parts[i].transform.InverseTransformPoint(attachmentRoots[i].position).x : 0f;
                renderedWidths[i] = partWidth;
                // The attachment point follows the body; only the hanging length
                // lags. This avoids tearing the veil/hair away from the head.
                parts[i].GetPropertyBlock(presentation);
                presentation.SetVector(TurnProperty, new Vector4(bodyWidth, squash, partWidth, anchor));
                bool cloth = attachmentRoots[i] != null || parts[i].name.StartsWith("arm_");
                presentation.SetVector("_ClothFloor", new Vector4(clothFloor, SpecialPoseActive && clothFloorActive && cloth ? 1f : 0f, 0, 0));
                parts[i].SetPropertyBlock(presentation);
            }
        }

        void ApplyPose(float cyclePhase, float blend)
        {
            int count = clip.frames.Length;
            float sample = Mathf.Repeat(cyclePhase, 1f) * count;
            int index = Mathf.FloorToInt(sample);
            float t = sample - index;
            GothicBonePose a = clip.frames[index], b = clip.frames[(index + 1) % count], rest = clip.bind;
            GothicBonePose ra = a, rb = b;
            float rt = t;
            if (clip.sprint && runBlend > 0f)
            {
                float rs = Mathf.Repeat(cyclePhase, 1f) * clip.sprint.frames.Length;
                int ri = Mathf.FloorToInt(rs); rt = rs - ri;
                ra = clip.sprint.frames[ri]; rb = clip.sprint.frames[(ri + 1) % clip.sprint.frames.Length];
            }
            for (int i = 0; i < bones.Length; i++)
            {
                float x = Mathf.Lerp(rest.x[i], Mathf.Lerp(Mathf.Lerp(a.x[i], b.x[i], t), Mathf.Lerp(ra.x[i], rb.x[i], rt), runBlend), blend);
                float y = Mathf.Lerp(rest.y[i], Mathf.Lerp(Mathf.Lerp(a.y[i], b.y[i], t), Mathf.Lerp(ra.y[i], rb.y[i], rt), runBlend), blend);
                float angle = Mathf.LerpAngle(a.angle[i] * Mathf.Rad2Deg, b.angle[i] * Mathf.Rad2Deg, t);
                float runAngle = Mathf.LerpAngle(ra.angle[i] * Mathf.Rad2Deg, rb.angle[i] * Mathf.Rad2Deg, rt);
                angle = Mathf.LerpAngle(angle, runAngle, runBlend);
                angle = Mathf.LerpAngle(rest.angle[i] * Mathf.Rad2Deg, angle, blend);
                bones[i].localPosition = new Vector3(x / clip.pixelsPerUnit, ((i == 0 ? clip.floor : 0f) - y) / clip.pixelsPerUnit, 0f);
                bones[i].localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }
    }
}

