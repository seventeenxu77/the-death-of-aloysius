using System;
using System.IO;
using System.Linq;
using System.Text;
using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class SerpentBezierLifeInstaller
    {
        const string Art="Assets/Art/SerpentKing/BezierLife_v010/";
        const string Source="ArtSource/SerpentKing/BezierLife_v010/";
        [MenuItem("Aloysius/Serpent/Install approved 1.35 Hz Bezier idle")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            AssetDatabase.Refresh();
            var profile=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"BezierLife.json");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"SerpentBezierLife.shader");
            if(!profile||!shader)throw new InvalidOperationException("Bezier idle profile or shader not imported.");
            if(ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Bezier idle shader has compilation errors.");
            var parsed=JsonUtility.FromJson<SerpentBezierLife.Data>(profile.text);
            if(parsed.version!=10||parsed.parts.Length!=4||Mathf.Abs(parsed.frequency-1.35f)>.0001f)throw new InvalidOperationException("Wrong approved idle profile.");
            foreach(var p in parsed.parts)
                if(p.poses.Length!=3||p.binding.Length!=p.vertices.Length/2*5||p.legacyJoints.Length!=p.vertices.Length||p.legacyWeights.Length!=p.vertices.Length)
                    throw new InvalidOperationException("Incomplete independent skin: "+p.name);
            const string path="Assets/Scenes/level1.unity";
            var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var skins=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<BlackGoldWakeSkin>(true)).ToArray();
                if(skins.Length!=2||!skins.Any(s=>s.drawBed)||!skins.Any(s=>!s.drawBed))throw new InvalidOperationException("Expected bed and far-view skins in level1.");
                Directory.CreateDirectory(Source);
                File.Copy(path,Source+"BeforeIdle_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
                var report=new StringBuilder(DateTime.Now.ToString("O")+"\n");
                foreach(var skin in skins)
                {
                    var life=skin.GetComponent<SerpentBezierLife>();if(!life)life=skin.gameObject.AddComponent<SerpentBezierLife>();
                    Undo.RecordObject(skin,"Install approved Bezier idle");Undo.RecordObject(life,"Install approved Bezier idle");
                    life.profile=profile;life.lifeShader=shader;life.transitionSeconds=.65f;life.detailsEnabled=true;life.detailsStrength=1;life.enabled=true;
                    skin.bezierLife=life;skin.idleMotion=true;skin.idleFrequencyHz=1.35f;skin.Clear();
                    EditorUtility.SetDirty(skin);EditorUtility.SetDirty(life);
                    report.AppendLine(skin.name+" | frequency="+skin.idleFrequencyHz+" | drawBed="+skin.drawBed+" | sorting="+skin.sortingOffset+" | exposure="+skin.skinMaterial.GetFloat("_Exposure"));
                }
                var wake=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<SerpentBedWake>(true)).First(w=>w.blackGoldSkin&&w.blackGoldSkin.drawBed);
                report.AppendLine("Wake duration="+wake.Duration+"; head cues="+wake.fang.start+", "+wake.venom.start+", "+wake.stone.start+", "+wake.timeHead.start);
                report.AppendLine("Music preserved: "+(wake.music&&wake.music.clip?wake.music.clip.name:"none")+"; full="+wake.playFullMusic);
                report.AppendLine("All four heads synchronous. Venom rear head +200 source-pixels. Root, bed, coils, room transforms and depth sorting retained.");
                report.AppendLine("Independent native curve meshes, eyelid masks, Fang tongue, Stone nod, Venom sac pulses, Time blink/nod. Stone ceiling override retained.");
                report.AppendLine("No Play-mode test or gameplay automation performed.");
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                File.WriteAllText(Source+"UnityIntegration.txt",report.ToString());
                Debug.Log("Installed BezierLife v010 in level1: wake -> native 1.35 Hz idle.");
            }
            finally
            {
                if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
            }
        }
    }
}
