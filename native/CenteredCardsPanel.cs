using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace Vocab
{
 public sealed class CenteredCardsPanel:Panel
 {
  protected override Size MeasureOverride(Size available){double width=available.Width;if(Double.IsInfinity(width))width=342*Math.Max(1,Children.Count);double x=0,rowHeight=0,height=0;foreach(UIElement child in Children){child.Measure(new Size(width,Double.PositiveInfinity));var size=child.DesiredSize;if(x>0 && x+size.Width>width+.1){height+=rowHeight;x=rowHeight=0;}x+=size.Width;rowHeight=Math.Max(rowHeight,size.Height);}return new Size(width,height+rowHeight);}
  protected override Size ArrangeOverride(Size area){var row=new List<UIElement>();double width=0,height=0,y=0;Action place=()=>{double x=Math.Max(0,(area.Width-width)/2);foreach(var child in row){child.Arrange(new Rect(x,y,child.DesiredSize.Width,height));x+=child.DesiredSize.Width;}y+=height;row.Clear();width=height=0;};foreach(UIElement child in Children){if(row.Count>0 && width+child.DesiredSize.Width>area.Width+.1)place();row.Add(child);width+=child.DesiredSize.Width;height=Math.Max(height,child.DesiredSize.Height);}if(row.Count>0)place();return area;}
 }
}
