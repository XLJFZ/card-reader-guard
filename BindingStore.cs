using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;

static partial class ReaderGuard {
 sealed class Binding {
  public string Usb,Model,Serial,Name; public int Minutes=5; public bool Enabled=true;
  public Binding Copy() { return (Binding)MemberwiseClone(); }
 }
 static void Use(Binding b) { Target=b.Usb; Model=b.Model; Serial=b.Serial; }
 static void Validate(Binding b) {
  if(String.IsNullOrWhiteSpace(b.Usb) || !b.Usb.StartsWith(@"USB\VID_",StringComparison.OrdinalIgnoreCase) || b.Usb.Split('\\').Length!=3 || b.Usb.Contains("REPLACE") || String.IsNullOrWhiteSpace(b.Model) || String.IsNullOrWhiteSpace(b.Serial)) throw new Exception("缺少完整设备身份");
  if(b.Minutes<1 || b.Minutes>120) throw new Exception("空闲时间应为 1 至 120 分钟");
  if(String.IsNullOrWhiteSpace(b.Name)) b.Name=b.Model;
 }
 static List<Binding> LoadBindings(string path) {
  var result=new List<Binding>(); if(!File.Exists(path)) return result;
  if(new FileInfo(path).Length>1048576) throw new Exception("配置文件过大");
  var xml=new XmlDocument(); xml.XmlResolver=null;
  using(var reader=XmlReader.Create(path,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null })) xml.Load(reader);
  var nodes=xml.SelectNodes("/device | /devices/device");
  if(xml.DocumentElement==null || (xml.DocumentElement.Name!="device" && xml.DocumentElement.Name!="devices")) throw new Exception("不支持的配置格式");
  foreach(XmlNode node in nodes) {
   Func<string,string> text=k=>node.SelectSingleNode(k)==null?null:node.SelectSingleNode(k).InnerText.Trim();
   var b=new Binding { Usb=text("usbInstanceId"), Model=text("model"), Serial=text("serial"), Name=text("name") };
   if(text("idleMinutes")!=null) b.Minutes=Int32.Parse(text("idleMinutes"));
   if(text("enabled")!=null) b.Enabled=Boolean.Parse(text("enabled"));
   Validate(b); if(result.Any(x=>String.Equals(x.Usb,b.Usb,StringComparison.OrdinalIgnoreCase))) throw new Exception("重复的 USB 设备绑定"); result.Add(b);
  }
  return result;
 }
 static void SaveBindings(List<Binding> bindings,string path) {
  var xml=new XmlDocument(); var root=xml.CreateElement("devices"); root.SetAttribute("version","2"); xml.AppendChild(root);
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var b in bindings) {
   Validate(b); if(!seen.Add(b.Usb)) throw new Exception("同一 USB 设备不能重复添加");
   var device=xml.CreateElement("device"); root.AppendChild(device);
   foreach(var pair in new[]{new[]{"usbInstanceId",b.Usb},new[]{"model",b.Model},new[]{"serial",b.Serial},new[]{"name",b.Name},new[]{"idleMinutes",b.Minutes.ToString()},new[]{"enabled",b.Enabled.ToString().ToLowerInvariant()}}) { var n=xml.CreateElement(pair[0]); n.InnerText=pair[1]; device.AppendChild(n); }
  }
  var temp=path+".tmp"; xml.Save(temp);
  if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
 }
}
