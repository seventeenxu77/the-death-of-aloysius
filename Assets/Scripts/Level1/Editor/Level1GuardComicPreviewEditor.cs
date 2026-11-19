using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aloysius.Level1.Editor
{
    // Temporary editor-only canvases share the runtime layout, but never play video,
    // enter gameplay, or serialize into the scene/build.
    [InitializeOnLoad]
    static class Level1GuardComicPreview
    {
        static double nextRefresh;

        static Level1GuardComicPreview()
        {
            EditorApplication.update += Refresh;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
            EditorSceneManager.sceneClosing += (scene, removing) => Clear();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Clear();
                if (state == PlayModeStateChange.EnteredEditMode) nextRefresh = 0;
            };
        }

        static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer
                || EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .15;
            bool visible = false;
            foreach (var comic in Resources.FindObjectsOfTypeAll<Level1GuardComic>())
            {
                if (!comic.gameObject.scene.IsValid() || !comic.gameObject.scene.isLoaded
                    || EditorUtility.IsPersistent(comic)) continue;
                visible |= comic.RefreshEditorPreview();
            }
            if (visible)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }

        static void Clear()
        {
            foreach (var comic in Resources.FindObjectsOfTypeAll<Level1GuardComic>())
                comic.ClearEditorPreview();
        }
    }

    [CustomEditor(typeof(Level1GuardComic))]
    sealed class Level1GuardComicInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("未运行时显示两张完整分镜，可直接看 Game 窗口调排版。\n"
                + "正常图：Panel Width（大小）、Offset（位置）、Tilt（倾斜）。\n"
                + "察觉图：Alert Size Ratio（大小）、Alert Offset（相对位置）、Alert Tilt（倾斜）。\n"
                + "Ctrl+S 保存调整。开始游戏后默认隐藏，仍由拖床和管道移动触发。", MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
