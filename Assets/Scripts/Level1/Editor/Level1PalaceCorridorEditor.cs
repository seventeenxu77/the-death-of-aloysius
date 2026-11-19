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
    public static class Level1PalaceCorridorEditor
    {
        const string Art = "Assets/Art/Textures/Level1/PalaceCorridor/";
        const string Materials = "Assets/Art/Materials/Level1/PalaceCorridor/";
        const string RootName = "Palace Corridor | glass gallery and cover";
        [Serializable] class Slice { public string name; public int x, y, width, height; }
        [Serializable] class SliceList { public Slice[] slices; }

        [MenuItem("Aloysius/Level1/Install Palace Corridor")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install in Edit mode.");
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/level1.unity", OpenSceneMode.Additive);
            if (scene.GetRootGameObjects().Any(g => g.name == RootName))
                throw new InvalidOperationException("Palace corridor already exists. Keep the user's layout; do not reinstall.");
            string backup = "ArtSource/PalaceCorridor/Integration/level1-before-palace-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not save live scene backup.");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var actor = all.Select(t => t.GetComponent<GothicBride2DWalker>()).First(c => c);
            var traversal = actor.GetComponent<Level1TraversalController>();
            var roomView = all.Select(t => t.GetComponent<Level1RoomView>()).First(c => c);
            var camera = roomView.GetComponent<Camera>();
            var lighting = all.Select(t => t.GetComponent<Level1Lighting>()).First(c => c);
            var body = actor.GetComponent<Level1PlayerContactBody>();
            float floor = actor.transform.position.y;
            var floorMarker = all.FirstOrDefault(t => t.name.StartsWith("Floor alignment"));
            if (floorMarker) floor = floorMarker.position.y;
            float start = roomView.RoomSize.x * .5f;
            const float moduleWidth = 12f;
            float end = start + 2 * moduleWidth;

            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var slices = JsonUtility.FromJson<SliceList>(File.ReadAllText("ArtSource/PalaceCorridor/Integration/PropSlices.json"));
            Import("PalaceProps", 100, slices.slices.Select(s => new SpriteMetaData {
                name = s.name, rect = new Rect(s.x, s.y, s.width, s.height), alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0)
            }).ToArray());
            Import("WindowArchitecture", 1672f / moduleWidth, null);
            Import("GardenFar", 100, new[] { new SpriteMetaData { name = "GardenFar", rect = new Rect(8, 724-492, 2154, 196), alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0) } });
            Import("GardenNear", 100, new[] { new SpriteMetaData { name = "GardenNear", rect = new Rect(6, 724-607, 2161, 285), alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0) } });
            var props = slices.slices.ToDictionary(s => s.name, s => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "PalaceProps-" + s.name + ".asset"));
            var architecture = AssetDatabase.LoadAllAssetsAtPath(Art + "WindowArchitecture.png").OfType<Sprite>().First();
            var far = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "GardenFar-GardenFar.asset");
            var near = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "GardenNear-GardenNear.asset");
            var material = MakeMaterial("Architecture", .01f, start, end);
            var propMaterial = MakeMaterial("PalaceProps", .8f, start, end);
            var gardenMaterial = MakeMaterial("Garden", .03f, start, end);
            var solid = SolidSprite();

            var root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Add palace corridor");
            var controller = root.AddComponent<Level1PalaceCorridor>();
            var markers = Child(root.transform, "01 Bounds and ground");
            var startMarker = Child(markers, "Start boundary"); startMarker.position = new Vector3(start, floor, 0);
            var endMarker = Child(markers, "End boundary"); endMarker.position = new Vector3(end, floor, 0);
            var ground = Child(markers, "Ground surface | shared room elevation"); ground.position = new Vector3((start+end)*.5f, floor, 0);
            var groundCollider = ground.gameObject.AddComponent<BoxCollider2D>();
            groundCollider.size = new Vector2(end-start, .15f); groundCollider.offset = new Vector2(0, -.075f);
            var architectureRoot = Child(root.transform, "02 Architecture | two window gallery sections");
            var distantRoot = Child(root.transform, "03 Parallax | distant palace and trees");
            var nearRoot = Child(root.transform, "04 Parallax | garden hedges");
            var foreground = Child(root.transform, "05 Cover props | move individual objects");
            var wallDecor = Child(root.transform, "06 Wall art");
            distantRoot.gameObject.AddComponent<Level1PalaceParallaxLayer>().Configure(camera, .26f, start);
            nearRoot.gameObject.AddComponent<Level1PalaceParallaxLayer>().Configure(camera, .62f, start);
            var visuals = new List<Renderer>();
            SpriteRenderer Add(string name, Transform parent, Sprite sprite, Material mat, Vector3 pos, Vector3 scale, int order)
            {
                var t=Child(parent,name);t.position=pos;t.localScale=scale;
                var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=mat;r.sortingOrder=order;visuals.Add(r);return r;
            }
            var sky=Add("Night sky | solid slate",architectureRoot,solid,material,new Vector3((start+end)*.5f,floor+3.3f,6),new Vector3(end-start,8,1),-40);
            sky.color=new Color(.16f,.18f,.20f,1);
            var gardenGround=Add("Garden ground | behind glass",architectureRoot,solid,material,new Vector3((start+end)*.5f,floor+.55f,5),new Vector3(end-start,1.5f,1),-35);
            gardenGround.color=new Color(.08f,.09f,.085f,1);
            // Overscan tiles continue beyond camera bounds, while the shader clips them to this corridor.
            for(int i=-1;i<4;i++)
                Add("Distant palace and treeline "+(i+2),distantRoot,far,gardenMaterial,new Vector3(start+8+i*17f,floor+1.05f,4),Vector3.one*.80f,-25);
            for(int i=-1;i<5;i++)
                Add("Near garden silhouette "+(i+2),nearRoot,near,gardenMaterial,new Vector3(start+5+i*10f,floor+.55f,3),Vector3.one*.48f,-15);
            float ppu=architecture.pixelsPerUnit;
            float centerY=floor-97f/ppu+architecture.bounds.size.y*.5f;
            for(int i=0;i<2;i++)
                Add("Window gallery "+(i+1),architectureRoot,architecture,material,new Vector3(start+moduleWidth*(i+.5f),centerY,2),Vector3.one,5);

            void Prop(string spriteName,string label,float x,float height,float bottom,int order,bool cover)
            {
                var s=props[spriteName];float scale=height/s.bounds.size.y;
                var r=Add(label,cover?foreground:wallDecor,s,propMaterial,new Vector3(x,bottom,cover?-.5f:1),Vector3.one*scale,order);
                if(cover)
                {
                    var collider=r.gameObject.AddComponent<BoxCollider2D>();collider.size=s.bounds.size;collider.offset=s.bounds.center;
                    collider.isTrigger=true;
                    r.gameObject.AddComponent<Level1PropHighlight>().Configure(body.BodyBox);
                }
            }
            Prop("Statue","Draped statue | full-height cover",start+4.4f,2.5f,floor,42,true);
            Prop("Bust","Royal bust and plinth | narrow cover",start+8.7f,2.1f,floor,42,true);
            Prop("Screen","Folding screen | broad cover",start+13.6f,2.3f,floor,43,true);
            Prop("Planter","Palace urn and plant | low cover",start+18.1f,1.5f,floor,42,true);
            Prop("Cabinet","Palace cabinet | low cover",start+21.8f,1.25f,floor,42,true);
            Prop("Painting","Framed royal portrait | wall decoration",start+12,1.35f,floor+1.8f,10,false);

            var pipe=traversal.PipeRenderer;var sprite=pipe.sprite;
            var traversalSettings=new SerializedObject(traversal);
            float pipeEnd=traversalSettings.FindProperty("pipeEnd").floatValue;
            float uY=traversalSettings.FindProperty("pipeFloor").animationCurveValue.Evaluate(pipeEnd);
            Vector3 mouthPosition=pipe.transform.TransformPoint(new Vector3((pipeEnd*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,(uY*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0));
            mouthPosition.y=traversal.PipeFloorAtX(mouthPosition.x);
            // The mouth remains attached to the original pipe. Moving the pipe keeps its exit with it.
            var mouth=Child(pipe.transform,"Right mouth | corridor exit");mouth.position=mouthPosition;
            traversalSettings.FindProperty("rightExitMouth").objectReferenceValue=mouth;
            traversalSettings.FindProperty("enableRightExit").boolValue=true;
            traversalSettings.ApplyModifiedProperties();

            var settings=new SerializedObject(controller);
            Set(settings,"actor",actor);Set(settings,"traversal",traversal);Set(settings,"drag",actor.GetComponent<Level1DragController>());
            Set(settings,"lighting",lighting);Set(settings,"startBoundary",startMarker);Set(settings,"endBoundary",endMarker);Set(settings,"ground",ground);
            var renderers=settings.FindProperty("corridorVisuals");renderers.arraySize=visuals.Count;
            for(int i=0;i<visuals.Count;i++)renderers.GetArrayElementAtIndex(i).objectReferenceValue=visuals[i];
            settings.ApplyModifiedProperties();
            var cameraSettings=new SerializedObject(roomView);Set(cameraSettings,"palaceCorridor",controller);cameraSettings.ApplyModifiedProperties();
            var lightSettings=new SerializedObject(lighting);Set(lightSettings,"palaceCorridor",controller);lightSettings.ApplyModifiedProperties();
            var actorSettings=new SerializedObject(actor);var platforms=actorSettings.FindProperty("oneWaySurfaces");
            int previous=platforms.arraySize;platforms.arraySize=previous+1;platforms.GetArrayElementAtIndex(previous).objectReferenceValue=groundCollider;actorSettings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save palace scene.");
            Selection.activeGameObject=root;
            Directory.CreateDirectory("Logs/Level1/PalaceCorridor");
            File.WriteAllText("Logs/Level1/PalaceCorridor/Installed.txt",DateTime.Now.ToString("O")+"\nScene: "+scene.path+"\n24 unit corridor; 2 gallery sections; 6 props; 2 parallax layers.\nLive backup: "+backup+"\nNo tests or Play mode requested or run.");
        }

        static Transform Child(Transform parent,string name)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
        static void Set(SerializedObject settings,string field,UnityEngine.Object value)=>settings.FindProperty(field).objectReferenceValue=value;
        static void Import(string name,float ppu,SpriteMetaData[] slices)
        {
            string path=Art+name+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=slices==null?TextureImporterType.Sprite:TextureImporterType.Default;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=ppu;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);textureSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            if(slices!=null)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                foreach(var slice in slices)
                {
                    var sprite=Sprite.Create(texture,slice.rect,slice.pivot,ppu,0,SpriteMeshType.FullRect);
                    sprite.name=slice.name;
                    AssetDatabase.CreateAsset(sprite,Art+name+"-"+slice.name+".asset");
                }
            }
        }
        static Material MakeMaterial(string name,float cutoff,float start,float end)
        {
            var shader=Shader.Find("Aloysius/Level1/PalaceSprite");if(!shader)throw new InvalidOperationException("Palace sprite shader is not imported.");
            var m=new Material(shader){name=name};m.SetFloat("_Cutoff",cutoff);m.SetFloat("_Exposure",.68f);m.SetVector("_WorldClip",new Vector4(start,end,-1000,1000));
            AssetDatabase.CreateAsset(m,Materials+name+".mat");return m;
        }
        static Sprite SolidSprite()
        {
            var t=new Texture2D(1,1,TextureFormat.RGBA32,false){name="Solid color",filterMode=FilterMode.Point};
            t.SetPixel(0,0,Color.white);t.Apply();AssetDatabase.CreateAsset(t,Art+"SolidColor.asset");
            var s=Sprite.Create(t,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);s.name="Solid color";AssetDatabase.AddObjectToAsset(s,t);return s;
        }
    }
}
