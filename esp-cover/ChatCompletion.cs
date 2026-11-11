using System;
namespace LocalSpt5Esp;
internal sealed record ChatSuggestion(string Text,string Label,string Detail="");

// Keep the candidate set while cycling, but invalidate it on edits or caret moves.
internal sealed class ChatCompletion {
 string text="",suffix="";int cursor=-1;
 internal ChatSuggestion[] Candidates { get; private set; }=Array.Empty<ChatSuggestion>();
 internal int Selected { get; private set; }=-1;
 internal bool Pending { get; private set; }
 internal int Start { get; private set; }
 internal void Reset(){text="";cursor=-1;suffix="";Candidates=Array.Empty<ChatSuggestion>();Selected=-1;Pending=false;Start=0;}
 internal void Refresh(string value,int position,Func<string,ChatSuggestion[]> complete){
  position=Math.Clamp(position,0,value.Length);
  if(value==text&&position==cursor)return;
  text=value;cursor=position;Selected=-1;Pending=false;
  string prefix=value[..position];Start=ChatCommandSyntax.CompletionStart(prefix);
  suffix=value[ChatCommandSyntax.CompletionEnd(value,position)..];
  Candidates=ChatCommandSyntax.IsCommand(value)?complete(prefix):Array.Empty<ChatSuggestion>();
 }
 internal bool MoveSelection(int direction){if(Candidates.Length==0)return false;Selected=Selected<0?(direction<0?Candidates.Length-1:0):(Selected+direction+Candidates.Length)%Candidates.Length;Pending=true;return true;}
 internal void Select(int index){if(index>=0&&index<Candidates.Length){Selected=index;Pending=true;}}
 internal (string Text,int Cursor) Accept(string value,int position){
  if(Selected<0||Selected>=Candidates.Length)return(value,position);
  string head=Candidates[Selected].Text;if(head.Length+suffix.Length>2048)return(value,position);
  text=head+suffix;cursor=head.Length;Pending=false;return(text,cursor);
 }
 internal (string Text,int Cursor) Cycle(string value,int position,bool reverse,Func<string,ChatSuggestion[]> complete){
  Refresh(value,position,complete);
  if(Candidates.Length==0)return(value,Math.Clamp(position,0,value.Length));
  if(!Pending)MoveSelection(reverse?-1:1);
  return Accept(value,position);
 }
}
