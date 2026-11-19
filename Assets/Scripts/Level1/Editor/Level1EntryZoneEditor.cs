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
    public static class Level1EntryZoneEditor
    {
        const string Source="ArtSource/PalaceCorridor/EntryZones_v001/";
        static double next;
        static Level1EntryZoneEditor(){EditorApplication.update+=Poll;}
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
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nEntry regions saved and player packaged. No Play mode or tests.");
            }
            catch(Exception ex){File.WriteAllText(Source+"ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Aloysius/Level1/Install Editable Entry Regions")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            if(route.mirrorToCorridorZone)throw new InvalidOperationException("Entry regions already installed; preserve edits.");
            Directory.CreateDirectory(Source);
            string backup=Source+"level1-before-entry-regions-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            var hallSettings=new SerializedObject(route.corridor);
            var end=(Transform)hallSettings.FindProperty("endBoundary").objectReferenceValue;
            var art=route.corridor.GetComponentsInChildren<SpriteRenderer>(true);
            var turn=art.First(r=>r.name=="Original depth turn | beyond cabinet");
            var cabinet=art.First(r=>r.name=="Palace cabinet | low cover");
            float left=Mathf.Max(turn.bounds.min.x,cabinet.bounds.max.x+.2f);
            float right=end.position.x-.05f;
            if(right<=left)throw new InvalidOperationException("Gallery entrance has no space beyond cabinet.");
            float floor=route.depthEntrance.position.y;
            route.mirrorToCorridorZone=Zone(route.depthEntrance,"入口区域 01 | 镜厅至第二走廊 | W",
                new Rect(left,floor-.3f,right-left,2.5f));
            route.corridorToMirrorZone=Around(route.enclosedRightDoor,"入口区域 02 | 第二走廊返回镜厅 | W E",2.8f,.3f,1.8f);
            // Broaden across the upper landing, with only a small overlap onto the
            // top stair. The lower floor remains outside this vertical interval.
            route.corridorToSmallRoomZone=Zone(route.enclosedLeftDoor,"入口区域 03 | 台阶顶部进入小房间 | W E",
                new Rect(route.enclosedLeftDoor.position.x-1.6f,route.enclosedLeftDoor.position.y-.3f,2.1f,1.65f));
            route.smallRoomToCorridorZone=Around(route.antechamberHole,"入口区域 04 | 小房间返回第二走廊 | W E",2.2f,.3f,1.8f);
            route.smallRoomToBedroomZone=Around(route.antechamberDoor,"入口区域 05 | 小房间进入大床房 | W E",2.4f,.3f,1.8f);
            route.bedroomToSmallRoomZone=Around(route.bedroomDoor,"入口区域 06 | 大床房返回小房间 | W E",2.4f,.3f,1.8f);
            route.correctedRoute=true;
            EditorUtility.SetDirty(route);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save entry regions.");
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nBackup: "+backup+
                "\nMirror entrance X: "+left+" .. "+right+" (width "+(right-left)+")"+
                "\nSix editable BoxCollider2D trigger regions on route markers. Feet inside region and grounded; area state prevents unrelated transitions."+
                "\nTop stair -> small room -> giant bedroom and reverse links use existing fade transition. Original layout, art, patrols and fades preserved. No Play mode or tests.");
        }
        static BoxCollider2D Around(Transform marker,string name,float width,float below,float height)
            =>Zone(marker,name,new Rect(marker.position.x-width*.5f,marker.position.y-below,width,height));
        static BoxCollider2D Zone(Transform parent,string name,Rect rect)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.transform.position=new Vector3(rect.center.x,rect.center.y,0);
            var box=go.AddComponent<BoxCollider2D>();box.isTrigger=true;
            var scale=go.transform.lossyScale;box.size=new Vector2(rect.width/Mathf.Abs(scale.x),rect.height/Mathf.Abs(scale.y));
            return box;
        }
    }
}
