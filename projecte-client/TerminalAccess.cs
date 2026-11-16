namespace ProjectE.Client;
// Access to a loaded snapshot is separate from draining an unconfirmed save.
internal sealed class TerminalAccess {
 internal bool Requested{get;private set;}
 internal bool Visible{get;private set;}
 internal bool Closing{get;private set;}
 internal void Request(){Requested=true;Visible=false;Closing=false;}
 internal bool CanAttach(bool local,bool held,bool ready)=>Requested&&!Closing&&local&&held&&ready;
 internal void Attach(){if(!Requested||Closing)throw new InvalidOperationException("Terminal was not requested");Requested=false;Visible=true;}
 internal bool CanResolve(bool local,bool held)=>Visible&&!Closing&&local&&held;
 internal void Disconnect(){Requested=Visible=false;Closing=true;}
 internal void Clear(){Requested=Visible=Closing=false;}
}
