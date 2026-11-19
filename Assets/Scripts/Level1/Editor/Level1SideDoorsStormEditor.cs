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
    public static class Level1SideDoorsStormEditor
    {
        const string Source="ArtSource/PalaceCorridor/SideDoorsStorm_v001/";
        const string Art="Assets/Art/Textures/Level1/SideDoorsStorm_v001/";
        const string Revision="Side doorway slits and gradual night light v1";
        static double next;
        static Level1SideDoorsStormEditor(){EditorApplication.update+=Poll;}
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
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nAuto landing entry, side door slits and storm saved; player packaged. No Play mode or tests.");
            }
            catch(Exception ex){File.WriteAllText(Source+"ApplyError.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Aloysius/Level1/Apply Side Doors And Gradual Night Light")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            if(route.transform.Find(Revision))throw new InvalidOperationException("Already applied; preserve later edits.");
            Directory.CreateDirectory(Source);Directory.CreateDirectory(Art);
            string backup=Source+"level1-before-side-doors-storm-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            var ante=route.antechamberSpawn.parent;var bed=route.bedroomSpawn.parent;
            var anteArt=ante.Find("Antechamber architecture").GetComponent<SpriteRenderer>();
            var bedArt=bed.Find("Bedroom architecture").GetComponent<SpriteRenderer>();
            var left=ante.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.name=="Left door leaf");
            var right=ante.Find("Right doorway leaf | enclosed corridor connection").GetComponent<SpriteRenderer>();
            var bedDoor=bed.Find("Door back to antechamber").GetComponent<SpriteRenderer>();
            var ab=anteArt.bounds;var bb=bedArt.bounds;
            float threshold=left.bounds.min.y;float h=left.bounds.size.y;
            float leftX=ab.min.x+.13f,rightX=ab.max.x-.26f,bedX=bb.max.x-.26f;
            var solid=new Material(anteArt.sharedMaterial){name="Side doorway | flat silhouette and subdued gold edge"};
            solid.mainTexture=Texture2D.whiteTexture;solid.SetVector("_WorldClip",new Vector4(-1000,1000,-1000,1000));
            AssetDatabase.CreateAsset(solid,Art+"DoorSlit.mat");
            // Occlude the wide arch painted into the old antechamber background with
            // a plain piece of that same side wall. The tiny visible opening is separate.
            var tex=anteArt.sprite.texture;float tx=tex.width/1774f,ty=tex.height/887f;
            var patch=Sprite.Create(tex,new Rect(1264*tx,tex.height-335*ty,20*tx,120*ty),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            patch.name="Plain near wall | original architecture crop";AssetDatabase.CreateAsset(patch,Art+"NearWallPatch.asset");
            var wall=new GameObject("近景墙沿 | 遮住原来的完整拱门");wall.transform.SetParent(ante,false);
            var wr=wall.AddComponent<SpriteRenderer>();wr.sprite=patch;wr.sharedMaterial=anteArt.sharedMaterial;wr.sortingOrder=-8;
            float wallWidth=ab.size.x*56f/510f;
            float wallHeight=Mathf.Max(h+.3f,ab.size.y*200f/455f);
            wall.transform.position=new Vector3(ab.min.x+wallWidth*.5f,threshold+wallHeight*.5f,0);
            wall.transform.localScale=new Vector3(wallWidth/patch.bounds.size.x,wallHeight/patch.bounds.size.y,1);
            Slit(ante,"侧面门缝 | 小房间左侧通往大床房",leftX,threshold,.14f,h,solid,"AnteLeft");
            Slit(ante,"侧面门缝 | 小房间右侧返回走廊",rightX,right.bounds.min.y,.14f,right.bounds.size.y,solid,"AnteRight");
            Slit(bed,"侧面门缝 | 大床房返回小房间",bedX,bedDoor.bounds.min.y,.14f,bedDoor.bounds.size.y,solid,"BedRight");
            left.enabled=right.enabled=bedDoor.enabled=false;
            EditorUtility.SetDirty(left);EditorUtility.SetDirty(right);EditorUtility.SetDirty(bedDoor);
            route.doorHinge=null;
            PlaceX(route.antechamberDoor,Mathf.Max(route.antechamberLeft.position.x+.15f,leftX+.45f));
            PlaceX(route.antechamberHole,Mathf.Min(route.antechamberRight.position.x-.1f,rightX-.4f));
            PlaceX(route.antechamberSpawn,route.antechamberHole.position.x-.15f);
            PlaceX(route.bedroomDoor,Mathf.Min(route.bedroomRight.position.x-.1f,bedX-.4f));
            PlaceX(route.bedroomSpawn,route.bedroomDoor.position.x-.15f);
            route.walkLeftIntoSmallRoom=true;route.automaticDoorThreshold=.08f;
            route.enclosedLeftDoor.name="台阶顶部入口 | 继续向左自动进入小房间";
            if(route.corridorToSmallRoomZone)route.corridorToSmallRoomZone.name="入口区域 03 | 台阶顶部向左自动进入";

            var storm=route.corridor.Storm;var ss=new SerializedObject(storm);
            foreach(string field in new[]{"weakFlashDuration","strongFlashDuration","afterglowDuration"})ss.FindProperty(field).floatValue*=2;
            ss.FindProperty("ambientExposure").floatValue=.07f;
            ss.FindProperty("darkCharacterExposure").floatValue=.07f;
            ss.FindProperty("darkPipeExposure").floatValue=.06f;
            ss.FindProperty("idleCharacterRim").floatValue=.012f;
            ss.FindProperty("darknessFade").floatValue=1.2f;
            var layers=ss.FindProperty("layers");float[] ambient={.11f,.065f,.045f,.08f,.055f};
            for(int i=0;i<layers.arraySize;i++)
            {
                var layer=layers.GetArrayElementAtIndex(i);int kind=layer.FindPropertyRelative("layer").enumValueIndex;
                layer.FindPropertyRelative("darkExposure").floatValue=ambient[Mathf.Clamp(kind,0,ambient.Length-1)];
            }
            string timing="Weak="+ss.FindProperty("weakFlashDuration").floatValue+", strong="+ss.FindProperty("strongFlashDuration").floatValue+", afterglow="+ss.FindProperty("afterglowDuration").floatValue;
            ss.ApplyModifiedProperties();EditorUtility.SetDirty(storm);EditorUtility.SetDirty(route);
            var marker=new GameObject(Revision);marker.transform.SetParent(route.transform,false);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save scene.");
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nBackup: "+backup+
                "\nWalk left across upper landing threshold -> small room; return re-entry latch prevents bouncing."+
                "\nThree doorway leaves hidden, replaced by 0.14-wide dark side slits with subtle edge; wide painted left arch occluded using original wall art."+
                "\n"+timing+"; dark interval retained; per-layer dim ambient 0.045..0.11, smooth afterglow to ambient, entering darkness fades over 1.2s."+
                "\nNo Play mode or tests.");
        }
        static void PlaceX(Transform t,float x){var p=t.position;p.x=x;t.position=p;EditorUtility.SetDirty(t);}
        static void Slit(Transform parent,string name,float x,float y,float width,float height,Material mat,string asset)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);root.position=new Vector3(x,y,0);
            Quad(root,"暗门缝",new Rect(0,0,width,height),new Color(.012f,.008f,.011f,1),mat,asset+"Gap",-6);
            Quad(root,"侧面门框细边",new Rect(-.018f,0,.018f,height),new Color(.38f,.27f,.13f,1),mat,asset+"Edge",-5);
            Quad(root,"门缝顶沿",new Rect(-.018f,height-.025f,width+.018f,.025f),new Color(.30f,.21f,.10f,1),mat,asset+"Top",-5);
        }
        static void Quad(Transform parent,string name,Rect r,Color color,Material mat,string asset,int order)
        {
            var mesh=new Mesh{name=asset};
            mesh.vertices=new[]{new Vector3(r.xMin,r.yMin),new Vector3(r.xMax,r.yMin),new Vector3(r.xMin,r.yMax),new Vector3(r.xMax,r.yMax)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.colors=new[]{color,color,color,color};
            mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Art+asset+".asset");
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.sortingOrder=order;
        }
    }
}
