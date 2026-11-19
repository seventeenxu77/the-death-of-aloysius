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
    public static class Level1BedRevealEditor
    {
        const string Source="ArtSource/PalaceCorridor/BedReveal_v001/";
        static double next;
        static Level1BedRevealEditor(){EditorApplication.update+=Poll;}
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
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nBedroom auto-entry and sudden curtain reveal saved and player packaged. No Play mode or tests.");
            }
            catch(Exception ex){File.WriteAllText(Source+"ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Aloysius/Level1/Install Bedroom Centre Reveal")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            if(route.bedRevealCenter)throw new InvalidOperationException("Reveal already installed; preserve user edits.");
            Directory.CreateDirectory(Source);
            string backup=Source+"level1-before-bed-reveal-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            var curtains=route.curtains;
            if(!curtains || !curtains.left || !curtains.right)throw new InvalidOperationException("Both independent curtain meshes are required.");
            var marker=new GameObject("床中央揭幕触发点 | 首次经过自动拉开").transform;
            marker.SetParent(curtains.transform,false);
            var center=curtains.transform.TransformPoint(new Vector3((curtains.leftAnchor+curtains.rightAnchor)*.5f,0,0));
            center.y=route.bedroomLeft.position.y;center.z=0;marker.position=center;
            route.bedRevealCenter=marker;route.autoRevealCurtains=true;
            route.bedRevealHalfWidth=.65f;route.bedRevealHeight=3.5f;route.walkLeftIntoBedroom=true;
            route.antechamberDoor.name="小房间左门 | 继续向左自动进入大床房";
            if(route.smallRoomToBedroomZone)route.smallRoomToBedroomZone.name="入口区域 05 | 向左自动进入大床房";
            curtains.snapOpenDuration=.38f;curtains.snapHemDelay=.07f;
            curtains.snapSettleDuration=.5f;curtains.snapRecoil=.12f;
            curtains.ResetClosed();
            EditorUtility.SetDirty(curtains);EditorUtility.SetDirty(route);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save reveal scene.");
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nBackup: "+backup+
                "\nSmall room left doorway: walk left to enter bedroom, no additional key; return re-entry latch retained."+
                "\nCurtain reveal centre: "+center+"; horizontal trigger width 1.3, height 3.5."+
                "\nPull 0.38 s, hem delay 0.07 s, settle 0.5 s, recoil 0.12; one automatic reveal until R reset. Starts closed."+
                "\nNo Play mode or tests.");
        }
    }
}
