using System;
using System.Collections;
using System.IO;
using System.Reflection;
class SmokeTests {
 static Type t; static int count;
 static object Call(string n,params object[] a) { try { return t.GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,a); } catch(TargetInvocationException e) { Console.WriteLine("DETAIL "+n+": "+e.InnerException.GetType().FullName+" "+e.InnerException.Message); throw; } }
 static void Set(string n,string v) { t.GetField(n,BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,v); }
 static void Check(bool b,string n) { if(!b) throw new Exception(n); count++; Console.WriteLine("PASS "+n); }
 static void Rejected(string field,string bad,string correct) { Set(field,bad); bool rejected=false; try { Call("Disk",new object[]{(uint)0}); } catch(TargetInvocationException) { rejected=true; } finally {Set(field,correct);} Check(rejected,field+" mismatch refuses target"); }
 static void Main(string[] args) {
  t=Assembly.LoadFile(Path.GetFullPath(args[0])).GetType("ReaderGuard");
  Check(!(bool)Call("ResetIdle","a","a","10:20","10:20",5.0),"unchanged counters preserve idle");
  Check((bool)Call("ResetIdle","a","a","11:20","10:20",5.0),"read activity resets idle");
  Check((bool)Call("ResetIdle","a","a","10:21","10:20",5.0),"write activity resets idle");
  Check((bool)Call("ResetIdle","b","a","10:20","10:20",5.0),"reconnected disk resets idle");
  Check((bool)Call("ResetIdle","a","a",null,"10:20",5.0),"pending IO resets idle");
  Check((bool)Call("ResetIdle","a","a","10:20","10:20",60.0),"resume or monitoring gap resets idle");
  Check((bool)Call("ResetIdle","a","a","10:20",null,5.0),"missing baseline resets idle");
  Set("Target",@"USB\VID_0000&PID_0000\NOT_PRESENT"); Check(Call("Disk",new object[]{(uint)0})==null,"unbound USB device is ignored");
  if(args.Length>1) {
   var cfg=new System.Xml.XmlDocument(); cfg.Load(args[1]); string target=cfg.SelectSingleNode("/device/usbInstanceId").InnerText,model=cfg.SelectSingleNode("/device/model").InnerText,serial=cfg.SelectSingleNode("/device/serial").InnerText;
   Set("Target",target); Set("Model",model); Set("Serial",serial);
   Check(Call("Disk",new object[]{(uint)0})!=null,"bound device resolves without ejecting");
   Rejected("Serial","WRONG",serial); Rejected("Model","WRONG",model);
   var candidates=(IList)Call("Candidates"); Check(candidates.Count==1,"only external candidate offered on this machine");
  }
  Console.WriteLine(count+" tests passed; no eject API invoked.");
 }
}
