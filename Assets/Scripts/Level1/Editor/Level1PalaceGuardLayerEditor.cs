using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1PalaceGuardLayerEditor
    {
        const string Pending="BringPalaceGuardsForwardAfterStop";
        static Level1PalaceGuardLayerEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Apply;
            };
            if(SessionState.GetBool(Pending,false) && !EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.delayCall+=Apply;
        }
        static void SetLayer(Level1PalaceGuard guard)
        {
            var group=guard.GetComponentInChildren<SortingGroup>(true);
            Undo.RecordObject(group,"Bring guard in front of statues");
            group.sortingLayerID=SortingLayer.NameToID("Default");
            group.sortingOrder=50;
            EditorUtility.SetDirty(group);
        }
        [MenuItem("Aloysius/Level1/Bring Palace Guards In Front Of Statues")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            SessionState.SetBool(Pending,false);EditorApplication.delayCall-=Apply;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            Directory.CreateDirectory("ArtSource/PalaceGuard/Backups");
            string backup="ArtSource/PalaceGuard/Backups/level1-before-guard-layer-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot back up guard scene.");
            const string path="Assets/Prefabs/Level1/PalaceGuard.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try{SetLayer(prefab.GetComponent<Level1PalaceGuard>());PrefabUtility.SaveAsPrefabAsset(prefab,path);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var guards=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1PalaceGuard>(true)).ToArray();
            foreach(var guard in guards)
            {
                Undo.RecordObject(guard,"Bring guard in front of statues");SetLayer(guard);
                PrefabUtility.RecordPrefabInstancePropertyModifications(guard.GetComponentInChildren<SortingGroup>(true));
            }
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save guard layer.");
            Directory.CreateDirectory("Logs/Level1/PalaceGuard");
            File.WriteAllText("Logs/Level1/PalaceGuard/Layer.txt",DateTime.Now.ToString("O")+"\nSet SortingGroup order 50 for prefab and "+guards.Length+" guards, ahead of statues at order 42 and cover at 43. Internal part ordering, positions and patrol settings preserved.");
        }
    }
}
