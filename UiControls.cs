using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
static partial class ReaderGuard {
 static GraphicsPath Round(Rectangle r,int radius) { var p=new GraphicsPath(); int d=radius*2; p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p; }
 sealed class UiPanel : TableLayoutPanel {
  public UiPanel() { DoubleBuffered=true; }
  protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var path=Round(new Rectangle(0,0,Width-1,Height-1),10)) using(var pen=new Pen(Line)) e.Graphics.DrawPath(pen,path); }
 }
 sealed class UiButton : Button {
  bool hover,down;
  public UiButton() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true); Height=36; }
  protected override void OnMouseEnter(EventArgs e) { hover=true;Invalidate();base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hover=false;down=false;Invalidate();base.OnMouseLeave(e); }
  protected override void OnMouseDown(MouseEventArgs e) { down=true;Invalidate();base.OnMouseDown(e); }
  protected override void OnMouseUp(MouseEventArgs e) { down=false;Invalidate();base.OnMouseUp(e); }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.Clear(Parent==null?Paper:Parent.BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   bool chosen=Convert.ToString(Tag)=="selected";
   bool primary=Convert.ToString(Tag)=="primary",danger=Convert.ToString(Tag)=="danger";
   Color fill=Enabled?(primary?Blue:chosen || hover?Tint:Surface):Paper;
   if(down && Enabled) fill=Tint;
   using(var path=Round(new Rectangle(1,1,Width-3,Height-3),6)) {
    using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);
    using(var pen=new Pen(Focused || chosen?Blue:primary?fill:Line,Focused?2:1))e.Graphics.DrawPath(pen,path);
   }
   var color=!Enabled?Muted:danger?Color.FromArgb(219,113,122):primary && !down?(Paper.GetBrightness()<0.5?Paper:Color.White):Ink;
   TextRenderer.DrawText(e.Graphics,Text,Font,Rectangle.Inflate(ClientRectangle,-(int)Math.Round(8*DeviceDpi/96f),-2),color,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
  }
 }
 // A native keyboard-accessible segmented picker; no editable text or OS selection highlight.
 sealed class UiThemePicker : TableLayoutPanel {
  readonly UiButton[] choices=new UiButton[3]; int selected=-1;
  public event EventHandler SelectedIndexChanged;
  public int SelectedIndex {
   get { return selected; }
   set {
    if(value<0 || value>2 || value==selected)return;
    selected=value;
    for(int i=0;i<choices.Length;i++) { choices[i].Tag=i==selected?"selected":"secondary"; choices[i].AccessibleDescription=i==selected?"当前主题":"切换主题"; choices[i].Invalidate(); }
    if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);
   }
  }
  public UiThemePicker() {
   DoubleBuffered=true;ColumnCount=3;RowCount=1;Padding=new Padding(0);Margin=new Padding(0);TabStop=false;
   RowStyles.Add(new RowStyle(SizeType.Percent,100));
   string[] labels={"系统","浅色","深色"};
   for(int i=0;i<3;i++) {
    int index=i;ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
    var button=new UiButton {Text=labels[i],AccessibleName=i==0?"跟随系统主题":labels[i]+"主题",Dock=DockStyle.Fill,Margin=new Padding(i==0?0:4,0,0,0),TabIndex=i};
    StyleButton(button,false);choices[i]=button;Controls.Add(button,i,0);
    button.Click+=(sender,args)=>SelectedIndex=index;
    button.KeyDown+=(sender,args)=>{int next=index;if(args.KeyCode==Keys.Left)next=(index+2)%3;else if(args.KeyCode==Keys.Right)next=(index+1)%3;else if(args.KeyCode==Keys.Home)next=0;else if(args.KeyCode==Keys.End)next=2;else return;SelectedIndex=next;choices[next].Focus();args.Handled=true;args.SuppressKeyPress=true;};
    button.PreviewKeyDown+=(sender,args)=>{if(args.KeyCode==Keys.Left || args.KeyCode==Keys.Right)args.IsInputKey=true;};
   }
   SelectedIndex=0;
  }
 }
 sealed class UiToggle : CheckBox {
  public UiToggle() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true); Cursor=Cursors.Hand; }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.Clear(Parent==null?Paper:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   float scale=DeviceDpi/96f; int w=(int)(36*scale),h=(int)(20*scale),x=2,y=(Height-h)/2;
   using(var path=Round(new Rectangle(x,y,w,h),h/2)) {using(var brush=new SolidBrush(Enabled && Checked?Blue:Line))e.Graphics.FillPath(brush,path);if(Focused)using(var pen=new Pen(Blue,2))e.Graphics.DrawPath(pen,path);}
   int dot=h-6; using(var brush=new SolidBrush(Checked?Paper:Muted))e.Graphics.FillEllipse(brush,x+(Checked?w-dot-3:3),y+3,dot,dot);
   TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(w+12,0,Math.Max(0,Width-w-12),Height),Enabled?Ink:Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
  }
 }
 static void PaintDeviceCell(DataGridView grid,DataGridViewCellPaintingEventArgs e) {
  if(e.RowIndex<0)return; e.Handled=true; e.PaintBackground(e.ClipBounds,true);
  var b=grid.Rows[e.RowIndex].Tag as Binding; if(b==null)return;
  bool selected=grid.Rows[e.RowIndex].Selected; int pad=(int)(12*grid.DeviceDpi/96f); var r=e.CellBounds;
  if(e.ColumnIndex==0) {
   if(selected)using(var brush=new SolidBrush(Blue))e.Graphics.FillRectangle(brush,r.X,r.Y+12,3,r.Height-24);
   using(var bold=new Font(grid.Font,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,b.Name,bold,new Rectangle(r.X+pad,r.Y+12,r.Width-pad*2,26),Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
   TextRenderer.DrawText(e.Graphics,b.Model,grid.Font,new Rectangle(r.X+pad,r.Y+39,r.Width-pad*2,24),Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
  } else {
   string text=Convert.ToString(e.FormattedValue); Color color=e.ColumnIndex==3 && text=="已连接"?Color.FromArgb(102,179,143):Muted;
   if(e.ColumnIndex==3)text="● "+text;
   TextRenderer.DrawText(e.Graphics,text,grid.Font,Rectangle.Inflate(r,-pad,0),color,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
  }
  using(var pen=new Pen(Line))e.Graphics.DrawLine(pen,r.Left,r.Bottom-1,r.Right,r.Bottom-1);
 }
}
