using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;
using AssistantChat;
namespace LocalSpt5Esp;
internal static partial class RaidChat {
 sealed record Line(string Text,float Time,Color Color);
 static readonly List<Line> lines=new();static readonly List<string> history=new();static readonly ConcurrentQueue<Action> completions=new();static readonly MenuInputGate gate=new();
 static AssistantChatClient? client;static ConfigEntry<KeyCode>? hotkey,commandKey;static ConfigEntry<bool>? enabled;static string session="",input="",draft="",reply="";static int frame=-1,openedFrame,index,caret=-1;static bool focus,oldVisible;static CursorLockMode oldLock;static GUIStyle? style,field;static Vector2 scroll;static float retryAt;
 internal static bool Open=>gate.Open;internal static bool InputBlocked=>gate.Blocked;
 internal static void Initialize(){commandKey=FeatureConfig.Bind("64. 聊天与命令","Command key","打开命令输入热键",KeyCode.Slash,"打开聊天并填入 /；可在热键页修改或清除。");hotkey=FeatureConfig.Bind("64. 聊天与命令","Chat key","聊天按键",KeyCode.T,"打开聊天时拦截游戏输入；可修改按键以避开原生 T 战术设备快捷键。");enabled=FeatureConfig.Bind("64. 聊天与命令","Encrypted chat","连接加密聊天",true,"需要服务端聊天组件和双方核验指纹。关闭后仍可使用本地命令。");}
 static string Session(){try{return SPTushonka.Common.Http.RequestHandler.SessionId??"";}catch{return "";}}
 internal static void CaptureInput(){if(frame==Time.frameCount)return;frame=Time.frameCount;gate.Tick(frame,Input.GetMouseButton(0)||Input.GetMouseButton(1));if(hotkey==null||!Application.isFocused)return;if(!Open){if(RaidClock.Active&&!Cursor.visible&&!InterfaceMenu.Open&&!WorldEditor.ModalOpen&&!FloatingStatus.Capturing&&(Input.GetKeyDown(hotkey.Value)||Input.GetKeyDown(commandKey!.Value))){SetOpen(true);input=Input.GetKeyDown(commandKey!.Value)?"/":"";focus=true;caret=input.Length;}}else if(Input.GetKeyDown(KeyCode.Escape)){if(ChatItemEditor.Active)ChatItemEditor.Reset();else SetOpen(false);}}
 static void SetOpen(bool value){if(value==Open)return;if(value){WorldEditor.Suspend();oldLock=Cursor.lockState;oldVisible=Cursor.visible;gate.SetOpen(true,Time.frameCount);openedFrame=Time.frameCount;historyIndexReset();try{Comfort.Common.Singleton<EFT.GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<EFT.Player.FirearmController>()?.SetTriggerPressed(false);}catch{}}else{ChatItemEditor.Reset();gate.SetOpen(false,Time.frameCount);Cursor.lockState=oldLock;Cursor.visible=oldVisible;input="";completion.Reset();editingChip=null;focus=false;caret=-1;Input.imeCompositionMode=IMECompositionMode.Auto;}KeepCursor();}
 static void historyIndexReset(){index=history.Count;draft="";}
 internal static void KeepCursor(){if(Open){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
 internal static void Tick(){CaptureInput();RaidCommands.Tick();while(completions.TryDequeue(out var action))action();string key=Session();if(key!=session){client?.Dispose();client=null;session=key;reply="";lines.Clear();history.Clear();retryAt=0;RaidCommands.Reset();}if(enabled?.Value!=true){client?.Dispose();client=null;return;}if(client==null&&key.Length>0&&Time.unscaledTime>=retryAt){retryAt=Time.unscaledTime+60;try{client=new(key);}catch{Output("无法读取聊天身份。请检查 Windows 用户和 chat 目录权限；没有自动重置密钥。",true);}}if(client==null)return;while(client.TryNotice(out var notice))Output(notice);while(client.TryReceive(out var packet)){var p=client.Peers.FirstOrDefault(p=>p.Profile==packet.Peer);string name=p?.Name??packet.Peer;var msg=packet.Message;if(msg.Kind is not ("chat" or "whisper" or "me"))continue;if(msg.Text.Length>2000)continue;Output(msg.Kind=="me"?"* "+name+" "+Clean(msg.Text):msg.Kind=="whisper"?"[私信] "+name+": "+Clean(msg.Text):name+": "+Clean(msg.Text));if(msg.Kind=="whisper")reply=packet.Peer;}}
 static string Clean(string text)=>new(text.Select(c=>char.IsControl(c)&&c!='\n'?' ':c).ToArray());
 internal static void Output(string value,bool error=false){foreach(string line in Clean(value).Split('\n')){lines.Add(new(line,Time.unscaledTime,error?new Color(1,.55f,.5f):Color.white));if(lines.Count>200)lines.RemoveAt(0);}scroll.y=float.MaxValue;}
 static void Submit(){
  string value=input.Trim();if(value.Length==0)return;
  if(history.Count==0||history[^1]!=value)history.Add(value);if(history.Count>100)history.RemoveAt(0);
  completion.Reset();
  try{if(ChatCommandSyntax.IsCommand(value))Command(value[1..]);else Send(value.StartsWith("//",StringComparison.Ordinal)?value[1..]:value,null,"chat");ReplaceInput("",0);}
  catch(Exception e){Output(e.Message,true);ReplaceInput(input,input.Length);}
  historyIndexReset();
 }
 static void Send(string text,string? to,string kind){if(client==null)throw new ArgumentException("聊天未连接。安装服务端组件后使用 /chat reconnect。");var current=client;_ = SendAsync(current,text,to,kind);}
 static async Task SendAsync(AssistantChatClient current,string text,string? to,string kind){try{await current.Send(text,to,kind);completions.Enqueue(()=>Output(to==null?"<我> "+text:"[私信 → "+to+"] "+text));}catch(Exception e){completions.Enqueue(()=>Output("发送结果未确认："+e.Message+"。不会自动重发。",true));}}
 static void Command(string value){var words=ChatCommandSyntax.Tokens(value);if(words.Length==0){Output(RaidCommands.Help());return;}string name=words[0].ToLowerInvariant();var args=words.Skip(1).ToArray();switch(name){
  case "help":Output(RaidCommands.Help(args.FirstOrDefault()));break;
  case "identity":if(client==null)throw new ArgumentException("聊天未连接。");if(args.Length==3&&args[0]=="verify")Output(client.Verify(args[1],args[2]));else if(args.Length==0){Output("我的档案 ID："+session+"\n我的指纹："+client.Fingerprint);foreach(var p in client.Peers)Output(client.Describe(p));}else throw new ArgumentException("/identity verify <档案 ID 或唯一名称> <通过其他可信渠道核对的指纹>");break;
  case "list":Output("当前在线：");if(client!=null)foreach(var p in client.Peers)Output(p.Name+"  "+p.Profile);if(RaidClock.Active)RaidCommands.Execute("list",args);break;
  case "msg":case "tell":case "w":if(args.Length<2)throw new ArgumentException("/msg <玩家> <消息>");Send(string.Join(" ",args.Skip(1)),args[0],"whisper");break;
  case "reply":case "r":if(reply.Length==0||args.Length==0)throw new ArgumentException("没有最近私信，或消息为空。");Send(string.Join(" ",args),reply,"whisper");break;
  case "me":Send(string.Join(" ",args),null,"me");break;
  case "teammsg":case "tm":throw new ArgumentException("当前版本没有服务端验证的队伍路由，不能把全服频道冒充队伍频道。");
  case "say":throw new ArgumentException("当前版本没有经过验证的公告签名，普通聊天不能冒充服务器公告。");
  case "clearhistory":lines.Clear();history.Clear();reply="";break;
  case "chat":if(args.Length==1&&args[0]=="reconnect"){client?.Dispose();client=null;retryAt=0;Output("正在使用新会话重新连接。");}else throw new ArgumentException("/chat reconnect");break;
  case "permission":if(args.Length>1||args.Length==1&&args[0]!="list")throw new ArgumentException("/permission [list] · 当前仅支持只读查询。");Output(RaidCommands.CommandPermissions+"\n聊天中继权限："+string.Join(", ",client?.Permissions??Array.Empty<string>()));break;
  default:RaidCommands.Execute(name,args);break;
 }}
 static TextEditor Editor()=>GUIUtility.GetStateObject(Il2CppInterop.Runtime.Il2CppType.Of<TextEditor>(),GUIUtility.keyboardControl).Cast<TextEditor>();
 internal static void Reset(){ChatItemEditor.Reset();client?.Dispose();client=null;SetOpen(false);gate.Reset();session="";frame=-1;lines.Clear();history.Clear();while(completions.TryDequeue(out _)){}RaidCommands.Reset();}
}
