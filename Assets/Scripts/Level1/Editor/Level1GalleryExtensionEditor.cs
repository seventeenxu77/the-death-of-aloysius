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
    public static class Level1GalleryExtensionEditor
    {
        const string Source="ArtSource/PalaceCorridor/GalleryExtension_v001/";
        const string Art="Assets/Art/Textures/Level1/GalleryExtension_v001/";
        const string Marker="12 Extension | two window bays before the turn";
        static double next;
        static Level1GalleryExtensionEditor(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Source+"Apply.pending")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Source+"Apply.pending");
            try
            {
                Apply();
                Level1RoyalBedchamberEditor.BuildPlayer();
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nGallery extension saved and player packaged. No Play mode or tests.");
            }
            catch(Exception ex){File.WriteAllText(Source+"ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Aloysius/Level1/Extend Gallery Beyond Cabinet")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            var hall=route.corridor;
            if(hall.transform.Find(Marker))throw new InvalidOperationException("Extension already installed; preserve subsequent edits.");
            var renderers=hall.GetComponentsInChildren<SpriteRenderer>(true);
            var original=renderers.First(r=>r.name=="Glass gallery and turn | one continuous architecture layer");
            var cabinet=renderers.First(r=>r.name=="Palace cabinet | low cover");
            var settings=new SerializedObject(hall);
            var start=(Transform)settings.FindProperty("startBoundary").objectReferenceValue;
            var end=(Transform)settings.FindProperty("endBoundary").objectReferenceValue;
            var ground=(Transform)settings.FindProperty("ground").objectReferenceValue;
            var guardRoot=hall.transform.Find("08 Guards | three independent patrols");
            var guards=guardRoot.GetComponentsInChildren<Level1PalaceGuard>(true);
            Directory.CreateDirectory(Source);Directory.CreateDirectory(Art);
            string backup=Source+"level1-before-gallery-extension-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");

            var old=original.bounds;
            float pixel=old.size.x/2172f;
            const float seam=1615, bayStart=1384;
            float bayWidth=(seam-bayStart)*pixel;
            // Mirror a complete bay, then repeat it normally: both outer seams sample
            // the same source column. Existing windows and the perspective turn keep their scale.
            float turnStart=old.min.x+1712*pixel;
            float required=Mathf.Max(2*bayWidth,cabinet.bounds.max.x+.9f-turnStart);
            int pairs=Mathf.Max(1,Mathf.CeilToInt(required/(2*bayWidth)));
            float extension=pairs*2*bayWidth;
            var root=new GameObject(Marker).transform;root.SetParent(hall.transform,false);
            var texture=original.sprite.texture;
            var left=Crop(texture,"OriginalStraight",0,seam);
            var bay=Crop(texture,"RepeatWindowBay",bayStart,seam-bayStart);
            var turn=Crop(texture,"OriginalTurn",seam,2172-seam);
            original.sprite=left;original.name="Glass gallery | original straight section";
            Fit(original,new Rect(old.min.x,old.min.y,seam*pixel,old.size.y));
            var additions=new List<Renderer>();
            for(int i=0;i<pairs*2;i++)
            {
                var r=Add(root,"Added window bay "+(i+1),bay,original,
                    new Rect(old.min.x+seam*pixel+i*bayWidth,old.min.y,bayWidth,old.size.y));
                r.flipX=i%2==0;additions.Add(r);
            }
            var turnRenderer=Add(root,"Original depth turn | beyond cabinet",turn,original,
                new Rect(old.min.x+seam*pixel+extension,old.min.y,(2172-seam)*pixel,old.size.y));
            additions.Add(turnRenderer);

            // Extend floor support and clipping/camera limits from their existing left edges.
            end.position+=Vector3.right*extension;
            var floorBox=ground.GetComponent<BoxCollider2D>();
            ground.position+=Vector3.right*(extension*.5f);
            floorBox.size+=Vector2.right*(extension/Mathf.Abs(ground.lossyScale.x));
            foreach(var t in new[]{route.depthNear,route.depthFar,route.depthLeftTurn,route.depthEntrance,route.returnToCorridor,route.depthCamera}.Where(t=>t).Distinct())
            {t.position+=Vector3.right*extension;EditorUtility.SetDirty(t);}
            foreach(string name in new[]{"Night sky | solid slate","Garden ground | behind glass","Opposite closed corridor facade"})
            {
                var r=renderers.FirstOrDefault(v=>v.name==name);
                if(r)ExtendRight(r,extension);
            }

            var extraGarden=new List<Renderer>();
            var hedges=hall.transform.Find("04 Parallax | garden hedges");
            if(hedges)
            {
                var last=hedges.GetComponentsInChildren<SpriteRenderer>().OrderByDescending(r=>r.bounds.max.x).FirstOrDefault();
                if(last && last.bounds.max.x<end.position.x+1)
                {
                    var copy=UnityEngine.Object.Instantiate(last,hedges);
                    copy.name="Near garden silhouette | gallery extension";
                    copy.transform.position+=Vector3.right*(last.bounds.size.x-.2f);
                    extraGarden.Add(copy);
                }
            }
            var visuals=settings.FindProperty("corridorVisuals");
            Append(visuals,additions.Concat(extraGarden));settings.ApplyModifiedProperties();
            var storm=hall.Storm;
            if(storm)
            {
                var ss=new SerializedObject(storm);
                var rain=ss.FindProperty("rain").objectReferenceValue as Renderer;
                if(rain)ExtendRight(rain,extension);
                var layers=ss.FindProperty("layers");
                for(int i=0;i<layers.arraySize;i++)
                {
                    var layer=layers.GetArrayElementAtIndex(i);
                    var kind=(Level1PalaceStorm.Layer)layer.FindPropertyRelative("layer").enumValueIndex;
                    if(kind==Level1PalaceStorm.Layer.Architecture)Append(layer.FindPropertyRelative("renderers"),additions);
                    if(kind==Level1PalaceStorm.Layer.Garden)Append(layer.FindPropertyRelative("renderers"),extraGarden);
                }
                ss.ApplyModifiedProperties();EditorUtility.SetDirty(storm);
            }

            float leftmost=guards.Min(g=>g.transform.position.x+new SerializedObject(g).FindProperty("patrolOffsets").vector2Value.x);
            float shift=Mathf.Min(1.5f,Mathf.Max(0,leftmost-start.position.x-.5f));
            guardRoot.position+=Vector3.left*shift;
            EditorUtility.SetDirty(guardRoot);EditorUtility.SetDirty(end);EditorUtility.SetDirty(ground);
            EditorUtility.SetDirty(floorBox);EditorUtility.SetDirty(original);EditorUtility.SetDirty(hall);EditorUtility.SetDirty(route);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save extended gallery.");
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nBackup: "+backup+
                "\nGallery extension: "+extension+"; end X: "+end.position.x+
                "\nCabinet right X: "+cabinet.bounds.max.x+"; depth entrance X: "+route.depthEntrance.position.x+
                "\nGuard group shift left: "+shift+"; guard count: "+guards.Length+
                "\nFloor, camera bounds, depth/return markers, sky, courtyard facade, garden and storm registrations updated.\nOriginal props stay in place. Return wing unchanged. No Play mode or tests.");
        }
        static Sprite Crop(Texture2D texture,string name,float x,float width)
        {
            float scale=texture.width/2172f;
            var sprite=Sprite.Create(texture,new Rect(x*scale,0,width*scale,texture.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            sprite.name=name;AssetDatabase.CreateAsset(sprite,Art+name+".asset");return sprite;
        }
        static void Fit(SpriteRenderer r,Rect rect)
        {
            r.transform.position=new Vector3(rect.center.x,rect.center.y,r.transform.position.z);
            var parent=r.transform.parent.lossyScale;
            r.transform.localScale=new Vector3(rect.width/r.sprite.bounds.size.x/parent.x,rect.height/r.sprite.bounds.size.y/parent.y,1);
        }
        static SpriteRenderer Add(Transform parent,string name,Sprite sprite,SpriteRenderer template,Rect rect)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=template.sharedMaterial;
            r.color=template.color;r.sortingLayerID=template.sortingLayerID;r.sortingOrder=template.sortingOrder;
            go.transform.position=new Vector3(0,0,template.transform.position.z);Fit(r,rect);return r;
        }
        static void ExtendRight(Renderer r,float width)
        {
            float current=r.bounds.size.x;if(current<=0)return;
            r.transform.position+=Vector3.right*(width*.5f);
            var scale=r.transform.localScale;scale.x*=(current+width)/current;r.transform.localScale=scale;
            EditorUtility.SetDirty(r.transform);
        }
        static void Append(SerializedProperty array,IEnumerable<Renderer> additions)
        {
            var refs=new List<Renderer>();
            for(int i=0;i<array.arraySize;i++)refs.Add(array.GetArrayElementAtIndex(i).objectReferenceValue as Renderer);
            var all=refs.Concat(additions).Where(r=>r).Distinct().ToArray();array.arraySize=all.Length;
            for(int i=0;i<all.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=all[i];
        }
    }
}
