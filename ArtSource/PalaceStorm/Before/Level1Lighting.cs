using UnityEngine;

namespace Aloysius.Level1
{
    /// <summary>Scene-local exposure for the painted room and unlit silhouette actor.</summary>
    [ExecuteAlways]
    public sealed class Level1Lighting : MonoBehaviour
    {
        [SerializeField] Renderer room;
        [SerializeField] Renderer[] characterParts;
        [SerializeField] Renderer[] roomProps;
        [SerializeField, Range(0f, 1f), Tooltip("0 = original brightness; 1 = completely black.")]
        float darkness = .84f;
        [SerializeField, Range(0f, 1f)] float edgeShadow = .6f;
        [SerializeField, Range(0f, 1f)] float characterLight = .8f;
        [Header("进入管道后的视野")]
        [SerializeField, Tooltip("进入管道时隐藏房间，只显示管内角色与淡化轮廓。")]
        bool enablePipeVision = true;
        [SerializeField, Min(.01f), Tooltip("进入管道时的画面过渡时间，单位：秒。")]
        float pipeEnterFade = .45f;
        [SerializeField, Min(.01f), Tooltip("离开管道时恢复房间的时间，单位：秒。")]
        float pipeExitFade = .35f;
        [SerializeField, Range(0f,1f), Tooltip("管内底色的亮度，与房间 Darkness 独立。")]
        float pipeInteriorBrightness = .32f;
        [SerializeField, Range(0f,1f), Tooltip("管内人物的亮度。")]
        float pipeCharacterBrightness = .8f;
        [SerializeField, Range(0f,1f), Tooltip("蛇皮壳体残留的透明度；0 表示只保留轮廓。")]
        float pipeShellOpacity = .035f;
        [SerializeField, Range(0f,1f), Tooltip("管道轮廓的透明度。")]
        float pipeOutlineOpacity = .45f;
        [SerializeField, Range(1f,12f), Tooltip("管道轮廓宽度，单位：贴图像素。")]
        float pipeOutlineWidth = 4f;
        [SerializeField] Color pipeInteriorTint = new Color(.65f,.48f,.24f,1f);
        [SerializeField] Level1PalaceCorridor palaceCorridor;
        Level1TraversalController traversal;
        Level1PipeVision pipeVision;
        float pipeBlend;
        public float PipeVisionBlend => pipeBlend;
        MaterialPropertyBlock properties;
        float appliedDarkness = -1f, appliedEdge = -1f, appliedCharacter = -1f;
        static readonly int Exposure = Shader.PropertyToID("_Exposure");
        static readonly int EdgeShadow = Shader.PropertyToID("_EdgeShadow");
        public float Darkness { get => darkness; set { darkness = Mathf.Clamp01(value); Apply(); } }
        public Renderer Room => room;
        public void ConfigureProps(Renderer[] props) { roomProps = props; Apply(); }

        public void Configure(Renderer backdrop, Renderer[] actorParts)
        {
            room = backdrop; characterParts = actorParts; Apply();
        }

        void OnEnable() => Apply();
        void OnDisable()
        {
            pipeBlend=0;
            Apply();
            pipeVision?.Dispose();pipeVision=null;
        }
        void OnValidate() { darkness = Mathf.Clamp01(darkness); Apply(); }
        void LateUpdate()
        {
            if(Application.isPlaying){TickPipeVision(Time.deltaTime);return;}
            if (darkness != appliedDarkness || edgeShadow != appliedEdge || characterLight != appliedCharacter) Apply();
        }

        public void TickPipeVision(float dt)
        {
            if(!traversal)traversal=FindObjectOfType<Level1TraversalController>();
            var pipe=traversal?traversal.PipeRenderer:null;
            bool inside=enablePipeVision && traversal && traversal.isActiveAndEnabled
                && traversal.State==Level1TraversalState.InsidePipe && pipe && pipe.enabled && pipe.gameObject.activeInHierarchy;
            pipeBlend=Mathf.MoveTowards(pipeBlend,inside?1f:0f,Mathf.Max(0,dt)/Mathf.Max(.01f,inside?pipeEnterFade:pipeExitFade));
            if(pipeVision!=null && pipeVision.Pipe!=pipe){pipeVision.Dispose();pipeVision=null;}
            if(pipe && pipeVision==null)pipeVision=new Level1PipeVision(pipe,characterParts);
            Apply();
        }

        public void Apply()
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            float brightness = 1f - darkness;
            if (room)
            {
                room.GetPropertyBlock(properties);
                properties.SetFloat(Exposure, brightness * (1f-pipeBlend));
                properties.SetFloat(EdgeShadow, edgeShadow);
                room.SetPropertyBlock(properties);
            }
            if (characterParts != null)
                foreach (Renderer part in characterParts)
                {
                    if (!part) continue;
                    // Preserve the walker's _Turn block, including cloth lag and anchoring.
                    part.GetPropertyBlock(properties);
                    properties.SetFloat(Exposure, Mathf.Lerp(palaceCorridor && palaceCorridor.Arrived ? palaceCorridor.CharacterExposure : brightness * characterLight,pipeCharacterBrightness,pipeBlend));
                    part.SetPropertyBlock(properties);
                }
            if (roomProps != null)
                foreach (Renderer prop in roomProps)
                {
                    if (!prop) continue;
                    prop.GetPropertyBlock(properties);
                    bool isPipe=pipeVision!=null && prop==pipeVision.Pipe;
                    properties.SetFloat(Exposure, brightness * (isPipe?1f:1f-pipeBlend));
                    properties.SetFloat("_SceneVisibility",isPipe?1f:1f-pipeBlend);
                    prop.SetPropertyBlock(properties);
                }
            pipeVision?.Apply(pipeBlend,pipeInteriorBrightness,pipeShellOpacity,pipeOutlineOpacity,pipeOutlineWidth,pipeInteriorTint);
            appliedDarkness = darkness; appliedEdge = edgeShadow; appliedCharacter = characterLight;
        }
    }
}
