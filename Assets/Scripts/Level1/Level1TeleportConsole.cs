using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aloysius.Level1
{
    // Keep the original component and GUID so existing scene references remain valid.
    [DefaultExecutionOrder(-10000)]
    public sealed class Level1TeleportConsole : MonoBehaviour
    {
        [Header("GM 控制台")]
        [SerializeField] Level1RoyalBedchamber route;
        [SerializeField] Transform hallSpawn;
        [SerializeField] KeyCode toggleKey=KeyCode.BackQuote;
        [SerializeField] bool pauseWhileTyping=true;
        [SerializeField] bool closeAfterTeleport=false;
        [SerializeField,Range(14,28)] int fontSize=18;
        [SerializeField,Min(20)] int logCapacity=200;
        [SerializeField,Min(10)] int historyCapacity=80;
        static Level1TeleportConsole active;
        static int suppressThroughFrame=-1;
        bool visible,focusRequested,ownsPause,closeRequested;
        float previousTimeScale;
        float lastToggleTime=float.NegativeInfinity;
        IMECompositionMode previousIME;
        string input="",draft="";
        int historyIndex,completionIndex=-1;
        Vector2 scroll;
        readonly List<string> history=new List<string>();
        readonly List<string> completionCycle=new List<string>();
        struct LogEntry {public string Text;public bool Error,Input;}
        readonly List<LogEntry> log=new List<LogEntry>();
        GUIStyle fieldStyle,labelStyle,titleStyle;
        Font consoleFont;
        GMCommandRegistry registry;
        static readonly string[] Destinations={"room","hall","end","turn","pass","ante","bed","wake","crack","spike","fang","loop","cannon","time"};
        const string Places="room 原房间 | hall 镜厅入口 | end 镜厅尽头\nturn 纵深走廊 | pass 密闭走廊 | ante 前厅 | bed 巨床寝宫\nwake 苏醒 | crack 裂缝地板 | spike 尖刺室 | fang 突刺攀登\nloop 卧室回环 | cannon 炮台 | time 时间之蛇";
        public static bool BlocksGameplayInput => (active && active.visible)||Time.frameCount<=suppressThroughFrame;
        public GMCommandRegistry Commands {get {EnsureRegistry();return registry;}}
        public void RegisterCommand(string name,string usage,string description,Func<string[],GMCommandResult> handler,
            Func<string[],IEnumerable<string>> completer=null) => Commands.Register(name,usage,description,handler,completer);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){active=null;suppressThroughFrame=-1;}
        void Awake(){active=this;EnsureRegistry();Append("GM 已就绪。help 查看命令，2dtrans 查看地点。",false);}
        public void Configure(Level1RoyalBedchamber value,Transform hall){route=value;hallSpawn=hall;}
        void EnsureRegistry()
        {
            if(registry!=null)return;
            registry=new GMCommandRegistry();
            registry.Register("help","help [命令]","查看命令帮助",args=> {
                if(args.Length==0)return GMCommandResult.Ok(string.Join("\n",registry.Commands.Select(item=>item.Usage+" — "+item.Description)));
                if(args.Length==1&&registry.TryGet(args[0],out var selected))
                    return GMCommandResult.Ok(selected.Usage+" — "+selected.Description+(selected.Name=="2dtrans"?"\n"+Places:""));
                return GMCommandResult.Error("命令不存在，输入 help 查看列表。");
            },args=>CompleteSingle(registry.Commands.Select(item=>item.Name),args));
            registry.Register("2dtrans","2dtrans <地点>","传送到指定地点",args=> {
                if(args.Length==0||(args.Length==1&&(args[0]=="?"||args[0].Equals("help",StringComparison.OrdinalIgnoreCase))))return GMCommandResult.Ok(Places);
                if(args.Length!=1)return GMCommandResult.Error("格式：2dtrans bed。一次输入一个地点。");
                if(!route)return GMCommandResult.Error("当前场景没有绑定传送控制器。");
                string place=args[0].ToLowerInvariant();var campaign=FindObjectOfType<Boss.SerpentCampaign>();
                if(!Destinations.Contains(place))return GMCommandResult.Error("未知地点。输入 2dtrans 查看地点列表。");
                if(campaign&&campaign.Teleport(place))return GMCommandResult.Ok("已传送："+place);
                if(campaign)campaign.SuspendForOriginalRoute();
                bool ok=route.TeleportForDebug(place,hallSpawn,out string message);
                if(ok&&closeAfterTeleport)closeRequested=true;
                return ok?GMCommandResult.Ok(message):GMCommandResult.Error(message);
            },args=>CompleteSingle(Destinations,args));
            registry.Register("clear","clear","清空控制台日志",args=> {
                if(args.Length!=0)return GMCommandResult.Error("格式：clear");
                log.Clear();return GMCommandResult.Ok("");
            });
            registry.Register("history","history","查看本次运行的命令历史",args=>args.Length==0?
                GMCommandResult.Ok(string.Join("\n",history.Select((item,index)=>(index+1)+". "+item))):GMCommandResult.Error("格式：history"));
            registry.Register("close","close","关闭 GM 控制台",args=> {
                if(args.Length!=0)return GMCommandResult.Error("格式：close");
                closeRequested=true;return GMCommandResult.Ok("控制台已关闭。");
            });
        }
        static IEnumerable<string> CompleteSingle(IEnumerable<string> choices,string[] args) => args.Length==1?
            choices.Where(value=>value.StartsWith(args[0],StringComparison.OrdinalIgnoreCase)):Enumerable.Empty<string>();
        void Update()
        {
            string chars=Input.inputString;
            bool typedToggle=chars.Length==1&&IsToggleCharacter(chars[0]);
            if(Input.GetKeyDown(toggleKey)||typedToggle){ToggleFromKeyboard();return;}
            if(visible&&Input.GetKeyDown(KeyCode.Escape))Close();
        }
        void ToggleFromKeyboard()
        {
            // IME can deliver the physical key and its character on separate frames.
            // Update and OnGUI may also report the same press; toggle only once.
            if(Time.realtimeSinceStartup-lastToggleTime<.25f)return;
            lastToggleTime=Time.realtimeSinceStartup;
            if(visible)Close();else Open();
        }
        [ContextMenu("打开 GM 控制台（运行时）")]
        public void ShowConsole()
        {
            if(Application.isPlaying&&!visible)Open();
        }
        static bool IsToggleCharacter(char value) => value=='·'||value=='`'||value=='~';
        void Open()
        {
            active=this;visible=true;focusRequested=true;input=draft="";historyIndex=history.Count;ResetCompletion();
            previousTimeScale=Time.timeScale;ownsPause=pauseWhileTyping;
            if(ownsPause)Time.timeScale=0;
            previousIME=Input.imeCompositionMode;Input.imeCompositionMode=IMECompositionMode.Off;
        }
        void Close()
        {
            if(!visible&&!ownsPause)return;
            visible=false;focusRequested=false;suppressThroughFrame=Time.frameCount+1;
            Input.imeCompositionMode=previousIME;
            if(ownsPause){Time.timeScale=previousTimeScale;ownsPause=false;}
        }
        void OnDisable(){Close();if(active==this)active=null;}
        void OnDestroy(){if(consoleFont)Destroy(consoleFont);}
        public bool Execute(string command,out string result)
        {
            var response=Commands.Execute(command);result=response.Message;return response.Success;
        }
        void Submit()
        {
            string command=input.Trim();if(command.Length==0)return;
            Append("> "+command,false,true);
            if(history.Count==0||history[history.Count-1]!=command)history.Add(command);
            while(history.Count>Mathf.Max(10,historyCapacity))history.RemoveAt(0);
            bool ok=Execute(command,out string result);
            if(!string.IsNullOrEmpty(result))Append(result,!ok);
            input=draft="";historyIndex=history.Count;ResetCompletion();focusRequested=true;
            if(closeRequested){closeRequested=false;Close();}
        }
        void Append(string message,bool error,bool command=false)
        {
            log.Add(new LogEntry {Text=message,Error=error,Input=command});
            while(log.Count>Mathf.Max(20,logCapacity))log.RemoveAt(0);
            scroll.y=float.MaxValue;
        }
        void ResetCompletion(){completionCycle.Clear();completionIndex=-1;}
        void Complete(bool backwards)
        {
            if(completionCycle.Count==0)completionCycle.AddRange(Commands.Complete(input));
            if(completionCycle.Count==0)return;
            completionIndex=completionIndex<0?(backwards?completionCycle.Count-1:0):
                (completionIndex+(backwards?-1:1)+completionCycle.Count)%completionCycle.Count;
            input=completionCycle[completionIndex];focusRequested=true;
        }
        void NavigateHistory(int delta)
        {
            if(historyIndex==history.Count)draft=input;
            historyIndex=Mathf.Clamp(historyIndex+delta,0,history.Count);
            input=historyIndex==history.Count?draft:history[historyIndex];ResetCompletion();focusRequested=true;
        }
        void OnGUI()
        {
            var e=Event.current;
            // Listen while hidden too: some layouts/IMEs expose this only as a GUI event.
            if(e.type==EventType.KeyDown&&(e.keyCode==toggleKey||IsToggleCharacter(e.character)))
            {
                ToggleFromKeyboard();e.Use();
            }
            if(!visible)return;
            if(e.type==EventType.KeyDown)
            {
                if(e.keyCode==toggleKey||IsToggleCharacter(e.character))e.Use();
                else if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){e.Use();Submit();if(!visible)return;}
                else if(e.keyCode==KeyCode.Tab){Complete(e.shift);e.Use();}
                else if(e.keyCode==KeyCode.UpArrow){NavigateHistory(-1);e.Use();}
                else if(e.keyCode==KeyCode.DownArrow){NavigateHistory(1);e.Use();}
            }
            if(fieldStyle==null)
            {
                consoleFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},fontSize);
                fieldStyle=new GUIStyle(GUI.skin.textField){font=consoleFont,fontSize=fontSize};
                labelStyle=new GUIStyle(GUI.skin.label){font=consoleFont,fontSize=fontSize,wordWrap=true};
                labelStyle.normal.textColor=Color.white;
                titleStyle=new GUIStyle(labelStyle){fontStyle=FontStyle.Bold};
            }
            GUI.depth=-1000;
            var rect=new Rect(12,12,Mathf.Min(Screen.width-24,1100),Mathf.Min(Screen.height-24,520));
            Color oldColor=GUI.color;GUI.color=new Color(.035f,.045f,.055f,.97f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=oldColor;
            GUILayout.BeginArea(new Rect(rect.x+12,rect.y+10,rect.width-24,rect.height-20));
            GUILayout.Label("GM CONSOLE    · 开关 / Esc 关闭",titleStyle);
            GUILayout.Label("Enter 执行 | Tab 补全 | Shift+Tab 反向补全 | ↑↓ 历史 | help 帮助",labelStyle);
            scroll=GUILayout.BeginScrollView(scroll,GUILayout.ExpandHeight(true));
            foreach(var entry in log)
            {
                GUI.contentColor=entry.Error?new Color(1,.45f,.4f):entry.Input?new Color(.5f,.8f,1):Color.white;
                GUILayout.Label(entry.Text,labelStyle);
            }
            GUI.contentColor=Color.white;GUILayout.EndScrollView();
            var suggestions=Commands.Complete(input).Take(7).Select(item=>item.Trim());
            GUILayout.Label("补全："+string.Join("   ",suggestions),labelStyle,GUILayout.Height(fontSize*2+10));
            GUI.SetNextControlName("gm_command_input");
            string edited=GUILayout.TextField(input,256,fieldStyle,GUILayout.Height(fontSize+18));
            if(edited!=input){input=edited;ResetCompletion();historyIndex=history.Count;}
            if(focusRequested&&e.type==EventType.Repaint)
            {
                GUI.FocusControl("gm_command_input");var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
                editor.cursorIndex=editor.selectIndex=input.Length;focusRequested=false;
            }
            GUILayout.EndArea();
        }
    }
}

