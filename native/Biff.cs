using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace Vocab
{
 // BIFF8 / compound-document reader for legacy Excel vocabulary tables.
 public static class Biff
 {
  static uint U(byte[] d,int p){return BitConverter.ToUInt32(d,p);}static ushort S(byte[] d,int p){return BitConverter.ToUInt16(d,p);}
  public static List<List<string>> Read(string file)
  {
   byte[] doc=File.ReadAllBytes(file);if(doc.Length<512 || U(doc,0)!=0xE011CFD0)throw new InvalidDataException("This is not an Excel .xls workbook.");int sector=1<<S(doc,30),mini=1<<S(doc,32);if(sector!=512 && sector!=4096)throw new InvalidDataException("Invalid workbook sectors.");
   Func<uint,byte[]> block=n=>{long at=(long)(n+1)*sector;if(at<sector || at+sector>doc.Length)throw new InvalidDataException("Invalid workbook allocation.");var b=new byte[sector];Buffer.BlockCopy(doc,(int)at,b,0,sector);return b;};var fats=new List<uint>();for(int i=0;i<109;i++){uint n=U(doc,76+i*4);if(n<0xFFFFFFFA)fats.Add(n);}uint dif=U(doc,68);for(uint i=0;i<U(doc,72) && dif<0xFFFFFFFA;i++){var b=block(dif);for(int p=0;p<sector-4;p+=4)if(U(b,p)<0xFFFFFFFA)fats.Add(U(b,p));dif=U(b,sector-4);}var fat=fats.SelectMany(n=>{var b=block(n);return Enumerable.Range(0,sector/4).Select(i=>U(b,i*4));}).ToArray();
   Func<uint,uint[],Func<uint,byte[]>,byte[]> chain=(start,map,read)=>{var seen=new HashSet<uint>();using(var outp=new MemoryStream()){uint n=start;while(n<0xFFFFFFFA){if(n>=map.Length || !seen.Add(n))throw new InvalidDataException("Invalid workbook stream.");var b=read(n);outp.Write(b,0,b.Length);n=map[n];}return outp.ToArray();}};
   var directory=chain(U(doc,48),fat,block);uint stream=0,root=0;long length=0,rootLength=0;for(int p=0;p+128<=directory.Length;p+=128){int chars=S(directory,p+64);string name=chars>=2 && chars<=64?Encoding.Unicode.GetString(directory,p,chars-2):"";if(directory[p+66]==5){root=U(directory,p+116);rootLength=BitConverter.ToInt64(directory,p+120);}if(name=="Workbook" || name=="Book"){stream=U(directory,p+116);length=BitConverter.ToInt64(directory,p+120);}}
   if(length<=0 || length>Int32.MaxValue)throw new InvalidDataException("The workbook stream is missing.");byte[] wb;
   if(length<U(doc,56)){var miniFatBytes=chain(U(doc,60),fat,block);var miniFat=Enumerable.Range(0,miniFatBytes.Length/4).Select(i=>U(miniFatBytes,i*4)).ToArray();var rootBytes=chain(root,fat,block);wb=chain(stream,miniFat,n=>{long p=(long)n*mini;if(p+mini>rootBytes.Length)throw new InvalidDataException("Invalid small workbook stream.");var b=new byte[mini];Buffer.BlockCopy(rootBytes,(int)p,b,0,mini);return b;});}else wb=chain(stream,fat,block);Array.Resize(ref wb,(int)length);
   var strings=new List<string>();int sheet=-1;for(int p=0;p+4<=wb.Length;){int type=S(wb,p),size=S(wb,p+2),at=p+4;if(at+size>wb.Length)break;if(type==0x2F)throw new InvalidDataException("Password-protected workbooks cannot be imported.");if(type==0x85 && sheet<0 && size>=8 && wb[at+5]==0)sheet=(int)U(wb,at);if(type==0xFC){var chunks=new List<byte[]>{wb.Skip(at).Take(size).ToArray()};int next=at+size;while(next+4<=wb.Length && S(wb,next)==0x3C){int count=S(wb,next+2);chunks.Add(wb.Skip(next+4).Take(count).ToArray());next+=4+count;}var reader=new Sst(chunks);reader.Dword();uint unique=reader.Dword();if(unique>1000000)throw new InvalidDataException("Too many workbook strings.");for(uint i=0;i<unique;i++)strings.Add(reader.String());p=next;continue;}p=at+size;}
   if(sheet<0)throw new InvalidDataException("The first worksheet was not found.");var cells=new Dictionary<int,Dictionary<int,string>>();Action<int,int,string> set=(r,c,v)=>{if(r>1000000 || c>16384)throw new InvalidDataException("Worksheet too large.");if(!cells.ContainsKey(r))cells[r]=new Dictionary<int,string>();cells[r][c]=v;};
   for(int p=sheet;p+4<=wb.Length;){int type=S(wb,p),size=S(wb,p+2),at=p+4;if(at+size>wb.Length)break;if(type==0xA)break;if(size>=6){int r=S(wb,at),c=S(wb,at+2);if(type==0xFD && size>=10){int index=(int)U(wb,at+6);set(r,c,index>=0 && index<strings.Count?strings[index]:"");}else if(type==0x203 && size>=14)set(r,c,BitConverter.ToDouble(wb,at+6).ToString(System.Globalization.CultureInfo.InvariantCulture));else if(type==0x27E && size>=10)set(r,c,Rk(U(wb,at+6)));else if(type==0xBD && size>=12){int last=S(wb,at+size-2);for(int k=c;k<=last && at+4+(k-c)*6+6<=at+size-2;k++)set(r,k,Rk(U(wb,at+6+(k-c)*6)));}else if(type==0x204 && size>=9){int count=S(wb,at+6);bool wide=(wb[at+8]&1)!=0;int bytes=count*(wide?2:1);if(at+9+bytes<=at+size)set(r,c,(wide?Encoding.Unicode:Encoding.GetEncoding(1252)).GetString(wb,at+9,bytes));}else if(type==0x205 && size>=8)set(r,c,wb[at+7]==0?(wb[at+6]==0?"false":"true"):"");else if(type==6 && size>=14 && S(wb,at+12)!=0xFFFF)set(r,c,BitConverter.ToDouble(wb,at+6).ToString(System.Globalization.CultureInfo.InvariantCulture));}p=at+size;}
   return cells.OrderBy(p=>p.Key).Select(p=>Enumerable.Range(0,p.Value.Keys.Max()+1).Select(i=>p.Value.ContainsKey(i)?p.Value[i]:"").ToList()).Where(r=>r.Any(v=>v.Length>0)).ToList();
  }
  static string Rk(uint value){double number;if((value&2)!=0)number=((int)value)>>2;else number=BitConverter.Int64BitsToDouble((long)(value&0xFFFFFFFC)<<32);if((value&1)!=0)number/=100;return number.ToString(System.Globalization.CultureInfo.InvariantCulture);}
  sealed class Sst
  {
   readonly List<byte[]> chunks;int chunk,pos;public Sst(List<byte[]> data){chunks=data;}
   byte Byte(){while(chunk<chunks.Count && pos>=chunks[chunk].Length){chunk++;pos=0;}if(chunk>=chunks.Count)throw new InvalidDataException("Incomplete Excel strings.");return chunks[chunk][pos++];}
   ushort Word(){return (ushort)(Byte()|(Byte()<<8));}public uint Dword(){return (uint)(Word()|((uint)Word()<<16));}
   public string String(){int count=Word();byte flags=Byte();int runs=(flags&8)!=0?Word():0;uint extra=(flags&4)!=0?Dword():0;bool wide=(flags&1)!=0;var s=new StringBuilder();for(int i=0;i<count;i++){if(pos>=chunks[chunk].Length){chunk++;pos=0;wide=(Byte()&1)!=0;}s.Append((char)(wide?Word():Byte()));}for(int i=0;i<runs*4;i++)Byte();for(uint i=0;i<extra;i++)Byte();return s.ToString();}
  }
 }
}
