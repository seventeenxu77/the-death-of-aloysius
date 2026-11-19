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
    public static class Level1PalaceGuardWidthEditor
    {
        const string Pending="WidenPalaceGuardsAfterStop";
        static Level1PalaceGuardWidthEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Apply;
            };
            if(SessionState.GetBool(Pending,false) && !EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.delayCall+=Apply;
        }
        static void SetWidth(Level1PalaceGuard guard)
        {
            var s=new SerializedObject(guard);s.FindProperty("bodyWidth").floatValue=1.2f;s.ApplyModifiedProperties();
            var visual=(Transform)s.FindProperty("visual").objectReferenceValue;
            if(visual)visual.localScale=new Vector3((s.FindProperty("initiallyFaceLeft").boolValue?-1:1)*1.2f,1,1);
        }
        [MenuItem("Aloysius/Level1/Widen Palace Guards Twenty Percent")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            SessionState.SetBool(Pending,false);EditorApplication.delayCall-=Apply;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            Directory.CreateDirectory("ArtSource/PalaceGuard/Backups");
            string backup="ArtSource/PalaceGuard/Backups/level1-before-width-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot back up guard scene.");
            const string path="Assets/Prefabs/Level1/PalaceGuard.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try{SetWidth(prefab.GetComponent<Level1PalaceGuard>());PrefabUtility.SaveAsPrefabAsset(prefab,path);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var guards=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1PalaceGuard>(true)).ToArray();
            foreach(var guard in guards)
            {
                Undo.RecordObject(guard,"Widen guard");SetWidth(guard);
                PrefabUtility.RecordPrefabInstancePropertyModifications(guard);
                var s=new SerializedObject(guard);
                PrefabUtility.RecordPrefabInstancePropertyModifications(s.FindProperty("visual").objectReferenceValue);
            }
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save guard width.");
            Directory.CreateDirectory("Logs/Level1/PalaceGuard");
            File.WriteAllText("Logs/Level1/PalaceGuard/Width.txt",DateTime.Now.ToString("O")+"\nSet Body Width to 1.2 for prefab and "+guards.Length+" guards. Height, positions and patrol settings preserved.");
        }
    }
}
