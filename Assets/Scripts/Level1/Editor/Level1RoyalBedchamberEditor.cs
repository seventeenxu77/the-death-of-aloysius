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
    public static class Level1RoyalBedchamberEditor
    {
        const string ScenePath="Assets/Scenes/level1.unity";
        const string RootName="Royal Bedchamber | front passage and giant curtained bed";
        const string Art="Assets/Art/Textures/Level1/RoyalBedchamber/";
        const string Mats="Assets/Art/Materials/Level1/RoyalBedchamber/";
        const string Source="ArtSource/SerpentKing/BedchamberLayers_v001/";
        static readonly Dictionary<string,Rect> crops=new Dictionary<string,Rect> {
            {"Bed_Interior",new Rect(42,151,1452,749)}, {"Bed_Frame",new Rect(0,0,1536,1024)},
            {"Curtain_Left",new Rect(72,39,866,1332)}, {"Curtain_Right",new Rect(158,31,851,1343)},
            {"Bench",new Rect(546,209,1080,366)}, {"Cabinet",new Rect(414,87,686,799)},
            {"Bell_Pull",new Rect(273,65,353,1422)}, {"Door_Leaf",new Rect(170,78,676,1377)},
            {"Snakeskin_Opening",new Rect(94,116,1061,1028)}, {"Chandelier",new Rect(275,23,762,1095)},
            {"Rug",new Rect(174,273,1824,176)}, {"Door_Lock",new Rect(416,352,708,376)}
        };
        static Dictionary<string,Sprite> sprites;
        static Material material;
        static float floor;

        [MenuItem("Aloysius/Level1/Install Royal Bedchamber")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install only in Edit mode.");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            if(scene.GetRootGameObjects().Any(g=>g.name==RootName))throw new InvalidOperationException("Bedchamber already installed. Preserve the edited layout.");
            Directory.CreateDirectory(Source+"Integration");
            string backup=Source+"Integration/level1-before-bedchamber-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save current scene backup.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Mats);
            foreach(var file in Directory.GetFiles(Source+"Sprites","*.png"))File.Copy(file,Art+Path.GetFileName(file),false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var file in Directory.GetFiles(Art,"*.png"))
            {
                var imp=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                imp.textureType=TextureImporterType.Default;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;
                imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;
                imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Bilinear;imp.SaveAndReimport();
            }
            sprites=new Dictionary<string,Sprite>();
            foreach(var crop in crops)sprites[crop.Key]=Slice(crop.Key,crop.Key,crop.Value);
            sprites["BedroomArchitecture"]=Slice("Scene_Empty","BedroomArchitecture",new Rect(0,0,1241,545));
            sprites["AntechamberArchitecture"]=Slice("Scene_Empty","AntechamberArchitecture",new Rect(1240,90,510,455));
            sprites["LockedEndWall"]=Slice("Scene_Empty","LockedEndWall",new Rect(1240,592,510,295));
            material=MakeMaterial("Room and furniture",null,.72f);

            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var actor=all.Select(t=>t.GetComponent<GothicBride2DWalker>()).First(c=>c);
            var corridor=all.Select(t=>t.GetComponent<Level1PalaceCorridor>()).First(c=>c);
            var roomView=all.Select(t=>t.GetComponent<Level1RoomView>()).First(c=>c);
            var corridorSettings=new SerializedObject(corridor);
            var end=(Transform)corridorSettings.FindProperty("endBoundary").objectReferenceValue;
            floor=((Transform)corridorSettings.FindProperty("ground").objectReferenceValue).position.y;
            var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,scene);
            Undo.RegisterCreatedObjectUndo(root,"Install royal bedchamber");
            var route=root.AddComponent<Level1RoyalBedchamber>();
            route.actor=actor;route.corridor=corridor;route.drag=actor.GetComponent<Level1DragController>();route.traversal=actor.GetComponent<Level1TraversalController>();
            var entry=Child(root.transform,"01 Mirror hall end | locked door and small frontal hole");
            float endX=end.position.x;
            Add("End wall | locked passage",entry,sprites["LockedEndWall"],new Rect(endX-5.8f,floor-.32f,5.8f,6.3f),6);
            Add("Locked door leaf",entry,sprites["Door_Leaf"],new Rect(endX-2.55f,floor+.1f,1.5f,3.85f),7);
            Add("Door latch and padlock",entry,sprites["Door_Lock"],new Rect(endX-2.1f,floor+1.5f,.60f,.32f),8);
            Add("Small snakeskin opening | facing the camera",entry,sprites["Snakeskin_Opening"],new Rect(endX-4.05f,floor+1.85f,.92f,.92f),9);
            route.corridorHole=Marker(entry,"Hole centre | jump and hold E",endX-3.59f,floor+2.31f);
            route.returnToCorridor=Marker(entry,"Return landing",endX-3.59f,floor);

            var ante=Child(root.transform,"02 Antechamber | left door leads to bedroom");
            Add("Antechamber architecture",ante,sprites["AntechamberArchitecture"],new Rect(92,floor-.375f,12.75f,11.375f),-30);
            Add("Small carpet",ante,sprites["Rug"],new Rect(93.8f,floor+.2f,8.25f,.75f),-18);
            Add("Narrow bench",ante,sprites["Bench"],new Rect(94.7f,floor+1.1f,2.4f,.95f),-10);
            Add("Service cabinet and tray",ante,sprites["Cabinet"],new Rect(98.9f,floor+1.1f,1.8f,2.1f),-10);
            Add("Bell and pull cord",ante,sprites["Bell_Pull"],new Rect(92.65f,floor+1.7f,.65f,4.3f),-9);
            Add("Small rear-side crawl opening",ante,sprites["Snakeskin_Opening"],new Rect(102.7f,floor+.8f,.9f,.9f),-9);
            Add("Antechamber chandelier",ante,sprites["Chandelier"],new Rect(97.6f,floor+6.6f,1.45f,2.15f),-8);
            var hinge=Child(ante,"Left door hinge | opens into bedroom");hinge.position=new Vector3(92.2f,floor+.05f,0);
            AddLocal("Left door leaf",hinge,sprites["Door_Leaf"],new Rect(0,0,1.05f,4.35f),-7);
            route.doorHinge=hinge;
            route.antechamberSpawn=Marker(ante,"Arrival at rear opening",102.9f,floor);
            route.antechamberHole=Marker(ante,"Return through opening | E",103.1f,floor);
            route.antechamberDoor=Marker(ante,"Left door interaction | E",93.15f,floor);
            route.antechamberLeft=Marker(ante,"Left walking limit",92.6f,floor);
            route.antechamberRight=Marker(ante,"Right walking limit",104.1f,floor);

            var room=Child(root.transform,"03 Giant bedchamber | spacious sleeping room");
            Add("Bedroom architecture",room,sprites["BedroomArchitecture"],new Rect(60,floor-.375f,31.025f,13.625f),-30);
            Add("Bedroom chandelier",room,sprites["Chandelier"],new Rect(74.4f,floor+10.2f,1.9f,2.9f),-7);
            Add("Door back to antechamber",room,sprites["Door_Leaf"],new Rect(88.9f,floor+.1f,1.2f,4.4f),-7);
            var bed=Child(room,"Giant bed | four independent animated layers");bed.position=new Vector3(68.5f,floor+10.55f,0);bed.localScale=new Vector3(15.5f/15.36f,9.75f/10.24f,1);
            AddLocal("01 Bed interior | behind curtains",bed,sprites["Bed_Interior"],new Rect(2.25f,-9.5f,10.9f,6),-15);
            Child(bed,"02 Sleeping serpent slot | empty");
            var cloth=bed.gameObject.AddComponent<Level1BedCurtains>();
            cloth.left=MakeCurtain(bed,"03 Left curtain | independent mesh","Curtain_Left",out var lUV);cloth.leftUV=lUV;
            cloth.right=MakeCurtain(bed,"04 Right curtain | independent mesh","Curtain_Right",out var rUV);cloth.rightUV=rUV;
            AddLocal("05 Fixed front canopy and posts",bed,sprites["Bed_Frame"],new Rect(0,-10.24f,15.36f,10.24f),-10);
            cloth.Rebuild();route.curtains=cloth;
            Add("Bed curtain pull | E nearby",room,sprites["Bell_Pull"],new Rect(84.3f,floor+.45f,.55f,3.2f),-8);
            route.curtainInteraction=Marker(room,"Curtain interaction | E",84.6f,floor);
            route.bedroomSpawn=Marker(room,"Bedroom arrival",88.8f,floor);
            route.bedroomDoor=Marker(room,"Return door interaction | E",89.1f,floor);
            route.bedroomLeft=Marker(room,"Left walking limit",60.8f,floor);
            route.bedroomRight=Marker(room,"Right walking limit",90.3f,floor);

            var platforms=new List<Collider2D>();
            platforms.Add(Ground(ante,92.6f,104.1f));platforms.Add(Ground(room,60.8f,90.3f));
            var aSO=new SerializedObject(actor);var list=aSO.FindProperty("oneWaySurfaces");int old=list.arraySize;list.arraySize+=platforms.Count;
            for(int i=0;i<platforms.Count;i++)list.GetArrayElementAtIndex(old+i).objectReferenceValue=platforms[i];aSO.ApplyModifiedPropertiesWithoutUndo();
            var camSO=new SerializedObject(roomView);camSO.FindProperty("royalBedchamber").objectReferenceValue=route;camSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(route);EditorUtility.SetDirty(cloth);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene could not be saved.");
            Selection.activeGameObject=root;
            File.WriteAllText(Source+"Integration/Installed.txt",DateTime.Now.ToString("O")+"\nScene: "+ScenePath+"\nBackup: "+backup+"\n13 textures; independent scene props, two curtain meshes, same-scene room route.\nAuthoring/import/save only; no Play mode or tests.\n");
        }
        static Sprite Slice(string textureName,string name,Rect topRect)
        {
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+textureName+".png");
            var rect=new Rect(topRect.x,tex.height-topRect.yMax,topRect.width,topRect.height);
            var s=Sprite.Create(tex,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);s.name=name;AssetDatabase.CreateAsset(s,Art+name+".asset");return s;
        }
        static Material MakeMaterial(string name,Texture texture,float exposure)
        {
            var m=new Material(Shader.Find("Aloysius/Level1/PalaceSprite")){name=name};m.SetFloat("_Exposure",exposure);m.SetVector("_WorldClip",new Vector4(-1000,1000,-1000,1000));
            if(texture)m.mainTexture=texture;AssetDatabase.CreateAsset(m,Mats+name.Replace(' ','_')+".mat");return m;
        }
        static Transform Child(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
        static Transform Marker(Transform parent,string name,float x,float y){var t=Child(parent,name);t.position=new Vector3(x,y,0);return t;}
        static SpriteRenderer Add(string name,Transform parent,Sprite sprite,Rect r,int order)
        {
            var t=Child(parent,name);t.position=new Vector3(r.center.x,r.center.y,0);t.localScale=new Vector3(r.width/sprite.bounds.size.x,r.height/sprite.bounds.size.y,1);
            var sr=t.gameObject.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sharedMaterial=material;sr.sortingOrder=order;return sr;
        }
        static SpriteRenderer AddLocal(string name,Transform parent,Sprite sprite,Rect r,int order)
        {var sr=Add(name,parent,sprite,r,order);sr.transform.localPosition=new Vector3(r.center.x,r.center.y,0);return sr;}
        static MeshFilter MakeCurtain(Transform parent,string name,string textureName,out Rect uv)
        {
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+textureName+".png");var c=crops[textureName];uv=new Rect(c.x/tex.width,1-c.yMax/tex.height,c.width/tex.width,c.height/tex.height);
            var t=Child(parent,name);var f=t.gameObject.AddComponent<MeshFilter>();var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=MakeMaterial(textureName,tex,.72f);r.sortingOrder=-12;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return f;
        }
        static BoxCollider2D Ground(Transform parent,float left,float right)
        {var t=Marker(parent,"Walking floor | one-way support",(left+right)*.5f,floor-.1f);var c=t.gameObject.AddComponent<BoxCollider2D>();c.size=new Vector2(right-left,.2f);return c;}

        [MenuItem("Aloysius/Level1/Start In Giant Bedchamber On Next Play")]
        public static void StartInBedroom(){SetStart(Level1RoyalBedchamber.StartArea.GiantBedchamber);}
        [MenuItem("Aloysius/Level1/Start In Original Room On Next Play")]
        public static void StartInOriginal(){SetStart(Level1RoyalBedchamber.StartArea.OriginalRoom);}
        static void SetStart(Level1RoyalBedchamber.StartArea value)
        {
            var route=UnityEngine.Object.FindObjectOfType<Level1RoyalBedchamber>();if(!route)return;
            Undo.RecordObject(route,"Change start area");route.startArea=value;EditorUtility.SetDirty(route);EditorSceneManager.MarkSceneDirty(route.gameObject.scene);Selection.activeGameObject=route.gameObject;
        }

        [MenuItem("Aloysius/Level1/Build Bedchamber Player")]
        public static void BuildPlayer()
        {
            // Produce the deliverable only. Do not launch it or enter Play mode.
            Directory.CreateDirectory("Builds/Level1Bedchamber");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{ScenePath}, locationPathName="Builds/Level1Bedchamber/AloysiusLevel1.exe",
                target=BuildTarget.StandaloneWindows64, options=BuildOptions.None
            });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new IOException("Player packaging failed: "+report.summary.result);
            File.WriteAllText(Source+"Integration/Build.txt",DateTime.Now.ToString("O")+"\nBuilt Builds/Level1Bedchamber/AloysiusLevel1.exe\nNot launched or tested.\n");
        }
    }
}
