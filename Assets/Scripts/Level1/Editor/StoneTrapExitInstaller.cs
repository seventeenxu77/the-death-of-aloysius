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
    public static class StoneTrapExitInstaller
    {
        const string Folder="ArtSource/LevelTask_20261116/";
        public static void RenderFangOverview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Static room artwork capture requires Edit mode.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Open level1 before the artwork capture.");
            var c=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();var room=c.rooms[2];
            var transforms=room.root.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
            var go=new GameObject("Temporary fang overview camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();
            cam.orthographic=true;cam.aspect=1;cam.orthographicSize=Mathf.Max(room.bounds.width,room.bounds.height)*.5f+1;
            cam.transform.position=new Vector3(room.bounds.center.x,room.bounds.center.y-.3f,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.01f,.025f);cam.cullingMask=1<<31;
            var target=new RenderTexture(1280,1280,24);target.Create();cam.targetTexture=target;var previous=RenderTexture.active;
            Texture2D image=null;var meshes=new System.Collections.Generic.List<Mesh>();
            try
            {
                foreach(var t in transforms)t.gameObject.layer=31;
                // A static baked protagonist gives the actual scale, without running gameplay.
                foreach(var part in c.actor.Parts)
                {
                    var mesh=new Mesh();part.BakeMesh(mesh);meshes.Add(mesh);
                    var t=new GameObject("Protagonist scale reference"){hideFlags=HideFlags.HideAndDontSave,layer=31}.transform;t.SetParent(go.transform,false);
                    var matrix=Matrix4x4.TRS(room.spawn.position,c.actor.transform.rotation,c.actor.transform.lossyScale)*c.actor.transform.worldToLocalMatrix*part.transform.localToWorldMatrix;
                    t.position=matrix.GetColumn(3);t.rotation=matrix.rotation;t.localScale=matrix.lossyScale;
                    t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=part.sharedMaterial;r.sortingOrder=part.sortingOrder;
                    var properties=new MaterialPropertyBlock();part.GetPropertyBlock(properties);r.SetPropertyBlock(properties);
                }
                cam.Render();RenderTexture.active=target;image=new Texture2D(1280,1280,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,1280),0,0);image.Apply();Directory.CreateDirectory(Folder);File.WriteAllBytes(Folder+"FangChamber_CurrentOverview.png",image.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<transforms.Length;i++)if(transforms[i])transforms[i].gameObject.layer=layers[i];
                RenderTexture.active=previous;if(image)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(go);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        [MenuItem("Aloysius/Serpent/Connect right gallery door to fang chamber")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before saving.");
            const string path="Assets/Scenes/level1.unity";
            var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var c=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();
                if(c.rooms.Length<3||c.rooms[2].id!="fang"||!c.rooms[2].spawn||!c.rooms[2].root||c.stairHeads==null||c.stairHeads.Length==0||!c.pitPortal)
                    throw new InvalidOperationException("Existing fang destination / spawn / snake platforms / right door missing.");
                Directory.CreateDirectory(Folder);File.Copy(path,Folder+"BeforeStoneExit_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
                var portal=c.pitPortal;portal.name="右侧贴墙门 | 击败巨石后自动进入突刺攀登";
                var zone=portal.Find("入口检测 | 二层门口，排除一层");
                if(!zone){zone=new GameObject("入口检测 | 二层门口，排除一层").transform;zone.SetParent(portal,false);}
                zone.localPosition=Vector3.zero;zone.localRotation=Quaternion.identity;zone.localScale=Vector3.one;
                var box=zone.GetComponent<BoxCollider2D>();if(!box)box=zone.gameObject.AddComponent<BoxCollider2D>();
                box.enabled=true;box.isTrigger=true;box.offset=new Vector2(0,.9f);box.size=new Vector2(1.4f,2.2f);
                c.pitDoorTrigger=box;c.enterPitDoorAutomatically=true;c.rooms[1].depthEntry=portal;
                EditorUtility.SetDirty(c);EditorUtility.SetDirty(portal);EditorUtility.SetDirty(box);
                EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Folder+"StoneExitIntegration.txt",DateTime.Now.ToString("O")+
                    "\nSaved: "+path+"\nSource: spike / right upper gallery door at "+portal.position+
                    "\nEntry bounds: "+box.bounds+"; automatic, requires StoneDefeated; lower floor excluded by vertical bounds."+
                    "\nDestination: fang / FangStairs, spawn "+c.rooms[2].spawn.position+"; existing alternating thrusting heads and support surfaces retained."+
                    "\nFlow: fade to black -> relocate / configure platforms and checkpoint / snap camera -> fade in -> enable destination gameplay."+
                    "\nFade per direction: "+c.fadeSeconds+" s. No new art, UI or Unity scene; same-scene room transition."+
                    "\nCompile and saved-reference checks only. No Play-mode or gameplay test.\n");
                Debug.Log("Saved right gallery door -> fang chamber automatic transition in level1.");
            }
            finally{if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}}
        }
    }
}
