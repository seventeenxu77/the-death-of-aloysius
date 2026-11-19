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
    public static class Level1EnclosedRoomEditor
    {
        const string Art="Assets/Art/Textures/Level1/GuardRoomEnclosed/";
        const string Mats="Assets/Art/Materials/Level1/GuardRoomEnclosed/";
        const string Pending="ApplyEnclosedRoomAfterStop";
        [Serializable] class Crop { public int x,y,width,height; }
        static Level1EnclosedRoomEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending,false))
                    EditorApplication.delayCall+=Apply;
            };
        }
        [MenuItem("Aloysius/Level1/Apply Approved Enclosed Room")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;
            }
            SessionState.SetBool(Pending,false);EditorApplication.delayCall-=Apply;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var lighting=all.Select(t=>t.GetComponent<Level1Lighting>()).First(c=>c);
            if(AssetDatabase.GetAssetPath(lighting.Room.sharedMaterial).StartsWith(Mats))return;
            Directory.CreateDirectory("ArtSource/GuardRoomEnclosed/Backups");
            string backup="ArtSource/GuardRoomEnclosed/Backups/level1-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up the live room scene.");
            Directory.CreateDirectory(Mats);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string name in new[]{"Room","Door"})
            {
                string path=Art+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            }
            var roomMat=new Material(lighting.Room.sharedMaterial){name="Enclosed guard room | wood floor and simple walls"};
            roomMat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Room.png");
            // Full image UVs preserve the room corners and floor depth. The walking
            // plane stays where it was, in front of the rear-wall skirting.
            roomMat.SetVector("_FloorUVRemap",Vector4.zero);
            AssetDatabase.CreateAsset(roomMat,Mats+"Room.mat");
            Undo.RecordObject(lighting.Room,"Apply enclosed room background");lighting.Room.sharedMaterial=roomMat;

            var door=all.First(t=>t.name=="Door");var renderer=door.GetComponent<SpriteRenderer>();var collider=door.GetComponent<BoxCollider2D>();
            Undo.RecordObjects(new UnityEngine.Object[]{door,renderer,collider},"Fit recessed door to rear wall");
            Vector3 originalScale=door.localScale;Vector3 position=door.position;
            var roomBounds=lighting.Room.bounds;
            float backWallFloor=roomBounds.min.y+(1f-768f/941f)*roomBounds.size.y;
            var crop=JsonUtility.FromJson<Crop>(File.ReadAllText("ArtSource/GuardRoomEnclosed/DoorRect.json"));
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Door.png");
            var sprite=Sprite.Create(texture,new Rect(crop.x,crop.y,crop.width,crop.height),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
            sprite.name="Recessed door | flush bottom";AssetDatabase.CreateAsset(sprite,Art+"Door.asset");
            Vector3 worldScale=door.lossyScale;
            // Match the approved image's compact door dimensions relative to the room.
            Vector2 desiredWorld=new Vector2(roomBounds.size.x*(166f/1672f),roomBounds.size.y*(275f/941f));
            Vector2 localSize=new Vector2(desiredWorld.x/Mathf.Abs(worldScale.x),desiredWorld.y/Mathf.Abs(worldScale.y));
            renderer.sprite=sprite;renderer.drawMode=SpriteDrawMode.Sliced;renderer.size=localSize;
            door.localScale=originalScale;
            position.y=backWallFloor;door.position=position;
            collider.size=localSize;collider.offset=new Vector2(0,localSize.y*.5f);
            var doorMat=new Material(renderer.sharedMaterial){name="Recessed door | simple old gold frame"};
            doorMat.SetFloat("_StyleBlend",0);AssetDatabase.CreateAsset(doorMat,Mats+"Door.mat");renderer.sharedMaterial=doorMat;
            lighting.Apply();
            EditorUtility.SetDirty(lighting.Room);EditorUtility.SetDirty(door);EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(collider);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save enclosed room.");
            Directory.CreateDirectory("Logs/Level1/GuardRoomEnclosed");
            File.WriteAllText("Logs/Level1/GuardRoomEnclosed/Installed.txt",DateTime.Now.ToString("O")+"\nEnclosed room background applied; original walking floor, bed, window and pipe retained.\nRight door bottom aligned to rear skirting; horizontal position retained; door size and contact box updated.\nLive backup: "+backup+"\nNo tests or Play mode started.");
            Selection.activeGameObject=lighting.Room.gameObject;
        }
    }
}
