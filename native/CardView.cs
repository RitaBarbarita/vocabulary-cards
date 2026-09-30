using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace Vocab
{
 public static class CardView
 {
  public static Brush StatusColor(string status){return status=="known"?Brushes.DarkGreen:status=="learning"?new SolidColorBrush(Color.FromRgb(128,96,0)):Brushes.Maroon;}
  public static WrapPanel Tags(Card c){var row=new WrapPanel();Action<string,Brush> tag=(label,bg)=>{if(String.IsNullOrWhiteSpace(label))return;row.Children.Add(new Border{Background=bg,Margin=new Thickness(0,0,3,2),Child=new TextBlock{Text=label,FontSize=10,Foreground=Brushes.White,Padding=new Thickness(3,1,3,1)}});};tag(c.level,Retro.Navy);tag(Defaults.Status(c.status),StatusColor(c.status));if(!String.IsNullOrEmpty(c.register))tag(c.register,Brushes.Gray);foreach(var s in c.sections)tag(s,Brushes.DimGray);foreach(var t in c.topics)tag(t,Brushes.DimGray);return row;}
  public static StackPanel Examples(Card c){var panel=new StackPanel{Margin=new Thickness(8)};var items=new List<UIElement>();if(!String.IsNullOrWhiteSpace(c.example)){var parts=Regex.Split(c.example,@"\r?\n|(?=\d+\.\s)").Select(s=>s.Trim()).Where(s=>s.Length>0).ToArray();for(int i=0;i<parts.Length;i++){var example=new StackPanel();example.Children.Add(new TextBlock{Text=parts[i],TextWrapping=TextWrapping.Wrap,FontStyle=FontStyles.Italic,LineHeight=18});if(i==0 && !String.IsNullOrEmpty(c.exampleTr))example.Children.Add(new TextBlock{Text=c.exampleTr,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray,FontSize=12,Margin=new Thickness(0,4,0,0)});items.Add(example);}}for(int i=0;i<c.dialogs.Count;i++){var lines=new StackPanel();if(c.dialogs.Count>1)lines.Children.Add(new TextBlock{Text="Dialogue "+(i+1),Foreground=Brushes.DimGray,FontWeight=FontWeights.Bold,FontSize=10,Margin=new Thickness(0,0,0,3)});foreach(var l in c.dialogs[i].lines){var row=new DockPanel{Margin=new Thickness(0,1,0,1)};var label=Retro.Text(l.speaker+":",true,12,l.speaker=="B"?Brushes.Maroon:Retro.Navy);label.Width=25;row.Children.Add(label);row.Children.Add(new TextBlock{Text=l.text??"",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black,LineHeight=18});lines.Children.Add(row);}items.Add(lines);}for(int i=0;i<items.Count;i++){if(i>0)panel.Children.Add(new DottedDivider());panel.Children.Add(items[i]);}return panel;}
  public static FrameworkElement Content(Store store,Card c,bool export=false)
  {
   var root=new StackPanel{Background=Retro.Silver};if(export){var title=Retro.Row(Retro.Icon("book"),Retro.Text("Card Details",true,12,Brushes.White));title.Margin=new Thickness(4,0,0,0);root.Children.Add(new Border{Height=22,Background=Retro.Navy,Margin=new Thickness(2),Child=title});}
   var inner=new StackPanel{Margin=new Thickness(18,export?10:14,18,18)};inner.Children.Add(Retro.Text(c.english,true,20));if(!String.IsNullOrEmpty(c.russian))inner.Children.Add(new TextBlock{Text=c.russian,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,FontSize=14,Margin=new Thickness(0,7,0,7)});
   var meta=new WrapPanel();Action<string,Brush> tag=(s,b)=>{if(!String.IsNullOrEmpty(s))meta.Children.Add(new Border{Background=b,Margin=new Thickness(0,0,4,2),Child=new TextBlock{Text=s,Foreground=Brushes.White,FontSize=10,Padding=new Thickness(4,2,4,2)}});};tag(c.level,Retro.Navy);tag(Defaults.Status(c.status),StatusColor(c.status));tag(c.register,Brushes.DimGray);if(c.favorite)tag("Favorite",new SolidColorBrush(Color.FromRgb(128,96,0)));inner.Children.Add(meta);
   if(!String.IsNullOrWhiteSpace(c.explanation)){inner.Children.Add(Retro.Heading("Explanation","document"));inner.Children.Add(Retro.White(c.explanation));}
   if(!String.IsNullOrWhiteSpace(c.grammar)){inner.Children.Add(Retro.Heading("Grammar note","grammar"));inner.Children.Add(Retro.White(c.grammar,true));}
   if(!String.IsNullOrWhiteSpace(c.example)||c.dialogs.Count>0){inner.Children.Add(Retro.Heading("Examples","book"));inner.Children.Add(Retro.Frame(Examples(c),true,Brushes.White));}
   if(!String.IsNullOrEmpty(c.synonyms)||!String.IsNullOrEmpty(c.antonyms)){var pair=new Grid{Margin=new Thickness(0,12,0,0)};pair.ColumnDefinitions.Add(new ColumnDefinition());pair.ColumnDefinitions.Add(new ColumnDefinition());pair.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});pair.RowDefinitions.Add(new RowDefinition());foreach(var key in new[]{"synonyms","antonyms"}){int col=key=="synonyms"?0:1;var label=Retro.Text(col==0?"Synonyms / Similar":"Antonyms / Opposite",true,11);label.Margin=new Thickness(col==0?0:5,0,col==0?5:0,5);Grid.SetColumn(label,col);pair.Children.Add(label);var panel=Retro.White((col==0?c.synonyms:c.antonyms)??"");panel.Margin=new Thickness(col==0?0:5,0,col==0?5:0,0);Grid.SetColumn(panel,col);Grid.SetRow(panel,1);pair.Children.Add(panel);}inner.Children.Add(pair);}
   if(c.sections.Count>0||c.topics.Count>0){inner.Children.Add(Retro.Heading("Tags","tag"));foreach(var list in new[]{c.sections,c.topics}){var tags=new WrapPanel();foreach(var s in list)tags.Children.Add(new Border{Background=Brushes.DimGray,Margin=new Thickness(0,0,4,3),Child=new TextBlock{Text=s,Foreground=Brushes.White,FontSize=10,Padding=new Thickness(4,2,4,2)}});inner.Children.Add(tags);}}
   if(c.attachments.Count>0){inner.Children.Add(Retro.Heading("Attached images","attach"));if(export)inner.Children.Add(Retro.White(String.Join("\n",c.attachments.Select(a=>a.name))));else foreach(var a in c.attachments)inner.Children.Add(Image(store,a,520));}
   root.Children.Add(inner);return Retro.Frame(root);
  }
  public static FrameworkElement Image(Store store,Attachment a,int maxWidth)
  {
   var p=new StackPanel{Margin=new Thickness(0,4,0,4)};p.Children.Add(Retro.Text(a.name,false,11));string file=store.ImagePath(a.id);if(!File.Exists(file)){p.Children.Add(Retro.Text("Image is unavailable.",false,11,Brushes.Gray));return p;}try{var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri(file);bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=maxWidth*2;bitmap.EndInit();bitmap.Freeze();var img=new System.Windows.Controls.Image{Source=bitmap,MaxWidth=maxWidth,MaxHeight=320,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};img.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2){var w=new RetroWindow(a.name,850,700,true);w.Body=new ScrollViewer{Content=new System.Windows.Controls.Image{Source=bitmap,Stretch=Stretch.Uniform}};w.ShowDialog();}};p.Children.Add(img);}catch{p.Children.Add(Retro.Text("Unable to display this image.",false,11,Brushes.Gray));}return p;
  }
  public static void Export(Store store,Card c){string safe=String.Concat((c.english??"card").Select(ch=>Path.GetInvalidFileNameChars().Contains(ch)?'_':ch));var d=new SaveFileDialog{Filter="PNG image (*.png)|*.png",FileName=safe+".png"};if(d.ShowDialog()==true)Retro.SavePng(Content(store,c,true),d.FileName,600,2);}
 }
 public sealed class DottedDivider:FrameworkElement{public DottedDivider(){Height=1;Margin=new Thickness(0,5,0,5);SnapsToDevicePixels=true;}protected override void OnRender(DrawingContext drawing){var pen=new Pen(new SolidColorBrush(Color.FromRgb(192,192,192)),1){DashStyle=new DashStyle(new[]{3.0,3.0},0)};drawing.DrawLine(pen,new Point(0,.5),new Point(ActualWidth,.5));}}
 public sealed class DetailWindow:RetroWindow
 {
  public event Action EditRequested;
  public DetailWindow(Store store,Card c):base(c.english,650,Math.Min(780,SystemParameters.WorkArea.Height-30),true){var dock=new DockPanel();var buttons=Retro.Row(Retro.Button("Edit","edit",()=>{if(EditRequested!=null)EditRequested();}),Retro.Button("Save as PNG","image",()=>CardView.Export(store,c)),Retro.Button("Close",null,()=>Close()));buttons.HorizontalAlignment=HorizontalAlignment.Right;buttons.Margin=new Thickness(8);DockPanel.SetDock(buttons,Dock.Bottom);dock.Children.Add(buttons);dock.Children.Add(new ScrollViewer{Content=CardView.Content(store,c),VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Body=dock;}
 }
}
