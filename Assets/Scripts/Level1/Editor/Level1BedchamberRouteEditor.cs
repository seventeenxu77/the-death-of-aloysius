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
    public static class Level1BedchamberRouteEditor
    {
        const string Source="ArtSource/SerpentKing/BedchamberRoute_v002/";
        const string Art="Assets/Art/Textures/Level1/RoyalBedchamberRoute/";
        const string Pending=Source+"Apply.pending";
        static double nextCheck;
        static Level1BedchamberRouteEditor(){EditorApplication.update+=ProcessPending;}
        static void ProcessPending()
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Pending)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                bool build=File.ReadAllText(Pending).Contains("build");Apply();File.Delete(Pending);
                if(build)Level1RoyalBedchamberEditor.BuildPlayer();
                File.WriteAllText(Source+"Completed.txt",DateTime.Now.ToString("O")+"\nSaved level1 with corrected route; player packaged. No game or tests launched.\n");
            }
            catch(Exception e)
            {
                File.WriteAllText(Source+"ApplyError.txt",e.ToString());File.Delete(Pending);Debug.LogException(e);
            }
        }
        [MenuItem("Aloysius/Level1/Apply Approved Depth Corridor Route")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply in Edit mode.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Level1RoyalBedchamber>(true)).First();
            if(route.correctedRoute)throw new InvalidOperationException("Route already revised; preserve user placement.");
            Directory.CreateDirectory(Source);
            string backup=Source+"level1-before-route-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Live scene backup failed.");
            foreach(var file in new[]{"DepthHall","EnclosedHall"})
            {
                string path=Art+file+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Default;imp.mipmapEnabled=false;imp.alphaIsTransparency=false;
                imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Bilinear;imp.SaveAndReimport();
            }
            var depth=SpriteAsset("DepthHall");var closed=SpriteAsset("EnclosedHall");
            var mat=new Material(Shader.Find("Aloysius/Level1/PalaceSprite")){name="Enclosed palace corridors | dark burgundy"};
            mat.SetFloat("_Exposure",.56f);mat.SetVector("_WorldClip",new Vector4(-1000,1000,-1000,1000));
            AssetDatabase.CreateAsset(mat,Art+"Corridor.mat");
            float floor=route.antechamberSpawn.position.y;
            var oldEntry=route.corridorHole.parent;float portalX=route.returnToCorridor.position.x+2.1f;
            // Retire only the rejected entrance, preserving unrelated room and gallery objects.
            Undo.RecordObject(oldEntry.gameObject,"Retire rejected locked-door shortcut");oldEntry.gameObject.SetActive(false);oldEntry.name="00 Retired v001 shortcut | inactive";
            var front=Child(route.transform,"01 Mirror hall end | enter passage toward background");
            Add(front,"Open depth passage entrance",depth,new Rect(portalX-1.75f,floor-.2f,3.5f,6.2f),6,mat);
            route.depthEntrance=Marker(front,"Entry | W or E while standing",portalX,floor);
            route.returnToCorridor=Marker(front,"Return to mirror hall",portalX,floor);
            var passage=Child(route.transform,"02 Depth passage | same floor toward background");
            Add(passage,"Perspective corridor | repeated arches and visible floor",depth,new Rect(135,floor-.9f,20,13.3333f),-30,mat);
            route.depthNear=Marker(passage,"Near floor point",145,floor);
            route.depthFar=Marker(passage,"Distant floor point",145,floor+4.43f);
            route.depthLeftTurn=Marker(passage,"Left arch at far end",142.8f,floor+4.25f);
            route.depthCamera=Marker(passage,"Depth camera framing",145,floor+4.7f);route.depthCamera.position+=Vector3.back*10;
            var hall=Child(route.transform,"03 Enclosed corridor | walk left toward antechamber");
            Add(hall,"Solid walls and red carpet",closed,new Rect(106,floor-.8f,24,8),-30,mat);
            route.enclosedLeft=Marker(hall,"Left walking bound",106.6f,floor);route.enclosedRight=Marker(hall,"Right walking bound",129.4f,floor);
            route.enclosedLeftDoor=Marker(hall,"Left exit to antechamber | E",106.9f,floor);route.enclosedRightDoor=Marker(hall,"Right exit to depth passage | E",129.0f,floor);
            route.enclosedLeftSpawn=Marker(hall,"Arrival from antechamber",107.3f,floor);route.enclosedRightSpawn=Marker(hall,"Arrival after left turn",128.6f,floor);
            var ground=Marker(hall,"Floor | one-way support",118,floor-.1f);var box=ground.gameObject.AddComponent<BoxCollider2D>();box.size=new Vector2(24,.2f);
            var actorSO=new SerializedObject(route.actor);var surfaces=actorSO.FindProperty("oneWaySurfaces");int n=surfaces.arraySize;surfaces.arraySize=n+1;surfaces.GetArrayElementAtIndex(n).objectReferenceValue=box;actorSO.ApplyModifiedProperties();
            var ante=route.antechamberSpawn.parent;ante.name="04 Antechamber | between enclosed corridor and bedroom";
            var formerHole=ante.Find("Small rear-side crawl opening");if(formerHole){formerHole.gameObject.SetActive(false);formerHole.name="Retired crawl opening | inactive";}
            route.antechamberHole.name="Right door to enclosed corridor | E";route.antechamberHole.position=new Vector3(103.35f,floor,0);
            route.antechamberSpawn.name="Arrival from enclosed corridor";route.antechamberSpawn.position=new Vector3(103.0f,floor,0);
            var door=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/Level1/RoyalBedchamber/Door_Leaf.asset");
            var doorR=Add(ante,"Right doorway leaf | enclosed corridor connection",door,new Rect(103.7f,floor+.05f,.8f,4.2f),-7,mat);doorR.transform.localRotation=Quaternion.Euler(0,55,0);
            route.bedroomSpawn.parent.name="05 Giant bedchamber | left of antechamber";
            route.correctedRoute=true;route.passageDuration=.55f;route.depthTravelDistance=8;route.depthWalkSpeed=1.35f;route.depthAcceleration=3;
            route.distantCharacterScale=.44f;route.leftTurnDuration=.65f;route.depthCameraSize=5.6f;route.enclosedCameraSize=4;
            EditorUtility.SetDirty(route);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save corrected route.");
            Selection.activeGameObject=route.gameObject;
            File.WriteAllText(Source+"Installed.txt",DateTime.Now.ToString("O")+"\nLive backup: "+backup+"\nMirror hall -> perspective depth passage -> left turn -> enclosed corridor -> antechamber -> left bedroom.\nOriginal room, original pipe, guards, furniture, bed and curtain parameters retained.\nNo Play mode or tests.\n");
        }
        static Transform Child(Transform p,string name){var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform;}
        static Transform Marker(Transform p,string name,float x,float y){var t=Child(p,name);t.position=new Vector3(x,y,0);return t;}
        static Sprite SpriteAsset(string name)
        {var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+name+".png");var s=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);s.name=name;AssetDatabase.CreateAsset(s,Art+name+".asset");return s;}
        static SpriteRenderer Add(Transform parent,string name,Sprite sprite,Rect rect,int order,Material material)
        {var t=Marker(parent,name,rect.center.x,rect.center.y);t.localScale=new Vector3(rect.width/sprite.bounds.size.x,rect.height/sprite.bounds.size.y,1);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=order;r.sharedMaterial=material;return r;}
    }
}
