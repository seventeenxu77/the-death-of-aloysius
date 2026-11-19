using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Aloysius.GothicStage;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1GuardComicEditor
    {
        const string ScenePath = "Assets/Scenes/level1.unity";
        const string ReportDir = "Logs/Level1/GuardComic";
        const string RequestPath = ReportDir + "/request.txt";
        static IEnumerator validation;
        static readonly List<string> checks = new List<string>();
        static double nextPoll;
        static Level1GuardComicEditor()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("GuardComicValidate", false))
                { checks.Clear(); validation = ValidatePlay(); }
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("GuardComicValidate", false))
                { SessionState.SetBool("GuardComicValidate", false); validation = null; }
            };
        }
        static void Poll()
        {
            if (validation != null)
            {
                try { if (!validation.MoveNext()) { validation = null; EditorApplication.isPlaying = false; } }
                catch (Exception e) { validation = null; Report(false, e.ToString()); EditorApplication.isPlaying = false; }
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (!File.Exists(RequestPath)) return;
            string command = File.ReadAllText(RequestPath).Trim(); File.Delete(RequestPath);
            try
            {
                if (command == "install") Install();
                else if (command == "validate") Validate();
                else if (command == "build") Build();
                else if (command == "install-palace") Level1PalaceCorridorEditor.Install();
                else if (command == "restyle-room") Level1GuardRoomRestyleEditor.Install();
                else if (command == "thicken-frame") ThickenFrame();
                else if (command == "ink-frame") ApplyInkFrame();
                else if (command == "reference-white-frame") ApplyReferenceWhiteFrame();
                else if (command == "two-panel-layout") ApplyTwoPanelLayout();
                else if (command == "preview-layout") EnableLayoutPreview();
                else if (command == "alert-timing") ApplyAlertTiming();
                else if (command == "capture-validate") { SessionState.SetBool("GuardComicCapture", true); Validate(); }
            }
            catch (Exception e) { Directory.CreateDirectory(ReportDir); File.WriteAllText(ReportDir + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        public static void ApplyAlertTiming()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply in Edit mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Directory.CreateDirectory("ArtSource/GuardComic/Integration");
            if (!EditorSceneManager.SaveScene(scene, "ArtSource/GuardComic/Integration/level1-before-alert-timing.unity", true))
                throw new IOException("Could not back up current scene.");
            var comic = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Level1GuardComic>(true)).Single();
            Undo.RecordObject(comic, "Set guard alert timing");
            var settings = new SerializedObject(comic);
            settings.FindProperty("alertMoveDelay").floatValue = 2;
            settings.FindProperty("alertStopDelay").floatValue = 1;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save alert timing.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/AlertTiming.txt", "Saved timing only; user layout preserved. " + DateTime.Now.ToString("O"));
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        [MenuItem("Aloysius/Level1/Show Both Comics For Layout")]
        public static void EnableLayoutPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Adjust layout in Edit mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var comic = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Level1GuardComic>(true)).Single();
            Undo.RecordObject(comic, "Show both comics for layout");
            var settings = new SerializedObject(comic);
            settings.FindProperty("previewBothInEditor").boolValue = true;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save preview setting.");
            Selection.activeGameObject = comic.gameObject;
            EditorGUIUtility.PingObject(comic);
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/LayoutPreview.txt", "Editor preview enabled; runtime starts hidden. " + DateTime.Now.ToString("O"));
        }
        [MenuItem("Aloysius/Level1/Apply Two Panel Comic Layout")]
        public static void ApplyTwoPanelLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply in Edit mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Directory.CreateDirectory("ArtSource/GuardComic/Integration");
            if (!EditorSceneManager.SaveScene(scene, "ArtSource/GuardComic/Integration/level1-before-two-panels.unity", true))
                throw new IOException("Could not back up current scene.");
            var comic = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Level1GuardComic>(true)).Single();
            Undo.RecordObject(comic, "Arrange two guard comic panels");
            var settings = new SerializedObject(comic);
            settings.FindProperty("anchor").vector2Value = new Vector2(0, 1);
            settings.FindProperty("offset").vector2Value = new Vector2(64, -66);
            settings.FindProperty("panelWidth").floatValue = 400;
            settings.FindProperty("tilt").floatValue = 3.5f;
            settings.FindProperty("paperBorder").floatValue = 22;
            settings.FindProperty("alertSizeRatio").floatValue = 1.4f;
            settings.FindProperty("alertOffset").vector2Value = new Vector2(.8f, -.15f);
            settings.FindProperty("alertTilt").floatValue = -2;
            settings.FindProperty("alertMoveDelay").floatValue = 2;
            settings.FindProperty("alertStopDelay").floatValue = 1;
            settings.FindProperty("pipeMovementThreshold").floatValue = .000001f;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save comic layout.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/TwoPanelLayout.txt", "Saved two-panel layout " + DateTime.Now.ToString("O"));
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        [MenuItem("Aloysius/Level1/Install Guard Comic")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install in Edit mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            // Save a copy of the current in-memory scene, including the user's unsaved layout.
            Directory.CreateDirectory("ArtSource/GuardComic/Integration");
            if (!EditorSceneManager.SaveScene(scene, "ArtSource/GuardComic/Integration/level1-live-before.unity", true))
                throw new IOException("Could not back up the current live scene.");
            var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .ToDictionary(t => t, t => t.localToWorldMatrix);
            foreach (string name in new[] { "GuardWatching", "GuardReactionStill" })
            {
                string path = "Assets/Art/Textures/Level1/GuardComic/" + name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!importer) throw new InvalidOperationException("Texture not imported: " + path);
                importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
            }
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var drag = components.OfType<Level1DragController>().Single();
            var traversal = components.OfType<Level1TraversalController>().Single();
            var bed = components.OfType<Level1Draggable>().Single(b => b.name == "Bed");
            var comic = components.OfType<Level1GuardComic>().SingleOrDefault();
            if (!comic)
            {
                var go = new GameObject("Guard Comic | bed drag and pipe reaction");
                SceneManager.MoveGameObjectToScene(go, scene); Undo.RegisterCreatedObjectUndo(go, "Add guard comic");
                comic = go.AddComponent<Level1GuardComic>();
            }
            Undo.RecordObject(comic, "Configure guard comic");
            comic.Configure(drag, traversal, bed,
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Level1/GuardComic/GuardWatching.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Level1/GuardComic/GuardReactionStill.png"),
                AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Art/Video/Level1/GuardReaction.mp4"));
            foreach (var pair in transforms)
                if (pair.Key.localToWorldMatrix != pair.Value) throw new InvalidOperationException("Unexpected scene layout change: " + pair.Key.name);
            EditorUtility.SetDirty(comic); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/Installed.json", "{\"installed\":true,\"existingTransformsPreserved\":true,\"objectsChecked\":" + transforms.Count + ",\"scene\":\"" + ScenePath + "\"}");
            if (opened) EditorSceneManager.CloseScene(scene, true);
            else Selection.activeGameObject = comic.gameObject;
            Debug.Log("[GuardComic] Installed and saved level1; existing transforms preserved.");
        }
        [MenuItem("Aloysius/Level1/Apply Solid Thick Comic Frame")]
        public static void ThickenFrame()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene, "ArtSource/GuardComic/Integration/level1-before-thick-frame.unity", true))
                throw new IOException("Could not back up scene before frame change.");
            var comic = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Level1GuardComic>(true)).Single();
            Undo.RecordObject(comic, "Thicken comic frame");
            var settings = new SerializedObject(comic);
            settings.FindProperty("paperBorder").floatValue = 42;
            settings.FindProperty("paperColor").colorValue = new Color(.98f, .965f, .91f, 1);
            settings.FindProperty("inkWidth").floatValue = 4;
            settings.FindProperty("paperRoughness").floatValue = 0;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Frame scene save failed.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/ThickFrame.json", "{\"saved\":true,\"paperBorder\":42,\"inkWidth\":4,\"roughness\":0}");
            if (opened) EditorSceneManager.CloseScene(scene, true);
            else Selection.activeGameObject = comic.gameObject;
            Debug.Log("[GuardComic] Solid thick frame saved.");
        }
        [MenuItem("Aloysius/Level1/Apply Ochre Ink Comic Style")]
        public static void ApplyInkFrame()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene, "ArtSource/GuardComic/Integration/level1-before-ochre-ink.unity", true))
                throw new IOException("Could not back up scene before ink revision.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/Level1/GuardComicInk.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Portrait ink shader failed compilation.");
            const string materialPath = "Assets/Art/Materials/Level1/GuardComicInk.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            var comic = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Level1GuardComic>(true)).Single();
            Undo.RecordObject(comic, "Apply ochre frame and strong inks");
            var settings = new SerializedObject(comic);
            settings.FindProperty("paperBorder").floatValue = 32;
            settings.FindProperty("paperColor").colorValue = new Color(.71f, .545f, .29f, 1);
            settings.FindProperty("inkWidth").floatValue = 7;
            settings.FindProperty("innerInkWidth").floatValue = 3.2f;
            settings.FindProperty("paperRoughness").floatValue = .7f;
            settings.FindProperty("shadowOffset").vector2Value = new Vector2(10, -11);
            settings.FindProperty("shadowColor").colorValue = new Color(.055f, .025f, .012f, 1);
            settings.FindProperty("portraitInkMaterial").objectReferenceValue = material;
            settings.FindProperty("shadowBlackPoint").floatValue = .045f;
            settings.FindProperty("portraitContrast").floatValue = 1.2f;
            settings.FindProperty("portraitInkSpread").floatValue = 2;
            settings.FindProperty("portraitInkStrength").floatValue = .85f;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Ink frame scene save failed.");
            File.WriteAllText(ReportDir + "/InkFrame.json", "{\"saved\":true,\"ochreFrame\":true,\"opaqueHardShadow\":true,\"unifiedPortraitInk\":true}");
            if (opened) EditorSceneManager.CloseScene(scene, true); else Selection.activeGameObject = comic.gameObject;
            Debug.Log("[GuardComic] Ochre border, hard shadow and portrait ink saved.");
        }
        [MenuItem("Aloysius/Level1/Apply Reference White Comic Frame")]
        public static void ApplyReferenceWhiteFrame()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene, "ArtSource/GuardComic/Integration/level1-before-reference-white.unity", true))
                throw new IOException("Could not back up scene before white frame revision.");
            var comic = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Level1GuardComic>(true)).Single();
            Undo.RecordObject(comic, "Restore reference white comic frame");
            var settings = new SerializedObject(comic);
            settings.FindProperty("paperBorder").floatValue = 30;
            settings.FindProperty("paperColor").colorValue = new Color(.965f, .96f, .90f, 1);
            settings.FindProperty("inkWidth").floatValue = 5;
            settings.FindProperty("innerInkWidth").floatValue = 2.8f;
            settings.FindProperty("paperRoughness").floatValue = 1.1f;
            settings.FindProperty("cornerMarks").boolValue = true;
            settings.FindProperty("shadowOffset").vector2Value = new Vector2(6, -7);
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("White frame scene save failed.");
            File.WriteAllText(ReportDir + "/ReferenceWhiteFrame.json", "{\"saved\":true,\"ivoryPaperFrame\":true,\"cornerInkMarks\":true,\"portraitInkPreserved\":true}");
            if (opened) EditorSceneManager.CloseScene(scene, true); else Selection.activeGameObject = comic.gameObject;
            Debug.Log("[GuardComic] Reference white paper frame saved; portrait ink unchanged.");
        }
        [MenuItem("Aloysius/Level1/Validate Guard Comic")]
        public static void Validate()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open level1 to run its Play-mode validation.");
            if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            SessionState.SetBool("GuardComicValidate", true); EditorApplication.isPlaying = true;
        }
        static void Check(bool condition, string description)
        { if (!condition) throw new Exception("FAILED: " + description); checks.Add(description); }

        static IEnumerator ValidatePlay()
        {
            yield return null;
            var comic = UnityEngine.Object.FindObjectOfType<Level1GuardComic>();
            var drag = UnityEngine.Object.FindObjectOfType<Level1DragController>();
            var traversal = UnityEngine.Object.FindObjectOfType<Level1TraversalController>();
            var walker = drag.GetComponent<GothicBride2DWalker>();
            var body = drag.GetComponent<Level1PlayerContactBody>();
            var bed = comic.Bed;
            var portraits = comic.GetComponentsInChildren<UnityEngine.UI.RawImage>();
            Check(portraits.Length == 2 && portraits[0].material == portraits[1].material && portraits[0].material.shader.name == "Aloysius/UI/GuardComicInk",
                "Static portrait and video share the same ink treatment");
            Check(!ShaderUtil.ShaderHasError(portraits[0].material.shader) && portraits[0].material.shader.isSupported,
                "Portrait ink shader compiles and is supported");
            var comicFrame = comic.GetComponentInChildren<Level1ComicFrame>();
            Check(comicFrame.Paper.a == 1 && comicFrame.Paper.r > comicFrame.Paper.g && comicFrame.Paper.g > comicFrame.Paper.b && comicFrame.InkWidth > comicFrame.InnerInkWidth,
                "Opaque paper border uses heavier outer ink than inner ink");
            Check(comic.GetComponentInChildren<UnityEngine.UI.Image>().color.a == 1, "Offset shadow is opaque rather than faded");
            drag.ExternalControl = true; comic.ExternalClock = true;
            drag.Tick(0, .02f, false, reset: true); comic.Tick(.02f);
            Check(comic.CurrentShot == GuardComicShot.Hidden && comic.Visibility == 0, "Hidden at scene start/reset");
            var p = walker.transform.position; p.x = bed.Bounds.min.x - .10f; walker.transform.position = p;
            walker.Advance(0, 0); body.FitToPose(); Physics2D.SyncTransforms();
            comic.Tick(.02f);
            Check(comic.CurrentShot == GuardComicShot.Hidden, "Touching the bed alone does not show the comic");
            for (int i = 0; i < 1400 && !drag.IsDragging; i++) { drag.Tick(0, .02f, true); comic.Tick(.02f); }
            Check(drag.IsDragging, "Real bed controller navigates and grabs successfully");
            Check(comic.CurrentShot == GuardComicShot.Hidden, "Navigation and stationary grip do not trigger a shot");
            Vector3 bedBefore = bed.transform.position;
            for (int i = 0; i < 30; i++) { drag.Tick(1, .02f, true); comic.Tick(.02f); }
            Check(Vector3.Distance(bedBefore, bed.transform.position) > .005f, "Bed actually moves while dragging");
            Check(comic.CurrentShot == GuardComicShot.Watching && comic.Visibility > .99f, "Actual bed movement shows the watching portrait");
            Check(comic.WatchingCount == 1, "Continuous drag triggers once without repeated pop-in");
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 3; i++) yield return null;
            var borderRenderer = comicFrame.GetComponent<CanvasRenderer>();
            Check(borderRenderer, "Comic frame has its required CanvasRenderer");
            var borderMesh = borderRenderer.GetMesh();
            Check(borderMesh && borderMesh.vertexCount > 0 && borderMesh.triangles.Length > 0,
                "Comic frame submits real border geometry to CanvasRenderer");
            Check(!borderRenderer.cull && borderRenderer.GetInheritedAlpha() > .99f,
                "Comic frame is visible and not culled after bed drag");
            if (SessionState.GetBool("GuardComicCapture", false))
            {
                var capture = CaptureProof(comic, "Watching-actual");
                while (capture.MoveNext()) yield return capture.Current;
            }
            for (int i = 0; i < 100; i++) { drag.Tick(0, .02f, false); comic.Tick(.02f); }
            comic.Tick(30);
            Check(comic.CurrentShot == GuardComicShot.Watching && comic.Visibility > .99f, "Releasing the bed and waiting keeps the watching portrait visible");
            drag.Tick(0, .02f, false, jump: true); comic.Tick(.02f);
            Check(comic.ReactionCount == 0, "Ordinary jump does not trigger the pipe reaction");
            traversal.BeginHang();
            for (int i = 0; i < 30; i++) { traversal.Tick(0, .02f, true, false, false, false, false); comic.Tick(.02f); }
            Check(traversal.State == Level1TraversalState.Hanging && comic.ReactionCount == 0, "Hanging at the mouth does not prematurely trigger the reaction");
            Check(comic.CurrentShot == GuardComicShot.Watching && comic.Visibility > .99f, "Watching shot persists from bed release through pipe hanging");
            traversal.Tick(0, .02f, true, false, false, true, false); comic.Tick(.02f);
            Check(traversal.State == Level1TraversalState.Pulling && comic.ReactionCount == 0, "Pull-up starts without premature reaction");
            for (int i = 0; i < 200 && traversal.State != Level1TraversalState.InsidePipe; i++)
            { traversal.Tick(0, .02f, true, false, false, true, false); comic.Tick(.02f); }
            Check(traversal.State == Level1TraversalState.InsidePipe && comic.CurrentShot == GuardComicShot.Reaction && comic.ReactionCount == 1,
                "Completed mantle triggers the reaction exactly once");
            // Let Unity's real video decoder and render loop run, not just the state machine.
            double until = EditorApplication.timeSinceStartup + 10;
            while (!comic.VideoHasFrame && EditorApplication.timeSinceStartup < until)
            { comic.Tick(.016f); yield return null; }
            Check(comic.VideoHasFrame && !comic.VideoFailed, "Imported MP4 decodes and presents its first frame in Play mode");
            long first = comic.Player.frame;
            double last = EditorApplication.timeSinceStartup;
            until = last + 1;
            while (EditorApplication.timeSinceStartup < until)
            { double now = EditorApplication.timeSinceStartup; comic.Tick((float)(now - last)); last = now; yield return null; }
            Check(comic.Player.frame > first + 2, "Head and mouth video advances beyond the first frame");
            Check(comic.ReactionCount == 1, "Remaining inside the pipe does not restart the comic");
            var lighting = UnityEngine.Object.FindObjectOfType<Level1Lighting>(); lighting.TickPipeVision(1);
            Check(lighting.PipeVisionBlend > .99f && comic.Visibility > .99f, "Pipe blackout remains active while comic stays visible");
            if (SessionState.GetBool("GuardComicCapture", false))
            {
                var capture = CaptureProof(comic, "Reaction-actual");
                while (capture.MoveNext()) yield return capture.Current;
            }
            var group = comic.GetComponentInChildren<CanvasGroup>();
            Check(!group.blocksRaycasts && !group.interactable && !comic.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(), "Comic does not intercept controls or pointer input");
            until = EditorApplication.timeSinceStartup + 9;
            while (comic.Player.isPlaying && EditorApplication.timeSinceStartup < until)
            { double now = EditorApplication.timeSinceStartup; comic.Tick((float)(now - last)); last = now; yield return null; }
            Check(!comic.Player.isPlaying && comic.CurrentShot == GuardComicShot.Reaction && comic.Visibility > .99f,
                "Reaction completion freezes the movie without dismissing the comic");
            long heldFrame = comic.Player.frame;
            comic.Tick(30); yield return null;
            Check(comic.Visibility > .99f && comic.Player.frame == heldFrame, "Last reaction frame stays visible after a long wait inside the pipe");
            for (int i = 0; i < 500 && traversal.State == Level1TraversalState.InsidePipe; i++)
            { traversal.Tick(-1, .02f, true, false, false, false, false); comic.Tick(.02f); }
            for (int i = 0; i < 30; i++) { traversal.Tick(0, .02f, true, false, false, false, false); comic.Tick(.02f); }
            Check(traversal.State == Level1TraversalState.Hanging && comic.Visibility > .99f,
                "Backing out to hang at the lip keeps the comic visible");
            traversal.Tick(0, .02f, true, false, false, true, false); comic.Tick(.02f);
            for (int i = 0; i < 200 && traversal.State != Level1TraversalState.InsidePipe; i++)
            { traversal.Tick(0, .02f, true, false, false, true, false); comic.Tick(.02f); }
            Check(traversal.State == Level1TraversalState.InsidePipe && comic.ReactionCount == 1 && comic.Visibility > .99f,
                "Climbing back in from the lip keeps the frame without restarting the reaction");
            for (int i = 0; i < 500 && traversal.State == Level1TraversalState.InsidePipe; i++)
            { traversal.Tick(-1, .02f, true, false, false, false, false); comic.Tick(.02f); }
            for (int i = 0; i < 30; i++) { traversal.Tick(0, .02f, true, false, false, false, false); comic.Tick(.02f); }
            traversal.Tick(0, .02f, false, false, false, false, true); comic.Tick(1);
            Check(traversal.State == Level1TraversalState.Normal && comic.CurrentShot == GuardComicShot.Hidden && comic.Visibility == 0,
                "Letting go and completely leaving pipe traversal dismisses the comic");
            drag.Tick(0, .02f, false, reset: true); comic.Tick(.02f);
            Check(comic.CurrentShot == GuardComicShot.Hidden && comic.ReactionCount == 0 && comic.WatchingCount == 0, "R reset clears both shots and re-arms triggers");
            Report(true, "");
            SessionState.SetBool("GuardComicCapture", false);
        }
        static IEnumerator CaptureProof(Level1GuardComic comic, string name)
        {
            var window = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            window.Show(); window.Repaint();
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 10; i++) { window.Repaint(); yield return null; }
            var frame = comic.GetComponentInChildren<Level1ComicFrame>();
            var renderer = frame.GetComponent<CanvasRenderer>();
            var mesh = renderer ? renderer.GetMesh() : null;
            File.WriteAllText(ReportDir + "/" + name + "-renderer.txt",
                "shot=" + comic.CurrentShot + "\nvisibility=" + comic.Visibility + "\nrect=" + frame.rectTransform.rect +
                "\nrendererPresent=" + (renderer != null) + "\nvertices=" + (mesh ? mesh.vertexCount : 0) + "\ncull=" + (renderer && renderer.cull) + "\nalpha=" + (renderer ? renderer.GetInheritedAlpha() : 0) +
                "\ncolor=" + frame.Paper + "\nmaterial=" + frame.materialForRendering.name + "\nshader=" + frame.materialForRendering.shader.name);
            string path = Path.GetFullPath(ReportDir + "/" + name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            double until = EditorApplication.timeSinceStartup + 8;
            while (!File.Exists(path) && EditorApplication.timeSinceStartup < until) { window.Repaint(); yield return null; }
            Check(File.Exists(path), "Actual Game view capture saved: " + name);
        }
        [Serializable] class Result { public bool passed; public string[] checks; public string error; }
        static void Report(bool success, string error)
        {
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/PlayValidation.json", JsonUtility.ToJson(new Result { passed = success, checks = checks.ToArray(), error = error }, true));
            if (success) Debug.Log("[GuardComic] Play validation passed: " + checks.Count + " checks."); else Debug.LogError(error);
        }
        [MenuItem("Aloysius/Level1/Build Level1 With Comic")]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().path == ScenePath && SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Level1/AloysiusLevel1.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/Build.json", "{\"result\":\"" + report.summary.result + "\",\"errors\":" + report.summary.totalErrors + "}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Level1 build failed.");
        }
    }
}
