using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static partial class ReaderGuard {
 // Shared visual tokens for both native Windows dialogs.
 static Color Ink=Color.FromArgb(35,39,47), Muted=Color.FromArgb(106,112,124),
  Blue=Color.FromArgb(83,102,188), Paper=Color.FromArgb(243,244,247), Line=Color.FromArgb(221,224,231), Tint=Color.FromArgb(231,235,247), Surface=Color.White;
 static void StyleButton(Button b,bool primary) {
  b.Tag=primary?"primary":"secondary"; b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderSize=primary?0:1; b.FlatAppearance.BorderColor=Line;
  b.FlatAppearance.MouseOverBackColor=primary?Blue:Tint;
  b.FlatAppearance.MouseDownBackColor=primary?Blue:Line;
  Action colors=()=>{b.BackColor=!b.Enabled?Line:primary?Blue:Surface;b.ForeColor=!b.Enabled?Muted:primary?(Surface==Color.White?Color.White:Paper):Ink;};
  b.EnabledChanged+=(s,e)=>colors(); colors(); b.Cursor=Cursors.Hand; b.UseVisualStyleBackColor=false;
 }
 static void StyleGrid(DataGridView g) {
  g.BackgroundColor=Surface; g.BorderStyle=BorderStyle.None; g.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal; g.GridColor=Line; g.EnableHeadersVisualStyles=false;
  g.ColumnHeadersDefaultCellStyle.BackColor=Paper; g.ColumnHeadersDefaultCellStyle.ForeColor=Muted; g.ColumnHeadersDefaultCellStyle.Font=new Font("Microsoft YaHei UI",9);
  g.ColumnHeadersDefaultCellStyle.SelectionBackColor=Paper; g.ColumnHeadersDefaultCellStyle.SelectionForeColor=Ink;
  g.ColumnHeadersHeight=42; g.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
  g.DefaultCellStyle.BackColor=Surface; g.DefaultCellStyle.ForeColor=Ink; g.DefaultCellStyle.SelectionBackColor=Tint; g.DefaultCellStyle.SelectionForeColor=Ink;
  g.DefaultCellStyle.Padding=new Padding(8,4,8,4); g.RowTemplate.Height=54;
  g.AllowUserToResizeRows=false; g.RowHeadersVisible=false; g.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  g.ShowCellToolTips=true; g.MultiSelect=false; g.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
 }
 static Label UiLabel(string text,int size,bool bold) {
  return new Label { Text=text, AutoSize=false, ForeColor=Ink, Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular), Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft, Margin=new Padding(0) };
 }
 static string UiThemePreference=ReadUiTheme();
 static string ReadUiTheme() {
  try { var value=File.ReadAllText(Path.Combine(Root,"appearance.txt")).Trim(); if(value=="light" || value=="dark") return value; } catch { }
  return "auto";
 }
 static bool SystemDark() {
  try { using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0; } catch { return false; }
 }
 static void ApplyUiTheme(Control root) {
  var previous=root.Tag as Color[]; if(previous==null) previous=new[]{Ink,Muted,Paper,Line,Tint,Surface};
  var oldInk=previous[0]; var oldMuted=previous[1]; var oldPaper=previous[2]; var oldSurface=previous[5];
  bool dark=UiThemePreference=="dark" || (UiThemePreference=="auto" && SystemDark());
  Ink=dark?Color.FromArgb(231,233,239):Color.FromArgb(35,39,47);
  Muted=dark?Color.FromArgb(147,153,167):Color.FromArgb(106,112,124);
  Paper=dark?Color.FromArgb(25,27,33):Color.FromArgb(243,244,247);
  Surface=dark?Color.FromArgb(32,35,43):Color.White;
  Line=dark?Color.FromArgb(49,53,64):Color.FromArgb(221,224,231);
  Tint=dark?Color.FromArgb(43,49,69):Color.FromArgb(231,235,247);
  Blue=dark?Color.FromArgb(161,175,239):Color.FromArgb(83,102,188);
  Action<Control> paint=null;
  paint=control=>{
   if(control.BackColor==oldSurface || control.BackColor==Color.White) control.BackColor=Surface;
   else if(control.BackColor==oldPaper) control.BackColor=Paper;
   if(control.ForeColor==oldMuted) control.ForeColor=Muted;
   else if(control.ForeColor==oldInk) control.ForeColor=Ink;
   if(control is TextBoxBase || control is ComboBox || control is NumericUpDown) {
    control.BackColor=control is TextBoxBase && ((TextBoxBase)control).ReadOnly?Paper:Surface;
    control.ForeColor=Ink;
   }
   var button=control as Button;
   if(button!=null) {
    bool primary=Convert.ToString(button.Tag)=="primary";
    button.BackColor=!button.Enabled?Line:primary?Blue:Surface;
    button.ForeColor=!button.Enabled?Muted:primary?(dark?Paper:Color.White):Ink;
    button.FlatAppearance.BorderColor=Line;
    button.FlatAppearance.MouseOverBackColor=primary?(dark?Color.FromArgb(179,217,190):Color.FromArgb(42,102,67)):Tint;
    button.FlatAppearance.MouseDownBackColor=primary?(dark?Color.FromArgb(138,180,150):Color.FromArgb(34,85,55)):Line;
   }
   var grid=control as DataGridView; if(grid!=null) {
    grid.BackgroundColor=Surface; grid.GridColor=Line; grid.DefaultCellStyle.BackColor=Surface; grid.DefaultCellStyle.ForeColor=Ink;
    grid.DefaultCellStyle.SelectionBackColor=Tint; grid.DefaultCellStyle.SelectionForeColor=Ink;
    grid.ColumnHeadersDefaultCellStyle.BackColor=Paper; grid.ColumnHeadersDefaultCellStyle.ForeColor=Muted;
    grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Paper; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Ink;
    foreach(DataGridViewRow row in grid.Rows) row.Cells[row.Cells.Count-1].Style.ForeColor=Convert.ToString(row.Cells[row.Cells.Count-1].Value)=="已连接"?Blue:Muted;
   }
   foreach(Control child in control.Controls) paint(child);
  };
  root.SuspendLayout(); paint(root); root.Tag=new[]{Ink,Muted,Paper,Line,Tint,Surface}; root.ResumeLayout(true); root.Invalidate(true);
 }
 static void WatchUiTheme(Form form) {
  UserPreferenceChangedEventHandler handler=(sender,args)=>{
   if(form.IsDisposed || !form.IsHandleCreated || UiThemePreference!="auto") return;
   try { form.BeginInvoke((Action)(()=>{if(!form.IsDisposed) ApplyUiTheme(form);})); } catch(InvalidOperationException) { }
  };
  SystemEvents.UserPreferenceChanged+=handler;
  form.FormClosed+=(sender,args)=>SystemEvents.UserPreferenceChanged-=handler;
 }
 static bool Manage(ref List<Binding> bindings) {
  var draft=bindings.Select(b=>b.Copy()).ToList(); bool accepted=false;
  using(var form=new Form()) {
   form.Icon=AppIcon;
   form.Tag=new[]{Ink,Muted,Paper,Line,Tint,Surface}; form.SuspendLayout(); form.Text="读卡器管理 · Card Reader Guard"; form.Font=new Font("Microsoft YaHei UI",10); form.ForeColor=Ink;
   form.AutoScaleDimensions=new SizeF(96,96); form.AutoScaleMode=AutoScaleMode.Dpi;
   form.ClientSize=new Size(1100,740); form.MinimumSize=new Size(940,680); form.StartPosition=FormStartPosition.CenterScreen; form.BackColor=Paper;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=3, Padding=new Padding(24,16,24,16) };
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,80)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,88));
   var header=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=2, Margin=new Padding(0,0,0,12) };
   header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,244));
   header.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); header.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   header.Controls.Add(UiLabel("Card Reader Guard",21,true),0,0);
   var subtitle=UiLabel("设备管理  /  仅监测已确认的读卡器，空闲后安全弹出。",10,false); subtitle.ForeColor=Muted; header.Controls.Add(subtitle,0,1); header.SetColumnSpan(subtitle,2);
   var appearance=new UiThemePicker { Dock=DockStyle.Top, Height=36, AccessibleName="应用外观", Margin=new Padding(0,4,0,0) };
   appearance.SelectedIndex=UiThemePreference=="light"?1:UiThemePreference=="dark"?2:0;
   appearance.SelectedIndexChanged+=(sender,args)=>{
    var next=appearance.SelectedIndex==1?"light":appearance.SelectedIndex==2?"dark":"auto";
    try { File.WriteAllText(Path.Combine(Root,"appearance.txt"),next); UiThemePreference=next; ApplyUiTheme(form); }
    catch(Exception error) { MessageBox.Show("无法保存外观设置："+error.Message,"外观未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning); appearance.SelectedIndex=UiThemePreference=="light"?1:UiThemePreference=="dark"?2:0; }
   };
   header.Controls.Add(appearance,1,0);
   var body=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=2, Margin=new Padding(0) };
   body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,320));
   body.RowStyles.Add(new RowStyle(SizeType.Percent,100)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
   var list=new UiPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=3, BackColor=Color.White, Padding=new Padding(16), Margin=new Padding(0,0,16,0) };
   list.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); list.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); list.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var toolbar=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=3, RowCount=1, Margin=new Padding(0) };
   toolbar.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,144)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));
   var section=UiLabel("已绑定的读卡器",12,true);
   var add=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0,2,8,4), Text="＋ 添加读卡器", AccessibleName="添加读卡器", TabIndex=0 };
   var refresh=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0,2,0,4), Text="刷新连接", AccessibleName="刷新连接状态", TabIndex=1 };
   toolbar.Controls.Add(section,0,0); toolbar.Controls.Add(add,1,0); toolbar.Controls.Add(refresh,2,0);
   var summary=UiLabel("正在读取连接状态…",9,false); summary.ForeColor=Muted;
   var grid=new DataGridView { Dock=DockStyle.Fill, Margin=new Padding(0,8,0,0), ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, TabIndex=2 };
   grid.Columns.Add("name","名称"); grid.Columns.Add("model","设备型号"); grid.Columns.Add("time","空闲时间"); grid.Columns.Add("status","状态");
   grid.Columns[0].FillWeight=50; grid.Columns[1].Visible=false; grid.Columns[2].FillWeight=22; grid.Columns[3].FillWeight=28; StyleGrid(grid); grid.ColumnHeadersVisible=false; grid.RowTemplate.Height=76; grid.CellPainting+=(sender,args)=>PaintDeviceCell(grid,args);
   grid.AccessibleName="已绑定的读卡器列表";
   var empty=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=5, BackColor=Surface, Padding=new Padding(24) };
   empty.RowStyles.Add(new RowStyle(SizeType.Percent,50)); empty.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); empty.RowStyles.Add(new RowStyle(SizeType.Absolute,64)); empty.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); empty.RowStyles.Add(new RowStyle(SizeType.Percent,50));
   var emptyTitle=UiLabel("▣  添加第一只读卡器",16,true); emptyTitle.TextAlign=ContentAlignment.MiddleCenter;
   var emptyHint=UiLabel("插入读卡器和存储卡，确认设备身份后添加。\n每只设备都可以设置独立的空闲时间。",10,false); emptyHint.ForeColor=Muted; emptyHint.TextAlign=ContentAlignment.MiddleCenter;
   var emptyAdd=new UiButton { Text="＋ 添加读卡器", Size=new Size(156,38), Anchor=AnchorStyles.Top, AccessibleName="添加第一只读卡器" }; StyleButton(emptyAdd,true); emptyAdd.Click+=(sender,args)=>add.PerformClick();
   empty.Controls.Add(emptyTitle,0,1); empty.Controls.Add(emptyHint,0,2); empty.Controls.Add(emptyAdd,0,3);
   var gridHost=new Panel { Dock=DockStyle.Fill, Margin=new Padding(0) }; gridHost.Controls.Add(grid); gridHost.Controls.Add(empty);
   list.Controls.Add(toolbar,0,0); list.Controls.Add(summary,0,1); list.Controls.Add(gridHost,0,2);
   var side=new UiPanel { Dock=DockStyle.Fill, BackColor=Color.White, ColumnCount=1, RowCount=9, Padding=new Padding(20), Margin=new Padding(0) };
   foreach(int height in new[]{40,28,44,32,44,44}) side.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   side.RowStyles.Add(new RowStyle(SizeType.Percent,100)); side.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); side.RowStyles.Add(new RowStyle(SizeType.Absolute,26));
   side.Controls.Add(UiLabel("设备设置",13,true),0,0);
   var labelName=UiLabel("设备名称",9,false); labelName.ForeColor=Muted;
   var name=new TextBox { MaxLength=80, AccessibleName="设备名称", TabIndex=0 };
   var nameFrame=new UiInputFrame(name) {Dock=DockStyle.Top,TabIndex=0};
   var labelTime=UiLabel("没有读写后多久弹出",9,false); labelTime.ForeColor=Muted;
   var timeRow=new FlowLayoutPanel { Dock=DockStyle.Fill, Margin=new Padding(0), WrapContents=false };
   var minutes=new NumericUpDown { Width=96, Minimum=1, Maximum=120, Value=5, AccessibleName="空闲时间（分钟）", TabIndex=1 };
   var minuteFrame=new UiInputFrame(minutes) {Width=140,TabIndex=1};
   var unit=new Label { Text="分钟（1–120）", AutoSize=true, Padding=new Padding(4,7,0,0), ForeColor=Muted }; timeRow.Controls.Add(minuteFrame); timeRow.Controls.Add(unit);
   var enabled=new UiToggle { Width=44, Height=28, Text="", AccessibleName="启用自动弹出", TabIndex=2, Margin=new Padding(0) };
   var enabledRow=new FlowLayoutPanel { Dock=DockStyle.Fill, WrapContents=false, Margin=new Padding(0) };
   var enabledText=new Label { Text="启用自动弹出", AutoSize=true, Padding=new Padding(0,3,0,0), ForeColor=Muted };
   enabledRow.Controls.Add(enabled); enabledRow.Controls.Add(enabledText);
   var deviceActions=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Margin=new Padding(0) };
   deviceActions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   deviceActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55)); deviceActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
   var replace=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0,0,8,0), Text="更换绑定", TabIndex=3 };
   var remove=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0), Text="删除绑定", TabIndex=4 };
   deviceActions.Controls.Add(replace,0,0); deviceActions.Controls.Add(remove,1,0);
   var rangeHint=UiLabel("修改在保存后生效。",9,false); rangeHint.ForeColor=Muted;
   side.Controls.Add(labelName,0,1); side.Controls.Add(nameFrame,0,2); side.Controls.Add(labelTime,0,3); side.Controls.Add(timeRow,0,4); side.Controls.Add(enabledRow,0,5); side.Controls.Add(deviceActions,0,7); side.Controls.Add(rangeHint,0,8);
   var sideHost=new Panel { Dock=DockStyle.Fill, Margin=new Padding(0) }; sideHost.Controls.Add(side);
   var guide=UiLabel("设备设置\n\n选择左侧设备，调整自动弹出规则。",11,false); guide.TextAlign=ContentAlignment.MiddleCenter; guide.ForeColor=Muted; guide.BackColor=Surface; sideHost.Controls.Add(guide);
   var logHost=new UiPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2, Padding=new Padding(12,0,12,0), Margin=new Padding(0,8,0,0), BackColor=Surface };
   logHost.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); logHost.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var logToggle=new UiButton { Text="›  活动日志 · 点击展开", Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft, Margin=new Padding(0) }; StyleButton(logToggle,false);
   var detail=new RichTextBox { Dock=DockStyle.Fill, ReadOnly=true, BorderStyle=BorderStyle.None, BackColor=Surface, ForeColor=Muted, Font=new Font("Consolas",9), AccessibleName="活动日志和设备身份", WordWrap=false, DetectUrls=false };
   bool logOpen=false; logToggle.Click+=(sender,args)=>{logOpen=!logOpen; body.RowStyles[1].Height=logOpen?176:40; logToggle.Text=logOpen?"⌄  活动日志 · 点击收起":"›  活动日志 · 点击展开";};
   Action readLog=()=>{try { detail.Clear(); if(File.Exists(LogPath)) foreach(var entry in File.ReadAllLines(LogPath)) { detail.SelectionColor=entry.Contains("ERROR") || entry.Contains("FAILED")?Color.FromArgb(224,120,126):entry.Contains("SKIP")?Color.FromArgb(196,161,99):Muted; detail.AppendText(entry+Environment.NewLine); } if(detail.TextLength==0) detail.Text="暂无运行日志。保存后将在后台记录设备活动。"; detail.SelectionStart=detail.TextLength; detail.ScrollToCaret(); } catch(Exception error) { detail.Text="日志暂时无法读取："+error.Message; } };
   logToggle.Click+=(sender,args)=>{if(logOpen)readLog();};
   logHost.Controls.Add(logToggle,0,0); logHost.Controls.Add(detail,0,1);
   body.Controls.Add(list,0,0); body.Controls.Add(sideHost,1,0); body.Controls.Add(logHost,0,1); body.SetColumnSpan(logHost,2);
   var footer=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Margin=new Padding(0), Padding=new Padding(0,16,0,0) };
   footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,320));
   var footInfo=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2, Margin=new Padding(0) };
   footInfo.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); footInfo.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var startup=new UiToggle { Dock=DockStyle.Fill, Text="登录 Windows 后自动启动", Margin=new Padding(0) };
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) startup.Checked=key!=null && key.GetValue("CardReaderGuard")!=null;
   if(!File.Exists(Path.Combine(Root,"device.xml"))) startup.Checked=true;
   var foot=UiLabel("管理期间暂停弹出；关闭后重新计算空闲时间。",9,false); foot.ForeColor=Muted;
   footInfo.Controls.Add(startup,0,0); footInfo.Controls.Add(foot,0,1);
   var footerActions=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Margin=new Padding(0), Padding=new Padding(0,8,0,8) };
   footerActions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   footerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120)); footerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   var cancel=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0,0,12,0), Text="取消更改", DialogResult=DialogResult.Cancel };
   var save=new UiButton { Dock=DockStyle.Fill, Margin=new Padding(0), Text="保存并静默运行" };
   footerActions.Controls.Add(cancel,0,0); footerActions.Controls.Add(save,1,0); footer.Controls.Add(footInfo,0,0); footer.Controls.Add(footerActions,1,0);
   layout.Controls.Add(header,0,0); layout.Controls.Add(body,0,1); layout.Controls.Add(footer,0,2);
   foreach(var button in new[]{add,refresh,replace,remove,cancel}) StyleButton(button,false); StyleButton(save,true); StyleButton(add,true);
   remove.Tag="danger"; var tips=new ToolTip { InitialDelay=400, AutoPopDelay=10000 }; tips.SetToolTip(replace,"重新选择物理设备，保留名称和弹出规则"); tips.SetToolTip(remove,"删除绑定不会删除存储卡中的文件"); form.FormClosed+=(sender,args)=>tips.Dispose();
   List<Candidate> connected=new List<Candidate>(); bool loading=false;
   Func<Binding> selected=()=>grid.SelectedRows.Count==1?grid.SelectedRows[0].Tag as Binding:null;
   Action show=()=>{ loading=true; var b=selected(); side.Visible=b!=null; guide.Visible=b==null; name.ReadOnly=b==null; minutes.Enabled=b!=null && b.Enabled; enabledText.Text=b!=null && b.Enabled?"自动弹出已启用":"自动弹出已暂停"; enabled.Enabled=b!=null; replace.Enabled=b!=null; remove.Enabled=b!=null; empty.Visible=draft.Count==0; grid.Visible=draft.Count!=0; if(b!=null) { name.Text=b.Name; minutes.Value=b.Minutes; enabled.Checked=b.Enabled; rangeHint.Text="修改在保存后生效。"; tips.SetToolTip(name,b.Model+Environment.NewLine+b.Usb); } else { name.Text=""; minutes.Value=5; enabled.Checked=false; rangeHint.Text="选择设备后编辑。"; } loading=false; };
   Action<Binding> render=keep=>{
    loading=true; grid.Rows.Clear(); foreach(var b in draft) { bool online=connected.Any(c=>String.Equals(c.Usb,b.Usb,StringComparison.OrdinalIgnoreCase) && c.Model==b.Model && c.Serial==b.Serial); int i=grid.Rows.Add(b.Name,b.Model,b.Minutes+" 分钟",!b.Enabled?"已暂停":online?"已连接":"未连接"); grid.Rows[i].Tag=b; grid.Rows[i].Cells[3].Style.ForeColor=!b.Enabled?Muted:online?Blue:Muted; }
    grid.ClearSelection(); int selectedIndex=keep==null?(draft.Count>0?0:-1):draft.IndexOf(keep); if(selectedIndex>=0 && selectedIndex<grid.Rows.Count) grid.Rows[selectedIndex].Selected=true;
    summary.Text=draft.Count+" 只设备  ·  "+draft.Count(b=>b.Enabled)+" 只启用  ·  "+draft.Count(b=>connected.Any(c=>String.Equals(c.Usb,b.Usb,StringComparison.OrdinalIgnoreCase) && c.Model==b.Model && c.Serial==b.Serial))+" 只连接"; loading=false; show();
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
   form.Controls.Add(layout); form.CancelButton=cancel; form.Shown+=(s,e)=>{rescan(); readLog(); ApplyUiTheme(form);}; WatchUiTheme(form); form.ResumeLayout(true); form.ShowDialog();
  }
  if(accepted) bindings=draft; return accepted;
 }
}
