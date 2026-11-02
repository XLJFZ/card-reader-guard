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

static partial class ReaderGuard {
 static string Target, Model, Serial;
 static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
 static readonly string LogPath = Path.Combine(Root,"status.log");
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
  using(var tray=new NotifyIcon()) { tray.Icon=SystemIcons.Information; tray.Visible=true; tray.BalloonTipTitle=title; tray.BalloonTipText=message; tray.ShowBalloonTip(5000); var until=DateTime.UtcNow.AddSeconds(7); while(DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(100); } tray.Visible=false; }
 }
 [STAThread] static int Main(string[] args) {
  if(args.Contains("--stop")) { File.WriteAllText(Path.Combine(Root,"stop.flag"),""); return 0; }
  bool created; using(var mutex=new Mutex(true,@"Local\CardReaderGuard_152D0583",out created)) {
   if(!created) { if(args.Contains("--setup")) MessageBox.Show("程序正在后台监测。请先双击“停止.vbs”，等待约 10 秒后再重新绑定。","读卡器自动弹出"); return args.Contains("--probe")?3:0; }
   bool interactive=!args.Contains("--background") && !args.Contains("--probe");
   if(args.Contains("--setup") || (!File.Exists(Path.Combine(Root,"device.xml")) && interactive)) {
    Application.EnableVisualStyles(); if(!Setup()) return 1;
   }
  try {
   var config=new System.Xml.XmlDocument(); config.XmlResolver=null; config.Load(Path.Combine(Root,"device.xml"));
   Target=config.SelectSingleNode("/device/usbInstanceId").InnerText.Trim();
   Model=config.SelectSingleNode("/device/model").InnerText.Trim();
   Serial=config.SelectSingleNode("/device/serial").InnerText.Trim();
   if(!Target.StartsWith(@"USB\VID_",StringComparison.OrdinalIgnoreCase) || Target.Split('\\').Length!=3 || String.IsNullOrWhiteSpace(Model) || String.IsNullOrWhiteSpace(Serial) || Target.Contains("REPLACE")) throw new Exception("Missing exact device binding");
  } catch(Exception e) { Log("CONFIG ERROR: "+e.Message); return 2; }
   if(args.Contains("--probe")) { try { uint usb; var d=Disk(out usb); if(d==null) throw new Exception("Target not present"); Log("PROBE OK: "+d["Model"]+" disk="+d["Index"]+" counters="+Counters(Convert.ToUInt32(d["Index"]))); return 0; } catch(Exception e) { Log("PROBE FAILED: "+e.Message); return 2; } }
   if(File.Exists(Path.Combine(Root,"stop.flag"))) File.Delete(Path.Combine(Root,"stop.flag"));
   Log("START idle=300s; exact USB identity pinned; no force removal");
   string previous=null, diskKey=null,lastError=null; var clock=Stopwatch.StartNew(); double idleSince=clock.Elapsed.TotalSeconds, lastSample=0;
   while(!File.Exists(Path.Combine(Root,"stop.flag"))) {
    try {
     uint usb; var d=Disk(out usb); var now=clock.Elapsed.TotalSeconds;
     if(d==null) { previous=null; diskKey=null; idleSince=now; }
     else {
      var key=Convert.ToString(d["PNPDeviceID"])+":"+d["Index"]; var current=Counters(Convert.ToUInt32(d["Index"]));
      if(ResetIdle(key,diskKey,current,previous,now-lastSample)) idleSince=now;
      diskKey=key; previous=current;
      if(current!=null && now-idleSince>=300) {
       // Revalidate both identity and counters immediately before the only removal call.
       uint checkedUsb; var check=Disk(out checkedUsb);
       if(check==null || checkedUsb!=usb || Convert.ToString(check["PNPDeviceID"])+":"+check["Index"]!=key || Counters(Convert.ToUInt32(check["Index"]))!=current) { previous=null; idleSince=now; }
       else {
        uint veto; var reason=new StringBuilder(1024); var result=CM_Request_Device_EjectW(usb,out veto,reason,1024,0);
        Log("EJECT result="+result+" veto="+veto+" reason="+reason);
        if(result==0) Notice("读卡器已安全弹出","连续 5 分钟没有读写，已安全停止指定读卡器。再次使用可能需要重新插拔。");
        // A veto remains silent and starts a fresh full idle interval.
        previous=null; diskKey=null; idleSince=clock.Elapsed.TotalSeconds;
       }
      }
     }
     lastSample=clock.Elapsed.TotalSeconds; lastError=null;
    } catch(Exception e) { previous=null; diskKey=null; idleSince=clock.Elapsed.TotalSeconds; if(lastError!=e.Message) Log("SKIP: "+e.Message); lastError=e.Message; }
    Thread.Sleep(5000);
   }
   Log("STOP"); return 0;
  }
 }
}
