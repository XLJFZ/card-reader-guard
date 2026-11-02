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
  var temp=Path.Combine(Path.GetTempPath(),"CardReaderGuard-tests-"+Guid.NewGuid().ToString("N")+".xml");
  try {
   File.WriteAllText(temp,"<device><usbInstanceId>USB\\VID_1111&amp;PID_2222\\TEST_A</usbInstanceId><model>Test Reader</model><serial>A</serial></device>");
   var list=(IList)Call("LoadBindings",temp); var bt=list[0].GetType();
   Check(list.Count==1 && (int)bt.GetField("Minutes").GetValue(list[0])==5,"v0.1 config migrates with five-minute default");
   var first=list[0]; var second=bt.GetMethod("Copy").Invoke(first,null);
   bt.GetField("Usb").SetValue(second,@"USB\VID_1111&PID_2222\TEST_B"); bt.GetField("Serial").SetValue(second,"B"); bt.GetField("Name").SetValue(second,"Reader & B"); bt.GetField("Minutes").SetValue(second,12); bt.GetField("Enabled").SetValue(second,false); list.Add(second);
   Call("SaveBindings",list,temp); var restored=(IList)Call("LoadBindings",temp);
   Check(restored.Count==2,"multiple devices survive save and reload");
   Check((int)bt.GetField("Minutes").GetValue(restored[0])==5 && (int)bt.GetField("Minutes").GetValue(restored[1])==12,"each device preserves independent timeout");
   Check(!(bool)bt.GetField("Enabled").GetValue(restored[1]) && (string)bt.GetField("Name").GetValue(restored[1])=="Reader & B","pause and XML-escaped nickname round trip");
   var before=File.ReadAllText(temp); list.Add(first); bool rejected=false; try{Call("SaveBindings",list,temp);}catch(TargetInvocationException){rejected=true;} list.RemoveAt(list.Count-1);
   Check(rejected && File.ReadAllText(temp)==before,"duplicate binding rejected without overwriting saved config");
   bt.GetField("Minutes").SetValue(second,0); rejected=false; try{Call("SaveBindings",list,temp);}catch(TargetInvocationException){rejected=true;}
   Check(rejected && File.ReadAllText(temp)==before,"invalid timeout cannot overwrite saved config");
   restored.RemoveAt(0); Call("SaveBindings",restored,temp); Check(((IList)Call("LoadBindings",temp)).Count==1,"deleting one binding preserves the other");
   restored.Clear(); Call("SaveBindings",restored,temp); Check(((IList)Call("LoadBindings",temp)).Count==0,"empty binding list persists without implicit rebinding");
   var st=t.GetNestedType("IdleState",BindingFlags.NonPublic); var sa=Activator.CreateInstance(st,true); var sb=Activator.CreateInstance(st,true); st.GetField("Previous").SetValue(sb,"other baseline"); st.GetMethod("Reset").Invoke(sa,new object[]{100.0});
   Check((string)st.GetField("Previous").GetValue(sb)=="other baseline","one device reset leaves another device baseline intact");
  } finally { foreach(var path in new[]{temp,temp+".bak",temp+".tmp"}) if(File.Exists(path)) File.Delete(path); }
  if(args.Length>1) {
   var live=(IList)Call("LoadBindings",args[1]); var liveType=live[0].GetType(); string target=(string)liveType.GetField("Usb").GetValue(live[0]),model=(string)liveType.GetField("Model").GetValue(live[0]),serial=(string)liveType.GetField("Serial").GetValue(live[0]);
   Set("Target",target); Set("Model",model); Set("Serial",serial);
   Check(Call("Disk",new object[]{(uint)0})!=null,"bound device resolves without ejecting");
   Rejected("Serial","WRONG",serial); Rejected("Model","WRONG",model);
   var candidates=(IList)Call("Candidates"); Check(candidates.Count==1,"only external candidate offered on this machine");
  }
  Console.WriteLine(count+" tests passed; no eject API invoked.");
 }
}
