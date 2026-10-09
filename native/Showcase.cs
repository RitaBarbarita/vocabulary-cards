using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Vocab
{
 public static class Showcase
 {
  // These demonstration cards are rendered only on explicit request, in isolated storage.
  public static int Run(string folder)
  {
   RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
   folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);
   try {
    var store=new Store(Path.Combine(folder,"demo-"+Guid.NewGuid().ToString("N")));
    var featured=new Card{english="take it with a grain of salt",russian="tomarlo con reservas",explanation="No creer algo por completo; mantener una actitud un poco escéptica. Se usa cuando la información puede ser exagerada o poco fiable.",grammar="take + something + with a grain of salt\nBritish English: take it with a pinch of salt.",example="1. Take that rumour with a grain of salt.\n2. I take online reviews with a grain of salt.\n3. You should take his advice with a grain of salt.",exampleTr="Toma ese rumor con reservas.",synonyms="be sceptical; doubt; question",antonyms="believe completely; take at face value",level="B2",register="Neutral",status="learning",favorite=true,sections=new List<string>{"Idioms"},topics=new List<string>{"Lies & Truth"},dialogs=new List<Dialogue>{new Dialogue{lines=new List<Line>{new Line{speaker="A",text="Did you hear that the café is closing?"},new Line{speaker="B",text="I'd take that with a grain of salt. It was busy yesterday."}}}}};
    store.Data.cards.Add(featured);
    Add(store,"a breath of fresh air","un soplo de aire fresco","Her new ideas are a breath of fresh air.","B2","new","Idioms","Work");
    Add(store,"get the hang of it","cogerle el truco","You'll get the hang of it with a little practice.","B1","known","Phrases & Expressions","Studies");
    Add(store,"make yourself at home","siéntete como en casa","Come in and make yourself at home.","A2","known","Phrases & Expressions","Politeness");
    Add(store,"look forward to","tener ganas de","I'm looking forward to our weekend trip.","B1","learning","Phrasal Verbs","Travel");
    Add(store,"on the same page","estar de acuerdo","Let's make sure we're on the same page.","B2","new","Idioms","Work");
    Add(store,"better late than never","más vale tarde que nunca","You're here at last! Better late than never.","B1","known","Phrases & Expressions","Everyday Communication");
    Add(store,"break the ice","romper el hielo","A friendly question can help break the ice.","B1","learning","Idioms","Relationships");
    Add(store,"a piece of cake","pan comido","The exam was a piece of cake.","A2","new","Idioms","Studies");
    store.Save();var language=AIPreferences.Languages().First(l=>l.Code=="es");
    var main=new MainWindow(store,language){Width=1280,Height=790};NativeTests.ShowOffscreen(main);
    Capture(main,folder,"01-cards.png","A little order for every new word.","Cards, favorites and filters — in a familiar Windows interface.");
    main.Switch("table");Capture(main,folder,"04-table.png","The whole collection, at a glance.","Sort your vocabulary and bring it in or out with Excel.");
    main.Switch("flash");main.UpdateLayout();var flash=Find<FlashPanel>((DependencyObject)main.Content);flash.Start(false);flash.Flip();
    Capture(main,folder,"05-flashcards.png","Make the words stick.","Reveal the answer, check your progress and keep practising.");main.Close();
    var detail=new DetailWindow(store,featured){Width=780,Height=690};NativeTests.ShowOffscreen(detail);
    Capture(detail,folder,"02-details.png","More than a translation.","Examples, grammar, dialogues and word connections, together.");detail.Close();
    var editor=new EditorWindow(store,language,featured){Width=800,Height=815};NativeTests.ShowOffscreen(editor);
    Capture(editor,folder,"03-editor.png","Room for your own understanding.","Write it yourself or fill a draft with optional local AI.");editor.Close();
    File.WriteAllText(Path.Combine(folder,"screenshots.json"),Json.Write(new{version="1.0",contact="systemfolder.dev@gmail.com",images=5,data="Synthetic demonstration cards; no personal database accessed"}));return 0;
   } catch(Exception error){File.WriteAllText(Path.Combine(folder,"screenshots-error.txt"),error.ToString());return 1;}
  }
  static void Add(Store store,string word,string translation,string example,string level,string status,string section,string topic)
  {
   store.Data.cards.Add(new Card{english=word,russian=translation,example=example,level=level,status=status,register="Neutral",favorite=word=="look forward to",sections=new List<string>{section},topics=new List<string>{topic}});
  }
  static T Find<T>(DependencyObject root) where T:DependencyObject
  {
   if(root is T)return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var result=Find<T>(VisualTreeHelper.GetChild(root,i));if(result!=null)return result;}return null;
  }
  static void Text(DrawingContext drawing,string value,double size,Brush color,double x,double y,bool bold=false)
  {
   var typeface=new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal);
   drawing.DrawText(new FormattedText(value,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,typeface,size,color,1.5),new Point(x,y));
  }
  static void Capture(Window window,string folder,string name,string heading,string note)
  {
   window.UpdateLayout();var root=(FrameworkElement)window.Content;
   var screen=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*1.5),(int)Math.Ceiling(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);screen.Render(root);
   var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){
    drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(0,112,112)),null,new Rect(0,0,1440,1080));
    drawing.DrawImage(Retro.Bitmap("app"),new Rect(110,43,32,32));
    Text(drawing,"Vocab Cards",29,Brushes.White,156,36,true);
    Text(drawing,"1.0",12,new SolidColorBrush(Color.FromRgb(197,226,226)),344,51);
    Text(drawing,heading,18,Brushes.White,110,87);
    double x=(1440-window.ActualWidth)/2,y=138;
    drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(0,75,75)),null,new Rect(x+9,y+9,window.ActualWidth,window.ActualHeight));
    drawing.DrawImage(screen,new Rect(x,y,window.ActualWidth,window.ActualHeight));
    Text(drawing,note,15,new SolidColorBrush(Color.FromRgb(217,236,236)),110,989);
    Text(drawing,"WINDOWS  ·  LOCAL AI  ·  YOUR DATA STAYS WITH YOU",10,new SolidColorBrush(Color.FromRgb(170,208,208)),110,1024);
   }
   var output=new RenderTargetBitmap(2160,1620,144,144,PixelFormats.Pbgra32);output.Render(visual);
   var pixels=new byte[2160*1620*4];output.CopyPixels(pixels,2160*4,0);int titlePixels=0;
   for(int y=54;y<115;y++)for(int x=234;x<515;x++){int p=(y*2160+x)*4;if(pixels[p]>220 && pixels[p+1]>220 && pixels[p+2]>220)titlePixels++;}
   if(titlePixels<400)throw new InvalidOperationException("Screenshot heading did not render: "+name);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(output));using(var file=File.Create(Path.Combine(folder,name)))encoder.Save(file);
  }
 }
}
