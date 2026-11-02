using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;

static partial class ReaderGuard {
 sealed class Candidate {
  public string Usb, Model, Serial, DiskId, Letters;
  public uint Index;
  public ulong Size;
 }
 static List<Candidate> Candidates() {
  var list=new List<Candidate>();
  foreach(var d in Query("SELECT * FROM Win32_DiskDrive")) {
   uint disk,parent,child,sibling;
   var diskId=Convert.ToString(d["PNPDeviceID"]);
   if(CM_Locate_DevNodeW(out disk,diskId,0)!=0 || CM_Get_Parent(out parent,disk,0)!=0) continue;
   var usb=Id(parent);
   // Only a physical USB storage node with one direct disk child is supported.
   // Never walk upwards to an arbitrary removable hub.
   if(!usb.StartsWith(@"USB\VID_",StringComparison.OrdinalIgnoreCase) || usb.Contains("&MI_")) continue;
   if(CM_Get_Child(out child,parent,0)!=0 || child!=disk || CM_Get_Sibling(out sibling,child,0)==0) continue;
   var serial=Convert.ToString(d["SerialNumber"]).Trim();
   if(String.IsNullOrWhiteSpace(serial)) continue;
   if(ProtectedDisk(d)) continue;
   var letters=new List<string>();
   using(var parts=d.GetRelated("Win32_DiskPartition")) foreach(ManagementObject part in parts)
    using(var vols=part.GetRelated("Win32_LogicalDisk")) foreach(ManagementObject vol in vols) letters.Add(Convert.ToString(vol["DeviceID"]));
   list.Add(new Candidate { Usb=usb, Model=Convert.ToString(d["Model"]), Serial=serial, DiskId=diskId, Index=Convert.ToUInt32(d["Index"]), Size=Convert.ToUInt64(d["Size"]), Letters=String.Join(" ",letters.ToArray()) });
  }
  return list;
 }
 static Binding PickDevice() {
  Binding picked=null;
  using(var form=new Form()) {
   form.SuspendLayout(); form.Text="选择要自动弹出的读卡器"; form.ClientSize=new Size(880,520); form.StartPosition=FormStartPosition.CenterScreen; form.MinimumSize=new Size(820,540); form.Font=new Font("Microsoft YaHei UI",10); form.AutoScaleDimensions=new SizeF(96,96); form.AutoScaleMode=AutoScaleMode.Dpi;
   var intro=new Label { Left=18,Top=16,Width=840,Height=65,Text="请插入读卡器和存储卡，选中对应设备。列表也可能包含移动硬盘，请核对后再确认。\n不确定时：拔掉读卡器并刷新，再插回并刷新，找出新增设备。",Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
   var grid=new DataGridView { Left=18,Top=88,Width=840,Height=210,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
   grid.Columns.Add("model","设备型号"); grid.Columns.Add("size","容量"); grid.Columns.Add("letters","盘符"); grid.Columns.Add("index","磁盘编号");
   grid.Columns[0].FillWeight=55; grid.Columns[1].FillWeight=18; grid.Columns[2].FillWeight=12; grid.Columns[3].FillWeight=15;
   var detail=new TextBox { Left=18,Top=308,Width=840,Height=65,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
   var confirm=new CheckBox { Left=18,Top=383,Width=840,Height=30,Text="我确认这是要自动弹出的读卡器；按管理页设置的空闲时间停止整只设备。",Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
   var hint=new Label { Left=18,Top=422,Width=830,Height=28,Text="每次新增都需确认；确认后返回管理页，可命名并设置独立空闲时间。",ForeColor=Color.DimGray };
   var refresh=new Button { Left=18,Top=466,Width=120,Height=34,Text="刷新设备" };
   var bind=new Button { Left=610,Top=466,Width=150,Height=34,Text="确认此读卡器",Enabled=false,Anchor=AnchorStyles.Bottom|AnchorStyles.Right };
   var cancel=new Button { Left=774,Top=466,Width=84,Height=34,Text="取消",DialogResult=DialogResult.Cancel,Anchor=AnchorStyles.Bottom|AnchorStyles.Right };
   Action update=()=>{ var row=grid.SelectedRows.Count==1?grid.SelectedRows[0]:null; var c=row==null?null:row.Tag as Candidate; detail.Text=c==null?"未选中设备。":("USB 标识："+c.Usb+Environment.NewLine+"设备报告的磁盘序列号："+c.Serial); confirm.Checked=false; bind.Enabled=false; };
   grid.SelectionChanged+=(s,e)=>update(); confirm.CheckedChanged+=(s,e)=>{bind.Enabled=confirm.Checked && grid.SelectedRows.Count==1;};
   Action load=()=>{ try { grid.Rows.Clear(); foreach(var c in Candidates()) { int i=grid.Rows.Add(c.Model,(c.Size/1000000000.0).ToString("F1")+" GB",c.Letters,c.Index); grid.Rows[i].Tag=c; } grid.ClearSelection(); update(); if(grid.Rows.Count==0) detail.Text="未发现可添加的读卡器。请先插入读卡器和存储卡，再点击“刷新设备”。若仍未显示，这只设备可能暂不受支持。"; } catch(Exception e) { detail.Text="读取设备失败："+e.Message; } };
   refresh.Click+=(s,e)=>load();
   bind.Click+=(s,e)=>{
    try {
     var chosen=grid.SelectedRows[0].Tag as Candidate;
     var fresh=Candidates().Find(c=>c.Usb==chosen.Usb && c.DiskId==chosen.DiskId && c.Model==chosen.Model && c.Serial==chosen.Serial && c.Index==chosen.Index);
     if(fresh==null) throw new Exception("设备已变化，请刷新后重新确认。");
     Target=fresh.Usb; Model=fresh.Model; Serial=fresh.Serial;
     uint usb; var d=Disk(out usb); if(d==null || Counters(fresh.Index)==null) throw new Exception("设备正忙或无法读取活动计数，请稍后重试。");
     if(MessageBox.Show("确认这是你的读卡器：\n"+fresh.Model+"（"+fresh.Letters+"，"+(fresh.Size/1000000000.0).ToString("F1")+" GB）\n\n将停止整只读卡器；再次使用可能需要重新插拔。安全弹出不保证断电或降温。\n\n是否将此设备加入管理列表？","确认设备身份",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes) return;
     picked=new Binding { Usb=fresh.Usb,Model=fresh.Model,Serial=fresh.Serial,Name=fresh.Model };
     form.DialogResult=DialogResult.OK; form.Close();
    } catch(Exception error) { MessageBox.Show(error.Message,"未能绑定",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
   };
   form.BackColor=Color.FromArgb(245,247,251); StyleGrid(grid); StyleButton(bind,true); StyleButton(refresh,false); StyleButton(cancel,false);
   form.Controls.AddRange(new Control[]{intro,grid,detail,confirm,hint,refresh,bind,cancel}); form.CancelButton=cancel; form.Shown+=(s,e)=>load();
   form.ResumeLayout(true); return form.ShowDialog()==DialogResult.OK?picked:null;
  }
 }
}
