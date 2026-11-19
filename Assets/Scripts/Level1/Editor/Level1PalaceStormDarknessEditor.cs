using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1PalaceStormDarknessEditor
    {
        const string Pending="ApplyPalaceStormBlackoutAfterStop";
        static Level1PalaceStormDarknessEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending,false))
                    EditorApplication.delayCall+=Apply;
            };
            if(SessionState.GetBool(Pending,false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall+=Apply;
        }
        [MenuItem("Aloysius/Level1/Apply Pitch Black Palace Storm")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            SessionState.SetBool(Pending,false);EditorApplication.delayCall-=Apply;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var storm=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1PalaceStorm>(true)).First();
            Directory.CreateDirectory("ArtSource/PalaceStorm/Backups");
            string backup="ArtSource/PalaceStorm/Backups/level1-before-blackout-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save blackout backup.");
            Undo.RecordObject(storm,"Set pitch black storm intervals");
            var settings=new SerializedObject(storm);
            foreach(string field in new[]{"darkCharacterExposure","idleCharacterRim","rainOpacity","darkPipeExposure"})
                settings.FindProperty(field).floatValue=0;
            var layers=settings.FindProperty("layers");
            for(int i=0;i<layers.arraySize;i++)layers.GetArrayElementAtIndex(i).FindPropertyRelative("darkExposure").floatValue=0;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save blackout settings.");
            Selection.activeGameObject=storm.gameObject;
            Directory.CreateDirectory("Logs/Level1/PalaceStorm");
            File.WriteAllText("Logs/Level1/PalaceStorm/Blackout.txt",DateTime.Now.ToString("O")+"\nSaved zero ambient exposure for all storm layers, actor and pipe; zero idle rim and rain. Flash settings preserved.\nBackup: "+backup);
        }
    }
}
