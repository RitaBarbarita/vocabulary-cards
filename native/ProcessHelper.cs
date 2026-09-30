using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
internal static class ProcessHelper
{
 internal static Dictionary<string,string> CleanEnvironment(){var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(DictionaryEntry entry in Environment.GetEnvironmentVariables())values[Convert.ToString(entry.Key)]=Convert.ToString(entry.Value);return values;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Startup{public int cb;public string reserved,desktop,title;public int x,y,xsize,ysize,xcount,ycount,fill,flags;public short show,reservedSize;public IntPtr reservedPointer,input,output,error;}
 [StructLayout(LayoutKind.Sequential)]struct ProcessInfo{public IntPtr process,thread;public int processId,threadId;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string app,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref Startup startup,out ProcessInfo info);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 internal static void Start(ProcessStartInfo process,Dictionary<string,string> environment)
 {
  string block=String.Join("\0",environment.OrderBy(e=>e.Key,StringComparer.OrdinalIgnoreCase).Select(e=>e.Key+"="+e.Value))+"\0\0";IntPtr pointer=Marshal.StringToHGlobalUni(block);try{var startup=new Startup{cb=Marshal.SizeOf(typeof(Startup))};ProcessInfo info;if(!CreateProcess(process.FileName,new StringBuilder("\""+process.FileName+"\" "+process.Arguments),IntPtr.Zero,IntPtr.Zero,false,0x08000000|0x400,pointer,process.WorkingDirectory,ref startup,out info))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());CloseHandle(info.thread);CloseHandle(info.process);}finally{Marshal.FreeHGlobal(pointer);}
 }
}
