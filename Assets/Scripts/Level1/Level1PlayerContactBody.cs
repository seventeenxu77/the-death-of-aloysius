using UnityEngine;
using Aloysius.GothicStage;

namespace Aloysius.Level1
{
    /// <summary>Body contact box follows the animated head and feet, excluding trailing cloth.</summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class Level1PlayerContactBody : MonoBehaviour
    {
        [SerializeField] GothicBride2DWalker walker;
        [SerializeField] BoxCollider2D bodyBox;
        [SerializeField] Transform head, nearFoot, farFoot;
        [SerializeField, Min(.1f)] float bodyWidth = .56f;
        public BoxCollider2D BodyBox => bodyBox;

        public void Configure(GothicBride2DWalker actor)
        {
            walker = actor; bodyBox = GetComponent<BoxCollider2D>();
            foreach (Transform bone in actor.Bones)
            {
                if (bone.name == "head") head = bone;
                else if (bone.name == "near_foot") nearFoot = bone;
                else if (bone.name == "far_foot") farFoot = bone;
            }
            var body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.useFullKinematicContacts = true;
            body.simulated = true;
            bodyBox.isTrigger = true;
            FitToPose();
        }

        void Awake()
        {
            if (!walker) walker = GetComponent<GothicBride2DWalker>();
            if (walker) Configure(walker);
        }
        void LateUpdate() => FitToPose();

        float Sole(Transform foot)
        {
            float ppu = walker.Clip.pixelsPerUnit;
            return Mathf.Min(foot.TransformPoint(new Vector3(-10f, -25f, 0f) / ppu).y,
                foot.TransformPoint(new Vector3(44f, -25f, 0f) / ppu).y);
        }

        public void FitToPose()
        {
            if (!bodyBox || !walker || !head || !nearFoot || !farFoot) return;
            if(walker.SpecialPoseActive)
            {
                float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
                foreach(var bone in walker.Bones)
                {
                    if(bone.name!="head"&&bone.name!="pelvis"&&bone.name!="near_hand"&&bone.name!="far_hand"&&bone.name!="near_foot"&&bone.name!="far_foot")continue;
                    Vector3 p=transform.InverseTransformPoint(bone.position);p.x*=walker.Facing;
                    float radius=bone.name=="head"?.20f:bone.name=="pelvis"?.12f:.045f;
                    minX=Mathf.Min(minX,p.x-radius);maxX=Mathf.Max(maxX,p.x+radius);minY=Mathf.Min(minY,p.y-radius);maxY=Mathf.Max(maxY,p.y+radius);
                }
                bodyBox.size=new Vector2(maxX-minX,maxY-minY);bodyBox.offset=new Vector2((minX+maxX)*.5f,(minY+maxY)*.5f);return;
            }
            float bottom = Mathf.Min(Sole(nearFoot), Sole(farFoot));
            float top = head.position.y + .24f * Mathf.Abs(transform.lossyScale.y);
            Vector3 low = transform.InverseTransformPoint(new Vector3(transform.position.x, bottom, transform.position.z));
            Vector3 high = transform.InverseTransformPoint(new Vector3(transform.position.x, top, transform.position.z));
            bodyBox.size = new Vector2(bodyWidth, Mathf.Max(.5f, high.y - low.y));
            bodyBox.offset = new Vector2(.03f * walker.BodyWidth, (low.y + high.y) * .5f);
        }
    }
}
