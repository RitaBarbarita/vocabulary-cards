using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Interop;
using System.Runtime.InteropServices;
namespace Vocab
{
 public static class Retro
 {
  public static readonly Brush Silver=new SolidColorBrush(Color.FromRgb(192,192,192)),Navy=new SolidColorBrush(Color.FromRgb(0,0,128));
  static readonly Dictionary<string,BitmapSource> images=new Dictionary<string,BitmapSource>();
  public static void Init(){using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("Theme.xaml")){Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(input));}}
  public static BitmapSource Bitmap(string name){BitmapSource img;if(images.TryGetValue(name,out img))return img;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Icons."+name+".png")){if(stream==null)return null;var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=stream;b.EndInit();b.Freeze();images[name]=b;return b;}}
  public static Image Icon(string name,int size=16){var i=new Image{Source=Bitmap(name),Width=size,Height=size,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,4,0)};RenderOptions.SetBitmapScalingMode(i,BitmapScalingMode.NearestNeighbor);return i;}
  public static TextBlock Text(string value,bool bold=false,double size=12,Brush color=null){return new TextBlock{Text=value??"",FontWeight=bold?FontWeights.Bold:FontWeights.Normal,FontSize=size,Foreground=color??Brushes.Black,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
  public static Button Button(string text,string icon,Action action){var b=new Button{HorizontalContentAlignment=HorizontalAlignment.Center,ToolTip=text};var row=new StackPanel{Orientation=Orientation.Horizontal};if(icon!=null)row.Children.Add(Icon(icon));if(!String.IsNullOrEmpty(text))row.Children.Add(Text(text));b.Content=row;if(action!=null)b.Click+=(s,e)=>{try{action();}catch(Exception error){Message(error.Message);}};return b;}
  public static Button Tiny(string icon,string tip,Action action){var b=Button("",icon,action);b.ToolTip=tip;b.Padding=new Thickness(3,1,0,1);b.MinHeight=22;return b;}
  public static Button Caption(string icon,string tip,Action action){var b=Button("",null,action);var image=Icon(icon);image.Margin=new Thickness(0);b.Name="Caption"+tip;b.Content=image;b.ToolTip=tip;b.Width=20;b.Height=b.MinHeight=18;b.Padding=new Thickness(0);b.Margin=new Thickness(0,3,2,3);WindowChrome.SetIsHitTestVisibleInChrome(b,true);return b;}
  public static Border Frame(UIElement content,bool inset=false,Brush background=null){var inner=new Border{BorderBrush=inset?Brushes.White:new SolidColorBrush(Color.FromRgb(64,64,64)),BorderThickness=new Thickness(0,0,1,1),Child=content};return new Border{BorderBrush=inset?new SolidColorBrush(Color.FromRgb(128,128,128)):Brushes.White,BorderThickness=new Thickness(1,1,0,0),Background=background??Silver,Child=inner,SnapsToDevicePixels=true};}
  public static StackPanel Row(params UIElement[] items){var row=new StackPanel{Orientation=Orientation.Horizontal};foreach(var i in items)row.Children.Add(i);return row;}
  public static UIElement Heading(string label,string icon=null){var p=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,5)};p.Children.Add(new Border{Width=3,Height=16,Background=Navy,Margin=new Thickness(0,0,8,0)});if(icon!=null)p.Children.Add(Icon(icon));p.Children.Add(Text(label,true,12,Navy));return p;}
  public static FrameworkElement White(string value,bool italic=false){return Frame(new TextBlock{Text=value??"",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black,FontStyle=italic?FontStyles.Italic:FontStyles.Normal,Margin=new Thickness(8),LineHeight=18},true,Brushes.White);}
  public static string Prompt(string title,string value=""){var w=new RetroWindow(title,420,160);var box=new TextBox{Text=value,Margin=new Thickness(10)};var p=new StackPanel();p.Children.Add(box);var r=Row(Button("OK",null,()=>{w.DialogResult=true;}),Button("Cancel",null,()=>{w.DialogResult=false;}));r.HorizontalAlignment=HorizontalAlignment.Right;r.Margin=new Thickness(10);p.Children.Add(r);w.Body=p;box.Focus();return w.ShowDialog()==true?box.Text:null;}
  public static void Message(string text){var w=new RetroWindow("English Vocab DB",480,210);var p=new StackPanel{Margin=new Thickness(14)};p.Children.Add(Text(text));var b=Button("OK",null,()=>w.Close());b.HorizontalAlignment=HorizontalAlignment.Right;b.Margin=new Thickness(0,16,0,0);p.Children.Add(b);w.Body=p;w.ShowDialog();}
  public static bool Confirm(string text){var w=new RetroWindow("English Vocab DB",450,200);var p=new StackPanel{Margin=new Thickness(14)};p.Children.Add(Text(text));var r=Row(Button("Yes",null,()=>w.DialogResult=true),Button("No",null,()=>w.DialogResult=false));r.HorizontalAlignment=HorizontalAlignment.Right;r.Margin=new Thickness(0,16,0,0);p.Children.Add(r);w.Body=p;return w.ShowDialog()==true;}
  public static void SavePng(FrameworkElement visual,string path,int width,int scale=1){visual.Measure(new Size(width,Double.PositiveInfinity));visual.Arrange(new Rect(0,0,width,visual.DesiredSize.Height));visual.UpdateLayout();int height=(int)Math.Ceiling(visual.ActualHeight);if(height<=0 || height*scale>60000)throw new InvalidOperationException("The card is too large to export.");var bitmap=new RenderTargetBitmap(width*scale,height*scale,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(path))png.Save(output);}
 }
 public class RetroWindow:Window
 {
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int x,y;}
  [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int left,top,right,bottom;}
  [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int size;public NativeRect monitor,work;public uint flags;}
  [StructLayout(LayoutKind.Sequential)]struct MinMaxInfo{public NativePoint reserved,maxSize,maxPosition,minTrack,maxTrack;}
  [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
  IntPtr WorkAreaBounds(IntPtr window,int message,IntPtr word,IntPtr pointer,ref bool handled){if(message==0x24){var monitor=new MonitorInfo{size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(MonitorFromWindow(window,2),ref monitor)){var bounds=(MinMaxInfo)Marshal.PtrToStructure(pointer,typeof(MinMaxInfo));bounds.maxPosition.x=monitor.work.left-monitor.monitor.left;bounds.maxPosition.y=monitor.work.top-monitor.monitor.top;bounds.maxSize.x=monitor.work.right-monitor.work.left;bounds.maxSize.y=monitor.work.bottom-monitor.work.top;var target=HwndSource.FromHwnd(window).CompositionTarget;var dpi=target==null?Matrix.Identity:target.TransformToDevice;bounds.minTrack.x=Math.Max(bounds.minTrack.x,(int)Math.Ceiling(MinWidth*dpi.M11));bounds.minTrack.y=Math.Max(bounds.minTrack.y,(int)Math.Ceiling(MinHeight*dpi.M22));bounds.maxTrack.x=Math.Max(bounds.maxTrack.x,bounds.maxSize.x);bounds.maxTrack.y=Math.Max(bounds.maxTrack.y,bounds.maxSize.y);Marshal.StructureToPtr(bounds,pointer,false);handled=true;}}return IntPtr.Zero;}
  readonly ContentControl body=new ContentControl();
  public UIElement Body{get{return body.Content as UIElement;}set{body.Content=value;}}
  public RetroWindow(string title,double width=600,double height=700,bool resize=false)
  {
   Title=title;Width=width;Height=height;MinWidth=360;MinHeight=130;WindowStyle=WindowStyle.None;ResizeMode=resize?ResizeMode.CanResize:ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;UseLayoutRounding=true;SnapsToDevicePixels=true;Icon=Retro.Bitmap("app");
   WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=28,ResizeBorderThickness=new Thickness(resize?4:0),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false,NonClientFrameEdges=NonClientFrameEdges.None});
   SourceInitialized+=(s,e)=>{var handle=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(handle).AddHook(WorkAreaBounds);int squareCorners=1;DwmSetWindowAttribute(handle,33,ref squareCorners,sizeof(int));};
   var dock=new DockPanel{Margin=new Thickness(3)};var head=new Grid{Background=Retro.Navy,Height=24};head.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});head.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var name=Retro.Row(Retro.Icon("app"),Retro.Text(title,true,12,Brushes.White));name.Margin=new Thickness(4,0,0,0);head.Children.Add(name);
   var controls=new StackPanel{Orientation=Orientation.Horizontal};if(resize){controls.Children.Add(Retro.Caption("minimize","Minimize",()=>SystemCommands.MinimizeWindow(this)));controls.Children.Add(Retro.Caption("maximize","Maximize",()=>{if(WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(this);else SystemCommands.MaximizeWindow(this);}));}
   controls.Children.Add(Retro.Caption("close","Close",()=>Close()));Grid.SetColumn(controls,1);head.Children.Add(controls);DockPanel.SetDock(head,Dock.Top);dock.Children.Add(head);dock.Children.Add(body);Content=Retro.Frame(dock);PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape && !resize)Close();};
  }
 }
 public class SquareMeter:FrameworkElement
 {
  public int Value;public bool Active;int frame;readonly System.Windows.Threading.DispatcherTimer timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(130)};
  public SquareMeter(){Height=27;timer.Tick+=(s,e)=>{frame++;if(Active)InvalidateVisual();};Loaded+=(s,e)=>timer.Start();Unloaded+=(s,e)=>timer.Stop();}
  public void Set(int percent,bool active){Value=Math.Max(0,Math.Min(100,percent));Active=active;InvalidateVisual();}
  protected override void OnRender(DrawingContext c){base.OnRender(c);int n=Math.Max(1,(int)(ActualWidth-8)/18);c.DrawRectangle(Retro.Silver,new Pen(Brushes.Gray,1),new Rect(1,1,Math.Max(0,ActualWidth-2),25));for(int i=0;i<n;i++){bool lit=Value>0?i<Math.Ceiling(n*Value/100.0):Active && (i-frame%n+n)%n<3;c.DrawRectangle(lit?Retro.Navy:Retro.Silver,null,new Rect(4+i*18,5,14,17));}}
 }
}
