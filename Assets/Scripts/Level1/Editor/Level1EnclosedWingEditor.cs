using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aloysius.GothicStage;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1EnclosedWingEditor
    {
        const string Source="ArtSource/PalaceCorridor/EnclosedWingAssets_v001/";
        const string Art="Assets/Art/Textures/Level1/EnclosedWing_v001/";
        const string RootName="03 Windowed return wing | foreground entry, left stairs";
        const string Pending=Source+"Integration/Apply.pending";
        static double nextPoll;
        static readonly string[] Names={"01_CorridorArchitecture","02_EntranceFrameSilhouette","03_NearTrees","04_MidForest","05_FarMountains","06_Clouds","07_NightSky"};
        static Level1EnclosedWingEditor(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Pending)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Pending);
            try {Install();Level1RoyalBedchamberEditor.BuildPlayer();File.WriteAllText(Source+"Integration/Completed.txt",DateTime.Now.ToString("O")+"\nScene saved and player packaged. No Play mode or tests launched.");}
            catch(Exception ex){File.WriteAllText(Source+"Integration/ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Aloysius/Level1/Install Approved Windowed Return Wing")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before installing.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            if(route.transform.Find(RootName))throw new InvalidOperationException("Return wing already installed; preserve user edits.");
            Directory.CreateDirectory(Source+"Integration");
            string backup=Source+"Integration/level1-before-windowed-wing-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            Directory.CreateDirectory(Art);
            foreach(string name in Names)if(!File.Exists(Art+name+".png"))File.Copy(Source+name+".png",Art+name+".png");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var sprites=new Dictionary<string,Sprite>();
            foreach(string name in Names)
            {
                string path=Art+name+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                string asset=Art+name+".asset";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(asset);
                if(!sprite){sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);sprite.name=name;AssetDatabase.CreateAsset(sprite,asset);}
                sprites[name]=sprite;
            }
            var corridorSettings=new SerializedObject(route.corridor);
            var galleryStart=(Transform)corridorSettings.FindProperty("startBoundary").objectReferenceValue;
            var galleryEnd=(Transform)corridorSettings.FindProperty("endBoundary").objectReferenceValue;
            var galleryGround=(Transform)corridorSettings.FindProperty("ground").objectReferenceValue;
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoomView>(true)).First().GetComponent<Camera>();
            float floor=galleryGround.position.y;
            float galleryLength=galleryEnd.position.x-galleryStart.position.x;
            float width=galleryLength*.95f/(1625f/2172f), sx=width/2172f;
            float actorHeight=route.actor.Parts.Where(p=>p).Select(p=>p.bounds.max.y-route.actor.transform.position.y).DefaultIfEmpty(1.5f).Max();
            float sy=Mathf.Max(sx,Mathf.Clamp(actorHeight,1,2.2f)*1.1f/95f);
            const float originX=220;
            Vector3 Point(float x,float y)=>new Vector3(originX+x*sx,floor+(545-y)*sy,0);
            var oldHall=route.enclosedRight.parent;
            var root=Child(route.transform,RootName);
            Undo.RegisterCreatedObjectUndo(root.gameObject,"Install approved windowed return wing");
            var indoor=MaterialAsset("Architecture",.86f,new Vector4(-1000,1000,-1000,1000));
            var architecture=Add(root,"01 Architecture | transparent windows, sealed right wall",sprites[Names[0]],
                new Rect(originX,floor+(545-724)*sy,width,724*sy),-30,indoor);
            // Scenery is clipped to the window band, then occluded by the opaque wall and mullions.
            var low=Point(497,479);var high=Point(1815,236);
            var clip=new Vector4(low.x,high.x,low.y,high.y);
            var exterior=MaterialAsset("Window landscape",.82f,clip);
            float refX=Point((65+1980)*.5f,545).x;
            float margin=galleryLength*.6f+2;
            var windowRect=new Rect(low.x-margin,low.y-.5f,high.x-low.x+margin*2,high.y-low.y+1);
            var ratios=new[]{0f,.08f,.18f,.40f,.72f};
            var layerNames=new[]{"07_NightSky","06_Clouds","05_FarMountains","04_MidForest","03_NearTrees"};
            for(int i=0;i<layerNames.Length;i++)
            {
                var layer=Child(root,"02 Parallax "+i+" | "+layerNames[i]);
                Add(layer,layerNames[i],sprites[layerNames[i]],windowRect,-65+i*5,exterior);
                layer.gameObject.AddComponent<Level1PalaceParallaxLayer>().Configure(camera,ratios[i],refX);
            }
            var frameMaterial=new Material(Shader.Find("Aloysius/Level1/EntranceSilhouette")){name="Foreground passage | faded bronze silhouette"};
            frameMaterial.SetColor("_Tint",new Color(.48f,.40f,.27f,.26f));AssetDatabase.CreateAsset(frameMaterial,Art+"Entrance.mat");
            var frameBottom=Point(1764,594);
            Add(root,"03 Foreground passage frame | no door, faded",sprites[Names[1]],new Rect(frameBottom.x,frameBottom.y,270*sx,360*sy),100,frameMaterial);
            var markers=Child(root,"04 Route markers | editable");
            route.enclosedLeft=Marker(markers,"Left walking limit",Point(65,450));
            route.enclosedRight=Marker(markers,"Right walking limit | solid end wall",Point(1980,545));
            route.enclosedLeftDoor=Marker(markers,"Upper landing doorway to antechamber | E",Point(250,450));
            route.enclosedRightDoor=Marker(markers,"Foreground arch return to mirror hall | E",Point(1900,545));
            route.enclosedLeftSpawn=Marker(markers,"Arrival from upper room",Point(246,450));
            route.enclosedRightSpawn=Marker(markers,"Arrival from depth passage",Point(1900,545));
            route.corridorHole=route.enclosedRightDoor; // Deprecated v001 field retains a valid marker.
            route.enclosedCameraSize=3.15f;
            var support=Child(root,"05 Walk surface | flat floor, stair incline, upper landing");
            support.position=Point(0,545);
            var box=support.gameObject.AddComponent<BoxCollider2D>();box.isTrigger=true;
            box.offset=new Vector2((65+1980)*.5f*sx,95*sy*.5f-.1f);box.size=new Vector2((1980-65)*sx,95*sy+.2f);
            var profile=support.gameObject.AddComponent<GothicWalkSurface>();
            profile.points=new[]{new Vector2(65*sx,95*sy),new Vector2(255*sx,95*sy),new Vector2(355*sx,0),new Vector2(1980*sx,0)};
            // Reject movement beyond the visible endpoint even for systems using physics contacts.
            var wall=Child(root,"06 Right end wall | physical boundary");wall.position=Point(2015,375);
            var wallCollider=wall.gameObject.AddComponent<BoxCollider2D>();wallCollider.size=new Vector2(.25f,340*sy);
            var actorSettings=new SerializedObject(route.actor);var surfaces=actorSettings.FindProperty("oneWaySurfaces");
            var retained=new List<UnityEngine.Object>();
            for(int i=0;i<surfaces.arraySize;i++)
            {
                var c=surfaces.GetArrayElementAtIndex(i).objectReferenceValue as Collider2D;
                if(c&&!c.transform.IsChildOf(oldHall))retained.Add(c);
            }
            retained.Add(box);surfaces.arraySize=retained.Count;
            for(int i=0;i<retained.Count;i++)surfaces.GetArrayElementAtIndex(i).objectReferenceValue=retained[i];
            actorSettings.ApplyModifiedPropertiesWithoutUndo();
            var removed=new List<string>();
            Action<GameObject> remove=obj=>{if(!obj)return;removed.Add(obj.name);Undo.DestroyObjectImmediate(obj);};
            remove(oldHall.gameObject);
            foreach(var t in route.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if(!t)continue;
                if(t.name=="00 Retired v001 shortcut | inactive"||t.name=="Open depth passage entrance"||
                   t.name=="Perspective corridor | repeated arches and visible floor"||t.name=="Retired crawl opening | inactive")remove(t.gameObject);
            }
            // The original mirror hall now uses its continuous gallery layer; remove its disabled predecessors.
            foreach(var t in route.corridor.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if(!t||t.gameObject.activeSelf)continue;
                if(t.name.StartsWith("Window gallery ")||t.name=="03 Parallax | distant palace and trees")remove(t.gameObject);
            }
            PruneRendererArrays(route.corridor,"corridorVisuals");
            foreach(var storm in route.corridor.GetComponentsInChildren<Level1PalaceStorm>(true))
            {
                var serialized=new SerializedObject(storm);var layers=serialized.FindProperty("layers");
                for(int i=0;i<layers.arraySize;i++)Prune(layers.GetArrayElementAtIndex(i).FindPropertyRelative("renderers"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(route);EditorUtility.SetDirty(route.actor);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Unable to save level1.");
            Selection.activeGameObject=root.gameObject;
            File.WriteAllText(Source+"Integration/Installed.txt",DateTime.Now.ToString("O")+"\nScene: Assets/Scenes/level1.unity\nBackup: "+backup+
                "\nGallery length: "+galleryLength+"; new corridor run: "+(1625*sx)+"\nRoot: "+RootName+
                "\n7 source textures; independently moving window scenery, alpha window openings, faded foreground frame, real walk height profile.\nRemoved obsolete scene objects:\n"+
                string.Join("\n",removed)+"\nKept original bedroom, pipe, mirror hall, guards, antechamber, giant bed and curtain interactions. No Play mode or tests.\n");
        }
        static void PruneRendererArrays(UnityEngine.Object target,string name){var s=new SerializedObject(target);Prune(s.FindProperty(name));s.ApplyModifiedPropertiesWithoutUndo();}
        static void Prune(SerializedProperty p){var list=new List<UnityEngine.Object>();for(int i=0;i<p.arraySize;i++){var v=p.GetArrayElementAtIndex(i).objectReferenceValue;if(v)list.Add(v);}p.arraySize=list.Count;for(int i=0;i<list.Count;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=list[i];}
        static Material MaterialAsset(string name,float exposure,Vector4 clip)
        {
            string path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Aloysius/Level1/PalaceSprite"));AssetDatabase.CreateAsset(m,path);}
            m.SetFloat("_Exposure",exposure);m.SetVector("_WorldClip",clip);return m;
        }
        static Transform Child(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
        static Transform Marker(Transform parent,string name,Vector3 position){var t=Child(parent,name);t.position=position;return t;}
        static SpriteRenderer Add(Transform parent,string name,Sprite sprite,Rect rect,int order,Material material)
        {
            var t=Child(parent,name);t.position=new Vector3(rect.center.x,rect.center.y,0);t.localScale=new Vector3(rect.width/sprite.bounds.size.x,rect.height/sprite.bounds.size.y,1);
            var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=order;r.sharedMaterial=material;return r;
        }
    }
}


