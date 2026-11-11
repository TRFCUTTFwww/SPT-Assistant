using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace LocalSpt5Esp;
// Windows DPAPI, current Windows account, no external runtime dependency.
internal static class ChatIdentityProtection {
 [StructLayout(LayoutKind.Sequential)]struct Blob{public int Length;public IntPtr Data;}
 [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("crypt32.dll",SetLastError=true)]static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr value);
 internal static byte[] Transform(byte[] data,bool protect){var input=new Blob{Length=data.Length,Data=Marshal.AllocHGlobal(data.Length)};Blob output=default;try{Marshal.Copy(data,0,input.Data,data.Length);bool ok=protect?CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;}finally{for(int i=0;i<input.Length;i++)Marshal.WriteByte(input.Data,i,0);Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero){for(int i=0;i<output.Length;i++)Marshal.WriteByte(output.Data,i,0);LocalFree(output.Data);}}}
}
