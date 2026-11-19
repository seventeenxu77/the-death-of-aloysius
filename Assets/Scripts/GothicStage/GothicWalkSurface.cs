using UnityEngine;
namespace Aloysius.GothicStage
{
    // Optional height profile for stairs. Ordinary one-way beds remain unchanged.
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GothicWalkSurface : MonoBehaviour
    {
        [Tooltip("Local-space walking line, ordered left to right. Smooth incline under the painted stair treads.")]
        public Vector2[] points;
        public bool TryHeight(float worldX,out float height)
        {
            height=transform.position.y;
            if(points==null||points.Length<2)return false;
            float x=transform.InverseTransformPoint(new Vector3(worldX,transform.position.y,0)).x;
            if(x<points[0].x-.001f||x>points[points.Length-1].x+.001f)return false;
            for(int i=1;i<points.Length;i++)
            {
                if(x>points[i].x)continue;
                float t=Mathf.InverseLerp(points[i-1].x,points[i].x,x);
                height=transform.TransformPoint(new Vector3(x,Mathf.Lerp(points[i-1].y,points[i].y,t),0)).y;
                return true;
            }
            return false;
        }
    }
}
