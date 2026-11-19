using System;
using Aloysius.TemplateWhitebox;
using UnityEngine;

namespace Aloysius.KineticDoors
{
    /// <summary>
    /// Two mirrored spatial closed linkages. Each side has two grounded triangular
    /// cranks and two rigid couplers (trapezoid base + small triangular handle).
    /// Four revolute joints and one central spherical joint determine each side.
    /// Input controls the central handle travel; all rigid poses follow from closure.
    /// </summary>
    [ExecuteAlways]
    public sealed class ConstrainedFoldingDoor : MonoBehaviour
    {
        [Serializable]
        public sealed class RigidPiece
        {
            public Transform part;
            public Vector3 bindEdge;
            public Vector3 bindThird;
        }

        [Serializable]
        public sealed class LeafMechanism
        {
            public Transform assembly;
            public float handedness = 1f;
            public RigidPiece upperLarge = new RigidPiece();
            public RigidPiece lowerLarge = new RigidPiece();
            public RigidPiece upperBase = new RigidPiece();
            public RigidPiece lowerBase = new RigidPiece();
            public Transform centralJoint;
        }

        public struct JointGeometry
        {
            public Vector3 top, bottom, upperA, upperB, lowerA, lowerB, handle;
        }

        [Header("Interaction")]
        public Camera playerCamera;
        public KeyCode interactionKey = KeyCode.F;
        public bool driveBothLeaves = true;
        [Min(.5f)] public float interactionDistance = 4f;
        [Min(.1f)] public float animationDuration = 3.2f;
        public bool startsOpen;

        [Header("Rigid linkage geometry")]
        [Min(.1f)] public float squareSide = 1.37f;
        [Min(0f)] public float centralSeam = .025f;
        [Min(0f)] public float bottomClearance = .04f;
        public float panelDepth = -.42f;
        [Min(0f)] public float leftDepthOffset = .055f;
        public LeafMechanism left = new LeafMechanism();
        public LeafMechanism right = new LeafMechanism();

        [Header("Edit-mode pose preview")]
        [Range(0f, 1f)] public float leftOpenness;
        [Range(0f, 1f)] public float rightOpenness;

        float leftTarget, rightTarget;
        public bool IsOpen => leftOpenness > .9999f && rightOpenness > .9999f;
        public bool IsMoving => Application.isPlaying &&
            (Mathf.Abs(leftOpenness - leftTarget) > .0001f || Mathf.Abs(rightOpenness - rightTarget) > .0001f);

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                leftTarget = rightTarget = startsOpen ? 1f : 0f;
                leftOpenness = leftTarget;
                rightOpenness = rightTarget;
            }
            ApplyPose();
        }

        void OnValidate() { ApplyPose(); }

        void Update()
        {
            bool moved = false;
            if (Application.isPlaying)
            {
                if (!playerCamera) playerCamera = Camera.main;
                TemplateFirstPerson player = playerCamera ? playerCamera.GetComponentInParent<TemplateFirstPerson>() : null;
                if (Input.GetKeyDown(interactionKey) && (!player || !player.InputSuspended) && TryAim(out int side))
                {
                    if (driveBothLeaves) Toggle();
                    else if (side < 0) SetLeftOpen(leftTarget < .5f);
                    else SetRightOpen(rightTarget < .5f);
                }
                float step = Time.deltaTime / Mathf.Max(.1f, animationDuration);
                float nextLeft = Mathf.MoveTowards(leftOpenness, leftTarget, step);
                float nextRight = Mathf.MoveTowards(rightOpenness, rightTarget, step);
                moved = nextLeft != leftOpenness || nextRight != rightOpenness;
                leftOpenness = nextLeft;
                rightOpenness = nextRight;
            }
            ApplyPose();
            if (moved) Physics.SyncTransforms();
        }

        [ContextMenu("Open Both / 全部打开")]
        public void Open() { SetOpen(true); }
        [ContextMenu("Close Both / 全部关闭")]
        public void Close() { SetOpen(false); }
        [ContextMenu("Toggle Both / 切换开关")]
        public void Toggle()
        {
            bool targetOpen = Application.isPlaying ? leftTarget > .5f || rightTarget > .5f : leftOpenness > .5f || rightOpenness > .5f;
            SetOpen(!targetOpen);
        }
        public void SetOpen(bool open) { SetLeftOpen(open); SetRightOpen(open); }
        public void SetLeftOpen(bool open)
        {
            leftTarget = open ? 1f : 0f;
            if (!Application.isPlaying) { leftOpenness = leftTarget; ApplyPose(); }
        }
        public void SetRightOpen(bool open)
        {
            rightTarget = open ? 1f : 0f;
            if (!Application.isPlaying) { rightOpenness = rightTarget; ApplyPose(); }
        }

        bool TryAim(out int side)
        {
            side = 1;
            if (!playerCamera) return false;
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hasHit && (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                side = left != null && left.assembly && hit.transform.IsChildOf(left.assembly) ? -1 : 1;
                return true;
            }
            Plane plane = new Plane(transform.forward, transform.TransformPoint(new Vector3(0, squareSide, panelDepth)));
            if (!plane.Raycast(ray, out float distance) || distance < 0f || distance > interactionDistance) return false;
            if (hasHit && hit.distance < distance - .1f) return false;
            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
            side = local.x < 0f ? -1 : 1;
            return Mathf.Abs(local.x) <= squareSide + .15f && local.y >= 0 && local.y <= squareSide * 2f + bottomClearance + .15f;
        }

        public void ApplyPose()
        {
            float a = Mathf.Max(.1f, squareSide);
            ApplyLeaf(left, a, leftOpenness, -1f, panelDepth - leftDepthOffset);
            ApplyLeaf(right, a, rightOpenness, 1f, panelDepth);
        }

        void ApplyLeaf(LeafMechanism leaf, float a, float openness, float side, float depth)
        {
            if (leaf == null || !leaf.assembly) return;
            leaf.assembly.localPosition = new Vector3(side * (a + centralSeam * .5f), bottomClearance, depth);
            leaf.assembly.localRotation = Quaternion.identity;
            JointGeometry g = Solve(a, openness, side);
            Pose(leaf.upperLarge, g.top, g.upperA, g.upperB);
            Pose(leaf.lowerLarge, g.bottom, g.lowerA, g.lowerB);
            Pose(leaf.upperBase, g.upperA, g.upperB, g.handle);
            Pose(leaf.lowerBase, g.lowerA, g.lowerB, g.handle);
            if (leaf.centralJoint) leaf.centralJoint.localPosition = g.handle;
        }

        public static JointGeometry Solve(float a, float openness, float handedness)
        {
            float p = Mathf.Clamp01(openness);
            p = p * p * (3f - 2f * p);
            float handX = Mathf.Lerp(-a, a, p);
            float theta = Mathf.PI * .25f + Mathf.Atan(handX / a);
            float s = Mathf.Sin(theta), c = Mathf.Cos(theta);
            // Equal fixed lengths |HU|=|HV|=a and H.y=a determine H.z.
            // This is linkage closure, not an independently authored depth arc.
            float yDistance = a * (1f - s);
            float xDistance = handX + a * c;
            float depthSquared = a * a - xDistance * xDistance - yDistance * yDistance;
            float z = p <= 0f || p >= 1f ? 0f : -Mathf.Sqrt(Mathf.Max(0f, depthSquared));
            Vector3 Map(float x, float y, float depth) { return new Vector3(x * handedness, y, depth); }
            return new JointGeometry {
                top = Map(0, a * 2f, 0), bottom = Vector3.zero,
                upperA = Map(-a * c, a * 2f - a * s, 0),
                upperB = Map(a * s, a * 2f - a * c, 0),
                lowerA = Map(-a * c, a * s, 0),
                lowerB = Map(a * s, a * c, 0),
                handle = Map(handX, a, z)
            };
        }

        static void Pose(RigidPiece piece, Vector3 origin, Vector3 edge, Vector3 third)
        {
            if (piece == null || !piece.part) return;
            Quaternion bind = Frame(piece.bindEdge, piece.bindThird);
            Quaternion current = Frame(edge - origin, third - origin);
            piece.part.localPosition = origin;
            piece.part.localRotation = current * Quaternion.Inverse(bind);
        }

        static Quaternion Frame(Vector3 edge, Vector3 third)
        {
            return Quaternion.LookRotation(Vector3.Cross(edge, third).normalized, edge.normalized);
        }
    }
}
