using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1PalacePlayerGlowEditor
    {
        const string Pending="ApplyPalacePlayerGlowAfterStop";
        static Level1PalacePlayerGlowEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending,false))
                    EditorApplication.delayCall+=Apply;
            };
            if(SessionState.GetBool(Pending,false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall+=Apply;
        }
        [MenuItem("Aloysius/Level1/Enable Palace Player Glow")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            SessionState.SetBool(Pending,false);EditorApplication.delayCall-=Apply;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var storm=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1PalaceStorm>(true)).First();
            Directory.CreateDirectory("ArtSource/PalaceStorm/Backups");
            string backup="ArtSource/PalaceStorm/Backups/level1-before-player-glow-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save blackout backup.");
            Undo.RecordObject(storm,"Enable player proximity light");
            var settings=new SerializedObject(storm);
            settings.FindProperty("enablePlayerGlow").boolValue=true;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save blackout settings.");
            Selection.activeGameObject=storm.gameObject;
            Directory.CreateDirectory("Logs/Level1/PalaceStorm");
            File.WriteAllText("Logs/Level1/PalaceStorm/PlayerGlow.txt",DateTime.Now.ToString("O")+"\nSaved soft player proximity light. Blackout and flash settings preserved.\nBackup: "+backup);
        }
    }
}
