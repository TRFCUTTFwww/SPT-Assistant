using System;
namespace LocalSpt5Esp;

// Keep the candidate set while cycling, but invalidate it on edits or caret moves.
internal sealed class ChatCompletion {
 string text="",suffix="";int cursor=-1;
 internal string[] Candidates { get; private set; }=Array.Empty<string>();
 internal int Selected { get; private set; }=-1;
 internal int Start { get; private set; }
 internal void Reset(){text="";cursor=-1;suffix="";Candidates=Array.Empty<string>();Selected=-1;Start=0;}
 internal void Refresh(string value,int position,Func<string,string[]> complete){
  position=Math.Clamp(position,0,value.Length);
  if(value==text&&position==cursor)return;
  text=value;cursor=position;Selected=-1;
  string prefix=value[..position];Start=ChatCommandSyntax.CompletionStart(prefix);
  suffix=value[ChatCommandSyntax.CompletionEnd(value,position)..];
  Candidates=ChatCommandSyntax.IsCommand(value)?complete(prefix):Array.Empty<string>();
 }
 internal (string Text,int Cursor) Cycle(string value,int position,bool reverse,Func<string,string[]> complete){
  Refresh(value,position,complete);
  if(Candidates.Length==0)return(value,Math.Clamp(position,0,value.Length));
  int next=Selected<0?(reverse?Candidates.Length-1:0):(Selected+(reverse?-1:1)+Candidates.Length)%Candidates.Length;
  string head=Candidates[next];
  if(head.Length+suffix.Length>2048)return(value,position);
  Selected=next;text=head+suffix;cursor=head.Length;
  return(text,cursor);
 }
}
