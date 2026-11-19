using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aloysius.Level1.Boss;

namespace Aloysius.Level1.Editor
{
    public static class StoneSeveringInstaller
    {
        const string Art="Assets/Art/SerpentKing/StoneSevering_v001/";
        const string Source="ArtSource/SerpentKing/StoneSevering_v001/";
        static Material MaterialAt(string name,Shader shader)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(Art+name+".mat");
            if(!m){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,Art+name+".mat");}else m.shader=shader;
            m.SetFloat("_Opacity",1);m.SetFloat("_Exposure",1);EditorUtility.SetDirty(m);return m;
        }
        [MenuItem("Aloysius/Serpent/Install marked-neck severing and whip death")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before saving.");
            AssetDatabase.Refresh();Directory.CreateDirectory(Source);
            AssetDatabase.ImportAsset(Art+"SeveredNeck.png",ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(Art+"SeveredNeck.png");
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"SeveredSerpent.shader");
            var attackShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/SerpentKing/StoneSlam_BlackGold_v028/StoneSlam.shader");
            if(!shader||!attackShader||ShaderUtil.ShaderHasError(shader)||ShaderUtil.ShaderHasError(attackShader))throw new InvalidOperationException("Death shader import failed.");
            var rig=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"WhipRig.json");var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"SeveredNeck.png");
            if(!rig||!texture)throw new InvalidOperationException("Approved whip data missing.");
            var data=JsonUtility.FromJson<SeveredSerpentWhip.Data>(rig.text);
            if(data.chain.Length!=130||data.binding.Length!=data.vertices.Length||data.indices.Length==0)throw new InvalidOperationException("Invalid continuous whip binding.");
            var skin=MaterialAt("SeveredNeck",shader);skin.SetTexture("_MainTex",texture);skin.SetFloat("_Solid",0);
            var blood=MaterialAt("BloodSpray",shader);blood.SetFloat("_Solid",1);
            const string path="Assets/Scenes/level1.unity";var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var c=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();
                if(!c.stone||!c.stone.stoneSlam||!c.leftSpikeMechanism||!c.ceilingBreach)throw new InvalidOperationException("Existing stone attack / left spike / ceiling presentation missing.");
                File.Copy(path,Source+"BeforeDeath_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
                var far=c.ceilingBreach.presentation.skin;var head=far.FindHead("Stone");if(head==null)throw new InvalidOperationException("Independent Stone branch missing.");
                var whipRoot=far.transform.Find("巨石断颈 | 成功命中后骨骼甩动");
                if(!whipRoot){whipRoot=new GameObject("巨石断颈 | 成功命中后骨骼甩动").transform;whipRoot.SetParent(far.transform,false);}
                whipRoot.localPosition=new Vector3(head.points[0]*.01f,-head.points[1]*.01f,0);whipRoot.localScale=Vector3.one;whipRoot.localRotation=Quaternion.identity;
                var whip=whipRoot.GetComponent<SeveredSerpentWhip>();if(!whip)whip=whipRoot.gameObject.AddComponent<SeveredSerpentWhip>();
                whip.rigAsset=rig;whip.neckTexture=texture;whip.skinMaterial=skin;whip.sourcePixelScale=.005f;whip.frequencyHz=.78f;whip.amplitude=1;
                whip.exposure=far.skinMaterial.GetFloat("_Exposure");whip.sortingOrder=far.sortingOffset+3;whip.bloodPerSecond=24;whip.bloodGravity=2.2f;
                var death=c.GetComponent<StoneSeveringSequence>();if(!death)death=c.gameObject.AddComponent<StoneSeveringSequence>();
                death.campaign=c;death.slam=c.stone.stoneSlam;death.breach=c.ceilingBreach;death.farSkin=far;death.whip=whip;death.bloodMaterial=blood;
                death.nearWithdrawSeconds=.85f;death.farWithdrawSeconds=.9f;death.stumpAppearSeconds=.35f;death.headAngularVelocity=-75;death.landedAngle=-85;
                c.stoneDeath=death;c.leftSpikeMechanism.tipHitLength=.65f;
                var slam=c.stone.stoneSlam;slam.neckMarkPixels=new Vector2(586,466);slam.neckMarkSizePixels=new Vector2(118,156);slam.severCutPixels=new Vector2(440,640);slam.maxNeckCompression=0;
                slam.skinMaterial.SetFloat("_CutMode",0);slam.skinMaterial.SetFloat("_CutSourceY",640);
                foreach(var o in new UnityEngine.Object[]{c,death,whip,slam,c.leftSpikeMechanism,slam.skinMaterial,skin,blood})EditorUtility.SetDirty(o);
                EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
                var report=new StringBuilder(DateTime.Now.ToString("O")+"\nSaved: "+path+"\n");
                report.AppendLine("Death trigger: spear TIP only intersects the four-diamond patch during Strike/Impact; body/face/shaft contact cannot defeat it.");
                report.AppendLine("Weak point: source (586,466), size (118,156). Collider and displayed rigid mark share the same skin mapping; swept mark and swept tip avoid fast-stroke tunnelling.");
                report.AppendLine("Impact neck compression aligns the real mark with the existing outlet height; keeps rigid skull proportions and floor contact. Limit and coordinates exposed in Inspector.");
                report.AppendLine("Cut: original source y=640 with complementary jagged red edges. Falling head snapshots the SAME deformed mesh/UV/art at hit time, not a replacement illustration.");
                report.AppendLine("Head tumbles and drops; near neck withdraws .85 s, far Stone branch withdraws .9 s; approved 65-joint fixed-root whip fades in .35 s and emits moving wound droplets.");
                report.AppendLine("Far whip root="+whipRoot.localPosition+"; native 0.78 Hz; scale=.005; sorting="+whip.sortingOrder+"; exposure="+whip.exposure+". Other three heads and shared coils retained.");
                report.AppendLine("Death persists on room transitions/checkpoint respawn. Chapter reset removes corpse/blood and restores the Stone branch. Existing E lever, outlet placement, stairs and exit retained.");
                report.AppendLine("Compile / data / reference validation only. No Play mode, gameplay simulation, screenshot or render test.");
                File.WriteAllText(Source+"UnityIntegration.txt",report.ToString());Debug.Log("Installed marked-neck severing sequence in level1.");
            }
            finally{if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}}
        }
    }
}
