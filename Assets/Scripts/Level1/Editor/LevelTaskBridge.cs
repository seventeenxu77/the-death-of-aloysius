using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad] public static class LevelTaskBridge
    {
        public const string Folder="ArtSource/LevelTask_20261116/";
        static double next;
        static LevelTaskBridge(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Folder+"Request.txt"))return;
            string command=File.ReadAllText(Folder+"Request.txt").Trim();File.Delete(Folder+"Request.txt");
            try
            {
                if(command=="dump")Dump();
                else if(command=="refresh")AssetDatabase.Refresh();
                else if(command=="stop")EditorApplication.isPlaying=false;
                else if(command=="play")EditorApplication.isPlaying=true;
                else
                {
                    string[] bits=command.Split('|');var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(bits[0])).FirstOrDefault(t=>t!=null);
                    if(type==null)throw new Exception("Type not loaded: "+bits[0]);
                    type.GetMethod(bits[1],System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).Invoke(null,null);
                }
                File.WriteAllText(Folder+"BridgeResult.txt",DateTime.Now.ToString("O")+" OK "+command);
            }
            catch(Exception ex){File.WriteAllText(Folder+"BridgeError.txt",ex.ToString());Debug.LogException(ex);}
        }
        public static void Dump()
        {
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/level1.unity",OpenSceneMode.Single);
            var s=new StringBuilder();s.AppendLine("Playing="+EditorApplication.isPlaying+" Dirty="+scene.isDirty);
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {if(t.GetComponent<Renderer>()||t.name.Contains("loor")||t.name.Contains("limit")||t.name.Contains("arrival")||t.name.Contains("centre"))s.AppendLine(t.name+" | "+t.position+" | "+t.lossyScale+" | "+(t.GetComponent<Renderer>()?t.GetComponent<Renderer>().bounds.ToString():""));}
            var route=UnityEngine.Object.FindObjectOfType<Level1RoyalBedchamber>();if(route)s.AppendLine(EditorJsonUtility.ToJson(route,true));
            File.WriteAllText(Folder+"SceneDump.txt",s.ToString());
        }
    }
}
