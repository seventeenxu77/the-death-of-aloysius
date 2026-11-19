using System;
using System.IO;
using System.Linq;
using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class SerpentBoneWorkshopBuilder
    {
        const string Folder="Assets/Art/SerpentKing/EditableBones/";
        static Vector3 Point(float x,float y)=>new Vector3(x*.01f,-y*.01f,0);
        static void SaveAsset(UnityEngine.Object value,string path)
        {
            if(AssetDatabase.LoadMainAssetAtPath(path))throw new InvalidOperationException("Asset exists; preserving user edits: "+path);
            AssetDatabase.CreateAsset(value,path);
        }
        [MenuItem("Aloysius/Serpent/Create editable bone workshop")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var level=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!level.isLoaded)throw new InvalidOperationException("Open level1 first.");
            var skins=level.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<BlackGoldWakeSkin>(true)).ToArray();
            var source=skins.First(s=>s.drawBed);
            foreach(var skin in skins){Undo.RecordObject(skin,"Stop procedural sway");skin.idleMotion=false;EditorUtility.SetDirty(skin);}
            EditorSceneManager.MarkSceneDirty(level);EditorSceneManager.SaveScene(level);
            if(File.Exists(SerpentBoneWorkshopWindow.ScenePath)){SerpentBoneWorkshopWindow.OpenWorkshop();return;}
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var data=JsonUtility.FromJson<BlackGoldWakeSkin.Rig>(source.rigAsset.text);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root=new GameObject("四头蛇 | 可编辑蒙皮骨架");var edit=root.AddComponent<SerpentEditableRig>();
            var skeleton=new GameObject("Skeleton").transform;skeleton.SetParent(root.transform,false);
            edit.bones=new Transform[data.joints.Length];edit.jointNames=data.joints.Select(j=>j.name).ToArray();edit.restLocalPositions=new Vector3[data.joints.Length];
            for(int i=0;i<edit.bones.Length;i++)
            {
                var joint=data.joints[i];var t=new GameObject(joint.name).transform;t.SetParent(joint.parent<0?skeleton:edit.bones[joint.parent],false);
                t.localPosition=Point(joint.rest[0],joint.rest[1])-(joint.parent<0?Vector3.zero:Point(data.joints[joint.parent].rest[0],data.joints[joint.parent].rest[1]));
                edit.bones[i]=t;edit.restLocalPositions[i]=t.localPosition;
            }
            var bind=edit.bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
            foreach(var skin in data.meshes)
            {
                int count=skin.vertices.Length/2;var v=new Vector3[count];var uv=new Vector2[count];var weights=new BoneWeight[count];
                for(int i=0;i<count;i++)
                {
                    v[i]=Point(skin.vertices[i*2],skin.vertices[i*2+1]);uv[i]=new Vector2(skin.uv[i*2],1-skin.uv[i*2+1]);
                    weights[i]=new BoneWeight{boneIndex0=skin.joints[i*2],boneIndex1=skin.joints[i*2+1],weight0=skin.weights[i*2],weight1=skin.weights[i*2+1]};
                }
                var mesh=new Mesh{name=skin.name+" native weighted skin"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=skin.indices;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.RecalculateBounds();
                SaveAsset(mesh,Folder+skin.name+"_Skin.asset");
                var art=Array.Find(source.art,a=>a.name==skin.name);bool body=skin.name=="Coils";
                var material=new Material(source.skinMaterial){name=skin.name+" editable"};
                material.SetTexture("_MainTex",art.open);material.SetTexture("_ClosedTex",art.closed?art.closed:art.open);
                material.SetFloat("_ClosedEye",0);material.SetFloat("_Roots",body?0:1);material.SetFloat("_Front",0);material.SetFloat("_Exposure",1);material.SetFloat("_RoofClipEnabled",0);
                SaveAsset(material,Folder+skin.name+".mat");
                AddSkin(root,edit,skeleton,mesh,material,body?1:Array.FindIndex(data.parts,p=>p.name==skin.name)+2,skin.name);
                if(body)
                {
                    var front=new Material(material){name="Coils foreground"};front.SetFloat("_Front",1);SaveAsset(front,Folder+"CoilsFront.mat");
                    AddSkin(root,edit,skeleton,mesh,front,6,"Coils foreground");
                }
            }
            AddBed(root,source,source.bedBackground,0,"BedBackground");AddBed(root,source,source.bedForeground,7,"BedForeground");
            var clip=new AnimationClip{name="Sway_Editable",frameRate=60};
            foreach(string name in new[]{"Stone_Skull","Venom_Skull","Fang_Skull","Time_Skull"})
            {
                string path=AnimationUtility.CalculateTransformPath(edit.FindBone(name),root.transform);
                clip.SetCurve(path,typeof(Transform),"localEulerAnglesRaw.z",new AnimationCurve(new Keyframe(0,0),new Keyframe(1.5f,0),new Keyframe(3,0)));
            }
            var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);clipSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,clipSettings);
            SaveAsset(clip,Folder+"Sway_Editable.anim");
            var controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"Serpent_Editable.controller");controller.AddMotion(clip);
            var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"SerpentKing_Editable.prefab");
            var camera=new GameObject("Workshop Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5.5f;camera.aspect=1.5f;camera.transform.position=new Vector3(7.68f,-5.12f,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.03f,.06f);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,SerpentBoneWorkshopWindow.ScenePath);EditorSceneManager.CloseScene(scene,true);
            SceneManager.SetActiveScene(level);
            File.WriteAllText("ArtSource/SerpentKing/SunflowerSway_v001/BoneWorkshopIntegration.txt",DateTime.Now.ToString("O")+"\nCreated saved 46 Transform bones, 6 native SkinnedMeshRenderers, 5 weighted meshes and an editable AnimationClip. No pose-writing scripts on the workshop rig. Procedural idle disabled on both game skins. No Play mode run.");
            SerpentBoneWorkshopWindow.OpenWorkshop();
        }
        static void AddSkin(GameObject root,SerpentEditableRig edit,Transform skeleton,Mesh mesh,Material material,int order,string name)
        {
            var t=new GameObject(name).transform;t.SetParent(root.transform,false);var r=t.gameObject.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh=mesh;r.sharedMaterial=material;r.bones=edit.bones;r.rootBone=skeleton;r.sortingOrder=order;r.updateWhenOffscreen=true;r.quality=SkinQuality.Bone2;
            r.localBounds=new Bounds(new Vector3(7.68f,-5.12f,0),new Vector3(45,35,5));
        }
        static void AddBed(GameObject root,BlackGoldWakeSkin source,Texture2D texture,int order,string name)
        {
            var mesh=new Mesh{name=name};mesh.vertices=new[]{Point(0,0),Point(1536,0),Point(0,1024),Point(1536,1024)};mesh.uv=new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0)};mesh.triangles=new[]{0,1,2,1,3,2};mesh.RecalculateBounds();SaveAsset(mesh,Folder+name+".asset");
            var material=new Material(source.skinMaterial){name=name};material.SetTexture("_MainTex",texture);material.SetTexture("_ClosedTex",texture);material.SetFloat("_Roots",0);material.SetFloat("_Front",0);material.SetFloat("_Exposure",1);SaveAsset(material,Folder+name+".mat");
            var t=new GameObject(name).transform;t.SetParent(root.transform,false);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=order;
        }
    }
}
