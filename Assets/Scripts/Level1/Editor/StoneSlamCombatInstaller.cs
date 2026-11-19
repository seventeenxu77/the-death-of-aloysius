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
    public static class StoneSlamCombatInstaller
    {
        const string Art="Assets/Art/SerpentKing/StoneSlam_BlackGold_v028/";
        const string Output="ArtSource/SerpentKing/StoneSlam_BlackGold_v028/";
        [MenuItem("Aloysius/Serpent/Install black gold stone slam (no FX)")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before installing.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            var campaign=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();
            if(!campaign.wake||!campaign.wake.bedWake)throw new InvalidOperationException("Approved wake is not installed.");
            File.Copy(scene.path,Output+"BeforeCombat_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
            foreach(var file in Directory.GetFiles(Art,"*.png"))
            {
                string path=file.Replace('\\','/');AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Default;imp.sRGBTexture=true;imp.alphaSource=TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency=false;imp.mipmapEnabled=false;imp.npotScale=TextureImporterNPOTScale.None;
                imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Bilinear;imp.SaveAndReimport();
            }
            var shader=Shader.Find("Aloysius/Stone Slam Black Gold");if(!shader)throw new InvalidOperationException("Stone shader unavailable.");
            var skin=MaterialAt("StoneSkin",shader);skin.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Stone_Open.png"));skin.SetTexture("_ClosedTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Stone_Closed.png"));skin.SetColor("_Color",Color.white);
            var ring=MaterialAt("WarningRing",shader);ring.SetTexture("_MainTex",Texture2D.whiteTexture);ring.SetTexture("_ClosedTex",Texture2D.whiteTexture);ring.SetColor("_Color",new Color(1,.035f,.09f,1));
            var attack=campaign.stone;
            foreach(var renderer in attack.GetComponentsInChildren<Renderer>(true)){renderer.enabled=false;EditorUtility.SetDirty(renderer);}
            var slam=attack.GetComponent<StoneSlamCombat>();if(!slam)slam=attack.gameObject.AddComponent<StoneSlamCombat>();
            slam.rigAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"StoneRig.json");slam.skinMaterial=skin;slam.ringMaterial=ring;slam.attack=attack;slam.viewCamera=Camera.main;
            slam.showWarningRing=false;slam.showImpactPulse=false;
            attack.stoneSlam=slam;attack.enabled=true;attack.gameObject.SetActive(false);
            campaign.firstStoneAttackDelay=.35f;
            EditorUtility.SetDirty(skin);EditorUtility.SetDirty(ring);EditorUtility.SetDirty(slam);EditorUtility.SetDirty(attack);EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Verify();
        }
        static Material MaterialAt(string name,Shader shader)
        {
            string path=Art+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;return mat;
        }
        [MenuItem("Aloysius/Serpent/Apply longer stone hold and visible hanging")]
        public static void ApplyHangingRecovery()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before saving.");
            const string path="Assets/Scenes/level1.unity";
            var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var campaign=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();
                var slam=campaign.stone.stoneSlam;
                if(!slam)throw new InvalidOperationException("Existing skeletal stone attack missing.");
                Directory.CreateDirectory(Output);
                File.Copy(path,Output+"BeforeHanging_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
                if(!campaign.stoneDeath||campaign.stoneDeath.slam!=slam||!campaign.stoneDeath.whip||!campaign.leftSpikeMechanism||!campaign.leftSpikeMechanism.bladeTip)
                    throw new InvalidOperationException("Marked-neck death / whip / spear tip references incomplete.");
                slam.impactSeconds=4f;slam.hangBetweenAttacks=true;slam.hangingVisibleDepth=3.8f;
                slam.maxNeckCompression=0;slam.markHeightAnchor=campaign.leftSpikeMechanism.bladeTip;
                EditorUtility.SetDirty(slam);EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Output+"HangingRecoveryIntegration.txt",DateTime.Now.ToString("O")+
                    "\nSaved: "+path+"\nImpact hold: 4 s (planted pose, original recoil at the end)."+
                    "\nRecover: "+slam.recoverSeconds+" s; settle: "+slam.exitSeconds+" s; hanging nose depth: 3.8 world units below camera top."+
                    "\nHanging remains visible and non-dangerous; cooldown advances; next attack begins from the same on-screen pose."+
                    "\nMark height: exact spear-tip anchor, compression limit disabled; joints 3/4 solve the visible mark and collider together; joint 5 fits to the fixed skull."+
                    "\nDeath references present: campaign -> StoneSeveringSequence -> slam / whip; marked-neck tip collision still invokes DefeatStone."+
                    "\nCancel, room changes, defeat and reset retain their explicit cleanup paths. Existing mark-only severing unchanged."+
                    "\nCompiled and saved only. No Play-mode or gameplay test.\n");
                Debug.Log("Saved longer stone impact hold and persistent on-screen hanging in level1.");
            }
            finally{if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}}
        }
        public static void Verify()
        {
            var c=UnityEngine.Object.FindObjectOfType<SerpentCampaign>();var a=c.stone;var s=a.stoneSlam;
            if(!s||s.attack!=a||!s.rigAsset||!s.skinMaterial||!s.ringMaterial||!s.viewCamera)throw new InvalidOperationException("Incomplete stone combat references.");
            if(ShaderUtil.ShaderHasError(s.skinMaterial.shader))throw new InvalidOperationException("Stone shader error.");
            var r=JsonUtility.FromJson<StoneSlamCombat.RigData>(s.rigAsset.text);
            if(r.poses.Length!=r.frameCount*(r.rest.Length/2)*3)throw new InvalidOperationException("Invalid pose track.");
            foreach(var m in r.meshes)if(m.vertices.Length!=m.weights.Length||m.joints.Length!=m.weights.Length)throw new InvalidOperationException("Invalid skin weights.");
            if(a.GetComponentsInChildren<Renderer>(true).Any(v=>v.enabled))throw new InvalidOperationException("Old attack renderers still visible.");
            File.WriteAllText(Output+"UnityCombatIntegration.txt",DateTime.Now.ToString("O")+"\nScene saved: "+c.gameObject.scene.path+
                "\nStone object: "+a.name+"\nWake duration: "+c.wake.bedWake.Duration+" s; first attack delay: "+c.firstStoneAttackDelay+
                "\nEnter -> Tracking -> Locked charge (no VFX) -> Strike -> Impact -> Recover -> Exit; camera remains independent."+
                "\nTracking/Warning/Strike: "+s.trackingSeconds+" / "+s.warningSeconds+" / "+s.strikeSeconds+
                "\nHead width: "+s.headWidth+"; target floor frozen at charge onset; swept head hit detection."+
                "\nSkeleton joints: "+r.rest.Length/2+"; pose frames: "+r.frameCount+"; old visuals disabled; shader errors: false."+
                "\nCompilation/reference/data checks only. No Play-mode test.\n");
            Debug.Log("Stone slam combat installed and saved; no Play-mode test.");
        }
    }
}
