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
    public static class Level1RouteTransitionEditor
    {
        const string Source="ArtSource/PalaceCorridor/RouteTransitions_v001/";
        const string Art="Assets/Art/Textures/Level1/RouteTransitions_v001/";
        const string Revision="Scene layout | compact palace route v1";
        static double next;
        static Level1RouteTransitionEditor(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Source+"Apply.pending")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Source+"Apply.pending");
            try
            {
                Apply();Level1RoyalBedchamberEditor.BuildPlayer();
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nScene saved and player packaged. No Play mode or tests.");
            }
            catch(Exception ex){File.WriteAllText(Source+"ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        static Level1RoyalBedchamber Route()=>SceneManager.GetSceneByPath("Assets/Scenes/level1.unity").GetRootGameObjects()
            .SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
        [MenuItem("Aloysius/Level1/Apply Fade Transitions And Compact Layout")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=Route();
            if(route.transform.Find(Revision))throw new InvalidOperationException("Already installed; preserve user changes.");
            Directory.CreateDirectory(Source);Directory.CreateDirectory(Art);
            string backup=Source+"level1-before-compact-transitions-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            var notes=new System.Text.StringBuilder(DateTime.Now.ToString("O")+"\nBackup: "+backup+"\n");
            float floor=route.antechamberLeft.position.y;
            var ante=route.antechamberSpawn.parent;
            var bed=route.bedroomSpawn.parent;
            var wing=route.transform.Find("03 Windowed return wing | foreground entry, left stairs");
            var anteArt=ante.Find("Antechamber architecture").GetComponent<SpriteRenderer>();
            var bedArt=bed.Find("Bedroom architecture").GetComponent<SpriteRenderer>();
            var ab=anteArt.bounds;var bb=bedArt.bounds;
            // Door bottoms meet the gold front floor line, not arbitrary sprite centres.
            // The original art's front floor is at row 538 (ante crop starts at row 90).
            float anteThreshold=ab.min.y+7f/455f*ab.size.y;
            float bedThreshold=bb.min.y+7f/545f*bb.size.y;
            var left=route.doorHinge.GetComponentInChildren<SpriteRenderer>();
            route.doorHinge.localRotation=Quaternion.identity;
            route.doorHinge.position=new Vector3(ab.min.x+.03f,anteThreshold,route.doorHinge.position.z);
            Fit(left,new Rect(ab.min.x+.03f,anteThreshold,1.20f,4.55f),0);
            var right=ante.Find("Right doorway leaf | enclosed corridor connection").GetComponent<SpriteRenderer>();
            Fit(right,new Rect(ab.max.x-.80f,anteThreshold,.48f,4.55f),55);
            var bedDoor=bed.Find("Door back to antechamber").GetComponent<SpriteRenderer>();
            Fit(bedDoor,new Rect(bb.max.x-.85f,bedThreshold,.50f,4.55f),55);
            Place(route.antechamberDoor,ab.min.x+1.38f,floor);
            Place(route.antechamberHole,right.bounds.center.x-.40f,floor);
            Place(route.antechamberSpawn,right.bounds.center.x-.55f,floor);
            Place(route.bedroomDoor,bedDoor.bounds.center.x-.45f,floor);
            Place(route.bedroomSpawn,bedDoor.bounds.center.x-.60f,floor);
            notes.AppendLine("Front room left door: fit existing arch; right/bedroom doors: narrow side view at wall, matching threshold and interaction/spawn points.");
            // Keep the approved narrow stair slit and its upper-landing support height.
            route.enclosedLeftSpawn.position=new Vector3(route.enclosedLeftDoor.position.x-.12f,route.enclosedLeftDoor.position.y,0);
            route.enclosedRightSpawn.position=route.enclosedRightDoor.position;
            route.depthEntrance.name="Entry | W with fade to return corridor";
            route.enclosedRightDoor.name="Foreground arch return to mirror hall | W or E";
            route.depthEntryKey=KeyCode.W;route.fadeOutSeconds=.55f;route.blackHoldSeconds=.15f;route.fadeInSeconds=.9f;

            // Compact the separated scenes on one horizontal row. Keep all local
            // art, collision, cloth anchors and markers together, with their floor Y unchanged.
            float bedShift=40-bb.min.x;
            MoveRegion(bed,bedShift,"Bedroom",notes);
            float anteShift=72-ab.min.x;
            MoveRegion(ante,anteShift,"Antechamber",notes);
            var wingLeft=wing.Find("01a Narrow stairs and occluded side entrance").GetComponent<SpriteRenderer>();
            float wingShift=86-wingLeft.bounds.min.x;
            MoveRegion(wing,wingShift,"ReturnWing",notes);
            var marker=new GameObject(Revision);marker.transform.SetParent(route.transform,false);
            EditorUtility.SetDirty(route);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save compact scene.");
            notes.AppendLine("W at mirror entrance -> fade out 0.55 s -> relocate and snap camera at full black -> hold 0.15 s -> fade in 0.90 s. Doors use same transition. Inspector adjustable.");
            notes.AppendLine("Original room door and approved stair opening retained; no Play mode, screenshots or tests.");
            File.WriteAllText(Source+"Installed.txt",notes.ToString());
        }
        static void MoveRegion(Transform root,float dx,string name,System.Text.StringBuilder notes)
        {
            root.position+=Vector3.right*dx;
            foreach(var layer in root.GetComponentsInChildren<Level1PalaceParallaxLayer>(true))
            {
                var so=new SerializedObject(layer);so.FindProperty("referenceCameraX").floatValue+=dx;so.ApplyModifiedProperties();
            }
            // World-coordinate window clipping must move with the room, including
            // material instances shared by the separate sky/forest/mountain layers.
            var copies=new Dictionary<Material,Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<mats.Length;i++)
                {
                    var source=mats[i];if(!source||!source.HasProperty("_WorldClip"))continue;
                    var clip=source.GetVector("_WorldClip");if(clip.y-clip.x>=1000)continue;
                    if(!copies.TryGetValue(source,out var copy))
                    {
                        copy=new Material(source){name=source.name+" | compact layout"};clip.x+=dx;clip.y+=dx;
                        copy.SetVector("_WorldClip",clip);
                        AssetDatabase.CreateAsset(copy,Art+name+"_Clip_"+copies.Count+".mat");copies.Add(source,copy);
                    }
                    mats[i]=copy;changed=true;
                }
                if(changed){renderer.sharedMaterials=mats;EditorUtility.SetDirty(renderer);}
            }
            EditorUtility.SetDirty(root);notes.AppendLine(name+" translated X by "+dx+"; parallax references and window clip translated with it.");
        }
        static void Place(Transform t,float x,float y){t.position=new Vector3(x,y,t.position.z);EditorUtility.SetDirty(t);}
        static void Fit(SpriteRenderer r,Rect rect,float yaw)
        {
            r.transform.rotation=Quaternion.Euler(0,yaw,0);
            var p=r.transform.parent.lossyScale;
            r.transform.localScale=new Vector3(rect.width/(r.sprite.bounds.size.x*Mathf.Cos(yaw*Mathf.Deg2Rad)*p.x),rect.height/(r.sprite.bounds.size.y*p.y),1);
            r.transform.position=new Vector3(rect.center.x,rect.center.y,r.transform.position.z);
            EditorUtility.SetDirty(r.transform);
        }
        static void Focus(Transform root)
        {
            if(!root)return;Selection.activeGameObject=root.gameObject;
            var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            if(renderers.Length==0||!SceneView.lastActiveSceneView)return;
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            SceneView.lastActiveSceneView.Frame(bounds,false);
        }
        [MenuItem("Aloysius/Level1/Scene View/Mirror Hall")]
        static void FocusHall()=>Focus(Route().corridor.transform.Find("09 Unified gallery | continuous floor and right turn"));
        [MenuItem("Aloysius/Level1/Scene View/Return Corridor")]
        static void FocusWing()=>Focus(Route().transform.Find("03 Windowed return wing | foreground entry, left stairs"));
        [MenuItem("Aloysius/Level1/Scene View/Antechamber")]
        static void FocusAnte()=>Focus(Route().antechamberSpawn.parent);
        [MenuItem("Aloysius/Level1/Scene View/Giant Bedroom")]
        static void FocusBed()=>Focus(Route().bedroomSpawn.parent);
    }
}
