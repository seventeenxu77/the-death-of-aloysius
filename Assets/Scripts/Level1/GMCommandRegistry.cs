using System;
using System.Collections.Generic;
using System.Linq;

namespace Aloysius.Level1
{
    public struct GMCommandResult
    {
        public bool Success;
        public string Message;
        public static GMCommandResult Ok(string message) => new GMCommandResult {Success=true,Message=message};
        public static GMCommandResult Error(string message) => new GMCommandResult {Success=false,Message=message};
    }

    // Command registration is independent of the console UI and world implementation.
    public sealed class GMCommandRegistry
    {
        public sealed class Command
        {
            public string Name, Usage, Description;
            public Func<string[],GMCommandResult> Handler;
            public Func<string[],IEnumerable<string>> CompleteArguments;
        }
        readonly Dictionary<string,Command> commands=new Dictionary<string,Command>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<Command> Commands => commands.Values.OrderBy(item=>item.Name,StringComparer.OrdinalIgnoreCase);
        public void Register(string name,string usage,string description,Func<string[],GMCommandResult> handler,
            Func<string[],IEnumerable<string>> completeArguments=null)
        {
            if(string.IsNullOrWhiteSpace(name)||name.Any(char.IsWhiteSpace))throw new ArgumentException("Invalid command name.");
            if(handler==null)throw new ArgumentNullException(nameof(handler));
            commands.Add(name,new Command {Name=name,Usage=usage,Description=description,Handler=handler,CompleteArguments=completeArguments});
        }
        public bool TryGet(string name,out Command command) => commands.TryGetValue(name,out command);
        public GMCommandResult Execute(string line)
        {
            var words=Tokens(line);
            if(words.Length==0)return GMCommandResult.Error("请输入命令，help 查看帮助。");
            if(!TryGet(words[0],out var command))return GMCommandResult.Error("未知命令："+words[0]+"。输入 help 查看帮助。");
            try{return command.Handler(words.Skip(1).ToArray());}
            catch(Exception ex){return GMCommandResult.Error(ex.GetType().Name+": "+ex.Message);}
        }
        public List<string> Complete(string line)
        {
            line=(line??"").TrimStart();
            var words=Tokens(line);
            if(words.Length==0)return Commands.Select(item=>item.Name).ToList();
            if(words.Length==1&&!char.IsWhiteSpace(line[line.Length-1]))
                return Commands.Where(item=>item.Name.StartsWith(words[0],StringComparison.OrdinalIgnoreCase)).Select(item=>item.Name+" ").ToList();
            if(!TryGet(words[0],out var command)||command.CompleteArguments==null)return new List<string>();
            var args=words.Skip(1).ToList();
            if(char.IsWhiteSpace(line[line.Length-1]))args.Add("");
            return command.CompleteArguments(args.ToArray()).Select(value=>command.Name+" "+value).Distinct().ToList();
        }
        static string[] Tokens(string text) => (text??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
    }
}
