using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aloysius.Level1.Editor
{
    public sealed class SerpentBoneWorkshopWindow : EditorWindow
    {
        public const string ScenePath="Assets/Scenes/SerpentBoneWorkshop.unity";
        SerpentEditableRig rig;
        static readonly string[] Names={"Stone","Venom","Fang","Time"};
        static readonly string[] Labels={"巨石","毒液","突刺","时间"};
        [MenuItem("Aloysius/Serpent/骨骼编辑工具")]
        public static void ShowWindow(){GetWindow<SerpentBoneWorkshopWindow>("四头蛇骨骼").Show();}
        void OnEnable(){SceneView.duringSceneGui+=SceneGUI;Selection.selectionChanged+=Repaint;}
        void OnDisable(){SceneView.duringSceneGui-=SceneGUI;Selection.selectionChanged-=Repaint;}
        void Locate()
        {
            if(Selection.activeGameObject)
            {
                var selected=Selection.activeGameObject.GetComponentInParent<SerpentEditableRig>();if(selected)rig=selected;
            }
            if(!rig)rig=Object.FindObjectOfType<SerpentEditableRig>();
        }
        public static void OpenWorkshop()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var current=Object.FindObjectOfType<SerpentEditableRig>();
            if(current)Selection.activeGameObject=current.gameObject;
            var view=SceneView.lastActiveSceneView;
            if(view)view.LookAt(new Vector3(7.68f,-5.12f,0),Quaternion.identity,11,true);
            ShowWindow();
        }
        void OnGUI()
        {
            Locate();
            if(GUILayout.Button("打开独立骨骼编辑场景"))OpenWorkshop();
            rig=(SerpentEditableRig)EditorGUILayout.ObjectField("正在编辑",rig,typeof(SerpentEditableRig),true);
            EditorGUILayout.HelpBox("这是实际蒙皮骨架。选关节后按 W 移动、E 旋转；调整会保存在场景中。没有脚本每帧把骨骼拉回。",MessageType.Info);
            if(!rig)return;
            bool visible=EditorGUILayout.Toggle("显示 / 点击骨骼",rig.showBones);
            if(visible!=rig.showBones){Undo.RecordObject(rig,"Show serpent bones");rig.showBones=visible;EditorUtility.SetDirty(rig);SceneView.RepaintAll();}
            for(int h=0;h<Names.Length;h++)
            {
                EditorGUILayout.LabelField(Labels[h],EditorStyles.boldLabel);EditorGUILayout.BeginHorizontal();
                SelectButton("颈根",Names[h]+"_Neck_0");SelectButton("中段",Names[h]+"_Neck_4");
                SelectButton("上段",Names[h]+"_Neck_7");SelectButton("头骨",Names[h]+"_Skull");
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            if(GUILayout.Button("选中动画根节点 / 打开 Animation 窗口"))
            {Selection.activeGameObject=rig.gameObject;EditorApplication.ExecuteMenuItem("Window/Animation/Animation");}
            EditorGUILayout.HelpBox("已准备 Sway_Editable 动画：0、1.5、3 秒为中立姿势。打开 Animation，点录制，在不同时间移动 / 旋转骨骼，退出录制后播放。用曲线切线设置快摆和停顿。",MessageType.None);
            if(GUILayout.Button("恢复当前骨架的初始姿势"))
            {Undo.RecordObjects(rig.bones,"Restore serpent bones");rig.RestoreRest();EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);SceneView.RepaintAll();}
            var animator=rig.GetComponent<Animator>();var controller=animator?animator.runtimeAnimatorController as AnimatorController:null;
            if(controller&&controller.layers.Length>0)
            {
                var state=controller.layers[0].stateMachine.defaultState;var clip=state?state.motion as AnimationClip:null;
                if(state&&clip)
                {
                    float seconds=Mathf.Max(.01f,clip.length),hz=state.speed/seconds;
                    float next=EditorGUILayout.FloatField("循环频率（Hz）",hz);
                    if(!Mathf.Approximately(hz,next)){Undo.RecordObject(state,"Change serpent animation frequency");state.speed=Mathf.Max(.01f,next)*seconds;EditorUtility.SetDirty(state);}
                    EditorGUILayout.LabelField("Animator 播放时生效；Animation 窗口用其自带的预览控件。",EditorStyles.wordWrappedLabel);
                }
            }
        }
        void SelectButton(string label,string name)
        {
            if(!GUILayout.Button(label))return;
            var bone=rig.FindBone(name);if(bone){Selection.activeTransform=bone;Tools.current=Tool.Move;SceneView.RepaintAll();}
        }
        void SceneGUI(SceneView view)
        {
            Locate();if(!rig||!rig.showBones||rig.bones==null)return;
            for(int i=0;i<rig.bones.Length;i++)
            {
                var bone=rig.bones[i];if(!bone)continue;
                string name=rig.jointNames[i];Handles.color=name.StartsWith("Stone")?new Color(1,.65f,.25f):name.StartsWith("Venom")?Color.cyan:name.StartsWith("Fang")?new Color(1,.35f,.55f):Color.yellow;
                if(bone.parent&&System.Array.IndexOf(rig.bones,bone.parent)>=0)Handles.DrawAAPolyLine(3,bone.parent.position,bone.position);
                float size=HandleUtility.GetHandleSize(bone.position)*.035f;
                if(Handles.Button(bone.position,Quaternion.identity,size,size*1.4f,Handles.DotHandleCap))Selection.activeTransform=bone;
                if(Selection.activeTransform==bone)Handles.Label(bone.position+Vector3.up*.15f,name);
            }
        }
    }
}
