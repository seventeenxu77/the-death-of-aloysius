using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1GuardRoomRestyleEditor
    {
        static Level1GuardRoomRestyleEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("FixPalaceRoomScaleAfterStop",false))
                {
                    SessionState.SetBool("FixPalaceRoomScaleAfterStop",false);
                    EditorApplication.delayCall+=FixPropScale;
                }
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("ApplyPalaceRoomAfterStop",false))
                {
                    SessionState.SetBool("ApplyPalaceRoomAfterStop",false);
                    EditorApplication.delayCall+=Install;
                }
            };
        }
        const string Art="Assets/Art/Textures/Level1/GuardRoomPalace/";
        const string Mats="Assets/Art/Materials/Level1/GuardRoomPalace/";
        [Serializable] class Slice
        {
            public string name;
            public int x,y,width,height;
            public float localMinX,localMinY,localWidth,localHeight;
        }
        [Serializable] class SliceList { public Slice[] slices; }
        [MenuItem("Aloysius/Level1/Apply Approved Palace Room Appearance")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool("ApplyPalaceRoomAfterStop",true);
                EditorApplication.isPlaying=false;
                return;
            }
            SessionState.SetBool("ApplyPalaceRoomAfterStop",false);
            EditorApplication.delayCall-=Install;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var lighting=all.Select(t=>t.GetComponent<Level1Lighting>()).First(c=>c);
            if(AssetDatabase.GetAssetPath(lighting.Room.sharedMaterial).StartsWith(Mats))
                return; // Preserve any subsequent manual changes on repeated menu requests.
            Directory.CreateDirectory("ArtSource/GuardRoomPalace/Backups");
            string backup="ArtSource/GuardRoomPalace/Backups/level1-live-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot back up live scene.");
            Directory.CreateDirectory(Mats);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string name in new[]{"Room","Bed","Door","Window","Pipe"})Import(name);
            var slices=JsonUtility.FromJson<SliceList>(File.ReadAllText("ArtSource/GuardRoomPalace/SpriteRects.json"));
            var floor=all.First(t=>t.name.StartsWith("Floor alignment"));
            var roomMaterial=new Material(lighting.Room.sharedMaterial){name="Palace guard room | approved brown and gold"};
            roomMaterial.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Room.png");
            float targetFloor=lighting.Room.transform.InverseTransformPoint(floor.position).y+.5f;
            roomMaterial.SetVector("_FloorUVRemap",new Vector4(targetFloor,142f/941f,1,0));
            AssetDatabase.CreateAsset(roomMaterial,Mats+"Room.mat");
            Undo.RecordObject(lighting.Room,"Replace room appearance");lighting.Room.sharedMaterial=roomMaterial;
            foreach(var slice in slices.slices)
            {
                var renderer=all.First(t=>t.name==slice.name).GetComponent<SpriteRenderer>();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+slice.name+".png");
                var pivot=new Vector2(Mathf.Clamp01(-slice.localMinX/slice.localWidth),Mathf.Clamp01(-slice.localMinY/slice.localHeight));
                var sprite=Sprite.Create(texture,new Rect(slice.x,slice.y,slice.width,slice.height),pivot,100,0,SpriteMeshType.FullRect);
                sprite.name=slice.name+" | palace silhouette";
                AssetDatabase.CreateAsset(sprite,Art+slice.name+".asset");
                Undo.RecordObjects(new UnityEngine.Object[]{renderer,renderer.transform},"Replace "+slice.name+" artwork only");
                Vector3 originalScale=renderer.transform.localScale;
                // Set renderer dimensions, never the object's Transform or Collider.
                // Original visible silhouette dimensions include its original pixel padding.
                renderer.sprite=sprite;renderer.drawMode=SpriteDrawMode.Sliced;
                renderer.size=new Vector2(slice.localWidth,slice.localHeight);
                // Switching Simple to Sliced can compensate Transform scale using
                // the new sprite's native dimensions. Restore the authored scale.
                renderer.transform.localScale=originalScale;
                var material=new Material(renderer.sharedMaterial){name=slice.name+" | palace silhouette"};
                material.SetFloat("_StyleBlend",0);
                AssetDatabase.CreateAsset(material,Mats+slice.name+".mat");renderer.sharedMaterial=material;
                EditorUtility.SetDirty(renderer);
            }
            var traversal=all.Select(t=>t.GetComponent<Level1TraversalController>()).First(c=>c);
            var pipe=traversal.PipeRenderer;
            var pipeMaterial=new Material(pipe.sharedMaterial){name="Snakeskin | dark scales and old gold"};
            pipeMaterial.SetTexture("_StyleTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Pipe.png"));
            pipeMaterial.SetFloat("_StyleBlend",1);
            AssetDatabase.CreateAsset(pipeMaterial,Mats+"Pipe.mat");
            Undo.RecordObject(pipe,"Replace pipe surface color only");pipe.sharedMaterial=pipeMaterial;
            // Keep the original pipe Sprite: its UV geometry, alpha mask and curve coordinates stay authoritative.
            var props=new SerializedObject(lighting);
            props.FindProperty("darkness").floatValue=.2f;
            props.FindProperty("edgeShadow").floatValue=.08f;
            props.FindProperty("characterLight").floatValue=.82f;
            props.ApplyModifiedProperties();
            AddCollar(lighting,traversal,pipeMaterial);
            lighting.Apply();
            EditorUtility.SetDirty(lighting);EditorUtility.SetDirty(pipe);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save approved room appearance.");
            Directory.CreateDirectory("Logs/Level1/GuardRoomPalace");
            File.WriteAllText("Logs/Level1/GuardRoomPalace/Installed.txt",DateTime.Now.ToString("O")+"\nApproved palace room appearance applied to live level1.\nNo existing Transform or Collider edited. Original pipe Sprite and traversal curves retained.\nLive scene backup: "+backup+"\nNo tests or Play mode run.");
            Selection.activeGameObject=lighting.Room.gameObject;
        }
        [MenuItem("Aloysius/Level1/Restore Palace Room Prop Proportions")]
        public static void FixPropScale()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool("FixPalaceRoomScaleAfterStop",true);
                EditorApplication.isPlaying=false;
                return;
            }
            SessionState.SetBool("FixPalaceRoomScaleAfterStop",false);
            EditorApplication.delayCall-=FixPropScale;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            string backup="ArtSource/GuardRoomPalace/Backups/level1-before-scale-fix-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            // Exact values from the live scene backup made before replacing the artwork.
            string[] names={"Bed","Door","Window"};
            float[] scales={.492126f,.42145595f,.519802f};
            for(int i=0;i<names.Length;i++)
            {
                var target=all.First(t=>t.name==names[i]);
                Undo.RecordObject(target,"Restore original "+names[i]+" proportions");
                target.localScale=Vector3.one*scales[i];
                EditorUtility.SetDirty(target);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save prop scale correction.");
            Directory.CreateDirectory("Logs/Level1/GuardRoomPalace");
            File.WriteAllText("Logs/Level1/GuardRoomPalace/ScaleFixed.txt",DateTime.Now.ToString("O")+"\nRestored Bed/Door/Window local scales from pre-restyle live backup. Positions and rotations untouched.\nNo tests or Play mode started.");
        }
        static void Import(string name)
        {
            string path=Art+name+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
        }
        static void AddCollar(Level1Lighting lighting,Level1TraversalController traversal,Material pipeMaterial)
        {
            var pipe=traversal.PipeRenderer;
            var sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Textures/Level1/PalaceCorridor/SolidColor.asset").OfType<Sprite>().First();
            float x=lighting.Room.bounds.max.x-.04f;
            float bottom=traversal.PipeFloorAtX(x)-.06f,top=traversal.PipeCeilingAtX(x)+.06f;
            if(top<=bottom || x<pipe.bounds.min.x || x>pipe.bounds.max.x)return;
            var root=new GameObject("Pipe wall collar | visual only");root.transform.SetParent(pipe.transform,false);
            root.transform.position=new Vector3(x,(bottom+top)*.5f,pipe.transform.position.z);
            var material=new Material(pipeMaterial){name="Pipe wall collar | muted bronze"};material.SetFloat("_StyleBlend",0);
            AssetDatabase.CreateAsset(material,Mats+"Collar.mat");
            var settings=new SerializedObject(lighting);var list=settings.FindProperty("roomProps");int first=list.arraySize;list.arraySize=first+2;
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Bronze band":"Thin rim");go.transform.SetParent(root.transform,false);
                go.transform.position=new Vector3(x-(i==1?.04f:0),(bottom+top)*.5f,root.transform.position.z);
                Vector3 parentScale=root.transform.lossyScale;
                go.transform.localScale=new Vector3((i==0?.09f:.012f)/parentScale.x,(top-bottom)/parentScale.y,1);
                var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;r.sortingOrder=pipe.sortingOrder+1;
                r.color=i==0?new Color(.25f,.19f,.11f,1):new Color(.43f,.33f,.18f,1);
                list.GetArrayElementAtIndex(first+i).objectReferenceValue=r;
            }
            settings.ApplyModifiedProperties();
        }
    }
}
