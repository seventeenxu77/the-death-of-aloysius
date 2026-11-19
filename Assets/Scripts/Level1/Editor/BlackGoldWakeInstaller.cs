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
    public static class BlackGoldWakeInstaller
    {
        const string Art="Assets/Art/SerpentKing/BlackGoldWake_v026/";
        const string Output="ArtSource/SerpentKing/BlackGoldWake_v026/";
        [MenuItem("Aloysius/Serpent/Install black gold four head wake")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            var campaign=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<SerpentCampaign>(true)).Single();
            var wake=campaign.wake.bedWake;
            if(!wake||!wake.music||!wake.music.clip)throw new InvalidOperationException("Existing wake/music missing.");
            File.Copy(scene.path,Output+"BeforeBlackGold_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
            foreach(string file in Directory.GetFiles(Art,"*.png"))
            {
                string path=file.Replace('\\','/');AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;
                importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
                importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            }
            var shader=Shader.Find("Aloysius/Black Gold Wake");
            if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Black gold shader unavailable.");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"Wake.mat");
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,Art+"Wake.mat");}else mat.shader=shader;
            // Reuse the existing bed root: its scene placement, curtains and camera references stay authoritative.
            wake.enabled=false;
            var skin=wake.GetComponent<BlackGoldWakeSkin>();if(!skin)skin=wake.gameObject.AddComponent<BlackGoldWakeSkin>();
            skin.rigAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"Rig.json");skin.skinMaterial=mat;
            skin.bedBackground=Tex("EmptyBed");skin.bedForeground=Tex("BedForeground");
            skin.art=new[]{"Venom","Stone","Fang","Time","Coils"}.Select(n=>new BlackGoldWakeSkin.Art{
                name=n,open=Tex(n=="Coils"?n:n+"_Open"),closed=Tex(n=="Coils"?n:n+"_Closed")}).ToArray();
            skin.idleMotion=true;wake.blackGoldSkin=skin;
            Configure(wake.fang,1,2.8f);Configure(wake.venom,5.5f,2.8f);Configure(wake.stone,12,2.8f);Configure(wake.timeHead,17,2.6f);
            wake.duration=20;wake.musicSeconds=25;wake.music.playOnAwake=false;
            wake.gameObject.name="Serpent King | black gold timed wake";
            wake.enabled=true;wake.ResetPose();
            if(skin.BoneCount!=46)throw new InvalidOperationException("Incomplete black gold rig.");
            EditorUtility.SetDirty(skin);EditorUtility.SetDirty(wake);EditorUtility.SetDirty(mat);EditorUtility.SetDirty(wake.music);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Output+"UnityIntegration.txt",DateTime.Now.ToString("O")+"\nSaved: "+scene.path+
                "\nWake root: "+wake.name+"\n46 bones; four independent complete neck skins; shared front/back coils; localized eyes only."+
                "\nStarts: 1 / 5.5 / 12 / 17 s; finished: "+wake.Duration+" s; music: "+AssetDatabase.GetAssetPath(wake.music.clip)+
                "\nMusic cutoff: 25 s. FullyOpen trigger and existing combat transition retained. Post-wake idle continues with eyes open."+
                "\nNo Play-mode test.\n");
            Debug.Log("Black gold four-head wake installed and level1 saved. No Play-mode test.");
        }
        static Texture2D Tex(string name)
        {
            var t=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+name+".png");
            if(!t)throw new InvalidOperationException("Missing "+name);return t;
        }
        static void Configure(SerpentBedWake.HeadTiming h,float start,float duration)
        {h.start=start;h.duration=duration;h.eyeDelay=.05f;h.eyeDuration=.18f;h.idleAmplitude=1;}
    }
}
