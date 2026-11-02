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
   form.Tag=new[]{Ink,Muted,Paper,Line,Tint,Surface}; form.SuspendLayout(); form.Text="添加读卡器 · Card Reader Guard"; form.ClientSize=new Size(900,650); form.MinimumSize=new Size(840,640); form.StartPosition=FormStartPosition.CenterScreen;
   form.Font=new Font("Microsoft YaHei UI",10); form.ForeColor=Ink; form.AutoScaleDimensions=new SizeF(96,96); form.AutoScaleMode=AutoScaleMode.Dpi;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=7, Padding=new Padding(28,20,28,20) };
   foreach(int height in new[]{46,66}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   foreach(int height in new[]{82,46,34,56}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   var title=UiLabel("先确认，哪一只是你的读卡器。",20,true);
   var intro=UiLabel("插入读卡器和存储卡，核对型号、容量与盘符。列表也可能包含移动硬盘。\n不确定时，先拔掉读卡器并刷新，再插回并刷新，找出新增设备。",10,false); intro.ForeColor=Muted;
   var grid=new DataGridView { Dock=DockStyle.Fill, Margin=new Padding(0,8,0,0), ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, AccessibleName="可绑定的外接存储候选列表", TabIndex=0 };
   grid.Columns.Add("model","设备型号"); grid.Columns.Add("size","容量"); grid.Columns.Add("letters","盘符"); grid.Columns.Add("index","磁盘编号");
   grid.Columns[0].FillWeight=52; grid.Columns[1].FillWeight=18; grid.Columns[2].FillWeight=12; grid.Columns[3].FillWeight=18;
   var detail=new TextBox { Dock=DockStyle.Fill, Margin=new Padding(0,14,0,0), Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, BorderStyle=BorderStyle.FixedSingle, BackColor=Paper, ForeColor=Muted, Font=new Font("Microsoft YaHei UI",9), AccessibleName="候选设备身份信息" };
   var confirm=new CheckBox { Dock=DockStyle.Fill, Margin=new Padding(0,10,0,0), Text="我确认这是要自动弹出的读卡器；达到空闲时间后停止整只设备。", TabIndex=2 };
   var hint=UiLabel("每次新增都需要确认；确认后可返回管理页设置名称和独立空闲时间。",9,false); hint.ForeColor=Muted;
   var actions=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=4, RowCount=1, Margin=new Padding(0), Padding=new Padding(0,12,0,0) };
   actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,102)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168));
   var refresh=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0,0,0,0), Text="刷新设备", TabIndex=1 };
   var bind=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(12,0,0,0), Text="确认此读卡器", Enabled=false, TabIndex=4 };
   var cancel=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0), Text="取消", DialogResult=DialogResult.Cancel, TabIndex=3 };
   actions.Controls.Add(refresh,0,0); actions.Controls.Add(cancel,2,0); actions.Controls.Add(bind,3,0);
   layout.Controls.Add(title,0,0); layout.Controls.Add(intro,0,1); layout.Controls.Add(grid,0,2); layout.Controls.Add(detail,0,3); layout.Controls.Add(confirm,0,4); layout.Controls.Add(hint,0,5); layout.Controls.Add(actions,0,6);
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
   form.BackColor=Paper; StyleGrid(grid); StyleButton(bind,true); StyleButton(refresh,false); StyleButton(cancel,false);
   form.Controls.Add(layout); form.CancelButton=cancel; form.Shown+=(s,e)=>{load(); ApplyUiTheme(form);}; WatchUiTheme(form);
   form.ResumeLayout(true); return form.ShowDialog()==DialogResult.OK?picked:null;
  }
 }
}
