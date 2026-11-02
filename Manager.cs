using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static partial class ReaderGuard {
 static readonly Color Ink=Color.FromArgb(26,39,61),Muted=Color.FromArgb(101,116,139),Blue=Color.FromArgb(38,96,220);
 static void StyleButton(Button b,bool primary) { b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderSize=primary?0:1; b.FlatAppearance.BorderColor=Color.FromArgb(214,222,232); Action colors=()=>{b.BackColor=!b.Enabled?Color.FromArgb(228,233,241):primary?Blue:Color.White;b.ForeColor=!b.Enabled?Muted:primary?Color.White:Ink;};b.EnabledChanged+=(s,e)=>colors();colors();b.Cursor=Cursors.Hand; }
 static void StyleGrid(DataGridView g) {
  g.BackgroundColor=Color.White; g.BorderStyle=BorderStyle.None; g.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal; g.GridColor=Color.FromArgb(233,237,243); g.EnableHeadersVisualStyles=false;
  g.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(242,245,250); g.ColumnHeadersDefaultCellStyle.ForeColor=Muted; g.ColumnHeadersDefaultCellStyle.Font=new Font("Microsoft YaHei UI",9);
  g.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(242,245,250); g.ColumnHeadersDefaultCellStyle.SelectionForeColor=Ink;
  g.ColumnHeadersHeight=40; g.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
  g.DefaultCellStyle.BackColor=Color.White; g.DefaultCellStyle.ForeColor=Ink; g.DefaultCellStyle.SelectionBackColor=Color.FromArgb(229,238,255); g.DefaultCellStyle.SelectionForeColor=Ink; g.DefaultCellStyle.Padding=new Padding(8,4,8,4); g.RowTemplate.Height=52;
  g.AllowUserToResizeRows=false; g.RowHeadersVisible=false; g.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
 }
 static bool Manage(ref List<Binding> bindings) {
  var draft=bindings.Select(b=>b.Copy()).ToList(); bool accepted=false;
  using(var form=new Form()) {
   form.SuspendLayout(); form.Text="读卡器管理 · Card Reader Guard"; form.Font=new Font("Microsoft YaHei UI",10); form.AutoScaleDimensions=new SizeF(96,96); form.AutoScaleMode=AutoScaleMode.Dpi; form.ClientSize=new Size(1000,650); form.FormBorderStyle=FormBorderStyle.FixedSingle; form.MaximizeBox=false; form.StartPosition=FormStartPosition.CenterScreen; form.BackColor=Color.FromArgb(243,246,250);
   var header=new Panel { Left=0,Top=0,Width=1000,Height=110,BackColor=Ink,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
   header.Controls.Add(new Label { Left=26,Top=19,Width=600,Height=40,Text="读卡器管理",ForeColor=Color.White,Font=new Font("Microsoft YaHei UI",22,FontStyle.Bold) });
   header.Controls.Add(new Label { Left=29,Top=69,Width=900,Height=25,Text="只监测你确认过的设备。设置完成后，安静地在后台运行。",ForeColor=Color.FromArgb(192,207,230) });
   var section=new Label { Left=28,Top=136,Width=420,Height=28,Text="已绑定的读卡器",Font=new Font("Microsoft YaHei UI",13,FontStyle.Bold),ForeColor=Ink };
   var summary=new Label { Left=28,Top=173,Width=650,Height=25,ForeColor=Muted };
   var add=new Button { Left=690,Top=136,Width=136,Height=36,Text="＋ 添加读卡器" };
   var refresh=new Button { Left=838,Top=136,Width=134,Height=36,Text="刷新连接状态" };
   var grid=new DataGridView { Left=28,Top=211,Width=630,Height=284,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false };
   grid.Columns.Add("name","名称"); grid.Columns.Add("model","设备型号"); grid.Columns.Add("time","空闲时间"); grid.Columns.Add("status","状态");
   grid.Columns[0].FillWeight=30; grid.Columns[1].FillWeight=35; grid.Columns[2].FillWeight=17; grid.Columns[3].FillWeight=18; StyleGrid(grid);
   var side=new Panel { Left=680,Top=211,Width=292,Height=284,BackColor=Color.White };
   var labelName=new Label { Left=18,Top=17,Width=250,Height=24,Text="设备名称",ForeColor=Muted };
   var name=new TextBox { Left=18,Top=46,Width=254,MaxLength=80 };
   var labelTime=new Label { Left=18,Top=91,Width=250,Height=24,Text="没有读写后多久弹出",ForeColor=Muted };
   var minutes=new NumericUpDown { Left=18,Top=122,Width=90,Minimum=1,Maximum=120,Value=5 };
   var unit=new Label { Left=120,Top=126,Width=100,Height=24,Text="分钟",ForeColor=Ink };
   var enabled=new CheckBox { Left=18,Top=168,Width=254,Height=28,Text="启用这只读卡器的自动弹出" };
   var replace=new Button { Left=18,Top=221,Width=119,Height=36,Text="更换绑定设备" };
   var remove=new Button { Left=149,Top=221,Width=123,Height=36,Text="删除绑定" };
   side.Controls.AddRange(new Control[]{labelName,name,labelTime,minutes,unit,enabled,replace,remove});
   var detail=new Label { Left=28,Top=507,Width=944,Height=44,ForeColor=Muted,AutoEllipsis=true,UseMnemonic=false,Text="选择一只读卡器查看绑定信息。" };
   var startup=new CheckBox { Left=28,Top=563,Width=600,Height=28,Text="登录 Windows 后自动启动" };
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) startup.Checked=key!=null && key.GetValue("CardReaderGuard")!=null;
   if(!File.Exists(Path.Combine(Root,"device.xml"))) startup.Checked=true;
   var foot=new Label { Left=28,Top=606,Width=530,Height=27,Text="管理期间暂停弹出；关闭后重新计时。",ForeColor=Muted,Font=new Font("Microsoft YaHei UI",9) };
   var cancel=new Button { Left=651,Top=590,Width=124,Height=40,Text="取消更改",DialogResult=DialogResult.Cancel };
   var save=new Button { Left=790,Top=590,Width=182,Height=40,Text="保存并静默运行" };
   foreach(var b in new[]{add,refresh,replace,remove,cancel}) StyleButton(b,false); StyleButton(save,true); StyleButton(add,true);
   List<Candidate> connected=new List<Candidate>(); bool loading=false;
   Func<Binding> selected=()=>grid.SelectedRows.Count==1?grid.SelectedRows[0].Tag as Binding:null;
   Action show=()=>{ loading=true; var b=selected(); side.Enabled=b!=null; if(b!=null) { name.Text=b.Name; minutes.Value=b.Minutes; enabled.Checked=b.Enabled; detail.Text="设备："+b.Model+"\nUSB 身份："+b.Usb; } else { name.Text=""; minutes.Value=5; enabled.Checked=false; detail.Text="还没有绑定读卡器。点击右上方“添加读卡器”开始。"; } loading=false; };
   Action<Binding> render=keep=>{
    loading=true; grid.Rows.Clear(); foreach(var b in draft) { bool online=connected.Any(c=>String.Equals(c.Usb,b.Usb,StringComparison.OrdinalIgnoreCase) && c.Model==b.Model && c.Serial==b.Serial); int i=grid.Rows.Add(b.Name,b.Model,b.Minutes+" 分钟",!b.Enabled?"已暂停":online?"已连接":"未连接"); grid.Rows[i].Tag=b; grid.Rows[i].Cells[3].Style.ForeColor=!b.Enabled?Muted:online?Color.FromArgb(18,128,96):Muted; }
    grid.ClearSelection(); int selectedIndex=keep==null?(draft.Count>0?0:-1):draft.IndexOf(keep); if(selectedIndex>=0 && selectedIndex<grid.Rows.Count) grid.Rows[selectedIndex].Selected=true;
    summary.Text=draft.Count+" 只已绑定 · "+draft.Count(b=>b.Enabled)+" 只启用 · 可随时添加、修改或删除"; loading=false; show();
   };
   Action rescan=()=>{try { connected=Candidates(); render(selected()); } catch(Exception error) { detail.Text="读取连接状态失败："+error.Message; } };
   grid.SelectionChanged+=(s,e)=>{if(!loading) show();};
   name.TextChanged+=(s,e)=>{if(!loading && selected()!=null) { selected().Name=name.Text; grid.SelectedRows[0].Cells[0].Value=name.Text; }};
   minutes.ValueChanged+=(s,e)=>{if(!loading && selected()!=null) { selected().Minutes=(int)minutes.Value; grid.SelectedRows[0].Cells[2].Value=minutes.Value+" 分钟"; }};
   enabled.CheckedChanged+=(s,e)=>{if(!loading && selected()!=null) { var b=selected(); b.Enabled=enabled.Checked; render(b); }};
   add.Click+=(s,e)=>{var b=PickDevice(); if(b==null) return; if(draft.Any(x=>String.Equals(x.Usb,b.Usb,StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("这只设备已在列表中。请直接修改已有绑定。","已添加"); return; } draft.Add(b); try { connected=Candidates(); } catch{} render(b);};
   replace.Click+=(s,e)=>{var old=selected(); if(old==null) return; var b=PickDevice(); if(b==null) return; if(draft.Any(x=>x!=old && String.Equals(x.Usb,b.Usb,StringComparison.OrdinalIgnoreCase))) {MessageBox.Show("目标设备已有绑定。","无法替换"); return;} b.Name=old.Name; b.Minutes=old.Minutes; b.Enabled=old.Enabled; draft[draft.IndexOf(old)]=b; rescan(); render(b);};
   remove.Click+=(s,e)=>{var b=selected(); if(b!=null && MessageBox.Show("删除“"+b.Name+"”的绑定？\n保存后将不再自动弹出这只设备，不会删除卡内文件。","删除绑定",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes){draft.Remove(b);render(null);}};
   refresh.Click+=(s,e)=>rescan();
   save.Click+=(s,e)=>{try { SaveBindings(draft,Path.Combine(Root,"device.xml")); using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {if(startup.Checked)key.SetValue("CardReaderGuard","\""+Application.ExecutablePath+"\" --background");else key.DeleteValue("CardReaderGuard",false);} accepted=true;form.DialogResult=DialogResult.OK;form.Close(); } catch(Exception error){MessageBox.Show(error.Message,"未能保存",MessageBoxButtons.OK,MessageBoxIcon.Warning);} };
   form.Controls.AddRange(new Control[]{header,section,summary,add,refresh,grid,side,detail,startup,foot,cancel,save}); form.CancelButton=cancel; form.Shown+=(s,e)=>rescan(); form.ResumeLayout(true); form.ShowDialog();
  }
  if(accepted) bindings=draft; return accepted;
 }
}
