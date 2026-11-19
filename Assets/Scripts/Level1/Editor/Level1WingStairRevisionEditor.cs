using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aloysius.GothicStage;
namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1WingStairRevisionEditor
    {
        const string Source="ArtSource/PalaceCorridor/EnclosedWingStairs_v002/";
        const string Art="Assets/Art/Textures/Level1/EnclosedWing_v002/";
        static double next;
        static Level1WingStairRevisionEditor(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Source+"Apply.pending")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Source+"Apply.pending");
            try {Apply();Level1RoyalBedchamberEditor.BuildPlayer();File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nStair side-entrance revision saved and player packaged. No Play mode or tests.");}
            catch(Exception ex){File.WriteAllText(Source+"ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Aloysius/Level1/Apply Narrow Stairs And Side Entrance")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            var root=route.transform.Find("03 Windowed return wing | foreground entry, left stairs");
            if(!root)throw new InvalidOperationException("Approved return wing is missing.");
            if(root.Find("01a Narrow stairs and occluded side entrance"))throw new InvalidOperationException("Revision already installed; preserve user edits.");
            string backup=Source+"level1-before-stair-revision-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save backup.");
            Directory.CreateDirectory(Art);
            string image=Art+"StairSideEntrance.png";
            if(!File.Exists(image))File.Copy(Source+"StairSideEntrance.png",image);
            AssetDatabase.ImportAsset(image,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(image);
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            var original=root.Find("01 Architecture | transparent windows, sealed right wall").GetComponent<SpriteRenderer>();
            var bounds=original.bounds;float sx=bounds.size.x/2172f,sy=bounds.size.y/724f;
            Vector3 Point(float x,float y)=>new Vector3(bounds.min.x+x*sx,bounds.max.y-y*sy,original.transform.position.z);
            const float seam=400f;
            var revised=AssetDatabase.LoadAssetAtPath<Texture2D>(image);
            var oldTexture=original.sprite.texture;
            var right=Sprite.Create(oldTexture,new Rect(oldTexture.width*seam/2172f,0,oldTexture.width*(1-seam/2172f),oldTexture.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            right.name="Existing corridor | right of stair section";AssetDatabase.CreateAsset(right,Art+"UnchangedRight.asset");
            var left=Sprite.Create(revised,new Rect(0,0,revised.width*seam/2172f,revised.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            left.name="Narrow stairs and side-wall opening";AssetDatabase.CreateAsset(left,Art+"CorrectedLeft.asset");
            // Split the rendering at an empty wall section; keep the original pixels for all windows and right end.
            original.sprite=right;original.name="01b Original corridor windows and right end";
            original.transform.position=Point((seam+2172)*.5f,362);
            original.transform.localScale=new Vector3((2172-seam)*sx/right.bounds.size.x,724*sy/right.bounds.size.y,1);
            var go=new GameObject("01a Narrow stairs and occluded side entrance");go.transform.SetParent(root,false);
            go.transform.position=Point(seam*.5f,362);go.transform.localScale=new Vector3(seam*sx/left.bounds.size.x,724*sy/left.bounds.size.y,1);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=left;renderer.sharedMaterial=original.sharedMaterial;renderer.sortingOrder=original.sortingOrder;renderer.color=original.color;
            var profile=root.GetComponentInChildren<GothicWalkSurface>();
            Vector3 ProfilePoint(float x,float y)=>profile.transform.InverseTransformPoint(Point(x,y));
            Vector2 LocalPoint(float x,float y){var p=ProfilePoint(x,y);return new Vector2(p.x,p.y);}
            profile.points=new[]{LocalPoint(65,455),LocalPoint(260,455),LocalPoint(350,545),LocalPoint(1980,545)};
            var box=profile.GetComponent<BoxCollider2D>();
            var low=LocalPoint(65,545);var high=LocalPoint(1980,455);
            box.offset=new Vector2((low.x+high.x)*.5f,(low.y+high.y)*.5f-.1f);box.size=new Vector2(high.x-low.x,high.y-low.y+.2f);
            route.enclosedLeftDoor.position=Point(259,455);
            route.enclosedLeftSpawn.position=Point(251,455);
            route.enclosedLeft.position=Point(65,455);
            route.stairDoorHeightTolerance=.20f;
            EditorUtility.SetDirty(route);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(box);EditorUtility.SetDirty(original);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save revised scene.");
            Selection.activeGameObject=go;
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nBackup: "+backup+
                "\nOnly left architecture section replaced. Right windows/landscape/foreground entry unchanged.\nNarrow stairs, occluded side-facing doorway, aligned upper landing; support line and doorway/spawn markers updated.\nDoor E interaction requires upper-landing height within 0.20 units. No Play mode or tests.");
        }
    }
}
