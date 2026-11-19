using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aloysius.Level1.Boss;

namespace Aloysius.Level1.Editor
{
    public static class PalaceLeftSpikeInstaller
    {
        const string Art="Assets/Art/SerpentKing/LeftSpikeLink_v001/";
        const string Source="ArtSource/SerpentKing/LeftSpikeLink_v001/";
        static Color C(string hex){ColorUtility.TryParseHtmlString(hex,out var c);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
        static readonly Color Ink=C("#08070b"),Gold=C("#d2a34c"),Dim=C("#8b642d"),Wine=C("#301021"),Red=C("#661b2e");
        sealed class Drawing
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();
            int V(Vector2 p,Color color){vertices.Add(p);colors.Add(color);return vertices.Count-1;}
            public void Polygon(Color color,params Vector2[] points)
            {int start=vertices.Count;foreach(var p in points)V(p,color);for(int i=1;i<points.Length-1;i++){indices.Add(start);indices.Add(start+i);indices.Add(start+i+1);}}
            public void Rect(float x,float y,float w,float h,Color color)=>Polygon(color,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
            public void Circle(Vector2 center,float radius,Color color)
            {var p=new Vector2[25];for(int i=0;i<25;i++){float a=i*Mathf.PI*2/24;p[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;}Polygon(color,p);}
            public void Line(Color color,float width,params Vector2[] points)
            {
                for(int i=1;i<points.Length;i++)
                {var a=points[i-1];var b=points[i];var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Polygon(color,a+n,b+n,b-n,a-n);}
                foreach(var p in points)Circle(p,width*.5f,color);
            }
            public Mesh Mesh(string name){var m=new Mesh{name=name};m.SetVertices(vertices);m.SetColors(colors);m.SetTriangles(indices,0);m.RecalculateBounds();return m;}
        }
        static Vector2 P(float x,float y)=>new Vector2(x,y);
        static Transform Child(string name,Transform parent,Vector3 local)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
        static T Save<T>(T asset,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old){EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(asset,path);return asset;
        }
        static MeshRenderer Draw(string name,Transform parent,Drawing drawing,Material material,int order,string file)
        {
            var t=Child(name,parent,Vector3.zero);var mesh=Save(drawing.Mesh(name),Art+file+".asset");
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=order;return r;
        }
        static Drawing Housing()
        {
            var d=new Drawing();
            // Compact metal sleeve on the LEFT vertical face of the gallery corner.
            // No freestanding pillar, doorway, arch or tall backing panel.
            d.Rect(0,-.17f,.39f,.34f,Ink);d.Rect(.055f,-.12f,.28f,.24f,Wine);
            d.Line(Dim,.021f,P(0,-.17f),P(.39f,-.17f),P(.39f,.17f),P(0,.17f),P(0,-.17f));
            d.Rect(-.045f,-.13f,.075f,.26f,Gold);
            d.Rect(-.052f,-.075f,.10f,.15f,Ink);
            d.Line(Gold,.018f,P(-.05f,.085f),P(.045f,.085f));
            d.Line(Gold,.018f,P(-.05f,-.085f),P(.045f,-.085f));
            d.Circle(P(.29f,.105f),.015f,Dim);d.Circle(P(.29f,-.105f),.015f,Dim);
            return d;
        }
        static Drawing Blade(float length)
        {
            var d=new Drawing();var outline=new[]{P(-length,0),P(-length+.9f,.25f),P(-length+1.45f,.075f),P(0,.075f),P(0,-.075f),P(-length+1.45f,-.075f),P(-length+.9f,-.25f),P(-length,0)};
            d.Polygon(Ink,P(-length,0),P(-length+.9f,.25f),P(-length+1.45f,.075f),P(-length+1.45f,-.075f),P(-length+.9f,-.25f));
            d.Rect(-length+1.40f,-.075f,length-1.40f,.15f,Ink);d.Line(Gold,.022f,outline);
            d.Line(Gold,.016f,P(-length+.72f,0),P(-length+1.03f,.062f),P(-length+1.30f,0),P(-length+1.03f,-.062f),P(-length+.72f,0));
            d.Line(Gold,.024f,P(-.25f,.075f),P(-.38f,.14f),P(-.48f,.13f));d.Line(Gold,.024f,P(-.25f,-.075f),P(-.38f,-.14f),P(-.48f,-.13f));
            return d;
        }
        static Drawing LegacyLeverStand(float pixel)
        {
            var d=new Drawing();float h=249*pixel;
            const float halfBase=.23f,halfTop=.075f;
            d.Polygon(Wine,P(-halfBase,0),P(-halfTop,h-.065f),P(halfTop,h-.065f),P(halfBase,0));
            d.Line(Gold,.023f,P(-halfBase,0),P(-halfTop,h-.065f),P(halfTop,h-.065f),P(halfBase,0));
            d.Rect(-.27f,0,.54f,.055f,Ink);d.Line(Gold,.022f,P(-.27f,0),P(-.27f,.055f),P(.27f,.055f),P(.27f,0),P(-.27f,0));
            d.Line(Dim,.018f,P(0,.13f),P(0,h-.09f));
            d.Circle(P(0,h),.073f,Gold);d.Circle(P(0,h),.05f,Ink);d.Circle(P(0,h),.023f,Gold);
            return d;
        }
        static Drawing LegacyLeverHandle(float pixel)
        {
            var d=new Drawing();d.Rect(0,-.019f,78*pixel,.038f,Ink);d.Line(Gold,.019f,P(0,0),P(78*pixel,0));
            d.Circle(P(78*pixel,0),.04f,Gold);d.Circle(P(78*pixel,0),.025f,Red);return d;
        }
        // Proportions of the user-approved preview: broad black semicircle,
        // low central pivot, long left-leaning rod and clearly visible round grip.
        static Drawing LeverStand(float pixel)
        {
            var d=new Drawing();var gold=C("#d4b153");const float radius=.50f,baseline=.03f;
            var arc=new Vector2[49];for(int i=0;i<arc.Length;i++){float a=i*Mathf.PI/(arc.Length-1);arc[i]=P(Mathf.Cos(a)*radius,baseline+Mathf.Sin(a)*radius);}
            d.Polygon(Ink,arc);d.Line(gold,.027f,arc);d.Line(gold,.027f,P(-radius,baseline),P(radius,baseline));
            var inner=new Vector2[45];for(int i=0;i<inner.Length;i++){float a=i*Mathf.PI/(inner.Length-1);inner[i]=P(Mathf.Cos(a)*.435f,baseline+Mathf.Sin(a)*.435f);}
            d.Line(gold,.014f,inner);
            d.Line(gold,.015f,P(-.465f,baseline),P(-.465f,0),P(.465f,0),P(.465f,baseline));
            d.Circle(P(0,.13f),.078f,gold);d.Circle(P(0,.13f),.057f,Ink);
            return d;
        }
        static Drawing LeverHandle(float pixel)
        {
            var d=new Drawing();var gold=C("#d4b153");const float length=.82f;
            d.Rect(0,-.026f,length,.052f,Ink);
            d.Line(gold,.014f,P(0,.026f),P(length,.026f));d.Line(gold,.014f,P(0,-.026f),P(length,-.026f));
            d.Circle(P(length,0),.09f,gold);d.Circle(P(length,0),.073f,C("#400b20"));
            // The hub sits in front of the root of the rod, matching the preview.
            d.Circle(Vector2.zero,.078f,gold);d.Circle(Vector2.zero,.057f,Ink);
            return d;
        }
        public static void InspectLoadedLever()
        {
            var report=new StringBuilder("Playing="+EditorApplication.isPlaying+"\n");
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);report.AppendLine("SCENE "+scene.path+" loaded="+scene.isLoaded+" dirty="+scene.isDirty);
                if(!scene.isLoaded)continue;
                foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)))
                {
                    var trap=c.leftSpikeMechanism;report.AppendLine("CAMPAIGN="+c.name+" trap="+(trap?trap.name:"missing"));if(!trap)continue;
                    report.AppendLine("marker="+trap.leverMarker.position+" visual="+trap.leverVisual.position+" pivot="+trap.leverPivot.localPosition);
                    foreach(var r in trap.leverVisual.GetComponentsInChildren<Renderer>(true))
                    {
                        var filter=r.GetComponent<MeshFilter>();var m=filter?filter.sharedMesh:null;
                        report.AppendLine("RENDER "+r.name+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" sort="+r.sortingOrder+" bounds="+r.bounds+" scale="+r.transform.lossyScale+" mesh="+(m?m.name:"none")+" meshPath="+(m?AssetDatabase.GetAssetPath(m):"none")+" vertices="+(m?m.vertexCount:0));
                    }
                    var old=c.spikeLever.GetComponent<SpriteRenderer>();report.AppendLine("OLD SPRITE enabled="+(old&&old.enabled)+" active="+c.spikeLever.gameObject.activeInHierarchy);
                    foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)))
                    {
                        var sr=r as SpriteRenderer;bool leverSprite=sr&&sr.sprite&&AssetDatabase.GetAssetPath(sr.sprite).EndsWith("/lever.asset",StringComparison.OrdinalIgnoreCase);
                        bool nearby=r.enabled&&r.gameObject.activeInHierarchy&&Mathf.Abs(r.bounds.center.x-trap.leverMarker.position.x)<3&&Mathf.Abs(r.bounds.center.y-trap.leverMarker.position.y)<2;
                        if(!leverSprite&&!nearby)continue;
                        string hierarchy=r.name;for(var p=r.transform.parent;p;p=p.parent)hierarchy=p.name+"/"+hierarchy;
                        report.AppendLine("NEARBY "+hierarchy+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" order="+r.sortingOrder+" bounds="+r.bounds+" material="+(r.sharedMaterial?AssetDatabase.GetAssetPath(r.sharedMaterial):"none")+" sprite="+(sr&&sr.sprite?AssetDatabase.GetAssetPath(sr.sprite):"none"));
                    }
                }
            }
            File.WriteAllText(Source+"LoadedLeverInspection.txt",report.ToString());
        }
        [MenuItem("Aloysius/Serpent/Replace stale lever renderers with approved assets")]
        public static void ReplaceLeverRenderers()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            const string path="Assets/Scenes/level1.unity";
            var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var c=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();var trap=c.leftSpikeMechanism;
                if(!trap||!trap.leverVisual||!trap.leverPivot)throw new InvalidOperationException("Live lever binding missing.");
                Directory.CreateDirectory(Source);File.Copy(path,Source+"BeforeRendererFix_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
                var standMesh=LeverStand(0).Mesh("SemicircleBase");standMesh.UploadMeshData(false);
                string standPath=AssetDatabase.GenerateUniqueAssetPath(Art+"SemicircleBase.asset");standMesh.name=Path.GetFileNameWithoutExtension(standPath);AssetDatabase.CreateAsset(standMesh,standPath);
                var handleMesh=LeverHandle(0).Mesh("SemicircleRod");handleMesh.UploadMeshData(false);
                string handlePath=AssetDatabase.GenerateUniqueAssetPath(Art+"SemicircleRod.asset");handleMesh.name=Path.GetFileNameWithoutExtension(handlePath);AssetDatabase.CreateAsset(handleMesh,handlePath);
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"LeftSpike.shader");
                var mat=new Material(shader){name="SemicircleLever"};mat.SetFloat("_ClipEnabled",0);mat.SetFloat("_ClipRight",100000);
                var matPath=AssetDatabase.GenerateUniqueAssetPath(Art+"SemicircleLever.mat");AssetDatabase.CreateAsset(mat,matPath);
                // Replace the actual renderer objects, not just serialized data on a reused mesh.
                foreach(var r in trap.leverVisual.GetComponentsInChildren<Renderer>(true))
                {
                    if(r.transform==trap.leverVisual||r.transform==trap.leverPivot){r.enabled=false;EditorUtility.SetDirty(r);}
                    else UnityEngine.Object.DestroyImmediate(r.gameObject);
                }
                void Add(string name,Transform parent,Mesh mesh,int order)
                {
                    var t=Child(name,parent,Vector3.zero);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.sortingOrder=order;
                    r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                }
                Add("半圆开关底座 | 当前显示",trap.leverVisual,standMesh,34);
                Add("长拨杆与圆握柄 | 当前显示",trap.leverPivot,handleMesh,35);
                trap.leverPivot.localPosition=new Vector3(0,.13f,0);trap.leverHandleLength=.82f;trap.leverStandDistance=.58f;trap.leverOffAngle=132;trap.leverOnAngle=100;
                if(!trap.leverGrip)trap.leverGrip=Child("圆握柄接触点",trap.leverPivot,new Vector3(.82f,0,0));
                trap.leverGrip.localPosition=new Vector3(.82f,0,0);trap.leverVisual.gameObject.SetActive(true);trap.leverPivot.gameObject.SetActive(true);
                // Remove the legacy sprite component completely. Leaving it merely disabled
                // also leaves the old selectable sprite geometry on the interaction marker.
                foreach(var r in c.spikeLever.GetComponentsInChildren<Renderer>(true))
                    if(!r.transform.IsChildOf(trap.leverVisual))UnityEngine.Object.DestroyImmediate(r);
                trap.leverVisual.name="半圆拨杆开关 | 当前显示与 E 联动";
                trap.Snap(trap.Armed);EditorUtility.SetDirty(trap);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
                if(!opened)Selection.activeGameObject=trap.leverVisual.gameObject;
                File.WriteAllText(Source+"RendererReplacement.txt",DateTime.Now.ToString("O")+"\nScene="+path+"\nBase="+standPath+"\nRod="+handlePath+"\nMaterial="+matPath+"\nFresh mesh assets and fresh renderer objects; old marker SpriteRenderer removed, not just disabled. Sorting 34/35, clipping disabled on dedicated lever material. Existing marker, spike outlet and E link preserved. No Play mode.\n");
            }
            finally{if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}}
        }
        [MenuItem("Aloysius/Serpent/Apply approved semicircle ground lever")]
        public static void ApplyApprovedLeverAppearance()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            const string path="Assets/Scenes/level1.unity";
            var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var campaign=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();var trap=campaign.leftSpikeMechanism;
                if(!trap||!trap.leverVisual||!trap.leverPivot)throw new InvalidOperationException("Ground lever not found.");
                Directory.CreateDirectory(Source);File.Copy(path,Source+"BeforeApprovedLever_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
                var markerPosition=trap.leverMarker.position;var outletPosition=trap.outlet.position;
                var stand=trap.leverVisual.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.transform.parent==trap.leverVisual);
                var handle=trap.leverPivot.GetComponentInChildren<MeshRenderer>(true);
                stand.GetComponent<MeshFilter>().sharedMesh=Save(LeverStand(0).Mesh("已确认半圆底座"),Art+"LeverStand.asset");
                handle.GetComponent<MeshFilter>().sharedMesh=Save(LeverHandle(0).Mesh("已确认长拨杆与圆握柄"),Art+"LeverHandle.asset");
                stand.name="已确认半圆底座 | 下层地面";handle.name="已确认长拨杆与圆握柄";handle.transform.localScale=Vector3.one;
                Undo.RecordObject(trap,"Apply user-approved semicircle lever");
                trap.leverHandleLength=.82f;trap.leverStandDistance=.58f;trap.leverOffAngle=132;trap.leverOnAngle=100;
                trap.leverPivot.localPosition=new Vector3(0,.13f,0);
                if(!trap.leverGrip)trap.leverGrip=Child("圆握柄接触点",trap.leverPivot,new Vector3(.82f,0,0));
                trap.leverGrip.localPosition=new Vector3(.82f,0,0);trap.Snap(trap.Armed);
                EditorUtility.SetDirty(trap);EditorUtility.SetDirty(stand);EditorUtility.SetDirty(handle);EditorUtility.SetDirty(trap.leverPivot);
                EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Source+"ApprovedLeverInstalled.txt",DateTime.Now.ToString("O")+"\nSaved level1. User-approved black semicircle, warm-gold contour, long rod and round wine-red grip.\nBase width=1 m; rod=.82 m; low pivot=.13 m. Lever hand retarget follows the grip.\nGround marker="+markerPosition+"; outlet="+outletPosition+". Existing E interaction, leftward stroke and firing cue preserved. No Play mode.\n");
            }
            finally{if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}}
        }
        // Undo only the unapproved appearance revision; do not replace the entire scene backup.
        public static void RevertUnapprovedLeverAppearance()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            const string path="Assets/Scenes/level1.unity";
            var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var campaign=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();var trap=campaign.leftSpikeMechanism;
                if(!trap||!trap.leverVisual||!trap.leverPivot)throw new InvalidOperationException("Linked ground lever not found.");
                float pixel=campaign.actor.transform.lossyScale.x/campaign.actor.Clip.pixelsPerUnit;
                var stand=trap.leverVisual.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.transform.parent==trap.leverVisual);
                var handle=trap.leverPivot.GetComponentInChildren<MeshRenderer>(true);
                stand.GetComponent<MeshFilter>().sharedMesh=Save(LegacyLeverStand(pixel).Mesh("下层可见开关底座"),Art+"LeverStand.asset");
                handle.GetComponent<MeshFilter>().sharedMesh=Save(LegacyLeverHandle(pixel).Mesh("下层可拨动杆柄"),Art+"LeverHandle.asset");
                stand.name="下层可见开关底座";handle.name="下层可拨动杆柄";handle.transform.localScale=Vector3.one;
                trap.leverPivot.localPosition=new Vector3(0,249*pixel,0);
                if(trap.leverGrip)UnityEngine.Object.DestroyImmediate(trap.leverGrip.gameObject);
                trap.leverGrip=null;trap.leverHandleLength=78*pixel;trap.leverStandDistance=115*pixel;trap.leverOffAngle=130;trap.leverOnAngle=50;
                trap.Snap(trap.Armed);
                EditorUtility.SetDirty(trap);EditorUtility.SetDirty(stand);EditorUtility.SetDirty(handle);EditorUtility.SetDirty(trap.leverPivot);
                EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Source+"LeverAppearanceRollback.txt",DateTime.Now.ToString("O")+"\nReverted only unapproved semicircle meshes, pivot, grip and runtime changes. Existing outlet placement, ground switch marker, firing link and other scene edits preserved. No Play mode.\n");
            }
            finally{if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}}
        }
        [MenuItem("Aloysius/Serpent/Install linked left spike and lever action")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Source);AssetDatabase.Refresh();
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"LeftSpike.shader");var clip=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"LeverMotion.json");
            if(!shader||!clip||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Missing or invalid spike shader / lever motion.");
            var data=JsonUtility.FromJson<PalaceLeftSpikeMechanism.Motion>(clip.text);
            if(data.fps!=60||data.onFrames.Length!=190||data.offFrames.Length!=190||data.bones.Length!=35)throw new InvalidOperationException("Wrong authored lever clip.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"BlackGold.mat");if(!material){material=new Material(shader){name="Left spike | black gold silhouette"};AssetDatabase.CreateAsset(material,Art+"BlackGold.mat");}else material.shader=shader;
            const string path="Assets/Scenes/level1.unity";var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var campaign=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();
                var room=campaign.rooms[1];
                foreach(var name in data.bones)if(!campaign.actor.Bones.Any(b=>b.name==name))throw new InvalidOperationException("Missing character bone: "+name);
                File.Copy(path,Source+"BeforeLink_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
                var old=campaign.leftSpikeMechanism;
                var geometry=old?old.transform:Child("横向弹刺机关 | 右侧基座向左射出",room.root,Vector3.zero);
                if(old){old.CancelOperation();for(int i=geometry.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(geometry.GetChild(i).gameObject);}
                // Registered design coordinates of the gallery's upper-left corner.
                // The old lever at x=560 lay inside the near-pillar crop (x<566, order 33).
                float unit=room.framedDimensions.x/1672f;
                float left=room.framedView.position.x-room.framedDimensions.x*.5f;
                Vector3 At(float x,float y)=>new Vector3(left+x*unit,room.floor+(858-y)*unit,0);
                geometry.name="平台左拐角弹刺 | 小型接口朝左";
                geometry.position=At(540,565);
                geometry.rotation=Quaternion.identity;geometry.localScale=Vector3.one;
                campaign.spikeLever.position=At(650,858);
                var trap=old?old:geometry.gameObject.AddComponent<PalaceLeftSpikeMechanism>();
                trap.strokeDistance=4.6f;trap.shootSeconds=.16f;trap.retractSeconds=.38f;trap.collisionHeight=.45f;
                trap.actionPlaybackSpeed=1.5f;trap.activateAtClipSeconds=1.52f;trap.leverMotion=clip;trap.leverMarker=campaign.spikeLever;
                trap.outlet=Child("射出口 | 平台拐角左侧",geometry,Vector3.zero);
                trap.bladeSlide=Child("刺体 | 独立平移，右侧遮挡",geometry,new Vector3(trap.strokeDistance,0));
                trap.bladeRenderer=Draw("黑金长刺",trap.bladeSlide,Blade(trap.strokeDistance),material,34,"Blade");
                Draw("嵌入边缘的金属接口 | 非门柱",geometry,Housing(),material,35,"Housing");
                trap.bladeTip=Child("刺尖 | 运行时位置",geometry,Vector3.zero);
                var detection=Child("刺体检测范围 | Trigger",geometry,Vector3.zero);trap.bladeDetection=detection.gameObject.AddComponent<BoxCollider2D>();trap.bladeDetection.isTrigger=true;trap.bladeDetection.enabled=false;
                var switchRoot=Child("开关 | 原位置，杆柄独立旋转",geometry,Vector3.zero);switchRoot.position=campaign.spikeLever.position;trap.leverVisual=switchRoot;
                float pixel=campaign.actor.transform.lossyScale.x/campaign.actor.Clip.pixelsPerUnit;
                Draw("下层可见开关底座",switchRoot,LeverStand(pixel),material,12,"LeverStand");
                trap.leverPivot=Child("杆柄转轴 | 与手部动作同一时间轴",switchRoot,new Vector3(0,.13f));
                trap.leverHandleLength=.82f;trap.leverStandDistance=.58f;trap.leverOffAngle=132;trap.leverOnAngle=100;
                trap.leverGrip=Child("圆握柄接触点",trap.leverPivot,new Vector3(.82f,0,0));
                Draw("下层可拨动杆柄",trap.leverPivot,LeverHandle(pixel),material,13,"LeverHandle");
                var oldSpike=campaign.spike.GetComponent<SpriteRenderer>();var oldSwitch=campaign.spikeLever.GetComponent<SpriteRenderer>();
                if(oldSpike){oldSpike.enabled=false;EditorUtility.SetDirty(oldSpike);}if(oldSwitch){oldSwitch.enabled=false;EditorUtility.SetDirty(oldSwitch);}
                campaign.spikeLever.name="一层开关 | E 拨杆联动左射机关";campaign.spikeRaiseDistance=0;campaign.leftSpikeMechanism=trap;trap.Snap(false);
                EditorUtility.SetDirty(campaign);EditorUtility.SetDirty(campaign.spikeLever);EditorUtility.SetDirty(trap);EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                var report=new StringBuilder(DateTime.Now.ToString("O")+"\nSaved: "+path+"\n");
                report.AppendLine("Lever="+campaign.spikeLever.position+"; outlet="+trap.outlet.position+"; direction=LEFT; travel="+trap.strokeDistance);
                report.AppendLine("Character: existing 35-bone approved lever clip (60 fps, on and off); native skinning, no movie.");
                report.AppendLine("Activate at clip 1.52 s / playback 1.5 = 1.013 s after pose starts. Hand, handle and launch share one clock.");
                report.AppendLine("Shoot=.16 s, withdraw=.38 s, collider follows visible extended portion. Old vertical sprite disabled; no upward translation.");
                report.AppendLine("Outlet is at design (540,565), the left-facing outer gallery corner. Tall door-like housing removed; compact .39 x .34 m sleeve only.");
                report.AppendLine("Ground lever moved from occluded design x=560 to clear bay x=650, y=858 (room floor). Lever mesh outlines widened and sorted 12/13; near pillar remains 33.");
                report.AppendLine("Inspector settings on PalaceLeftSpikeMechanism. Gallery, stair, actual right exit doorway, camera, room placement and music retained.");
                report.AppendLine("Native flat black/gold geometry follows the approved concept. No Play mode, gameplay automation or screenshots.");
                File.WriteAllText(Source+"UnityIntegration.txt",report.ToString());Debug.Log("Linked palace lever -> horizontal left spike, saved level1.");
            }
            finally{if(opened){EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}}
        }
    }
}
