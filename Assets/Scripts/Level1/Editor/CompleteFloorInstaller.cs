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
    public static class CompleteFloorInstaller
    {
        const string Art="Assets/Art/SerpentKing/FullFloor_v002/";
        const string Source="ArtSource/SerpentKing/FullFloor_v002/";
        [MenuItem("Aloysius/Serpent/Replace floor patches with complete floor")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            var c=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SerpentCampaign>(true)).Single();
            var fx=c.floorBreak;if(!fx)throw new InvalidOperationException("Floor break effect missing.");
            var room=c.route.bedroomSpawn.parent;var wall=room.Find("Bedroom architecture").GetComponent<SpriteRenderer>();
            var bounds=wall.bounds;var anchor=c.hatch.position;
            File.Copy(scene.path,Source+"BeforeFullFloor_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
            foreach(string name in new[]{"Floor_Intact","Floor_Broken"})
            {
                string path=Art+name+".png";AssetDatabase.ImportAsset(path);var i=(TextureImporter)AssetImporter.GetAtPath(path);
                i.textureType=TextureImporterType.Default;i.sRGBTexture=true;i.alphaSource=TextureImporterAlphaSource.FromInput;i.alphaIsTransparency=false;
                i.mipmapEnabled=false;i.npotScale=TextureImporterNPOTScale.None;i.maxTextureSize=2048;
                i.textureCompression=TextureImporterCompression.Uncompressed;i.filterMode=FilterMode.Bilinear;i.wrapMode=TextureWrapMode.Clamp;i.SaveAndReimport();
            }
            var shader=Shader.Find("Aloysius/Complete Bedroom Floor");if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Complete floor shader unavailable.");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"CompleteFloor.mat");
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,Art+"CompleteFloor.mat");}else mat.shader=shader;
            mat.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Floor_Intact.png"));
            mat.SetTexture("_AfterTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Floor_Broken.png"));mat.SetFloat("_Reveal",0);
            float exposure=wall.sharedMaterial.HasProperty("_Exposure")?wall.sharedMaterial.GetFloat("_Exposure"):.72f;
            var clip=new Vector4(bounds.min.x,bounds.max.x,anchor.y,1);mat.SetVector("_Bounds",clip);mat.SetFloat("_Exposure",exposure);
            // The architectural image now renders walls only; its original baked floor is clipped out.
            var walls=AssetDatabase.LoadAssetAtPath<Material>(Art+"BedroomWallsOnly.mat");
            if(!walls){walls=new Material(wall.sharedMaterial);AssetDatabase.CreateAsset(walls,Art+"BedroomWallsOnly.mat");}
            else walls.CopyPropertiesFromMaterial(wall.sharedMaterial);
            if(!walls.HasProperty("_FloorCutout"))throw new InvalidOperationException("Bedroom wall shader lacks floor separation.");
            walls.SetVector("_FloorCutout",clip);wall.sharedMaterial=walls;
            fx.enabled=false;fx.fullFloorMode=true;fx.floorMaterial=mat;fx.floorWorldSpan=new Vector2(bounds.min.x,bounds.max.x);
            fx.floorSortingOrder=wall.sortingOrder+1;fx.floorTop=2.5f;fx.floorBottom=-3.04f;
            fx.gameObject.name="Complete bedroom floor | intact, broken, dust and passage";
            fx.enabled=true;fx.ResetFloor();c.crackedFloor.gameObject.SetActive(false);
            EditorUtility.SetDirty(fx);EditorUtility.SetDirty(wall);EditorUtility.SetDirty(walls);EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Source+"UnityIntegration.txt",DateTime.Now.ToString("O")+"\nSaved "+scene.path+
                "\nEntire floor width: "+bounds.size.x+"; world span: "+fx.floorWorldSpan+"; floor order: "+fx.floorSortingOrder+
                "\nOriginal baked floor removed from architecture through a dedicated wall material. One whole-width floor mesh, two full-width textures. No local patch quad or rectangular edge fade."+
                "\nAuthored weak tile aligned to unchanged hatch at "+anchor+". Existing dust/debris, damage trigger, passage and destination retained."+
                "\nNo Play-mode test.\n");
            Debug.Log("Complete two-state bedroom floor installed and level1 saved. No Play-mode test.");
        }
    }
}
