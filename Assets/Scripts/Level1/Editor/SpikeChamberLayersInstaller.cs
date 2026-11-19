using System;
using System.IO;
using System.Linq;
using System.Text;
using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class SpikeChamberLayersInstaller
    {
        const string Art="Assets/Art/SpikeChamberLayers_v003/",Source="ArtSource/SpikeChamberLayers_v003/";
        static SerpentCampaign campaign;
        static PalaceRoom room;
        static Transform layers;
        static Vector3 center;
        static float unit;
        static Material paper;
        [MenuItem("Aloysius/Serpent/Install layered spike hall and distant king")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            campaign=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SerpentCampaign>(true)).Single();room=campaign.rooms[1];
            File.Copy(scene.path,Source+"BeforeLayers_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
            center=room.framedView.position;unit=room.framedDimensions.x/1672;
            var old=room.root.GetComponentsInChildren<SpriteRenderer>(true);
            var foreground=old.First(r=>r.sprite&&r.sprite.texture.name=="Foreground");paper=foreground.sharedMaterial;
            layers=room.root.Find("Layered hall | independent architecture and distant serpent");
            if(!layers){layers=new GameObject("Layered hall | independent architecture and distant serpent").transform;layers.SetParent(room.root,false);}
            foreach(var r in old)if(!r.transform.IsChildOf(layers)&&r.sprite&&(r.sprite.texture.name=="Interior"||r.sprite.texture.name=="Foreground"||r.sprite.texture.name=="Serpent"))
                r.gameObject.SetActive(false);
            var far=Import("FarInterior");var mid=Import("MiddleArcade");
            Plate("01 Far walls, ceiling and perspective floor",far,-60,.055f);
            Plate("03 Middle arcade | in front of serpent",mid,-30,.018f);
            // Partition the approved transparent foreground without repainting or relocating it.
            // Each original pixel belongs to only one crop; world positions remain registered.
            Piece(foreground.sprite,"04 Near upper arches and capitals",new Rect(0,0,1672,534),-12);
            Piece(foreground.sprite,"06 Near left pillar lower",new Rect(0,534,566,316),33);
            Piece(foreground.sprite,"04 Upper gallery, supporting arches and staircase",new Rect(566,534,1090,316),-5);
            Piece(foreground.sprite,"06 Right boundary wall and doorway",new Rect(1656,534,16,316),33);
            Piece(foreground.sprite,"05 Walkable red carpet",new Rect(0,850,1672,28),-3);
            Piece(foreground.sprite,"07 Foreground walkway lip",new Rect(0,878,1672,63),32);
            var king=layers.Find("02 Distant Serpent King | independent skeletal body");
            if(!king){king=new GameObject("02 Distant Serpent King | independent skeletal body").transform;king.SetParent(layers,false);}
            king.position=new Vector3(room.bounds.xMin-.2f+6.5f,room.floor+11.6f,0);
            king.localScale=Vector3.one*(13.5f/15.36f);king.rotation=Quaternion.identity;
            var group=king.GetComponent<SortingGroup>();if(group)group.enabled=false;
            var template=campaign.wake.bedWake.blackGoldSkin;if(!template)throw new InvalidOperationException("Approved black/gold rig missing.");
            AssetDatabase.ImportAsset("Assets/Art/SerpentKing/BlackGoldWake_v026/BlackGoldWake.shader",ImportAssetOptions.ForceUpdate);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"FarSerpent.mat");
            if(!mat){mat=new Material(template.skinMaterial);AssetDatabase.CreateAsset(mat,Art+"FarSerpent.mat");}else mat.CopyPropertiesFromMaterial(template.skinMaterial);
            mat.shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/SerpentKing/BlackGoldWake_v026/BlackGoldWake.shader");
            if(!mat.HasProperty("_Exposure"))throw new InvalidOperationException("Refreshed distant-skin shader missing exposure.");
            mat.SetFloat("_Exposure",.28f);
            var skin=king.GetComponent<BlackGoldWakeSkin>();if(!skin)skin=king.gameObject.AddComponent<BlackGoldWakeSkin>();
            skin.enabled=false;skin.rigAsset=template.rigAsset;skin.skinMaterial=mat;skin.art=template.art;
            skin.bedBackground=null;skin.bedForeground=null;skin.drawBed=false;skin.sortingOffset=-40;skin.idleMotion=true;skin.enabled=true;
            var display=king.GetComponent<FarSerpentPresentation>();if(!display)display=king.gameObject.AddComponent<FarSerpentPresentation>();
            display.skin=skin;display.timingSource=campaign.wake.bedWake;display.awakePoseTime=25;display.Refresh();
            Depth(king,.035f);
            EditorUtility.SetDirty(skin);EditorUtility.SetDirty(display);if(group)EditorUtility.SetDirty(group);EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Source+"UnityIntegration.txt",DateTime.Now.ToString("O")+"\nSaved: "+scene.path+
                "\nDepth order: far hall -60; skinned king -40; middle arcade -30; near overhead arches -12; gallery/stairs -5; carpet -3; actor 20..31; foreground lip 32; near pillar/wall 33."+
                "\nFar king width 13.5m, 46 bones, no bed. Approved black/gold assets reused with exposure 0.28 and independent idle motion."+
                "\nFar/middle/king parallax: 0.055 / 0.018 / 0.035. Existing staircase support, spike, switch, doorway, bounds and camera frame unchanged."+
                "\nForeground partitioned into six registered sprite crops. Far plate inpainted; middle arcade extracted with alpha."+
                "\nNo Play-mode test.\n");
            RenderPreview();
        }
        static Sprite Import(string name)
        {
            string path=Art+name+".png";AssetDatabase.ImportAsset(path);var i=(TextureImporter)AssetImporter.GetAtPath(path);
            i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=100;i.alphaIsTransparency=true;
            i.mipmapEnabled=false;i.npotScale=TextureImporterNPOTScale.None;i.maxTextureSize=2048;i.textureCompression=TextureImporterCompression.Uncompressed;
            i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void Depth(Transform t,float amount)
        {
            var d=t.GetComponent<PalaceDepthLayer>();if(!d)d=t.gameObject.AddComponent<PalaceDepthLayer>();d.parallax=amount;d.cameraOrigin=center;EditorUtility.SetDirty(d);
        }
        static void Plate(string name,Sprite sprite,int order,float parallax)
        {
            var t=layers.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(layers,false);}
            t.gameObject.SetActive(true);
            t.position=center;t.localScale=new Vector3(room.framedDimensions.x/sprite.bounds.size.x,room.framedDimensions.y/sprite.bounds.size.y,1);
            var r=t.GetComponent<SpriteRenderer>();if(!r)r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=paper;r.sortingOrder=order;
            Depth(t,parallax);EditorUtility.SetDirty(r);
        }
        static void Piece(Sprite source,string name,Rect crop,int order)
        {
            string file="FG_"+crop.x+"_"+crop.y+"_"+crop.width+"_"+crop.height;
            string path=Art+file+".asset";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            // Crop coordinates are design pixels from top-left. The original alpha and exact geometry are retained.
            if(!sprite){float sx=source.texture.width/1672f,sy=source.texture.height/941f;
                sprite=Sprite.Create(source.texture,new Rect(crop.x*sx,source.texture.height-(crop.y+crop.height)*sy,crop.width*sx,crop.height*sy),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
                sprite.name=name;AssetDatabase.CreateAsset(sprite,path);}
            var t=layers.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(layers,false);}
            t.gameObject.SetActive(true);
            t.position=center+new Vector3((crop.center.x-836)*unit,(470.5f-crop.center.y)*unit,0);
            t.localScale=new Vector3(crop.width*unit/sprite.bounds.size.x,crop.height*unit/sprite.bounds.size.y,1);
            var r=t.GetComponent<SpriteRenderer>();if(!r)r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=paper;r.sortingOrder=order;EditorUtility.SetDirty(r);
        }
        public static void RenderPreview()
        {RenderPreviewTo(Source+"LayeredHall_WithSerpent.png");}
        public static void RenderPreviewTo(string outputPath)
        {
            // Deliverable render only: a temporary camera, no Play mode or gameplay simulation.
            var c=UnityEngine.Object.FindObjectOfType<SerpentCampaign>();var room=c.rooms[1];
            var go=new GameObject("Temporary hall artwork render"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();
            cam.orthographic=true;cam.orthographicSize=room.framedDimensions.y*.5f;cam.aspect=room.framedDimensions.x/room.framedDimensions.y;
            cam.transform.position=room.framedView.position+Vector3.back*10;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.012f,.025f);
            var target=new RenderTexture(1672,941,24);target.Create();cam.targetTexture=target;var previous=RenderTexture.active;
            var meshes=new System.Collections.Generic.List<Mesh>();
            // A temporary baked pose gives a scale reference without moving or operating the player.
            foreach(var part in c.actor.Parts)
            {
                var mesh=new Mesh();part.BakeMesh(mesh);meshes.Add(mesh);
                var t=new GameObject("Reference protagonist pose"){hideFlags=HideFlags.HideAndDontSave}.transform;t.SetParent(go.transform,false);
                var matrix=Matrix4x4.TRS(room.spawn.position,c.actor.transform.rotation,c.actor.transform.lossyScale)*c.actor.transform.worldToLocalMatrix*part.transform.localToWorldMatrix;
                t.position=matrix.GetColumn(3);t.rotation=matrix.rotation;t.localScale=matrix.lossyScale;
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=part.sharedMaterial;r.sortingOrder=part.sortingOrder;
                var properties=new MaterialPropertyBlock();part.GetPropertyBlock(properties);r.SetPropertyBlock(properties);
            }
            try{cam.Render();RenderTexture.active=target;var image=new Texture2D(1672,941,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1672,941),0,0);image.Apply();File.WriteAllBytes(outputPath,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
            finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
