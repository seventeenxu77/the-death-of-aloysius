using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1TeleportConsoleEditor
    {
        const string Source="ArtSource/DebugTools/GMConsole/";
        const string Pending=Source+"Apply.pending";
        static double nextCheck;
        static Level1TeleportConsoleEditor(){EditorApplication.update+=ProcessPending;}
        static void ProcessPending()
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Pending)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                Install();File.Delete(Pending);Level1RoyalBedchamberEditor.BuildPlayer();
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nInstalled GM console in level1; player packaged. No Play mode or tests.\n");
            }
            catch(Exception e){File.WriteAllText(Source+"ApplyError.txt",e.ToString());File.Delete(Pending);Debug.LogException(e);}
        }
        [MenuItem("Aloysius/Level1/Install GM Console")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install in Edit mode.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            Directory.CreateDirectory(Source);string backup=Source+"level1-before-GM-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up live scene.");
            var console=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1TeleportConsole>(true)).FirstOrDefault();
            if(!console)
            {
                var root=new GameObject("GM Console | backquote");root.transform.SetParent(route.transform,false);
                Undo.RegisterCreatedObjectUndo(root,"Add GM console");console=root.AddComponent<Level1TeleportConsole>();
                var point=new GameObject("hall | mirror gallery arrival");point.transform.SetParent(root.transform,false);
                var settings=new SerializedObject(route.corridor);
                var start=(Transform)settings.FindProperty("startBoundary").objectReferenceValue;
                var floor=(Transform)settings.FindProperty("ground").objectReferenceValue;
                point.transform.position=new Vector3(start.position.x+2,floor.position.y,0);
                console.Configure(route,point.transform);
            }
            console.gameObject.name="GM Console | backquote";
            var config=new SerializedObject(console);
            config.FindProperty("toggleKey").intValue=(int)KeyCode.BackQuote;
            config.FindProperty("closeAfterTeleport").boolValue=false;
            config.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(console);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save console scene.");
            Selection.activeGameObject=console.gameObject;
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nScene: Assets/Scenes/level1.unity\nBackup: "+backup+"\nBackquote GM console: 2dtrans room/hall/end/turn/pass/ante/bed.\nInput gated; Escape closes console without quitting; existing furniture placements retained.\nNo Play mode or tests.\n");
        }
    }
}

// GM console migration: reload editor assembly before packaging.
