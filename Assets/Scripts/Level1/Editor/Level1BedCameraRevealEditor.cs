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
    public static class Level1BedCameraRevealEditor
    {
        const string Source="ArtSource/PalaceCorridor/BedCameraReveal_v001/";
        static double next;
        static Level1BedCameraRevealEditor(){EditorApplication.update+=Poll;}
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
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nClose-follow and gradual full-room camera reveal saved and player packaged. No Play mode or tests.");
            }
            catch(Exception ex){File.WriteAllText(Source+"ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Aloysius/Level1/Install Bedroom Camera Reveal")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            if(route.bedroomOverviewCenter)throw new InvalidOperationException("Already installed; preserve user adjustments.");
            Directory.CreateDirectory(Source);
            string backup=Source+"level1-before-bedroom-camera-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            var room=route.bedroomSpawn.parent;
            var bounds=room.Find("Bedroom architecture").GetComponent<SpriteRenderer>().bounds;
            var marker=new GameObject("寝宫全景镜头中心 | 揭幕后缓缓展示").transform;
            marker.SetParent(room,false);marker.position=new Vector3(bounds.center.x,bounds.center.y,-10);
            route.bedroomOverviewCenter=marker;route.bedroomOverviewDimensions=new Vector2(bounds.size.x,bounds.size.y);
            route.bedroomFollowSize=3f;route.bedroomFollowOffset=new Vector2(-.4f,2.5f);
            route.bedroomRevealCameraDelay=.25f;route.bedroomRevealCameraSeconds=3.2f;
            EditorUtility.SetDirty(route);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save camera reveal.");
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nBackup: "+backup+
                "\nEntry: close player follow, orthographic size 3.0; left look offset -0.4, vertical offset 2.5."+
                "\nAt curtain reveal: wait 0.25 s, ease from current camera to room overview over 3.2 s."+
                "\nOverview centre "+marker.position+", dimensions "+route.bedroomOverviewDimensions+"; fit full room to current aspect."+
                "\nConcept image is preview only; no serpent art installed. No Play mode or tests.");
        }
    }
}
