using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

static partial class ReaderGuard {
 static string Target, Model, Serial;
 static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
 static readonly string LogPath = Path.Combine(Root,"status.log");
 [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
 [DllImport("user32.dll")] static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
 [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)] static extern uint CM_Locate_DevNodeW(out uint n,string id,uint flags);
 [DllImport("cfgmgr32.dll")] static extern uint CM_Get_Parent(out uint p,uint n,uint flags);
 [DllImport("cfgmgr32.dll")] static extern uint CM_Get_Child(out uint p,uint n,uint flags);
 [DllImport("cfgmgr32.dll")] static extern uint CM_Get_Sibling(out uint p,uint n,uint flags);
 [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)] static extern uint CM_Get_Device_IDW(uint n,StringBuilder s,uint len,uint flags);
 [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)] static extern uint CM_Request_Device_EjectW(uint n,out uint veto,StringBuilder name,uint len,uint flags);
 static string Id(uint n) { var s=new StringBuilder(1024); if(CM_Get_Device_IDW(n,s,1024,0)!=0) throw new Exception("Device identity unavailable"); return s.ToString(); }
 static void Log(string s) { try { if(File.Exists(LogPath) && new FileInfo(LogPath).Length>262144) File.Delete(LogPath); File.AppendAllText(LogPath,DateTime.Now.ToString("s")+" "+s+Environment.NewLine); } catch {} }
 static ManagementObject[] Query(string q) { using(var search=new ManagementObjectSearcher(q)) using(var result=search.Get()) return result.Cast<ManagementObject>().ToArray(); }
 static bool ProtectedDisk(ManagementObject disk) {
  using(var parts=disk.GetRelated("Win32_DiskPartition")) foreach(ManagementObject part in parts) {
   using(var volumes=part.GetRelated("Win32_LogicalDisk")) foreach(ManagementObject vol in volumes) {
    var letter=Convert.ToString(vol["DeviceID"])+@"\";
    if(Environment.SystemDirectory.StartsWith(letter,StringComparison.OrdinalIgnoreCase) || Root.StartsWith(letter,StringComparison.OrdinalIgnoreCase)) return true;
   }
  }
  return false;
 }
 static ManagementObject Disk(out uint usb) {
  usb=0; uint node; if(CM_Locate_DevNodeW(out node,Target,0)!=0) return null;
  if(!String.Equals(Id(node),Target,StringComparison.OrdinalIgnoreCase)) throw new Exception("USB identity mismatch");
  uint child,sibling; if(CM_Get_Child(out child,node,0)!=0 || CM_Get_Sibling(out sibling,child,0)==0) throw new Exception("Unexpected USB child topology; skipped");
  var all=Query("SELECT * FROM Win32_DiskDrive"); ManagementObject match=null;
  foreach(var d in all) {
   uint dn,parent; var id=Convert.ToString(d["PNPDeviceID"]);
   if(CM_Locate_DevNodeW(out dn,id,0)!=0 || CM_Get_Parent(out parent,dn,0)!=0 || parent!=node) continue;
   if(match!=null) throw new Exception("Multiple disks on target; skipped");
   if(ProtectedDisk(d)) throw new Exception("System or application disk; skipped");
   if(!String.Equals(Convert.ToString(d["Model"]),Model,StringComparison.OrdinalIgnoreCase) || Convert.ToString(d["SerialNumber"]).Trim()!=Serial) throw new Exception("Disk identity mismatch");
   if(Id(child)!=Id(dn)) throw new Exception("Unexpected child device"); match=d;
  }
  usb=node; return match;
 }
 static string Counters(uint index) {
  var rows=Query("SELECT Name,DiskReadBytesPersec,DiskWriteBytesPersec,DiskReadsPersec,DiskWritesPersec,CurrentDiskQueueLength FROM Win32_PerfRawData_PerfDisk_PhysicalDisk");
  foreach(var p in rows) if(Convert.ToString(p["Name"]).Split(' ')[0]==index.ToString()) {
   if(Convert.ToUInt64(p["CurrentDiskQueueLength"])!=0) return null;
   return String.Join(":",new[]{"DiskReadBytesPersec","DiskWriteBytesPersec","DiskReadsPersec","DiskWritesPersec"}.Select(k=>Convert.ToString(p[k])).ToArray());
  }
  throw new Exception("No disk activity counters; ejection disabled");
 }
 static bool ResetIdle(string key,string oldKey,string current,string previous,double gap) {
  return key!=oldKey || current==null || previous==null || current!=previous || gap>30;
 }
 static void Notice(string title,string message) {
  using(var tray=new NotifyIcon()) { tray.Icon=AppIcon; tray.Visible=true; tray.BalloonTipTitle=title; tray.BalloonTipText=message; tray.ShowBalloonTip(5000); var until=DateTime.UtcNow.AddSeconds(7); while(DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(100); } tray.Visible=false; }
 }
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  if(args.Contains("--dpi-check")) { using(var form=new Form()) { var handle=form.Handle; var awareness=GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(handle)); Log("DPI CHECK awareness="+awareness+" windowDpi="+GetDpiForWindow(handle)+" target="+AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName); return awareness==2?0:2; } }
  if(args.Contains("--stop")) { File.WriteAllText(Path.Combine(Root,"stop.flag"),""); return 0; }
  bool created; using(var mutex=new Mutex(true,@"Local\CardReaderGuard_152D0583",out created)) {
   if(!created) { if(!args.Contains("--background") && !args.Contains("--probe")) File.WriteAllText(Path.Combine(Root,"manage.flag"),""); return args.Contains("--probe")?3:0; }
   bool interactive=!args.Contains("--background") && !args.Contains("--probe");
   List<Binding> bindings;
   try { bindings=LoadBindings(Path.Combine(Root,"device.xml")); }
   catch(Exception error) { Log("CONFIG ERROR: "+error.Message); if(interactive) MessageBox.Show("绑定配置无法读取。原文件已保留，请检查 device.xml 或从备份恢复。\n"+error.Message,"无法读取配置"); return 2; }
   if(interactive) {
    if(!Manage(ref bindings) && bindings.Count==0) return 1;
   }
   if(args.Contains("--probe")) { try { if(bindings.Count==0) throw new Exception("No bound devices"); foreach(var b in bindings.Where(b=>b.Enabled)) { Use(b); uint usb; var d=Disk(out usb); if(d==null || Counters(Convert.ToUInt32(d["Index"]))==null) throw new Exception("Target absent or busy"); Log("PROBE OK: "+b.Name); } return 0; } catch(Exception e) { Log("PROBE FAILED: "+e.Message); return 2; } }
   if(!File.Exists(Path.Combine(Root,"device.xml"))) return 2;
   if(File.Exists(Path.Combine(Root,"stop.flag"))) File.Delete(Path.Combine(Root,"stop.flag"));
   Log("START v0.2.3 bindings="+bindings.Count+"; exact USB identity pinned; no force removal");
   var states=new Dictionary<string,IdleState>(StringComparer.OrdinalIgnoreCase); var clock=Stopwatch.StartNew();
   while(!File.Exists(Path.Combine(Root,"stop.flag"))) {
    var request=Path.Combine(Root,"manage.flag");
    if(File.Exists(request)) { File.Delete(request); Manage(ref bindings); states.Clear(); }
    foreach(var b in bindings.Where(b=>b.Enabled)) {
     IdleState state; if(!states.TryGetValue(b.Usb,out state)) { state=new IdleState(); states.Add(b.Usb,state); }
     Use(b);
    try {
     uint usb; var d=Disk(out usb); var now=clock.Elapsed.TotalSeconds;
     if(d==null) { state.Reset(now); }
     else {
      var key=Convert.ToString(d["PNPDeviceID"])+":"+d["Index"]; var current=Counters(Convert.ToUInt32(d["Index"]));
      if(ResetIdle(key,state.Key,current,state.Previous,now-state.LastSample)) state.IdleSince=now;
      state.Key=key; state.Previous=current;
      if(current!=null && now-state.IdleSince>=b.Minutes*60) {
       // Revalidate both identity and counters immediately before the only removal call.
       uint checkedUsb; var check=Disk(out checkedUsb);
       if(check==null || checkedUsb!=usb || Convert.ToString(check["PNPDeviceID"])+":"+check["Index"]!=key || Counters(Convert.ToUInt32(check["Index"]))!=current) { state.Reset(now); }
       else {
        uint veto; var reason=new StringBuilder(1024); var result=CM_Request_Device_EjectW(usb,out veto,reason,1024,0);
        Log("EJECT device="+b.Name+" result="+result+" veto="+veto+" reason="+reason);
        if(result==0) Notice("读卡器已安全弹出",b.Name+" 已连续 "+b.Minutes+" 分钟没有读写，现已安全停止。再次使用可能需要重新插拔。");
        // A veto remains silent and starts a fresh full idle interval.
        state.Reset(clock.Elapsed.TotalSeconds);
       }
      }
     }
     state.LastSample=clock.Elapsed.TotalSeconds; state.LastError=null;
    } catch(Exception e) { state.Reset(clock.Elapsed.TotalSeconds); if(state.LastError!=e.Message) Log("SKIP device="+b.Name+": "+e.Message); state.LastError=e.Message; }
    }
    Thread.Sleep(5000);
   }
   Log("STOP"); return 0;
  }
 }
 sealed class IdleState {
  public string Previous,Key,LastError; public double IdleSince,LastSample;
  public void Reset(double now) { Previous=null; Key=null; IdleSince=now; }
 }
}
