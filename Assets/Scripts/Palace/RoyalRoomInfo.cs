using UnityEngine;

namespace Aloysius.RoyalScene
{
    [DisallowMultipleComponent]
    public sealed class RoyalRoomInfo : MonoBehaviour
    {
        public string areaId;
        [TextArea(2, 6)] public string description;
        [TextArea(2, 4)] public string unlockCondition;
        public int mainFloor;
    }
}
