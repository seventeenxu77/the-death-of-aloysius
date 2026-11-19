using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Aloysius.Level1
{
    public enum GuardComicShot { Hidden, Watching, Reaction }

    [DefaultExecutionOrder(200)]
    public sealed class Level1GuardComic : MonoBehaviour
    {
        [Header("编辑器排版预览")]
        [SerializeField, Tooltip("仅在未运行游戏时完整显示两张图，便于调整。游戏开始时始终隐藏，不受此开关影响。")]
        bool previewBothInEditor = true;
        [Header("触发对象")]
        [SerializeField] Level1DragController dragController;
        [SerializeField] Level1TraversalController traversal;
        [SerializeField] Level1Draggable bed;
        [Header("已确认的守卫素材")]
        [SerializeField] Texture2D watchingImage;
        [SerializeField] Texture2D reactionStill;
        [SerializeField] VideoClip reactionAnimation;
        [Header("漫画位置（1920 × 1080 参考画布）")]
        [SerializeField, Tooltip("左上为 (0,1)，右上为 (1,1)。")] Vector2 anchor = new Vector2(0, 1);
        [SerializeField] Vector2 offset = new Vector2(64, -66);
        [SerializeField, Range(280, 900)] float panelWidth = 400;
        [SerializeField, Range(-12, 12)] float tilt = 3.5f;
        [Header("第二张察觉图（叠在正常守卫图前方）")]
        [SerializeField, Tooltip("察觉图最终宽度 / 正常图宽度。"), Range(1, 2)] float alertSizeRatio = 1.4f;
        [SerializeField, Tooltip("相对正常图的位置，以正常图宽度为单位。X 向右，Y 向上。")]
        Vector2 alertOffset = new Vector2(.8f, -.15f);
        [SerializeField, Range(-12, 12)] float alertTilt = -2;
        [SerializeField, Tooltip("在管道内连续实际移动超过此秒数，察觉图才出现；停止移动会重置计时。"), Min(0)] float alertMoveDelay = 2;
        [SerializeField, Tooltip("察觉图出现后，连续静止满此秒数才消失；继续移动会重置静止计时。"), Min(0)] float alertStopDelay = 1;
        [SerializeField, Tooltip("管道路径坐标的实际变化阈值，不依赖按键。"), Min(.0000001f)] float pipeMovementThreshold = .000001f;
        [Header("白纸漫画边框")]
        [SerializeField, Range(8, 80)] float paperBorder = 22;
        [SerializeField] Color paperColor = new Color(.965f, .96f, .90f, 1);
        [SerializeField, Range(1, 12)] float inkWidth = 5;
        [SerializeField, Range(1, 8)] float innerInkWidth = 2.8f;
        [SerializeField, Range(0, 3)] float paperRoughness = 1.1f;
        [SerializeField] bool cornerMarks = true;
        [Header("硬边投影")]
        [SerializeField] Vector2 shadowOffset = new Vector2(6, -7);
        [SerializeField] Color shadowColor = new Color(.055f, .025f, .012f, 1);
        [Header("人物墨色（静帧和动画统一处理）")]
        [SerializeField] Material portraitInkMaterial;
        [SerializeField, Range(0, .2f)] float shadowBlackPoint = .045f;
        [SerializeField, Range(1, 2)] float portraitContrast = 1.2f;
        [SerializeField, Range(0, 4)] float portraitInkSpread = 2;
        [SerializeField, Range(0, 1)] float portraitInkStrength = .85f;
        [Header("出现与停留")]
        [SerializeField, Min(.05f)] float enterDuration = .24f;
        [SerializeField, Min(.05f)] float exitDuration = .25f;
        [SerializeField, Range(.4f, 2)] float reactionSpeed = 1;
        [SerializeField, Tooltip("察觉图内部视频的短促擦入时间。"), Min(.01f)] float shotTransition = .18f;
        [SerializeField, Tooltip("必须实际拖动床才触发，单纯碰撞或走向床不会触发。"), Min(.00001f)] float movementThreshold = .0001f;

        public GuardComicShot CurrentShot { get; private set; }
        public int WatchingCount { get; private set; }
        public int ReactionCount { get; private set; }
        public bool VideoHasFrame => video && video.isPrepared && video.frame >= 0;
        public float Visibility => group ? group.alpha : 0;
        public bool VideoFailed { get; private set; }
        public bool ExternalClock { get; set; }
        public Level1Draggable Bed => bed;
        public VideoPlayer Player => video;

        Canvas canvas;
        CanvasGroup group;
        CanvasGroup alertGroup;
        RectTransform watchingPanel;
        RawImage watchingPortrait;
        Image watchingShade;
        Level1ComicFrame watchingFrame;
        RectTransform panel, reveal;
        RawImage still, movie;
        Image shade;
        Material runtimeInk;
        Level1ComicFrame frame;
        VideoPlayer video;
        RenderTexture videoTexture;
        Vector3 lastBedPosition;
        Level1TraversalState lastTraversal;
        float visible, reactionClock, revealClock;
        float lastPipePosition, alertAmount, stopClock, moveClock;
        bool alertWanted, freshVideoFrame;
        bool wanted, reactionStarted, reactionFinished, enteredPipeThisSequence;
        bool presentationIsPreview;

        public void Configure(Level1DragController drag, Level1TraversalController pipe, Level1Draggable target,
            Texture2D watch, Texture2D reaction, VideoClip animation)
        { dragController = drag; traversal = pipe; bed = target; watchingImage = watch; reactionStill = reaction; reactionAnimation = animation; }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            ReleasePresentation();
            BuildPresentation();
            if (dragController) dragController.ResetPerformed += ResetSequence;
            ResetSequence();
        }
        void LateUpdate()
        {
            if (ExternalClock) return;
            Tick(Time.deltaTime);
        }
        public void Tick(float dt)
        {
            if (!group) return;
            dt = Mathf.Max(0, dt);
            bool dragging = dragController && dragController.isActiveAndEnabled && bed && bed.isActiveAndEnabled
                && dragController.IsDragging && dragController.HeldObject == bed;
            bool moved = dragging && Vector3.Distance(bed.transform.position, lastBedPosition) > movementThreshold;
            Level1TraversalState state = traversal && traversal.isActiveAndEnabled ? traversal.State : Level1TraversalState.Normal;
            bool entered = state == Level1TraversalState.InsidePipe && lastTraversal != Level1TraversalState.InsidePipe;
            float pipePosition = traversal ? traversal.PipePathPosition : 0;
            bool pipeMoved = state == Level1TraversalState.InsidePipe && !entered
                && Mathf.Abs(pipePosition - lastPipePosition) > pipeMovementThreshold;
            lastPipePosition = pipePosition;
            if (bed) lastBedPosition = bed.transform.position;
            lastTraversal = state;
            bool atPipe = state == Level1TraversalState.InsidePipe || state == Level1TraversalState.Catching
                || state == Level1TraversalState.Hanging || state == Level1TraversalState.Pulling;
            // Returning to the lip still belongs to the same pipe visit. Dismiss only
            // after the player lets go / finishes leaving all pipe traversal states.
            if (enteredPipeThisSequence && !atPipe)
            { wanted = false; enteredPipeThisSequence = false; }
            if (entered) enteredPipeThisSequence = true;
            if (moved || entered)
            {
                if (!wanted) WatchingCount++;
                wanted = true;
            }
            bool eligible = wanted && state == Level1TraversalState.InsidePipe;
            moveClock = eligible && pipeMoved ? moveClock + dt : 0;
            stopClock = eligible && !pipeMoved ? stopClock + dt : 0;
            bool nextAlert = eligible && (alertWanted
                ? pipeMoved || stopClock < alertStopDelay
                : pipeMoved && moveClock > alertMoveDelay);
            if (nextAlert && !alertWanted) ShowReaction();
            alertWanted = nextAlert;
            alertAmount = alertWanted ? 1 : 0;
            alertGroup.alpha = alertAmount;
            // Show at the exact authored size and position; keep the normal panel behind it.
            if (alertAmount > 0)
            {
                reactionClock += dt;
                if (!reactionStarted && !reactionFinished && video && video.isPrepared && !VideoFailed)
                { video.time = 0; video.Play(); reactionStarted = true; }
                if (VideoHasFrame && freshVideoFrame && reactionStarted && !VideoFailed)
                {
                    movie.enabled = true;
                    revealClock += dt;
                    reveal.anchorMax = new Vector2(Mathf.SmoothStep(0, 1, revealClock / Mathf.Max(.01f, shotTransition)), 1);
                }
                // Playback completion is not a dismissal trigger. Keep the last
                // decoded frame, or the reaction still if decoding is unavailable.
                float length = reactionAnimation ? (float)reactionAnimation.length / Mathf.Max(.1f, reactionSpeed) : 5.2f;
                if (!reactionFinished && reactionClock > length + 3)
                {
                    reactionFinished = true;
                    if (video) video.Pause();
                    if (!VideoHasFrame) { movie.enabled = false; still.texture = reactionStill; }
                }
            }
            else
            {
                if (video && video.isPlaying) video.Pause();
                movie.enabled = false;
            }
            visible = Mathf.MoveTowards(visible, wanted ? 1 : 0, dt / Mathf.Max(.01f, wanted ? enterDuration : exitDuration));
            group.alpha = Mathf.SmoothStep(0, 1, visible);
            CurrentShot = visible == 0 ? GuardComicShot.Hidden
                : alertAmount > 0 ? GuardComicShot.Reaction : GuardComicShot.Watching;
            Layout();
            if (!wanted && visible == 0)
            {
                CurrentShot = GuardComicShot.Hidden;
                if (video && video.isPlaying) video.Pause();
                movie.enabled = false;
            }
        }
        void ShowReaction()
        {
            CurrentShot = GuardComicShot.Reaction; ReactionCount++;
            wanted = true; reactionClock = revealClock = 0;
            reactionStarted = reactionFinished = false;
            freshVideoFrame = false;
            still.texture = reactionStill;
            movie.enabled = false; reveal.anchorMax = new Vector2(0, 1);
            if (video && video.isPrepared) { video.Pause(); video.time = 0; }
        }
        public void ResetSequence()
        {
            CurrentShot = GuardComicShot.Hidden; wanted = false;
            visible = reactionClock = revealClock = 0;
            alertAmount = stopClock = moveClock = 0; alertWanted = freshVideoFrame = false;
            reactionStarted = reactionFinished = enteredPipeThisSequence = false;
            WatchingCount = ReactionCount = 0;
            if (bed) lastBedPosition = bed.transform.position;
            lastTraversal = traversal ? traversal.State : Level1TraversalState.Normal;
            lastPipePosition = traversal ? traversal.PipePathPosition : 0;
            if (group) group.alpha = 0;
            if (alertGroup) alertGroup.alpha = 0;
            if (movie) movie.enabled = false;
            if (video && video.isPlaying) video.Pause();
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        void BuildPresentation(bool editorPreview = false)
        {
            presentationIsPreview = editorPreview;
            var root = new GameObject("Comic canvas | no input blocking", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (editorPreview) root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false; group.alpha = 0;
            watchingPanel = Rect("01 | Normal guard | back small panel", root.transform);
            var normalShadow = Rect("Solid offset ink shadow", watchingPanel); Stretch(normalShadow);
            watchingShade = normalShadow.gameObject.AddComponent<Image>(); watchingShade.raycastTarget = false;
            var normalPicture = Rect("Normal guard portrait", watchingPanel); Stretch(normalPicture);
            watchingPortrait = normalPicture.gameObject.AddComponent<RawImage>();
            watchingPortrait.texture = watchingImage; watchingPortrait.raycastTarget = false;
            var normalBorder = Rect("Hand inked white paper border", watchingPanel); Stretch(normalBorder);
            watchingFrame = normalBorder.gameObject.AddComponent<Level1ComicFrame>(); watchingFrame.raycastTarget = false;
            panel = Rect("02 | Alert guard | front panel", root.transform);
            alertGroup = panel.gameObject.AddComponent<CanvasGroup>();
            alertGroup.blocksRaycasts = alertGroup.interactable = false; alertGroup.alpha = 0;
            var shadow = Rect("Solid offset ink shadow", panel); Stretch(shadow);
            shadow.anchoredPosition = shadowOffset;
            shade = shadow.gameObject.AddComponent<Image>(); shade.color = shadowColor; shade.raycastTarget = false;
            var picture = Rect("Guard portrait", panel); Stretch(picture);
            still = picture.gameObject.AddComponent<RawImage>(); still.texture = reactionStill; still.raycastTarget = false;
            // This clip crops the reveal; the video keeps its full portrait size.
            reveal = Rect("Reaction reveal", picture); Stretch(reveal); reveal.gameObject.AddComponent<RectMask2D>();
            var movieRect = Rect("Head sway and mouth animation", reveal);
            movieRect.anchorMin = movieRect.anchorMax = new Vector2(0, .5f); movieRect.pivot = new Vector2(0, .5f);
            movie = movieRect.gameObject.AddComponent<RawImage>(); movie.raycastTarget = false; movie.enabled = false;
            if (portraitInkMaterial)
            {
                runtimeInk = new Material(portraitInkMaterial) { name = "Guard portrait ink | runtime" };
                watchingPortrait.material = still.material = movie.material = runtimeInk;
            }
            var border = Rect("Hand inked paper border", panel); Stretch(border);
            frame = border.gameObject.AddComponent<Level1ComicFrame>(); frame.raycastTarget = false;
            if (editorPreview)
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                if (runtimeInk) runtimeInk.hideFlags = HideFlags.HideAndDontSave;
                group.alpha = alertGroup.alpha = 1; alertAmount = 1;
                Layout();
                return;
            }
            videoTexture = new RenderTexture(1080, 810, 0, RenderTextureFormat.ARGB32) { name = "Guard comic video", filterMode = FilterMode.Bilinear };
            videoTexture.Create(); movie.texture = videoTexture;
            video = root.AddComponent<VideoPlayer>(); video.playOnAwake = false; video.isLooping = false;
            video.source = VideoSource.VideoClip; video.clip = reactionAnimation; video.audioOutputMode = VideoAudioOutputMode.None;
            video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = videoTexture;
            video.waitForFirstFrame = true; video.skipOnDrop = true; video.playbackSpeed = reactionSpeed;
            video.loopPointReached += OnVideoEnd; video.errorReceived += OnVideoError;
            video.sendFrameReadyEvents = true; video.frameReady += OnVideoFrame;
            if (reactionAnimation) video.Prepare(); else VideoFailed = true;
            Layout();
        }
        void Layout()
        {
            Vector2 basePosition = offset + new Vector2(-20, 14) * (1 - group.alpha);
            LayoutPanel(watchingPanel, watchingPortrait, watchingShade, watchingFrame, 1, basePosition,
                tilt, Mathf.Lerp(.965f, 1, group.alpha));
            Vector2 size = LayoutPanel(panel, still, shade, frame, alertSizeRatio,
                offset + alertOffset * panelWidth, alertTilt, 1);
            movie.rectTransform.sizeDelta = size;
            if (runtimeInk)
            {
                runtimeInk.SetFloat("_BlackPoint", shadowBlackPoint); runtimeInk.SetFloat("_Contrast", portraitContrast);
                runtimeInk.SetFloat("_InkSpread", portraitInkSpread); runtimeInk.SetFloat("_InkStrength", portraitInkStrength);
            }
            if (video) video.playbackSpeed = reactionSpeed;
        }
        Vector2 LayoutPanel(RectTransform rect, RawImage portrait, Image shadow, Level1ComicFrame border,
            float ratio, Vector2 position, float angle, float scale)
        {
            float w = panelWidth * ratio, b = Mathf.Min(paperBorder, panelWidth * .2f) * ratio;
            float h = (w - b * 2) * .75f + b * 2;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0, 0, angle); rect.localScale = Vector3.one * scale;
            portrait.rectTransform.offsetMin = Vector2.one * b; portrait.rectTransform.offsetMax = -Vector2.one * b;
            shadow.rectTransform.anchoredPosition = shadowOffset * ratio; shadow.color = shadowColor;
            float outer = inkWidth * ratio, inner = innerInkWidth * ratio, rough = paperRoughness * ratio;
            if (border.Border != b || border.Paper != paperColor || border.InkWidth != outer
                || border.InnerInkWidth != inner || border.Roughness != rough || border.CornerMarks != cornerMarks)
            {
                border.Border = b; border.Paper = paperColor; border.InkWidth = outer; border.InnerInkWidth = inner;
                border.Roughness = rough; border.CornerMarks = cornerMarks; border.SetVerticesDirty();
            }
            return new Vector2(w - b * 2, h - b * 2);
        }
        void OnVideoFrame(VideoPlayer source, long frameIndex) { if (reactionStarted) freshVideoFrame = true; }
        void OnVideoEnd(VideoPlayer source) { reactionFinished = true; source.Pause(); }
        void OnVideoError(VideoPlayer source, string message)
        { VideoFailed = true; reactionFinished = true; movie.enabled = false; if (CurrentShot == GuardComicShot.Reaction) still.texture = reactionStill; Debug.LogWarning("[GuardComic] Video unavailable; showing reaction still. " + message, this); }
        void OnDisable()
        {
            if (dragController) dragController.ResetPerformed -= ResetSequence;
            ReleasePresentation();
        }
        void ReleasePresentation()
        {
            if (video) { video.loopPointReached -= OnVideoEnd; video.errorReceived -= OnVideoError; video.frameReady -= OnVideoFrame; video.Stop(); }
            if (canvas) DisposePresentationObject(canvas.gameObject);
            if (videoTexture) { videoTexture.Release(); DisposePresentationObject(videoTexture); }
            if (runtimeInk) { DisposePresentationObject(runtimeInk); runtimeInk = null; }
            canvas = null; group = alertGroup = null; video = null; videoTexture = null;
            VideoFailed = false; presentationIsPreview = false;
        }
        void DisposePresentationObject(Object target)
        {
            if (!Application.isPlaying || presentationIsPreview) DestroyImmediate(target);
            else Destroy(target);
        }
#if UNITY_EDITOR
        public bool RefreshEditorPreview()
        {
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (!previewBothInEditor || !isActiveAndEnabled)
            { ClearEditorPreview(); return false; }
            if (!canvas) BuildPresentation(true);
            if (!presentationIsPreview) return false;
            group.alpha = alertGroup.alpha = 1; alertAmount = 1;
            watchingPortrait.texture = watchingImage; still.texture = reactionStill;
            Layout();
            return true;
        }
        public void ClearEditorPreview()
        { if (presentationIsPreview) ReleasePresentation(); }
#endif
    }
}
