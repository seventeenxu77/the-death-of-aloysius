using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class StoneCeilingBreachInstaller
    {
        const string Source="ArtSource/SpikeCeilingBreach_v001/";
        [MenuItem("Aloysius/Serpent/Install stone head ceiling breach")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            var c=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<SerpentCampaign>(true)).Single();var room=c.rooms[1];
            var far=room.root.GetComponentInChildren<FarSerpentPresentation>(true);if(!far)throw new InvalidOperationException("Distant skeletal serpent missing.");
            Directory.CreateDirectory(Source);File.Copy(scene.path,Source+"BeforeCeiling_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
            AssetDatabase.ImportAsset("Assets/Art/SerpentKing/BlackGoldWake_v026/BlackGoldWake.shader",ImportAssetOptions.ForceUpdate);
            far.skin.enabled=false;far.skin.enabled=true;
            var anchor=far.transform.parent.Find("Ceiling breach | editable opening anchor");
            if(!anchor){anchor=new GameObject("Ceiling breach | editable opening anchor").transform;anchor.SetParent(far.transform.parent,false);}
            float unit=room.framedDimensions.x/1672;
            anchor.position=room.framedView.position+new Vector3((660-836)*unit,(470.5f-90)*unit,0);anchor.localScale=Vector3.one;
            var depth=anchor.GetComponent<PalaceDepthLayer>();if(!depth)depth=anchor.gameObject.AddComponent<PalaceDepthLayer>();depth.parallax=.035f;depth.cameraOrigin=room.framedView.position;
            var breach=far.GetComponent<StoneCeilingBreach>();if(!breach)breach=far.gameObject.AddComponent<StoneCeilingBreach>();
            breach.campaign=c;breach.presentation=far;breach.roofOpening=anchor;
            breach.stoneMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SerpentKing/FloorBreak_v001/Debris.mat");
            breach.dustMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SerpentKing/FloorBreak_v001/Dust.mat");
            far.ceilingBreach=breach;c.ceilingBreach=breach;breach.previewTime=0;breach.ResetBreach();far.Refresh();
            EditorUtility.SetDirty(breach);EditorUtility.SetDirty(far);EditorUtility.SetDirty(c);EditorUtility.SetDirty(depth);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            breach.PreviewAt(breach.HoldTime+.6f);SpikeChamberLayersInstaller.RenderPreviewTo(Source+"CeilingBreach_Hold.png");breach.EndPreview();
            File.WriteAllText(Source+"UnityIntegration.txt",DateTime.Now.ToString("O")+"\nSaved: "+scene.path+
                "\nStone-only head/neck override. Lower roots fixed; rigid skull rotates upward; upper neck stretches into the ceiling and holds."+
                "\nOther three heads keep independent normal skeletal idle; shared coils remain connected."+
                "\nRoof opening: "+anchor.position+"; impact "+breach.ContactTime+" s; hold "+breach.HoldTime+" s; entry delay "+breach.entryDelay+" s."+
                "\nJagged rear void/front rim, falling stone and dust. Stone skin clips above roof plane. Combat waits for extension readiness."+
                "\nNo Play-mode test.\n");
        }
        public static void ExportVideo()
        {
            var c=UnityEngine.Object.FindObjectOfType<SerpentCampaign>();var room=c.rooms[1];var breach=c.ceilingBreach;var far=breach.presentation;
            var cameraObject=new GameObject("Ceiling breach production camera"){hideFlags=HideFlags.HideAndDontSave};var cam=cameraObject.AddComponent<Camera>();
            cam.orthographic=true;cam.orthographicSize=room.framedDimensions.y*.5f;cam.aspect=1672f/941;
            cam.transform.position=room.framedView.position+Vector3.back*10;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.012f,.025f);
            var target=new RenderTexture(1672,942,24);target.Create();cam.targetTexture=target;var previous=RenderTexture.active;
            var image=new Texture2D(1672,942,TextureFormat.RGB24,false);var referenceMeshes=new List<Mesh>();
            foreach(var part in c.actor.Parts)
            {
                var mesh=new Mesh();part.BakeMesh(mesh);referenceMeshes.Add(mesh);
                var t=new GameObject("Protagonist size reference"){hideFlags=HideFlags.HideAndDontSave}.transform;t.SetParent(cameraObject.transform,false);
                var matrix=Matrix4x4.TRS(room.spawn.position,c.actor.transform.rotation,c.actor.transform.lossyScale)*c.actor.transform.worldToLocalMatrix*part.transform.localToWorldMatrix;
                t.position=matrix.GetColumn(3);t.rotation=matrix.rotation;t.localScale=matrix.lossyScale;
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=part.sharedMaterial;r.sortingOrder=part.sortingOrder;
                var block=new MaterialPropertyBlock();part.GetPropertyBlock(block);r.SetPropertyBlock(block);
            }
            string ffmpeg=File.ReadAllText(Source+"FFmpegPath.txt").Trim(),output=Path.GetFullPath(Source+"StoneCeilingBreach.mp4");
            var start=new System.Diagnostics.ProcessStartInfo(ffmpeg,"-y -f image2pipe -vcodec png -framerate 60 -i - -an -c:v libx264 -crf 18 -preset fast -pix_fmt yuv420p -movflags +faststart \""+output+"\"")
                {UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardError=true};
            var process=new System.Diagnostics.Process{StartInfo=start};var log=new StringBuilder();process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(log)log.AppendLine(e.Data);};
            try
            {
                process.Start();process.BeginErrorReadLine();
                for(int i=0;i<360;i++)
                {
                    float time=i/60f;breach.PreviewAt(Mathf.Max(0,time-breach.entryDelay));far.SampleForPreview(time);
                    cam.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1672,942),0,0);image.Apply();byte[] png=image.EncodeToPNG();
                    process.StandardInput.BaseStream.Write(png,0,png.Length);
                    if(i==0||i==90||i==132||i==270)File.WriteAllBytes(Source+"Frame_"+i.ToString("D3")+".png",png);
                    if(i%60==0)File.WriteAllText(Source+"ExportProgress.txt",i+"/360 production frames");
                }
                process.StandardInput.Close();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException("Video encoder failed.");
                File.WriteAllText(Source+"ExportProgress.txt","Complete: 6 s, 60 fps; actual skeletal rendering, no Play-mode test.");
            }
            finally
            {
                File.WriteAllText(Source+"encode.log",log.ToString());process.Dispose();breach.EndPreview();RenderTexture.active=previous;
                target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(cameraObject);
                foreach(var m in referenceMeshes)UnityEngine.Object.DestroyImmediate(m);
            }
        }
    }
}
