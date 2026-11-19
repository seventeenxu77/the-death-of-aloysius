using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Aloysius.Level1.Boss;
using Aloysius.GothicStage;
namespace Aloysius.Level1.Editor
{
    public static class LevelTaskBuilder
    {
        const string Art="Assets/Art/LevelTask/",Out="Assets/Art/LevelTask/Built/";
        static Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static Material plain;
        static Transform root;
        static int geometryIndex;
        static readonly Color gold=new Color(.49f,.34f,.13f),ink=new Color(.045f,.023f,.045f);
        [MenuItem("Aloysius/Level1/Build Task Board Chapter")]
        public static void Build()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity");
            var route=UnityEngine.Object.FindObjectOfType<Level1RoyalBedchamber>();if(!route)throw new Exception("level1 route missing");
            Directory.CreateDirectory(LevelTaskBridge.Folder+"Backups");
            string backup=LevelTaskBridge.Folder+"Backups/level1-before-boss.unity";
            if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
            var old=GameObject.Find("任务板关卡 | Serpent Campaign");if(old)UnityEngine.Object.DestroyImmediate(old);
            var priorWake=route.curtains.transform.Find("Serpent King | 66 bone wake");if(priorWake)UnityEngine.Object.DestroyImmediate(priorWake.gameObject);
            Directory.CreateDirectory(Out);geometryIndex=0;ImportArt();
            root=new GameObject("任务板关卡 | Serpent Campaign").transform;
            var campaign=root.gameObject.AddComponent<SerpentCampaign>();campaign.actor=route.actor;campaign.route=route;campaign.drag=route.drag;
            campaign.wake=MakeWake(route.curtains.transform);
            // A readable close-follow shot opens into the full bed on the existing trigger.
            if(!route.bedroomOverviewCenter){var b=route.bedroomSpawn.parent.Find("Bedroom architecture").GetComponent<SpriteRenderer>().bounds;route.bedroomOverviewCenter=Marker(route.transform,"Bedroom reveal overview",b.center);route.bedroomOverviewDimensions=b.size;}
            float f=route.bedroomSpawn.position.y,bedL=route.bedroomLeft.position.x,bedR=route.bedroomRight.position.x;
            float hx=bedL+3;
            var bed=Child(root,"01 寝宫出口 | 裂缝与封门");
            campaign.fallingRocks=Prop("Falling entry rubble",bed,"rubble",new Vector2(bedR-1.1f,f),new Vector2(3.2f,4.2f),8);
            campaign.hatch=Marker(bed,"Hatch / floor opening",new Vector3(hx,f));
            campaign.crackedFloor=Prop("Cracked floor / breakable",bed,"platform",new Vector2(hx,f-.5f),new Vector2(2.4f,.55f),6);
            Quad(bed,"Solid masonry under bedroom left",new Rect(bedL-2,f-3,hx-bedL+.7f,2.7f),ink,-8);
            Quad(bed,"Solid masonry under bedroom right",new Rect(hx+1.3f,f-3,bedR-hx+.7f,2.7f),ink,-8);
            Line(bed,"Floor fracture",new[]{new Vector3(hx-.9f,f+.03f),new Vector3(hx-.3f,f-.12f),new Vector3(hx+.1f,f+.02f),new Vector3(hx+.5f,f-.19f),new Vector3(hx+1,f)},new Color(.7f,.45f,.14f),.045f,8);
            campaign.leftLight=Prop("Left guiding lamp",bed,"hourglass",new Vector2(hx-1.5f,f+.3f),new Vector2(.5f,.8f),5);
            // The shaft is physically below the hole, not a wall portal.
            var shaft=Child(root,"02 垂直通道 | 直接向下");
            Quad(shaft,"Shaft dark interior",new Rect(hx-1.3f,f-7,2.6f,7),new Color(.025f,.013f,.025f),-10);
            Quad(shaft,"Left shaft masonry",new Rect(hx-1.65f,f-7,.35f,7),gold*.5f,-9);
            Quad(shaft,"Right shaft masonry",new Rect(hx+1.3f,f-7,.35f,7),gold*.5f,-9);
            var pit=Room("03 地下尖刺室",new Rect(hx-3,f-11,22,10));float pf=f-11;
            var climb=Room("04 远景突刺塔",new Rect(hx+24,pf,14,13));
            var loop=Child(root,"05 原卧室回环机关 | 初次进入隐藏");
            var arsenal=Room("06 地下炮台",new Rect(-12,f-16,24,11));
            var court=Room("07 时间之蛇王庭",new Rect(17,f-19,24,12));
            var pitSurfaces=new List<Collider2D>();
            // Permanent safe steps along the wall are revealed after Stone is defeated.
            campaign.trapStairs=Child(pit,"Stone defeat stair route").gameObject;
            for(int i=0;i<5;i++)pitSurfaces.Add(Platform(campaign.trapStairs.transform,"Step "+i,hx+11+i*1.05f,pf+(i+1)*.7f,1.4f));
            pitSurfaces.Add(Platform(pit,"Upper gallery",hx+9,pf+3.5f,7));
            campaign.spike=Prop("Retractable spike",pit,"spike",new Vector2(hx+7.2f,pf+2.35f),new Vector2(1.25f,2.1f),-2);
            campaign.spikeLever=Prop("Spike switch / E",pit,"lever",new Vector2(hx+3,pf),new Vector2(.9f,1),3);
            campaign.pitPortal=Marker(pit,"Second floor depth entrance / W",new Vector3(hx+13.2f,pf+3.5f));
            DepthPassage(pit,campaign.pitPortal.position,new Vector3(hx+14.8f,pf+5.2f));
            campaign.shaftLanding=Marker(pit,"Shaft landing",new Vector3(hx,pf));
            var climbSurfaces=new List<Collider2D>();campaign.stairHeads=new SerpentAttack[4];
            for(int i=0;i<4;i++)
            {
                bool left=i%2==0;float y=pf+.6f+i*.85f;float x0=hx+24+(left?.4f:13.6f),x1=hx+24+(left?12f:2f);
                var a=Attack(climb,"Fang bridge "+(i+1),SerpentAttack.Kind.Fang,new Vector3(x0,y),new Vector3(x1,y));a.holdSeconds=9.5f;a.recoverSeconds=1.4f;a.hitRadius=.5f;campaign.stairHeads[i]=a;
                var surface=Child(a.transform,"One-way snake support "+i).gameObject.AddComponent<BoxCollider2D>();surface.isTrigger=true;surface.enabled=false;a.standingSurface=surface;climbSurfaces.Add(surface);
                climbSurfaces.Add(Platform(climb,"Safe ledge "+i,hx+24+(left?2.2f:11.8f),y+.43f,2.6f));
            }
            climbSurfaces.Add(Platform(climb,"Upper return landing",hx+31,pf+4.1f,5));
            var climbPortal=Marker(climb,"Return to bedroom / W",new Vector3(hx+31,pf+4.1f));DepthPassage(climb,climbPortal.position,climbPortal.position+new Vector3(2,2));
            // Return to the ACTUAL starting bedroom (the same art and dragged bed).
            Vector3 start=route.actor.transform.position;float of=start.y;
            campaign.lift=Prop("Bedroom lift",loop,"platform",new Vector2(-1.2f,of-.15f),new Vector2(1.8f,.45f),5);var liftSurface=campaign.lift.gameObject.AddComponent<BoxCollider2D>();liftSurface.isTrigger=true;liftSurface.size=new Vector2(1.8f,.15f);liftSurface.offset=new Vector2(0,.15f);
            // Override collider size after sprite scaling by placing it on a neutral sibling.
            var liftSupport=Child(campaign.lift,"Lift standing support");liftSupport.localScale=new Vector3(1/campaign.lift.localScale.x,1/campaign.lift.localScale.y,1);UnityEngine.Object.DestroyImmediate(liftSurface);liftSurface=liftSupport.gameObject.AddComponent<BoxCollider2D>();liftSurface.size=new Vector2(1.8f,.15f);liftSurface.offset=new Vector2(0,.37f);liftSurface.isTrigger=true;
            campaign.liftLever=Prop("Raise platform / E",loop,"lever",new Vector2(-.3f,of),new Vector2(.65f,.7f),8);
            campaign.blade=Prop("Guillotine blade",loop,"blade",new Vector2(-3.2f,of+3.6f),new Vector2(1.5f,1.4f),7);
            campaign.chains=Marker(loop,"Corrodible chain target",new Vector3(-3.2f,of+2.5f));
            // Snake shoots rightward from the left: chains must lie beyond the lifted player.
            campaign.chains.position=new Vector3(1.5f,of+2.5f);campaign.blade.position=new Vector3(1.5f,of+3.6f);
            Line(campaign.chains,"Two chain strands",new[]{new Vector3(1.3f,of+4.7f),new Vector3(1.3f,of+2.1f),new Vector3(1.7f,of+2.1f),new Vector3(1.7f,of+4.7f)},gold,.06f,7);
            campaign.underGate=Marker(loop,"Under gate entrance",new Vector3(1.5f,of));
            campaign.gateExit=Child(loop,"Open shaft below guillotine").gameObject;Quad(campaign.gateExit.transform,"Open hole",new Rect(.5f,of-.45f,2,.5f),Color.black,12);
            campaign.ironGate=Prop("Corrodible iron gate",arsenal,"gate",new Vector2(-6.5f,f-16),new Vector2(2.2f,4.8f),5);
            campaign.cannon=Prop("Cannon / E load or launch",arsenal,"cannon",new Vector2(-1.5f,f-16),new Vector2(3,2.1f),5);
            campaign.cannonBall=Prop("Stone cannonball / E",arsenal,"ball",new Vector2(-4,f-16),new Vector2(.7f,.7f),6);
            campaign.projectile=Prop("Fired stone cannonball",arsenal,"ball",new Vector2(-1.5f,f-16),new Vector2(.65f,.65f),9).gameObject;campaign.projectile.SetActive(false);
            var landingSurface=Platform(arsenal,"Venom balcony",8,f-11.8f,5.5f);campaign.cannonLanding=Marker(arsenal,"Cannon landing",new Vector3(7.4f,f-11.8f));campaign.ladder=Marker(arsenal,"Ladder / W",new Vector3(9.2f,f-11.8f));
            Ladder(arsenal,campaign.ladder.position,3.5f);
            campaign.controller=Prop("Time controller / E bait",court,"hourglass",new Vector2(29,f-19),new Vector2(.8f,1.3f),4);
            campaign.timeTraps=new Transform[4];campaign.timeWeights=new Transform[4];campaign.trapDepths=new[]{0f,.65f,.3f,1f};
            for(int i=0;i<4;i++)
            {
                float x=20+i*5.3f,y=f-19+1.3f+campaign.trapDepths[i]*3;
                campaign.timeTraps[i]=Marker(court,"Time seal "+(i+1)+" depth="+campaign.trapDepths[i],new Vector3(x,y));
                Prop("Seal pedestal "+i,court,"spike",new Vector2(x,y-.7f),new Vector2(.9f,1.4f),-3-i);
                Prop("Active time seal",campaign.timeTraps[i],"hourglass",new Vector2(x,y-.3f),new Vector2(.45f,.65f),4);
                Prop("Depth pedestal shaft "+i,court,"column",new Vector2(x,f-19),new Vector2(.7f,y-(f-19)-.5f),-8);
                campaign.timeWeights[i]=Prop("Suspended crushing weight "+i,court,"weight",new Vector2(x,y+5),new Vector2(2.1f,2.7f),6);
                Line(court,"Suspension "+i,new[]{new Vector3(x,y+5),new Vector3(x,y+7)},gold,.045f,-1);
            }
            campaign.rooms=new[]{
                Descriptor("crack",bed,new Rect(bedL,f,bedR-bedL-1,12),new Vector3(hx+3,f),5.2f,new Collider2D[0]),
                Descriptor("spike",pit,new Rect(hx-2,pf,20,10),campaign.shaftLanding.position,4.8f,pitSurfaces.ToArray()),
                Descriptor("fang",climb,new Rect(hx+24,pf,14,13),new Vector3(hx+26,pf),4.4f,climbSurfaces.ToArray()),
                Descriptor("loop",loop,new Rect(-4.4f,of,8.8f,6),new Vector3(3.6f,of),3.1f,new Collider2D[]{liftSurface}),
                Descriptor("cannon",arsenal,new Rect(-11,f-16,22,10),new Vector3(-10,f-16),4.7f,new Collider2D[]{landingSurface}),
                Descriptor("time",court,new Rect(18,f-19,22,11),new Vector3(29,f-19),5.4f,new Collider2D[0])};
            campaign.rooms[1].depthEntry=campaign.pitPortal;campaign.rooms[1].depthEnd=Marker(pit,"Depth destination",new Vector3(hx+14.8f,pf+5.2f));
            campaign.rooms[2].depthEntry=climbPortal;campaign.rooms[2].depthEnd=Marker(climb,"Return depth destination",climbPortal.position+new Vector3(2,2));
            campaign.rooms[4].depthEnd=Marker(arsenal,"Ladder distant landing",campaign.ladder.position+Vector3.up*4);
            campaign.rooms[5].depthEnd=Marker(court,"Time serpent distant origin",new Vector3(29,f-12));
            campaign.stone=Attack(root,"Stone / editable downward slam",SerpentAttack.Kind.Stone,new Vector3(hx,f+9),new Vector3(hx,f));
            campaign.venom=Attack(root,"Venom / adjustable horizontal spray",SerpentAttack.Kind.Venom,new Vector3(-5,of+2.5f),new Vector3(10,of+2.5f));
            campaign.fang=Attack(loop,"Fang / guillotine victim",SerpentAttack.Kind.Fang,new Vector3(5,of+1),new Vector3(1.5f,of+1));
            campaign.timeHead=Attack(court,"Time head / controlled assault",SerpentAttack.Kind.Time,new Vector3(29,f-12),new Vector3(29,f-18));
            foreach(var a in new[]{campaign.stone,campaign.venom,campaign.fang,campaign.timeHead})a.gameObject.SetActive(false);
            campaign.fallingRocks.gameObject.SetActive(false);campaign.leftLight.gameObject.SetActive(false);campaign.trapStairs.SetActive(false);campaign.gateExit.SetActive(false);loop.gameObject.SetActive(false);
            // Save attack prefabs with marker-driven endpoints for later reuse.
            Directory.CreateDirectory("Assets/Prefabs/SerpentKing");
            foreach(var a in new[]{campaign.stone,campaign.venom,campaign.stairHeads[0]}){bool wasActive=a.gameObject.activeSelf;a.gameObject.SetActive(true);PrefabUtility.SaveAsPrefabAsset(a.gameObject,"Assets/Prefabs/SerpentKing/"+a.kind+"Attack.prefab");a.gameObject.SetActive(wasActive);}
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            File.WriteAllText(LevelTaskBridge.Folder+"Installed.txt",DateTime.Now.ToString("O")+"\nScene: Assets/Scenes/level1.unity\nRooms: "+string.Join(", ",campaign.rooms.Select(r=>r.id))+"\nWake: "+campaign.wake.bones.Length+" bones; source v021 preserves v016 tracks.\n");
        }
        static PalaceRoom Descriptor(string id,Transform r,Rect bounds,Vector3 spawn,float size,Collider2D[] platforms)
        {return new PalaceRoom{id=id,root=r,bounds=bounds,floor=bounds.yMin,spawn=Marker(r,"Checkpoint / "+id,spawn),cameraSize=size,platforms=platforms};}
        static Transform Child(Transform p,string name){var t=new GameObject(name).transform;t.SetParent(p,false);return t;}
        static Transform Marker(Transform p,string name,Vector3 pos){var t=Child(p,name);t.position=pos;return t;}
        static void ImportArt()
        {
            AssetDatabase.Refresh();
            foreach(string path in Directory.GetFiles(Art,"*.png",SearchOption.AllDirectories))
            {
                var t=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));if(t==null)continue;
                t.textureType=TextureImporterType.Default;t.alphaIsTransparency=true;t.isReadable=true;t.mipmapEnabled=false;t.wrapMode=TextureWrapMode.Clamp;t.maxTextureSize=4096;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();
            }
            plain=AssetDatabase.LoadAssetAtPath<Material>(Out+"Paper.mat");if(!plain){plain=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(plain,Out+"Paper.mat");}
            sprites.Clear();
            var props=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"PuzzleObjects.png");
            string[] names={"platform","rubble","spike","lever","blade","cannon","gate","hourglass"};
            for(int i=0;i<8;i++)sprites[names[i]]=Slice(props,names[i],new RectInt(i%4*props.width/4,(1-i/4)*props.height/2,props.width/4,props.height/2),true);
            var details=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"MechanismDetails.png");string[] detailNames={"ball","weight","column","arch"};
            for(int i=0;i<4;i++)sprites[detailNames[i]]=Slice(details,detailNames[i],new RectInt(i*details.width/4,0,details.width/4,details.height),true);
            var heads=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"AttackHeads.png");
            sprites["venom"]=Slice(heads,"venom",new RectInt(0,0,heads.width/2,heads.height),true);sprites["fang"]=Slice(heads,"fang",new RectInt(heads.width/2,0,heads.width/2,heads.height),true);
            var stone=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"StoneAttack.png");sprites["stone"]=Slice(stone,"stone",new RectInt(0,0,stone.width,stone.height),true);
            var bg=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"PalaceDepth.png");sprites["background"]=Slice(bg,"background",new RectInt(0,0,bg.width,bg.height),false);
            var wt=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Wake/part3.png");sprites["time"]=Slice(wt,"time",new RectInt(315,wt.height-610,440,410),true);
            var white=Texture2D.whiteTexture;sprites["white"]=Sprite.Create(white,new Rect(0,0,white.width,white.height),new Vector2(.5f,.5f),1);
            var solid=AssetDatabase.LoadAssetAtPath<Sprite>(Out+"Solid.asset");if(solid){UnityEngine.Object.DestroyImmediate(sprites["white"]);sprites["white"]=solid;}else AssetDatabase.CreateAsset(sprites["white"],Out+"Solid.asset");
        }
        static Sprite Slice(Texture2D t,string name,RectInt r,bool trim)
        {
            if(trim){int x0=r.xMax,y0=r.yMax,x1=r.x,y1=r.y;var pixels=t.GetPixels32();for(int y=r.y;y<r.yMax;y++)for(int x=r.x;x<r.xMax;x++)if(pixels[y*t.width+x].a>20){x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}r=new RectInt(x0,y0,Mathf.Max(1,x1-x0+1),Mathf.Max(1,y1-y0+1));}
            var s=Sprite.Create(t,new Rect(r.x,r.y,r.width,r.height),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);s.name=name;
            string path=Out+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(old){EditorUtility.CopySerialized(s,old);UnityEngine.Object.DestroyImmediate(s);return old;}AssetDatabase.CreateAsset(s,path);return s;
        }
        static Transform Prop(string name,Transform parent,string key,Vector2 bottom,Vector2 size,int order)
        {var t=Child(parent,name);t.position=bottom;var s=t.gameObject.AddComponent<SpriteRenderer>();s.sprite=sprites[key];s.sharedMaterial=plain;s.sortingOrder=order;t.localScale=new Vector3(size.x/s.sprite.bounds.size.x,size.y/s.sprite.bounds.size.y,1);return t;}
        static void Quad(Transform parent,string name,Rect rect,Color color,int order)
        {var t=Child(parent,name);t.position=new Vector3(rect.center.x,rect.center.y);var s=t.gameObject.AddComponent<SpriteRenderer>();s.sprite=sprites["white"];s.sharedMaterial=plain;s.color=color;s.sortingOrder=order;t.localScale=new Vector3(rect.width/s.sprite.bounds.size.x,rect.height/s.sprite.bounds.size.y,1);}
        static LineRenderer Line(Transform parent,string name,Vector3[] pts,Color color,float width,int order)
        {var t=Child(parent,name);var l=t.gameObject.AddComponent<LineRenderer>();l.sharedMaterial=plain;l.positionCount=pts.Length;l.SetPositions(pts);l.startWidth=l.endWidth=width;l.startColor=l.endColor=color;l.sortingOrder=order;l.numCapVertices=2;return l;}
        static Collider2D Platform(Transform parent,string name,float x,float y,float width)
        {
            var t=Prop(name,parent,"platform",new Vector2(x,y-.65f),new Vector2(width,.65f),-1);
            var c=Child(parent,name+" / one-way collision");c.position=new Vector3(x,y-.075f);var box=c.gameObject.AddComponent<BoxCollider2D>();box.size=new Vector2(width,.15f);box.isTrigger=true;return box;
        }
        static Transform Room(string name,Rect area)
        {
            var r=Child(root,name);int tiles=Mathf.CeilToInt(area.width/12);
            for(int i=0;i<tiles;i++)
            {
                var t=Prop("Far arcade "+i,r,"background",new Vector2(area.xMin+(i+.5f)*area.width/tiles,area.yMin),new Vector2(area.width/tiles+.15f,area.height),-35);
                var p=t.gameObject.AddComponent<PalaceDepthLayer>();p.parallax=.08f;p.cameraOrigin=area.center;
            }
            // Physical middle planes, clear passage and sparse near columns.
            for(int i=0;i<4;i++)
            {
                float x=area.xMin+(i+.5f)*area.width/4;
                Quad(r,"Middle pier "+i,new Rect(x-.13f,area.yMin,.26f,area.height*.7f),ink,-22);
                Line(r,"Pier gold edge "+i,new[]{new Vector3(x+.13f,area.yMin),new Vector3(x+.13f,area.yMin+area.height*.7f)},gold*.65f,.025f,-21);
            }
            int slabs=Mathf.CeilToInt(area.width/4);for(int i=0;i<slabs;i++)Prop("Ground slab "+i,r,"platform",new Vector2(area.xMin+(i+.5f)*area.width/slabs,area.yMin-1.15f),new Vector2(area.width/slabs+.02f,1.15f),1);
            Quad(r,"Walkable red carpet",new Rect(area.xMin,area.yMin-.045f,area.width,.055f),new Color(.3f,.04f,.065f),3);
            Quad(r,"Left foreground pier",new Rect(area.xMin-.3f,area.yMin,.45f,area.height),ink,15);
            Quad(r,"Right foreground pier",new Rect(area.xMax-.15f,area.yMin,.45f,area.height),ink,15);
            return r;
        }
        static void DepthPassage(Transform p,Vector3 a,Vector3 b)
        {
            Polygon(p,"Depth floor",new[]{a-Vector3.right,a+Vector3.right,b+Vector3.right*.5f,b-Vector3.right*.5f},new Color(.17f,.025f,.045f),-19);
            Polygon(p,"Depth left wall",new[]{a-Vector3.right,a+new Vector3(-1,3.1f),b+new Vector3(-.5f,1.6f),b-Vector3.right*.5f},new Color(.065f,.038f,.06f),-20);
            Polygon(p,"Depth right wall",new[]{a+Vector3.right,a+new Vector3(1,3.1f),b+new Vector3(.5f,1.6f),b+Vector3.right*.5f},new Color(.1f,.048f,.067f),-20);
            Quad(p,"Distant doorway shadow",new Rect(b.x-.5f,b.y,1,1.6f),Color.black,-21);
            Prop("Near passage carved arch",p,"arch",a,new Vector2(2.6f,3.4f),-10);
            Prop("Far passage carved arch",p,"arch",b,new Vector2(1.3f,1.7f),-17);
            for(int i=0;i<4;i++){float t=i/3f;var q=Vector3.Lerp(a,b,t);float s=Mathf.Lerp(1,.5f,t);Line(p,"Receding arch "+i,new[]{q+Vector3.left*1.1f*s,q+new Vector3(-1.1f,2.6f)*s,q+Vector3.up*3.2f*s,q+new Vector3(1.1f,2.6f)*s,q+Vector3.right*1.1f*s},gold*.7f,.045f,-15-i);}
            Line(p,"Depth floor left",new[]{a-Vector3.right,b-Vector3.right*.5f},gold,.035f,-10);Line(p,"Depth floor right",new[]{a+Vector3.right,b+Vector3.right*.5f},gold,.035f,-10);
        }
        static void Polygon(Transform p,string name,Vector3[] vertices,Color color,int order)
        {
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.colors=Enumerable.Repeat(color,4).ToArray();mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();string path=Out+"Geometry"+(geometryIndex++)+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            var g=Child(p,name);g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=plain;r.sortingOrder=order;
        }
        static void Ladder(Transform p,Vector3 pos,float h)
        {Line(p,"Ladder left",new[]{pos+Vector3.left*.28f,pos+Vector3.left*.28f+Vector3.up*h},gold,.06f,-1);Line(p,"Ladder right",new[]{pos+Vector3.right*.28f,pos+Vector3.right*.28f+Vector3.up*h},gold,.06f,-1);for(float y=0;y<h;y+=.32f)Line(p,"Rung",new[]{pos+new Vector3(-.28f,y),pos+new Vector3(.28f,y)},gold,.045f,-1);}
        static SerpentAttack Attack(Transform parent,string name,SerpentAttack.Kind kind,Vector3 from,Vector3 to)
        {
            var t=Child(parent,name);t.position=from;var a=t.gameObject.AddComponent<SerpentAttack>();a.kind=kind;
            a.startPoint=Marker(t,"Start / 可调起点",from);a.endPoint=Marker(t,"End / 可调终点",to);a.neckRoot=Marker(t,"Neck anchor",from+(kind==SerpentAttack.Kind.Stone?Vector3.up*4:Vector3.left*3));
            var pivot=Marker(t,"Head pivot",from);a.head=pivot;
            string key=kind==SerpentAttack.Kind.Stone?"stone":kind==SerpentAttack.Kind.Venom?"venom":kind==SerpentAttack.Kind.Time?"time":"fang";
            Vector2 size=kind==SerpentAttack.Kind.Stone?new Vector2(2.7f,5.5f):new Vector2(3,1.9f);
            var visual=Prop("Painted head",pivot,key,from-new Vector3(0,size.y*.5f),size,-3);visual.localPosition=kind==SerpentAttack.Kind.Stone?new Vector3(0,-.4f):new Vector3(-.85f,-.85f);
            a.spriteForwardAngle=kind==SerpentAttack.Kind.Stone?-90:0;
            if(kind==SerpentAttack.Kind.Time)a.autoOrient=false;
            a.mouth=Marker(pivot,"Mouth / spray origin",from+Vector3.right*.7f);
            a.neck=Line(t,"Continuous neck",new[]{a.neckRoot.position,from},new Color(.105f,.045f,.115f),.9f,-4);
            var neckMat=AssetDatabase.LoadAssetAtPath<Material>(Out+"NeckScales.mat");
            if(!neckMat){neckMat=new Material(Shader.Find("Aloysius/Serpent Skin"));AssetDatabase.CreateAsset(neckMat,Out+"NeckScales.mat");}
            var neckTex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"NeckScales.png");neckMat.SetTexture("_MainTex",neckTex);neckMat.SetTexture("_SleepTex",neckTex);neckMat.SetFloat("_Phase",1);neckMat.mainTextureScale=new Vector2(1,.5f);neckMat.mainTextureOffset=new Vector2(0,.25f);a.neck.sharedMaterial=neckMat;a.neck.startColor=a.neck.endColor=Color.white;a.neckWidth=.85f;
            a.warning=Line(t,"Telegraph / target line",new[]{from,to},new Color(.75f,.35f,.1f,.5f),.025f,5);a.warning.enabled=false;
            a.jet=Line(t,"Horizontal venom",new[]{from,to},new Color(.55f,.58f,.16f,.9f),.2f,5);a.jet.enabled=false;
            if(kind==SerpentAttack.Kind.Venom)
            {
                RigVenomSacs(visual,a);
                a.shakeSeconds=.75f;a.strikeSeconds=.3f;a.holdSeconds=1.8f;
            }
            return a;
        }
        static void RigVenomSacs(Transform visual,SerpentAttack attack)
        {
            var spriteRenderer=visual.GetComponent<SpriteRenderer>();var sprite=spriteRenderer.sprite;
            var rest=Child(visual,"Head rigid bone");var l=Child(visual,"Venom sac left bone");var r=Child(visual,"Venom sac right bone");
            Bounds bounds=sprite.bounds;l.localPosition=new Vector3(bounds.min.x+bounds.size.x*.55f,bounds.size.y*.29f);r.localPosition=new Vector3(bounds.min.x+bounds.size.x*.76f,bounds.size.y*.32f);
            var bones=new[]{rest,l,r};var v=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var tri=new List<int>();
            const int cols=32,rows=20;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {
                float u=x/(float)cols,w=y/(float)rows;var p=new Vector3(bounds.min.x+u*bounds.size.x,w*bounds.size.y);v.Add(p);
                uv.Add(new Vector2((sprite.rect.x+u*sprite.rect.width)/sprite.texture.width,(sprite.rect.y+w*sprite.rect.height)/sprite.texture.height));
                float wl=Mathf.Exp(-((u-.55f)*(u-.55f)/.012f+(w-.29f)*(w-.29f)/.025f))* .92f;
                float wr=Mathf.Exp(-((u-.76f)*(u-.76f)/.008f+(w-.32f)*(w-.32f)/.022f))* .92f;
                float sum=Mathf.Max(1,wl+wr);wl/=sum;wr/=sum;
                weights.Add(new BoneWeight{boneIndex0=0,weight0=1-wl-wr,boneIndex1=1,weight1=wl,boneIndex2=2,weight2=wr});
                if(x<cols&&y<rows){int k=y*(cols+1)+x;tri.AddRange(new[]{k,k+1,k+cols+1,k+1,k+cols+2,k+cols+1});}
            }
            var mesh=new Mesh{name="Venom sac weighted mesh"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.boneWeights=weights.ToArray();mesh.SetTriangles(tri,0);mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*visual.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
            string path=Out+"VenomSacs.mesh.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Out+"VenomSacs.mat");if(!material){material=new Material(Shader.Find("Aloysius/Serpent Skin"));AssetDatabase.CreateAsset(material,Out+"VenomSacs.mat");}
            material.SetTexture("_MainTex",sprite.texture);material.SetTexture("_SleepTex",sprite.texture);material.SetFloat("_Phase",1);
            int order=spriteRenderer.sortingOrder;UnityEngine.Object.DestroyImmediate(spriteRenderer);var skin=visual.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterial=material;skin.bones=bones;skin.rootBone=rest;skin.sortingOrder=order;skin.updateWhenOffscreen=true;
            attack.sacLeft=l;attack.sacRight=r;
        }
        static SerpentWakePlayer MakeWake(Transform bed)
        {
            var source=JsonUtility.FromJson<WakeImport>(File.ReadAllText(LevelTaskBridge.Folder+"UnityWake.json"));
            var t=Child(bed,"Serpent King | 66 bone wake");t.localPosition=new Vector3(3.1f,-2.15f,0);t.localScale=new Vector3(.82f,.505f,1);
            var group=t.gameObject.AddComponent<SortingGroup>();group.sortingOrder=-14;
            var cleanTex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Wake/CleanPlate.png");
            var clean=Slice(cleanTex,"WakeUnderpainting",new RectInt(0,0,cleanTex.width,cleanTex.height),false);
            var cleanObject=Child(t,"Underpainting / closes deformation gaps");cleanObject.localPosition=new Vector3(source.width/200f,-source.height/100f,0);
            var cleanRenderer=cleanObject.gameObject.AddComponent<SpriteRenderer>();cleanRenderer.sprite=clean;cleanRenderer.sharedMaterial=plain;cleanRenderer.sortingOrder=-2;
            var player=t.gameObject.AddComponent<SerpentWakePlayer>();var data=AssetDatabase.LoadAssetAtPath<SerpentWakeData>(Out+"Wake.asset");
            if(!data){data=ScriptableObject.CreateInstance<SerpentWakeData>();AssetDatabase.CreateAsset(data,Out+"Wake.asset");}data.frames=source.frames;data.fps=source.fps;data.duration=source.duration;EditorUtility.SetDirty(data);player.clip=data;
            var bones=new Transform[source.joints.Length];for(int i=0;i<bones.Length;i++){var j=source.joints[i];bones[i]=Child(j.parent<0?t:bones[j.parent],j.name);bones[i].localPosition=new Vector3(j.x,j.y,0);}
            var binds=bones.Select(b=>b.worldToLocalMatrix*t.localToWorldMatrix).ToArray();var heads=new SkinnedMeshRenderer[4];int order=0;
            foreach(var m in source.meshes)
            {
                var g=Child(t,m.name);var mesh=new Mesh{name=m.name};int count=m.vertices.Length/2;var v=new Vector3[count];var uv=new Vector2[count];var weights=new BoneWeight[count];
                for(int i=0;i<count;i++)
                {
                    v[i]=new Vector3(m.vertices[i*2],m.vertices[i*2+1]);uv[i]=new Vector2(m.uv[i*2],m.uv[i*2+1]);int k=i*4;
                    weights[i]=new BoneWeight{boneIndex0=m.joints[k],boneIndex1=m.joints[k+1],boneIndex2=m.joints[k+2],boneIndex3=m.joints[k+3],weight0=m.weights[k],weight1=m.weights[k+1],weight2=m.weights[k+2],weight3=m.weights[k+3]};
                }
                mesh.vertices=v;mesh.uv=uv;mesh.boneWeights=weights;mesh.bindposes=binds;mesh.triangles=m.indices;mesh.RecalculateBounds();
                string mp=Out+m.texture+".mesh.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(mp);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,mp);
                var renderer=g.gameObject.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=bones[0];renderer.updateWhenOffscreen=true;renderer.localBounds=new Bounds(new Vector3(5.6f,-7),new Vector3(28,30,5));renderer.sortingOrder=order++;
                string matPath=Out+m.texture+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!material){material=new Material(Shader.Find("Aloysius/Serpent Skin"));AssetDatabase.CreateAsset(material,matPath);}
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Wake/"+m.texture+".png");material.SetTexture("_MainTex",texture);material.SetTexture("_SleepTex",m.part>=0?AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Wake/asleep"+m.part+".png"):texture);material.SetFloat("_Phase",1);renderer.sharedMaterial=material;
                if(m.part>=0)heads[m.part]=renderer;if(m.texture=="tongue")player.tongue=renderer;
            }
            player.bones=bones;player.heads=heads;player.Sample(0);return player;
        }
        public static void BuildPlayer()
        {
            string path="D:/the death of aloysius/the death of aloysius/Builds/SerpentChapter/AloysiusLevel1.exe";Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/level1.unity"},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed "+result.summary.totalErrors);
            File.WriteAllText(LevelTaskBridge.Folder+"Build.txt",result.summary.result+" "+path);
        }
        public static void BuildAll(){Build();BuildPlayer();}
    }
}
