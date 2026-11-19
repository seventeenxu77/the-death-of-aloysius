using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class SerpentAssetMaintenance
    {
        const string Output="ArtSource/Maintenance/AudioCleanup_20261117/";
        public static void InstallFullAudioAndAudit()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            File.Copy(scene.path,Output+"BeforeFullAudio_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
            var c=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SerpentCampaign>(true)).Single();
            string path="Assets/Audio/SerpentKing/SerpentHeads_Full.wav";AssetDatabase.ImportAsset(path);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;
            importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=true;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(!clip||clip.length<100)throw new InvalidOperationException("Full audio missing or truncated.");
            var root=c.transform.Find("Boss music | complete Serpent Heads");
            if(!root){root=new GameObject("Boss music | complete Serpent Heads").transform;root.SetParent(c.transform,false);}
            var source=root.GetComponent<AudioSource>();if(!source)source=root.gameObject.AddComponent<AudioSource>();
            var wake=c.wake.bedWake;
            if(wake.music&&wake.music!=source){wake.music.Stop();wake.music.clip=null;wake.music.enabled=false;EditorUtility.SetDirty(wake.music);}
            source.enabled=true;source.clip=clip;source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.pitch=1;source.volume=1;
            wake.music=source;wake.playFullMusic=true;
            // The selected black/gold renderer owns all its art. Remove serialized legacy-only dependencies.
            if(wake.blackGoldSkin){wake.rigData=null;wake.material=null;wake.bedBackground=null;wake.bedForeground=null;wake.art=new SerpentBedWake.PartArt[0];}
            EditorUtility.SetDirty(source);EditorUtility.SetDirty(wake);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Output+"AudioIntegration.txt",DateTime.Now.ToString("O")+"\nSaved: "+scene.path+"\nAudio: "+path+"\nDuration: "+clip.length+
                " s\nFull playback enabled; 20s animation timing independent; no 25s scheduled cutoff. Non-spatial streaming source belongs to persistent campaign root.\nNo Play-mode test.\n");
            Audit();
        }
        public static void Audit()
        {
            // All project scenes are protected, not only level1 or enabled build entries.
            var scenes=AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var dependencies=new HashSet<string>(AssetDatabase.GetDependencies(scenes,true),StringComparer.OrdinalIgnoreCase);
            // Preserve explicit Resources/StreamingAssets/runtime addressable inputs as well.
            foreach(var path in AssetDatabase.GetAllAssetPaths())if(path.StartsWith("Assets/Resources/")||path.StartsWith("Assets/StreamingAssets/"))
                foreach(var dep in AssetDatabase.GetDependencies(path,true))dependencies.Add(dep);
            File.WriteAllLines(Output+"ProtectedDependencies.txt",dependencies.OrderBy(p=>p));
            var candidates=new List<string>();var notes=new StringBuilder();
            foreach(string folder in new[]{"Assets/Art/SerpentKing/BedWake_v023","Assets/Art/SerpentKing/StoneSlam_v025"})
            {
                var used=dependencies.Where(p=>p.StartsWith(folder+"/",StringComparison.OrdinalIgnoreCase)).ToArray();
                if(used.Length==0)candidates.Add(folder);else notes.AppendLine("Kept "+folder+" because referenced:\n"+string.Join("\n",used));
            }
            foreach(string file in new[]{"Assets/Art/SerpentKing/FloorBreak_v001/Before.png","Assets/Art/SerpentKing/FloorBreak_v001/After.png","Assets/Art/SerpentKing/FloorBreak_v001/Floor.mat","Assets/Art/SerpentKing/FloorBreak_v001/FloorBreak.shader"})
                if(File.Exists(file)&&!dependencies.Contains(file))candidates.Add(file);
            File.WriteAllLines(Output+"SafeAssetCandidates.txt",candidates);
            File.WriteAllText(Output+"AuditNotes.txt","All "+scenes.Length+" scenes protected. "+dependencies.Count+" transitive dependencies.\n"+notes);
            Debug.Log("Full music saved; scene dependency audit completed. No Play-mode test.");
        }
    }
}
