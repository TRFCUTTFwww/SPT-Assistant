namespace LocalSpt5Esp;
internal static class ChatPaginationRules {
 // EFT expects fixed-size pages. SPT can return the entire dialogue list,
 // ignoring offset/limit; a full repeated page must also terminate loading.
 internal static bool Complete(int received,int pageSize,bool hasNewDialogue)=>
  pageSize>0&&received>=0&&(received!=pageSize||!hasNewDialogue);
}
