using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
namespace LocalSpt5Esp;

// Native Win32 folder and file pickers executed on a dedicated STA thread so
// Unity's MTA main thread is never blocked and the working directory stays intact.
internal static class WindowsDialogs {
 internal static Task<string?> PickFolder(string title,string initialDirectory=""){
  if(!OperatingSystem.IsWindows())return Task.FromResult<string?>(null);
  IntPtr owner=GetActiveWindow();
  return RunSta(()=>PickFolderCore(owner,title,initialDirectory));
 }
 internal static Task<string?> PickOpenFile(string title,string filter="JSON 文件 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0\0",string initialDirectory=""){
  if(!OperatingSystem.IsWindows())return Task.FromResult<string?>(null);
  if(!filter.EndsWith("\0\0",StringComparison.Ordinal))filter=filter.TrimEnd('\0')+"\0\0";
  IntPtr owner=GetActiveWindow();
  return RunSta(()=>OpenFileCore(owner,title,filter,initialDirectory));
 }
 internal static Task<string?> PickSaveFile(string title,string defaultExtension,string defaultName,string filter="JSON 文件 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0\0",string initialDirectory=""){
  if(!OperatingSystem.IsWindows())return Task.FromResult<string?>(null);
  if(!filter.EndsWith("\0\0",StringComparison.Ordinal))filter=filter.TrimEnd('\0')+"\0\0";
  IntPtr owner=GetActiveWindow();
  return RunSta(()=>SaveFileCore(owner,title,defaultExtension,defaultName,filter,initialDirectory));
 }
 [SupportedOSPlatform("windows")]
 static Task<string?> RunSta(Func<string?> work){
  var completion=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
  var thread=new Thread(()=>{
   try{completion.TrySetResult(work());}
   catch(Exception error){completion.TrySetException(error);}
  }){IsBackground=true,Name="SPTAssistant.WinDialog"};
  thread.SetApartmentState(ApartmentState.STA);
  thread.Start();
  return completion.Task;
 }
 [SupportedOSPlatform("windows")]
 static string? PickFolderCore(IntPtr owner,string title,string initialDirectory){
  var dialog=(IFileOpenDialog)new FileOpenDialogClass();
  try{
   dialog.GetOptions(out uint options);
   dialog.SetOptions(options|FOS_PICKFOLDERS|FOS_FORCEFILESYSTEM|FOS_PATHMUSTEXIST|FOS_NOCHANGEDIR);
   if(!string.IsNullOrWhiteSpace(title))dialog.SetTitle(title);
   if(!string.IsNullOrWhiteSpace(initialDirectory)&&Directory.Exists(initialDirectory)){
    if(SHCreateItemFromParsingName(initialDirectory,IntPtr.Zero,ref IID_IShellItem,out var folder)==0&&folder!=null){
     try{dialog.SetFolder(folder);}finally{Marshal.FinalReleaseComObject(folder);}
    }
   }
   int hr=dialog.Show(owner);
   if(hr==ERROR_CANCELLED)return null;
   if(hr<0)Marshal.ThrowExceptionForHR(hr);
   dialog.GetResult(out var item);
   if(item==null)return null;
   try{
    item.GetDisplayName(SIGDN_FILESYSPATH,out IntPtr psz);
    if(psz==IntPtr.Zero)return null;
    try{return Marshal.PtrToStringUni(psz);}finally{Marshal.FreeCoTaskMem(psz);}
   }finally{Marshal.FinalReleaseComObject(item);}
  }finally{Marshal.FinalReleaseComObject(dialog);}
 }
 static string? OpenFileCore(IntPtr owner,string title,string filter,string initialDirectory){
  var buffer=new char[4096];
  var handle=GCHandle.Alloc(buffer,GCHandleType.Pinned);
  try{
   var ofn=new OpenFileName{
    lStructSize=Marshal.SizeOf<OpenFileName>(),
    hwndOwner=owner,
    lpstrFilter=EnsureDoubleNull(filter),
    lpstrFile=handle.AddrOfPinnedObject(),
    nMaxFile=buffer.Length,
    lpstrInitialDir=Directory.Exists(initialDirectory)?initialDirectory:null,
    lpstrTitle=title,
    Flags=OFN_EXPLORER|OFN_FILEMUSTEXIST|OFN_PATHMUSTEXIST|OFN_NOCHANGEDIR
   };
   return GetOpenFileNameW(ref ofn)?ReadNullTerminated(buffer):null;
  }finally{handle.Free();}
 }
 static string? SaveFileCore(IntPtr owner,string title,string defaultExtension,string defaultName,string filter,string initialDirectory){
  var buffer=new char[4096];
  if(!string.IsNullOrEmpty(defaultName))defaultName.AsSpan(0,Math.Min(defaultName.Length,buffer.Length-1)).CopyTo(buffer);
  var handle=GCHandle.Alloc(buffer,GCHandleType.Pinned);
  try{
   var ofn=new OpenFileName{
    lStructSize=Marshal.SizeOf<OpenFileName>(),
    hwndOwner=owner,
    lpstrFilter=EnsureDoubleNull(filter),
    lpstrFile=handle.AddrOfPinnedObject(),
    nMaxFile=buffer.Length,
    lpstrInitialDir=Directory.Exists(initialDirectory)?initialDirectory:null,
    lpstrTitle=title,
    lpstrDefExt=defaultExtension,
    Flags=OFN_EXPLORER|OFN_PATHMUSTEXIST|OFN_OVERWRITEPROMPT|OFN_NOCHANGEDIR
   };
   return GetSaveFileNameW(ref ofn)?ReadNullTerminated(buffer):null;
  }finally{handle.Free();}
 }
 static string EnsureDoubleNull(string filter)=>filter.EndsWith("\0\0",StringComparison.Ordinal)?filter:filter.TrimEnd('\0')+"\0\0";
 static string? ReadNullTerminated(char[] buffer){int end=Array.IndexOf(buffer,'\0');string value=end>=0?new string(buffer,0,end):new string(buffer);return string.IsNullOrWhiteSpace(value)?null:value;}
 const int ERROR_CANCELLED=unchecked((int)0x800704C7);
 const uint FOS_OVERWRITEPROMPT=0x00000002,FOS_NOCHANGEDIR=0x00000008,FOS_PICKFOLDERS=0x00000020,FOS_FORCEFILESYSTEM=0x00000040,FOS_PATHMUSTEXIST=0x00000800;
 const uint SIGDN_FILESYSPATH=0x80058000;
 const int OFN_OVERWRITEPROMPT=0x00000002,OFN_NOCHANGEDIR=0x00000008,OFN_PATHMUSTEXIST=0x00000800,OFN_FILEMUSTEXIST=0x00001000,OFN_EXPLORER=0x00080000;
 static Guid IID_IShellItem=new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
 [DllImport("user32.dll")]static extern IntPtr GetActiveWindow();
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)]string pszPath,IntPtr pbc,ref Guid riid,[MarshalAs(UnmanagedType.Interface)]out IShellItem? ppv);
 [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetOpenFileNameW(ref OpenFileName lpofn);
 [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetSaveFileNameW(ref OpenFileName lpofn);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 struct OpenFileName {
  public int lStructSize;public IntPtr hwndOwner;public IntPtr hInstance;
  [MarshalAs(UnmanagedType.LPWStr)]public string? lpstrFilter;
  public IntPtr lpstrCustomFilter;public int nMaxCustFilter;public int nFilterIndex;
  public IntPtr lpstrFile;public int nMaxFile;public IntPtr lpstrFileTitle;public int nMaxFileTitle;
  [MarshalAs(UnmanagedType.LPWStr)]public string? lpstrInitialDir;
  [MarshalAs(UnmanagedType.LPWStr)]public string? lpstrTitle;
  public int Flags;public short nFileOffset;public short nFileExtension;
  [MarshalAs(UnmanagedType.LPWStr)]public string? lpstrDefExt;
  public IntPtr lCustData;public IntPtr lpfnHook;
  [MarshalAs(UnmanagedType.LPWStr)]public string? lpTemplateName;
  public IntPtr pvReserved;public int dwReserved;public int FlagsEx;
 }
 [ComImport,Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]class FileOpenDialogClass {}
 [ComImport,Guid("d57c7288-d4ad-4768-be02-9d969532d960"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IFileOpenDialog {
  [PreserveSig]int Show(IntPtr parent);
  void SetFileTypes(uint cFileTypes,IntPtr rgFilterSpec);void SetFileTypeIndex(uint iFileType);void GetFileTypeIndex(out uint piFileType);
  void Advise(IntPtr pfde,out uint pdwCookie);void Unadvise(uint dwCookie);
  void SetOptions(uint fos);void GetOptions(out uint pfos);
  void SetDefaultFolder([MarshalAs(UnmanagedType.Interface)]IShellItem psi);void SetFolder([MarshalAs(UnmanagedType.Interface)]IShellItem psi);void GetFolder([MarshalAs(UnmanagedType.Interface)]out IShellItem ppsi);
  void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)]out IShellItem ppsi);
  void SetFileName([MarshalAs(UnmanagedType.LPWStr)]string pszName);void GetFileName([MarshalAs(UnmanagedType.LPWStr)]out string pszName);
  void SetTitle([MarshalAs(UnmanagedType.LPWStr)]string pszTitle);void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)]string pszText);void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)]string pszLabel);
  void GetResult([MarshalAs(UnmanagedType.Interface)]out IShellItem ppsi);
  void AddPlace([MarshalAs(UnmanagedType.Interface)]IShellItem psi,int fdap);void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)]string pszDefaultExtension);void Close(int hr);
  void SetClientGuid(ref Guid guid);void ClearClientData();void SetFilter(IntPtr pFilter);
  void GetResults(out IntPtr ppenum);void GetSelectedItems(out IntPtr ppsai);
 }
 [ComImport,Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IShellItem {
  void BindToHandler(IntPtr pbc,ref Guid bhid,ref Guid riid,out IntPtr ppv);
  void GetParent([MarshalAs(UnmanagedType.Interface)]out IShellItem ppsi);
  void GetDisplayName(uint sigdnName,out IntPtr ppszName);
  void GetAttributes(uint sfgaoMask,out uint psfgaoAttribs);
  void Compare([MarshalAs(UnmanagedType.Interface)]IShellItem psi,uint hint,out int piOrder);
 }
}
