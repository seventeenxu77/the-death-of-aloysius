using System;
using System.IO;
using System.Linq;
using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class SerpentSwayInstaller
    {
        [MenuItem("Aloysius/Serpent/Apply accented head sway")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            const string folder="ArtSource/SerpentKing/SunflowerSway_v001/";
            Directory.CreateDirectory(folder);
            string backup=folder+"BeforeSway.unity.backup";
            if(!File.Exists(backup))File.Copy(scene.path,backup);
            var skins=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<BlackGoldWakeSkin>(true)).ToArray();
            foreach(var skin in skins)
            {
                Undo.RecordObject(skin,"Configure accented serpent sway");
                skin.idleMotion=true;skin.idleFrequencyHz=.36f;
                EditorUtility.SetDirty(skin);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(folder+"UnityIntegration.txt",DateTime.Now.ToString("O")+
                "\nApplied to "+skins.Length+" BlackGoldWakeSkin components. Frequency 0.36 Hz; independent head phases; root pinned; end-of-swing downward accent and hold."+
                "\nWake cues, eye timing, textures, room positions and ceiling-breach override retained. No Play-mode run.");
        }
    }
}
