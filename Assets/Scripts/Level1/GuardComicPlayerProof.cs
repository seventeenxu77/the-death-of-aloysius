using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Aloysius.GothicStage;

namespace Aloysius.Level1
{
    // Opt-in executable smoke test. Never instantiated during ordinary gameplay.
    public sealed class GuardComicPlayerProof : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("-guard-comic-proof")) return;
            new GameObject("Guard comic executable verification").AddComponent<GuardComicPlayerProof>();
        }
        string directory;
        IEnumerator Start()
        {
            Application.runInBackground = true;
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Logs/Level1/GuardComic/Standalone"));
            Directory.CreateDirectory(directory);
            yield return null;
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            var routine = Exercise();
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) break; current = routine.Current; }
                catch (Exception e)
                { File.WriteAllText(Path.Combine(directory, "result.json"), JsonUtility.ToJson(new Result { error = e.ToString() }, true)); Application.Quit(1); yield break; }
                yield return current;
            }
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonUtility.ToJson(new Result { passed = true }, true));
            Application.Quit(0);
        }
        IEnumerator Exercise()
        {
            var comic = FindObjectOfType<Level1GuardComic>();
            var drag = FindObjectOfType<Level1DragController>();
            var traversal = FindObjectOfType<Level1TraversalController>();
            var walker = drag.GetComponent<GothicBride2DWalker>();
            drag.ExternalControl = true; comic.ExternalClock = true;
            drag.Tick(0, .02f, false, reset: true);
            var p = walker.transform.position; p.x = comic.Bed.Bounds.min.x - .10f; walker.transform.position = p;
            walker.Advance(0, 0); drag.GetComponent<Level1PlayerContactBody>().FitToPose(); Physics2D.SyncTransforms();
            for (int i = 0; i < 1400 && !drag.IsDragging; i++) { drag.Tick(0, .02f, true); comic.Tick(.02f); }
            if (!drag.IsDragging) throw new Exception("Bed grab failed in executable.");
            for (int i = 0; i < 30; i++) { drag.Tick(1, .02f, true); comic.Tick(.02f); }
            yield return new WaitForEndOfFrame();
            var frame = comic.GetComponentInChildren<Level1ComicFrame>();
            var renderer = frame.GetComponent<CanvasRenderer>();
            if (!renderer || !renderer.GetMesh() || renderer.GetMesh().vertexCount == 0 || renderer.cull || renderer.GetInheritedAlpha() < .99f)
                throw new Exception("Frame is not rendered in executable.");
            if (comic.CurrentShot != GuardComicShot.Watching) throw new Exception("Watching shot not triggered.");
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "Watching.png"));
            yield return new WaitForSeconds(.3f);
            drag.Tick(0, .02f, false, reset: true);
            traversal.BeginHang();
            for (int i = 0; i < 30; i++) { traversal.Tick(0, .02f, true, false, false, false, false); comic.Tick(.02f); }
            traversal.Tick(0, .02f, true, false, false, true, false); comic.Tick(.02f);
            for (int i = 0; i < 200 && traversal.State != Level1TraversalState.InsidePipe; i++)
            { traversal.Tick(0, .02f, true, false, false, true, false); comic.Tick(.02f); }
            float until = Time.realtimeSinceStartup + 10;
            while ((!comic.VideoHasFrame || comic.Player.frame < 20) && Time.realtimeSinceStartup < until)
            { comic.Tick(Time.unscaledDeltaTime); yield return null; }
            if (!comic.VideoHasFrame || comic.VideoFailed || comic.CurrentShot != GuardComicShot.Reaction)
                throw new Exception("Reaction movie did not play in executable.");
            FindObjectOfType<Level1Lighting>().TickPipeVision(1);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "Reaction.png"));
            yield return new WaitForSeconds(.5f);
            if (!File.Exists(Path.Combine(directory, "Watching.png")) || !File.Exists(Path.Combine(directory, "Reaction.png")))
                throw new Exception("Executable captures missing.");
            VerifyWhiteBorder(Path.Combine(directory, "Watching.png"));
            VerifyWhiteBorder(Path.Combine(directory, "Reaction.png"));
        }
        static void VerifyWhiteBorder(string path)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            int white = 0;
            foreach (var pixel in texture.GetPixels32())
                if (pixel.r > 180 && pixel.g > 180 && pixel.b > 160) white++;
            Destroy(texture);
            if (white < 100) throw new Exception("Capture has no visible white border: " + path);
        }
        [Serializable] sealed class Result { public bool passed; public string error = ""; }
    }
}
