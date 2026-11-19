using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aloysius.Level1.Editor
{
    [InitializeOnLoad]
    static class GMConsoleStateReader
    {
        const string Request="ArtSource/DebugTools/GMConsole/ReadState.pending";
        static GMConsoleStateReader(){EditorApplication.update+=ReadPending;}
        static void ReadPending()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            File.Delete(Request);
            var list=Resources.FindObjectsOfTypeAll<Level1TeleportConsole>().Where(item=>item.gameObject.scene.IsValid()).ToArray();
            string report=DateTime.Now.ToString("O")+"\nPlaying="+EditorApplication.isPlaying+" Paused="+EditorApplication.isPaused+
                " Focus="+(EditorWindow.focusedWindow?EditorWindow.focusedWindow.GetType().FullName:"none")+"\nConsoleCount="+list.Length;
            foreach(var item in list)
            {
                var serialized=new SerializedObject(item);
                report+="\nScene="+item.gameObject.scene.path+" Active="+item.gameObject.activeInHierarchy+" Enabled="+item.enabled+
                    " Key="+serialized.FindProperty("toggleKey").intValue+" Object="+item.gameObject.name;
                var field=typeof(Level1TeleportConsole).GetField("visible",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                report+=" Visible="+field.GetValue(item);
            }
            File.WriteAllText("ArtSource/DebugTools/GMConsole/CurrentState.txt",report);
        }
    }
}
