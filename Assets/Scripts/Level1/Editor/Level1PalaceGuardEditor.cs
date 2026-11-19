using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    public static class Level1PalaceGuardEditor
    {
        const string Pending="InstallThreePalaceGuardsAfterStop";
        const string TexturePath="Assets/Art/PalaceGuard/GuardParts.png";
        const string Data="Assets/Data/PalaceGuard/";
        static readonly string[] Names={"Head and open crown","Torso","Near arm","Far arm","Near leg","Far leg","Pleated inner skirt","Front overskirt","Rear openwork skirt","Upright shoulder ornament","Sheathed sword","Waist chains and pendant"};
        static readonly string[] Joints={"Pelvis","Head","Near shoulder","Near elbow","Near wrist","Far shoulder","Far elbow","Far wrist","Near hip","Near knee","Near ankle","Far hip","Far knee","Far ankle","Inner cloth top","Inner cloth middle","Inner cloth hem","Front cloth top","Front cloth middle","Front cloth hem","Rear cloth top","Rear cloth middle","Rear cloth hem","Shoulder crest","Sword mount","Pendant mount"};
        static Level1PalaceGuardEditor()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Install;
            };
            if(SessionState.GetBool(Pending,false) && !EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.delayCall+=Install;
        }
        [MenuItem("Aloysius/Level1/Install Three Palace Patrol Guards")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            SessionState.SetBool(Pending,false);EditorApplication.delayCall-=Install;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Additive);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var corridor=all.Select(t=>t.GetComponent<Level1PalaceCorridor>()).First(c=>c);
            if(corridor.GetComponentInChildren<Level1PalaceGuard>(true))return;
            var lighting=all.Select(t=>t.GetComponent<Level1Lighting>()).First(c=>c);
            Directory.CreateDirectory("ArtSource/PalaceGuard/Backups");
            string backup="ArtSource/PalaceGuard/Backups/level1-before-guards-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot save live guard scene backup.");
            Directory.CreateDirectory(Data);Directory.CreateDirectory("Assets/Prefabs/Level1");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true;importer.isReadable=true;importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var shader=Shader.Find("Aloysius/Level1/Silhouette Shadow");
            if(!shader)throw new InvalidOperationException("Cannot find guard silhouette shader.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Data+"Guard.mat");
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Data+"Guard.mat");}
            material.mainTexture=texture;material.SetFloat("_Exposure",.65f);material.SetVector("_Turn",new Vector4(1,1,1,0));
            var template=new GameObject("Palace Guard | rigged patrol");
            SceneManager.MoveGameObjectToScene(template,scene);
            var visual=new GameObject("Visual | 12 skinned parts").transform;visual.SetParent(template.transform,false);
            visual.gameObject.AddComponent<SortingGroup>().sortingOrder=50;
            var boneRoot=new GameObject("Skeleton | 26 joints").transform;boneRoot.SetParent(visual,false);
            var bones=new Transform[Joints.Length];
            for(int i=0;i<bones.Length;i++)
            {
                bones[i]=new GameObject(i.ToString("00")+" "+Joints[i]).transform;
                bones[i].SetParent(boneRoot,false);bones[i].localPosition=Level1PalaceGuard.BindPositions[i];
            }
            var pixels=texture.GetPixels32();
            var parts=new SkinnedMeshRenderer[12];
            for(int i=0;i<12;i++)
            {
                Rect uv=FindPartUV(texture,pixels,i);
                Rect bounds=PartBounds(i,uv.width*texture.width/(uv.height*texture.height));
                var mesh=MakeMesh(i,bounds,uv,bones,visual);
                string meshPath=Data+"Part"+i.ToString("00")+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                else AssetDatabase.CreateAsset(mesh,meshPath);
                var part=new GameObject(Names[i]);part.transform.SetParent(visual,false);
                var renderer=part.AddComponent<SkinnedMeshRenderer>();parts[i]=renderer;
                renderer.sharedMesh=mesh;renderer.sharedMaterial=material;renderer.bones=bones;renderer.rootBone=boneRoot;
                renderer.quality=SkinQuality.Bone4;renderer.updateWhenOffscreen=true;
                renderer.localBounds=new Bounds(new Vector3(0,1.1f,0),new Vector3(2.5f,3,1));
                renderer.sortingOrder=new[]{9,5,11,3,4,1,6,7,0,10,2,8}[i];
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            var guard=template.AddComponent<Level1PalaceGuard>();guard.Configure(null,null,visual,bones,parts);
            const string prefabPath="Assets/Prefabs/Level1/PalaceGuard.prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(template,prefabPath);
            UnityEngine.Object.DestroyImmediate(template);
            var settings=new SerializedObject(corridor);
            var start=(Transform)settings.FindProperty("startBoundary").objectReferenceValue;
            var end=(Transform)settings.FindProperty("endBoundary").objectReferenceValue;
            var ground=(Transform)settings.FindProperty("ground").objectReferenceValue;
            var root=new GameObject("08 Guards | three independent patrols");root.transform.SetParent(corridor.transform,false);
            Undo.RegisterCreatedObjectUndo(root,"Add three palace patrol guards");
            float span=end.position.x-start.position.x;
            var entries=new System.Text.StringBuilder();
            for(int i=0;i<3;i++)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
                instance.name="Guard "+(i+1).ToString("00")+" | random patrol";
                float center=start.position.x+span*(.22f+i*.30f);
                instance.transform.position=new Vector3(center,ground.position.y,0);
                var s=new SerializedObject(instance.GetComponent<Level1PalaceGuard>());
                s.FindProperty("corridor").objectReferenceValue=corridor;s.FindProperty("lighting").objectReferenceValue=lighting;
                float radius=Mathf.Min(2.3f,span*.115f);
                s.FindProperty("patrolOffsets").vector2Value=new Vector2(-radius,radius);
                s.FindProperty("initiallyFaceLeft").boolValue=i==1;
                s.ApplyModifiedProperties();
                if(i==1)instance.transform.Find("Visual | 12 skinned parts").localScale=new Vector3(-1,1,1);
                entries.AppendLine(instance.name+": X="+center.ToString("F2")+", patrol="+(center-radius).ToString("F2")+".."+(center+radius).ToString("F2"));
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save guard scene.");
            Selection.activeGameObject=root;
            Directory.CreateDirectory("Logs/Level1/PalaceGuard");
            File.WriteAllText("Logs/Level1/PalaceGuard/Installed.txt",DateTime.Now.ToString("O")+"\n"+entries+"12 skinned atlas parts, 26 bones per guard. Original scene layout preserved.\nBackup: "+backup+"\nNo tests or Play mode started.");
        }
        static Rect FindPartUV(Texture2D texture,Color32[] pixels,int index)
        {
            int column=index%4,row=index/4;
            int x0=column*texture.width/4,x1=(column+1)*texture.width/4;
            int y0=texture.height-(row+1)*texture.height/3,y1=texture.height-row*texture.height/3;
            int minX=x1,minY=y1,maxX=x0,maxY=y0;
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
                if(pixels[y*texture.width+x].a>12){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
            if(minX>maxX)throw new InvalidOperationException("Atlas part is empty: "+Names[index]);
            minX=Mathf.Max(x0,minX-2);maxX=Mathf.Min(x1-1,maxX+2);minY=Mathf.Max(y0,minY-2);maxY=Mathf.Min(y1-1,maxY+2);
            return new Rect(minX/(float)texture.width,minY/(float)texture.height,(maxX-minX+1)/(float)texture.width,(maxY-minY+1)/(float)texture.height);
        }
        static Rect PartBounds(int i,float aspect)
        {
            float height;
            switch(i)
            {
                case 0:height=.70f;return new Rect(.015f-height*aspect*.72f,1.55f,height*aspect,height);
                case 1:height=.65f;return new Rect(.025f-height*aspect*.5f,.91f,height*aspect,height);
                case 2:case 3:height=.78f;return new Rect((i==2?-.07f:.035f)-height*aspect*.48f,.74f,height*aspect,height);
                case 4:case 5:height=.96f;return new Rect((i==4?0:.018f)-height*aspect*.34f,0,height*aspect,height);
                case 6:height=.78f;return new Rect(-height*aspect*.5f,.16f,height*aspect,height);
                case 7:height=.74f;return new Rect(.09f-height*aspect*.78f,.20f,height*aspect,height);
                case 8:height=.81f;return new Rect(-.10f-height*aspect*.90f,.13f,height*aspect,height);
                case 9:height=.25f;return new Rect(-.065f-height*aspect*.5f,1.39f,height*aspect,height);
                case 10:height=.85f;return new Rect(.19f-height*aspect*.5f,.22f,height*aspect,height);
                default:height=.31f;return new Rect(.025f-height*aspect*.5f,.63f,height*aspect,height);
            }
        }
        static Mesh MakeMesh(int part,Rect rectangle,Rect uv,Transform[] bones,Transform visual)
        {
            const int columns=18,rows=32;
            int count=(columns+1)*(rows+1);
            var vertices=new Vector3[count];var coords=new Vector2[count];var weights=new BoneWeight[count];
            var triangles=new int[columns*rows*6];int t=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                int v=y*(columns+1)+x;float u=x/(float)columns,w=y/(float)rows;
                vertices[v]=new Vector3(Mathf.Lerp(rectangle.xMin,rectangle.xMax,u),Mathf.Lerp(rectangle.yMin,rectangle.yMax,w),0);
                coords[v]=new Vector2(Mathf.Lerp(uv.xMin,uv.xMax,u),Mathf.Lerp(uv.yMin,uv.yMax,w));
                weights[v]=Weight(part,vertices[v]);
                if(x<columns && y<rows)
                {
                    triangles[t++]=v;triangles[t++]=v+columns+1;triangles[t++]=v+1;
                    triangles[t++]=v+1;triangles[t++]=v+columns+1;triangles[t++]=v+columns+2;
                }
            }
            var bind=new Matrix4x4[bones.Length];for(int i=0;i<bind.Length;i++)bind[i]=bones[i].worldToLocalMatrix*visual.localToWorldMatrix;
            var mesh=new Mesh{name=Names[part]};mesh.vertices=vertices;mesh.uv=coords;mesh.triangles=triangles;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.RecalculateBounds();return mesh;
        }
        static BoneWeight Weight(int part,Vector3 point)
        {
            int rigid=-1,first=0;
            switch(part)
            {
                case 0:rigid=1;break;case 1:rigid=0;break;
                case 2:first=2;break;case 3:first=5;break;
                case 4:first=8;break;case 5:first=11;break;
                case 6:first=14;break;case 7:first=17;break;case 8:first=20;break;
                case 9:rigid=23;break;case 10:rigid=24;break;case 11:rigid=25;break;
            }
            if(rigid>=0)return new BoneWeight{boneIndex0=rigid,weight0=1};
            var rest=Level1PalaceGuard.BindPositions;
            // Use narrow smooth blends around the joints; rigid limb lengths are
            // retained instead of rubber-like interpolation over the whole limb.
            float upper=(rest[first].y+rest[first+1].y)*.5f;
            float lower=(rest[first+1].y+rest[first+2].y)*.5f;
            if(part>=6){upper=rest[first].y;lower=rest[first+1].y;}
            int a,b;float blend;
            if(part<6)
            {
                if(point.y>rest[first+1].y+.07f)return new BoneWeight{boneIndex0=first,weight0=1};
                if(point.y>rest[first+1].y-.07f){a=first;b=first+1;blend=Mathf.InverseLerp(rest[first+1].y+.07f,rest[first+1].y-.07f,point.y);}
                else if(point.y>rest[first+2].y+.045f)return new BoneWeight{boneIndex0=first+1,weight0=1};
                else{a=first+1;b=first+2;blend=Mathf.InverseLerp(rest[first+2].y+.045f,rest[first+2].y-.025f,point.y);}
            }
            else if(point.y>=lower){a=first;b=first+1;blend=Mathf.InverseLerp(upper,lower,point.y);}
            else{a=first+1;b=first+2;blend=Mathf.InverseLerp(lower,rest[first+2].y,point.y);}
            blend=Mathf.SmoothStep(0,1,blend);return new BoneWeight{boneIndex0=a,boneIndex1=b,weight0=1-blend,weight1=blend};
        }
    }
}
