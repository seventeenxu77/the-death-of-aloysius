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
    public static class Level1PalaceStormEditor
    {
        const string Dir="Assets/Art/PalaceStorm/";
        const string Pending="InstallPalaceStormAfterStop";
        static Level1PalaceStormEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Install;
            };
        }
        [MenuItem("Aloysius/Level1/Install Palace Thunderstorm Lighting")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            SessionState.SetBool(Pending,false);EditorApplication.delayCall-=Install;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var corridor=all.Select(t=>t.GetComponent<Level1PalaceCorridor>()).First(c=>c);
            if(corridor.GetComponentInChildren<Level1PalaceStorm>(true))return;
            Directory.CreateDirectory("ArtSource/PalaceStorm/Backups");
            string backup="ArtSource/PalaceStorm/Backups/level1-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live scene backup.");
            Directory.CreateDirectory(Dir);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader=Shader.Find("Aloysius/Level1/Palace Storm Overlay");if(!shader)throw new InvalidOperationException("Storm shader is not imported.");
            var material=new Material(shader){name="Palace storm overlays"};AssetDatabase.CreateAsset(material,Dir+"StormOverlay.mat");
            var settings=new SerializedObject(corridor);
            var left=(Transform)settings.FindProperty("startBoundary").objectReferenceValue;
            var right=(Transform)settings.FindProperty("endBoundary").objectReferenceValue;
            var ground=(Transform)settings.FindProperty("ground").objectReferenceValue;
            var view=all.Select(t=>t.GetComponent<Level1RoomView>()).First(c=>c).GetComponent<Camera>();
            var windows=corridor.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith("Window gallery ")).OrderBy(r=>r.bounds.center.x).ToArray();
            var distant=corridor.transform.Find("03 Parallax | distant palace and trees");
            var near=corridor.transform.Find("04 Parallax | garden hedges");
            var cover=corridor.transform.Find("05 Cover props | move individual objects");
            var art=corridor.transform.Find("06 Wall art");
            var architecture=corridor.transform.Find("02 Architecture | two window gallery sections");
            var sky=architecture.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Night sky")).ToArray();
            var garden=architecture.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Garden ground")).Concat(near.GetComponentsInChildren<Renderer>()).ToArray();
            var root=new GameObject("07 Thunderstorm | lighting and window effects");root.transform.SetParent(corridor.transform,false);
            Undo.RegisterCreatedObjectUndo(root,"Add palace thunderstorm");
            var storm=root.AddComponent<Level1PalaceStorm>();
            var floorRoot=new GameObject("Window light on floor | no collisions");floorRoot.transform.SetParent(root.transform,false);
            var projections=new List<Renderer>();
            float[] centers={.107f,.31f,.503f,.702f,.903f};
            int index=0;
            foreach(var window in windows)
            {
                var bounds=window.bounds;
                foreach(float center in centers)
                {
                    float x=Mathf.Lerp(bounds.min.x,bounds.max.x,center),w=bounds.size.x*.125f;
                    float y=ground.position.y;
                    Vector3[] points={new Vector3(x-w*.63f+.4f,y-.56f,1),new Vector3(x+w*.63f+.4f,y-.56f,1),
                        new Vector3(x-w*.5f,y+.10f,1),new Vector3(x+w*.5f,y+.10f,1)};
                    projections.Add(Quad("Window projection "+(++index),floorRoot.transform,points,material,window.sortingOrder+1,"WindowProjection"+index));
                }
            }
            float top=windows.Max(w=>w.bounds.max.y);
            var rain=Quad("Rain | outside glass",root.transform,new[]{new Vector3(left.position.x,ground.position.y+.25f,3),new Vector3(right.position.x,ground.position.y+.25f,3),new Vector3(left.position.x,top,3),new Vector3(right.position.x,top,3)},material,-12,"Rain");
            var boltObject=new GameObject("Lightning bolt | distant parallax");boltObject.transform.SetParent(distant,false);
            var bolt=boltObject.AddComponent<LineRenderer>();bolt.sharedMaterial=material;bolt.useWorldSpace=false;bolt.positionCount=0;
            bolt.widthMultiplier=.023f;bolt.widthCurve=AnimationCurve.Linear(0,1,1,.3f);bolt.numCornerVertices=2;bolt.numCapVertices=2;
            bolt.textureMode=LineTextureMode.Stretch;bolt.sortingOrder=-30;bolt.enabled=false;
            var s=new SerializedObject(storm);
            Set(s,"corridor",corridor);Set(s,"view",view);Set(s,"leftBoundary",left);Set(s,"rightBoundary",right);Set(s,"ground",ground);Set(s,"rain",rain);Set(s,"lightningBolt",bolt);
            var p=s.FindProperty("windowProjections");p.arraySize=projections.Count;for(int i=0;i<projections.Count;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=projections[i];
            var groups=s.FindProperty("layers");groups.arraySize=5;
            Group(groups,0,Level1PalaceStorm.Layer.Sky,sky,0f,1.15f,.9f,0);
            // The lightning line is animated independently, so exclude it from the distant renderer group.
            Group(groups,1,Level1PalaceStorm.Layer.Distant,distant.GetComponentsInChildren<SpriteRenderer>().Cast<Renderer>().ToArray(),0f,.85f,.32f,.1f);
            Group(groups,2,Level1PalaceStorm.Layer.Garden,garden,0f,.62f,.16f,.12f);
            Group(groups,3,Level1PalaceStorm.Layer.Architecture,windows.Cast<Renderer>().Concat(art.GetComponentsInChildren<Renderer>()).ToArray(),0f,.95f,.12f,.08f);
            Group(groups,4,Level1PalaceStorm.Layer.Cover,cover.GetComponentsInChildren<Renderer>(),0f,.50f,.015f,.45f);
            s.ApplyModifiedProperties();
            settings.FindProperty("storm").objectReferenceValue=storm;settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save palace storm.");
            Directory.CreateDirectory("Logs/Level1/PalaceStorm");
            File.WriteAllText("Logs/Level1/PalaceStorm/Installed.txt",DateTime.Now.ToString("O")+"\nStorm lighting, ten window projections, outside rain and distant lightning installed.\nExisting object transforms, physics, player controls and camera settings untouched.\nBackup: "+backup+"\nNo tests or Play mode started.");
            Selection.activeGameObject=root;
        }
        static Renderer Quad(string name,Transform parent,Vector3[] world,Material material,int sortingOrder,string assetName)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);
            var mesh=new Mesh {name=assetName};mesh.vertices=world.Select(v=>g.transform.InverseTransformPoint(v)).ToArray();
            mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
            mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Dir+assetName+".asset");
            g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=sortingOrder;r.enabled=false;return r;
        }
        static void Set(SerializedObject target,string field,UnityEngine.Object value)=>target.FindProperty(field).objectReferenceValue=value;
        static void Group(SerializedProperty list,int index,Level1PalaceStorm.Layer layer,Renderer[] renderers,float dark,float bright,float fill,float rim)
        {
            var g=list.GetArrayElementAtIndex(index);g.FindPropertyRelative("layer").enumValueIndex=(int)layer;
            g.FindPropertyRelative("darkExposure").floatValue=dark;g.FindPropertyRelative("flashExposure").floatValue=bright;g.FindPropertyRelative("blueFill").floatValue=fill;g.FindPropertyRelative("rim").floatValue=rim;
            var rs=g.FindPropertyRelative("renderers");rs.arraySize=renderers.Length;for(int i=0;i<renderers.Length;i++)rs.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];
        }
    }
}
