using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1UnifiedGalleryEditor
    {
        const string Art="Assets/Art/Textures/Level1/UnifiedGallery/";
        const string Source="ArtSource/SerpentKing/UnifiedGallery_v003/";
        const string Pending=Source+"Apply.pending";
        static double nextCheck;
        static Level1UnifiedGalleryEditor(){EditorApplication.update+=ProcessPending;}
        static void ProcessPending()
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Pending)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                Apply();File.Delete(Pending);
                Level1RoyalBedchamberEditor.BuildPlayer();
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nUnified gallery saved and standalone player packaged. No Play mode or tests.\n");
            }
            catch(Exception e){File.WriteAllText(Source+"ApplyError.txt",e.ToString());File.Delete(Pending);Debug.LogException(e);}
        }
        [MenuItem("Aloysius/Level1/Apply Continuous Gallery And Courtyard Layers")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply in Edit mode.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            if(route.integratedGalleryTurn)throw new InvalidOperationException("Unified gallery already installed; preserve user changes.");
            Directory.CreateDirectory(Source);
            var backup=Source+"level1-before-unified-gallery-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save live scene backup.");
            foreach(string name in new[]{"GalleryWithTurn","OppositeClosedWing","EnclosedHall"})
            {
                string path=Art+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Default;
                imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.npotScale=TextureImporterNPOTScale.None;
                imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.filterMode=FilterMode.Bilinear;imp.wrapMode=TextureWrapMode.Clamp;imp.SaveAndReimport();
            }
            var gallery=SpriteAsset("GalleryWithTurn",null);
            var wing=SpriteAsset("OppositeClosedWing",new Rect(16,138,2141,393));
            var closed=SpriteAsset("EnclosedHall",null);
            var corridor=route.corridor;var settings=new SerializedObject(corridor);
            var start=(Transform)settings.FindProperty("startBoundary").objectReferenceValue;
            var end=(Transform)settings.FindProperty("endBoundary").objectReferenceValue;
            var ground=(Transform)settings.FindProperty("ground").objectReferenceValue;
            float floor=ground.position.y, width=end.position.x-start.position.x;
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoomView>(true)).First().GetComponent<Camera>();
            var oldArchitecture=corridor.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith("Window gallery ")).ToArray();
            var material=new Material(oldArchitecture.First().sharedMaterial){name="Unified gallery and turn | matched exposure"};
            AssetDatabase.CreateAsset(material,Art+"Gallery.mat");
            foreach(var r in oldArchitecture){Undo.RecordObject(r.gameObject,"Replace gallery architecture");r.gameObject.SetActive(false);}
            var oldDistant=corridor.transform.Find("03 Parallax | distant palace and trees");if(oldDistant)oldDistant.gameObject.SetActive(false);
            // Remove the overlaid, independently scaled corridor image from both locations.
            foreach(var r in route.GetComponentsInChildren<SpriteRenderer>(true))
                if(r.name=="Open depth passage entrance" || r.name=="Perspective corridor | repeated arches and visible floor")r.gameObject.SetActive(false);

            var root=Child(corridor.transform,"09 Unified gallery | continuous floor and right turn");
            float height=width*gallery.rect.height/gallery.rect.width;
            float bottom=floor-height*(64f/724f);
            var architecture=Add(root,"Glass gallery and turn | one continuous architecture layer",gallery,new Rect(start.position.x,bottom,width,height),5,material);
            var farRoot=Child(corridor.transform,"10 Courtyard return wing | nearby closed corridor");
            var opposite=Add(farRoot,"Opposite closed corridor facade",wing,new Rect(start.position.x-5,floor+.35f,width+10,4.1f),-28,material);
            opposite.color=new Color(.82f,.86f,.9f,1);
            farRoot.gameObject.AddComponent<Level1PalaceParallaxLayer>().Configure(view,.72f,start.position.x);
            var treeRoot=Child(corridor.transform,"11 Midcourt trees | separate parallax layer");
            var treeSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/Level1/PalaceCorridor/GardenNear-GardenNear.asset");
            var trees=new List<Renderer>();
            for(int i=0;i<2;i++)
            {
                var t=Add(treeRoot,"Midcourt tree silhouettes "+(i+1),treeSprite,new Rect(start.position.x-7+i*18,floor+.35f,19,2.3f),-22,material);
                t.color=new Color(.60f,.64f,.66f,1);trees.Add(t);
            }
            treeRoot.gameObject.AddComponent<Level1PalaceParallaxLayer>().Configure(view,.83f,start.position.x);
            var nearRoot=corridor.transform.Find("04 Parallax | garden hedges");
            if(nearRoot){nearRoot.GetComponent<Level1PalaceParallaxLayer>().Configure(view,.94f,start.position.x);EditorUtility.SetDirty(nearRoot.GetComponent<Level1PalaceParallaxLayer>());}
            var visuals=settings.FindProperty("corridorVisuals");var active=ReadRenderers(visuals).Where(r=>r&&r.gameObject.activeInHierarchy).ToList();
            active.Add(architecture);active.Add(opposite);active.AddRange(trees);WriteRenderers(visuals,active);settings.ApplyModifiedProperties();
            var storm=corridor.GetComponent<Level1PalaceStorm>();
            if(!storm)storm=corridor.GetComponentInChildren<Level1PalaceStorm>(true);
            if(storm)
            {
                var s=new SerializedObject(storm);var layers=s.FindProperty("layers");
                for(int i=0;i<layers.arraySize;i++)
                {
                    var layer=layers.GetArrayElementAtIndex(i);var refs=layer.FindPropertyRelative("renderers");
                    var items=ReadRenderers(refs).Where(r=>r&&r.gameObject.activeInHierarchy).ToList();
                    int kind=layer.FindPropertyRelative("layer").enumValueIndex;
                    if(kind==(int)Level1PalaceStorm.Layer.Architecture)items.Add(architecture);
                    if(kind==(int)Level1PalaceStorm.Layer.Distant)items.Add(opposite);
                    if(kind==(int)Level1PalaceStorm.Layer.Garden)items.AddRange(trees);
                    WriteRenderers(refs,items);
                }
                s.ApplyModifiedProperties();EditorUtility.SetDirty(storm);
            }
            var enclosed=route.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.name=="Solid walls and red carpet");
            var before=enclosed.bounds;enclosed.sprite=closed;enclosed.transform.localScale=new Vector3(before.size.x/closed.bounds.size.x,before.size.y/closed.bounds.size.y,1);
            var indoor=new Material(material){name="Closed return corridor | same muted palette"};indoor.SetVector("_WorldClip",new Vector4(-1000,1000,-1000,1000));indoor.SetFloat("_Exposure",.68f);
            AssetDatabase.CreateAsset(indoor,Art+"EnclosedHall.mat");enclosed.sharedMaterial=indoor;EditorUtility.SetDirty(enclosed);

            // All three path markers now occupy the actual painted turn at the gallery end.
            Vector3 Point(float pixelX,float pixelY)=>new Vector3(start.position.x+pixelX/2172f*width,bottom+(724-pixelY)/724f*height,0);
            route.depthNear.position=Point(1840,660);route.depthFar.position=Point(2090,490);route.depthLeftTurn.position=Point(2065,490);
            route.depthEntrance.position=route.depthNear.position;route.returnToCorridor.position=route.depthNear.position;
            route.depthCamera.position=new Vector3(end.position.x-3.15f*16f/9f,floor+2.65f,-10);
            route.depthCameraSize=3.15f;route.distantCharacterScale=.55f;route.depthTravelDistance=4.8f;route.depthWalkSpeed=1.15f;
            route.integratedGalleryTurn=true;
            EditorUtility.SetDirty(route);EditorUtility.SetDirty(corridor);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save unified gallery.");
            Selection.activeGameObject=root.gameObject;
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nLive backup: "+backup+"\nContinuous gallery and turn replace both old gallery modules and pasted entrance.\nDepth path lies in original gallery coordinates. Old remote depth backdrop disabled.\nSeparate sky / opposite closed wing / midcourt trees / near hedges / glass architecture.\nOriginal floor, props, player, guards and original pipe kept. Storm renderer registrations updated.\nNo Play mode or tests.\n");
        }
        static List<Renderer> ReadRenderers(SerializedProperty p){var r=new List<Renderer>();for(int i=0;i<p.arraySize;i++)r.Add(p.GetArrayElementAtIndex(i).objectReferenceValue as Renderer);return r;}
        static void WriteRenderers(SerializedProperty p,IEnumerable<Renderer> refs){var a=refs.Where(r=>r).Distinct().ToArray();p.arraySize=a.Length;for(int i=0;i<a.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=a[i];}
        static Transform Child(Transform p,string name){var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform;}
        static Sprite SpriteAsset(string name,Rect? rect)
        {var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+name+".png");var s=Sprite.Create(tex,rect??new Rect(0,0,tex.width,tex.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);s.name=name;AssetDatabase.CreateAsset(s,Art+name+".asset");return s;}
        static SpriteRenderer Add(Transform p,string name,Sprite sprite,Rect rect,int order,Material material)
        {var t=Child(p,name);t.position=new Vector3(rect.center.x,rect.center.y,0);t.localScale=new Vector3(rect.width/sprite.bounds.size.x,rect.height/sprite.bounds.size.y,1);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;r.sortingOrder=order;return r;}
    }
}
