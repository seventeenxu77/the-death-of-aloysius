using UnityEngine;

namespace Aloysius.Level1
{
    [RequireComponent(typeof(BoxCollider2D),typeof(Level1PropHighlight))]
    public sealed class Level1Draggable : MonoBehaviour
    {
        [SerializeField] float leftBound=-4.6f, rightBound=4.6f;
        BoxCollider2D box;
        Vector3 initialPosition;
        public Level1PropHighlight Highlight { get; private set; }
        public Bounds Bounds => box.bounds;
        public float LeftBound => leftBound;
        public float RightBound => rightBound;
        public void Initialize()
        {
            box=GetComponent<BoxCollider2D>();Highlight=GetComponent<Level1PropHighlight>();initialPosition=transform.position;
        }
        void Awake()=>Initialize();
        public void ResetPosition(){transform.position=initialPosition;Highlight.Held=false;}
        public Vector3 Grip(int side)
        {
            var b=Bounds;
            return new Vector3(side<0?b.min.x:b.max.x,b.max.y-.055f,transform.position.z);
        }
    }
}
