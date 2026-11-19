using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Aloysius.GothicStage;
using Aloysius.Level1.Boss;
namespace Aloysius.Level1.Editor
{
    public static class SpikeChamberRevision
    {
        const string Art="Assets/Art/SpikeChamber_v002/",Folder="ArtSource/SpikeChamber_v002/";
        static float left,floor,unit;static Transform root;static Material paper;
        static Vector3 At(float x,float y)=>new Vector3(left+x*unit,floor+(858-y)*unit,0);
        static Transform Child(string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root,false);t.position=p;return t;}
        static Sprite Import(string file)
        {
            string path=Art+file+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.spritePivot=new Vector2(.5f,.5f);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static SpriteRenderer Layer(string name,Sprite sprite,int order,float parallax,Vector3 center,float scale=1)
        {
            var t=Child(name,center);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=paper;r.sortingOrder=order;t.localScale=Vector3.one*(26/sprite.bounds.size.x*scale);
            if(parallax>0){var p=t.gameObject.AddComponent<PalaceDepthLayer>();p.parallax=parallax;p.cameraOrigin=center;}
            return r;
        }
        static Transform Prop(string name,string asset,Vector3 bottom,Vector2 size,int order)
        {
            var t=Child(name,bottom);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/LevelTask/Built/"+asset+".asset");r.sharedMaterial=paper;r.sortingOrder=order;t.localScale=new Vector3(size.x/r.sprite.bounds.size.x,size.y/r.sprite.bounds.size.y,1);return t;
        }
        [MenuItem("Aloysius/Level1/Apply Approved Spike Chamber Only")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity");var c=UnityEngine.Object.FindObjectOfType<SerpentCampaign>();if(!c)throw new Exception("Campaign not found");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();paper=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LevelTask/Built/Paper.mat");
            var room=c.rooms[1];var old=room.root;var untouched=c.rooms.Where((r,i)=>i!=1).Select(r=>r.id+"="+r.root.position+" "+r.bounds).ToArray();
            // Keep this taller approved room clear of the existing tower and court.
            // The entry uses a short shaft plus a fade, not a long visible fall.
            left=c.hatch.position.x+1;floor=c.rooms[0].floor-28;unit=26f/1672;
            root=new GameObject("03 尖刺房 | 已确认构图 v002").transform;root.SetParent(c.transform,false);
            var center=At(836,470.5f);var bg=Layer("01 远景 | 室内地面与拱廊",Import("Interior"),-45,.025f,center);
            var fg=Layer("03 近景 | 实体楼梯平台与右墙门",Import("Foreground"),-5,0,center);
            var view=Child("Camera | 保留小人物与巨蛇比例",center);
            c.spike=Prop("左侧尖刺 | E 开关激活","spike",At(616,544),new Vector2(.92f,1.65f),1);
            c.spikeLever=Prop("一层开关 | 位于尖刺下方","lever",At(560,858),new Vector2(1.1f,1.28f),3);
            var stair=Child("连续楼梯 | 直接左右行走",At(820,858));
            var surface=stair.gameObject.AddComponent<GothicWalkSurface>();var box=stair.GetComponent<BoxCollider2D>();box.isTrigger=true;
            Vector3 top=At(1310,544);float width=top.x-stair.position.x,height=top.y-stair.position.y;
            surface.points=new[]{Vector2.zero,new Vector2(width,height)};box.size=new Vector2(width,height+.1f);box.offset=new Vector2(width*.5f,height*.5f-.05f);
            var gallery=Child("二层连续地面 | 尖刺至右墙",At(1100,544));var support=gallery.gameObject.AddComponent<BoxCollider2D>();support.isTrigger=true;
            support.size=new Vector2((1655-545)*unit,.12f);support.offset=new Vector2(0,-.06f);
            c.trapStairs=stair.gameObject;c.stairsAlwaysVisible=true;c.spikeRaiseDistance=0;
            c.pitPortal=Child("右侧贴墙门 | W 进入",At(1625,544));
            c.shaftLanding=Child("竖井着陆位置",new Vector3(c.hatch.position.x,floor));
            room.root=root;room.floor=floor;room.bounds=new Rect(left+.2f,floor,25.6f,14.63f);room.cameraSize=7.32f;
            room.spawn=Child("房间出生点",At(430,858));room.platforms=new Collider2D[]{box,support};room.depthEntry=c.pitPortal;room.depthEnd=Child("门后过渡终点",At(1660,500));room.framedView=view;room.framedDimensions=new Vector2(26,941*unit);
            var presentation=root.gameObject.AddComponent<SpikeChamberPresentation>();presentation.campaign=c;presentation.cameraFrame=view;presentation.dimensions=room.framedDimensions;presentation.staircase=surface;presentation.stairBottom=Child("楼梯起点",At(820,858));presentation.stairTop=Child("楼梯顶点",top);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            var after=c.rooms.Where((r,i)=>i!=1).Select(r=>r.id+"="+r.root.position+" "+r.bounds).ToArray();if(!untouched.SequenceEqual(after))throw new Exception("An unrelated room changed");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Folder+"Installed.txt",DateTime.Now.ToString("O")+"\nOnly room spike replaced. Other five room roots and bounds unchanged.\nGallery height="+height+"; camera size="+room.cameraSize+"; world width=26\n");
        }
        public static void Build(){Apply();LevelTaskBuilder.BuildPlayer();}
        public static void RemoveSerpentBackdrop()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity");
            var c=UnityEngine.Object.FindObjectOfType<SerpentCampaign>();
            var p=c.rooms[1].root.GetComponent<SpikeChamberPresentation>();
            var snake=c.rooms[1].root.Find("02 巨蛇 | 位于柱列后方");
            if(snake)UnityEngine.Object.DestroyImmediate(snake.gameObject);
            if(p)p.farSerpent=null;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Folder+"BackdropRemoved.txt",DateTime.Now.ToString("O")+"\nStatic serpent backdrop removed only. Architecture and gameplay transforms preserved.\n");
            LevelTaskBuilder.BuildPlayer();
        }
    }
}
