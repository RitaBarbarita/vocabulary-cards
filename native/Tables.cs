using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace Vocab
{
 public static class Tables
 {
  public static readonly string[] Fields={"english","russian","synonyms","antonyms","example","explanation","grammar","sections","topics","level","status","register","favorite","dialogA","dialogB"};
  public static readonly string[] Labels={"Word or Phrase","Translation","Synonyms / Similar","Antonyms / Opposite","Example","Explanation","Grammar","Sections","Topics","Level","Status","Register","Favorite","Dialogue — A","Dialogue — B"};
  static readonly string[][] aliases={new[]{"english","word","phrase","wordorphrase","английский","слово","фраза"},new[]{"translation","russian","перевод","русский"},new[]{"synonyms","synonymssimilar","similar","синонимы"},new[]{"antonyms","antonymsopposite","opposite","антонимы"},new[]{"example","examples","пример","примеры"},new[]{"explanation","note","definition","объяснение","пояснение","значение"},new[]{"grammar","grammarnote","грамматика"},new[]{"section","sections","раздел","разделы"},new[]{"topic","topics","тема","темы"},new[]{"level","уровень"},new[]{"status","статус"},new[]{"register","регистр","стиль"},new[]{"favorite","favourite","избранное"},new[]{"dialoga","dialoguea","диалога","репликаа"},new[]{"dialogb","dialogueb","диалогб","репликаб"}};
  public static string Header(string s){return Regex.Replace((s??"").ToLowerInvariant().Trim().Replace('ё','е'),@"[^a-zа-я0-9]","");}
  public static int[] Map(IList<string> headers){return aliases.Select(a=>headers.ToList().FindIndex(h=>a.Contains(Header(h)))).ToArray();}
  public static List<List<string>> Parse(string text)
  {
   char delimiter=text.Contains('\t')?'\t':text.Split('\n').FirstOrDefault().Count(c=>c==';')>text.Split('\n').FirstOrDefault().Count(c=>c==',')?';':',';
   var rows=new List<List<string>>();var row=new List<string>();var cell=new StringBuilder();bool quoted=false;
   for(int i=0;i<text.Length;i++){char c=text[i];if(c=='"'){if(quoted && i+1<text.Length && text[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;}else if(!quoted && (c==delimiter || c=='\n' || c=='\r')){row.Add(cell.ToString());cell.Clear();if(c!=delimiter){if(c=='\r' && i+1<text.Length && text[i+1]=='\n')i++;if(row.Any(v=>v.Trim().Length>0))rows.Add(row);row=new List<string>();}}else cell.Append(c);}
   row.Add(cell.ToString());if(row.Any(v=>v.Trim().Length>0))rows.Add(row);return rows;
  }
  public static string Tsv(IEnumerable<IEnumerable<string>> rows){return String.Join("\n",rows.Select(r=>String.Join("\t",r.Select(v=>v!=null && v.IndexOfAny(new[]{'\t','\n','"'})>=0?'"'+v.Replace("\"","\"\"")+'"':v??""))));}
  public static List<string> Split(string value){return Regex.Split(value??"",@"[;|,]").Select(v=>v.Trim()).Where(v=>v.Length>0).Distinct().ToList();}
  public static string Status(string value){var s=(value??"").ToLowerInvariant();return new[]{"known","know it","знаю","изучено"}.Contains(s)?"known":new[]{"learning","almost","учу","изучаю"}.Contains(s)?"learning":"new";}
  public static List<Card> Import(List<List<string>> rows,int[] map,bool skip,Store store,out int skipped)
  {
   if(rows.Count<2 || map.Length!=Fields.Length || map[0]<0)throw new InvalidDataException("Choose the Word or Phrase column.");
   var words=new HashSet<string>(store.Data.cards.Select(c=>(c.english??"").Trim()),StringComparer.OrdinalIgnoreCase);var imported=new List<Card>();skipped=0;
   foreach(var r in rows.Skip(1)){Func<int,string> cell=i=>map[i]>=0 && map[i]<r.Count?r[map[i]].Trim():"";var word=cell(0);if(word.Length==0 || skip && words.Contains(word)){skipped++;continue;}var c=new Card{english=word,russian=cell(1),synonyms=cell(2),antonyms=cell(3),example=cell(4),explanation=cell(5),grammar=cell(6),sections=Split(cell(7)),topics=Split(cell(8)),level=cell(9),status=Status(cell(10)),register=cell(11),favorite=new[]{"true","yes","1","да","избранное","★"}.Contains(cell(12).ToLowerInvariant())};if(cell(13).Length>0 || cell(14).Length>0){var d=new Dialogue();if(cell(13).Length>0)d.lines.Add(new Line{speaker="A",text=cell(13)});if(cell(14).Length>0)d.lines.Add(new Line{speaker="B",text=cell(14)});c.dialogs.Add(d);}imported.Add(c);words.Add(word);store.RegisterTags(c);}
   store.Data.cards.InsertRange(0,imported);store.Save();return imported;
  }
  public static IEnumerable<string[]> Export(IEnumerable<Card> cards){yield return Labels.Take(13).ToArray();foreach(var c in cards)yield return new[]{c.english,c.russian,c.synonyms,c.antonyms,c.example,c.explanation,c.grammar,String.Join("; ",c.sections),String.Join("; ",c.topics),c.level,Defaults.Status(c.status),c.register,c.favorite?"Yes":"No"};}
  static readonly XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  public static string Column(int i){string result="";for(i++;i>0;i=(i-1)/26)result=(char)('A'+(i-1)%26)+result;return result;}
  public static void WriteXlsx(string file,IEnumerable<IEnumerable<string>> data)
  {
   using(var output=File.Open(file,FileMode.Create,FileAccess.Write))using(var archive=new ZipArchive(output,ZipArchiveMode.Create)){
    Action<string,string> put=(name,text)=>{using(var s=new StreamWriter(archive.CreateEntry(name).Open(),new UTF8Encoding(false)))s.Write(text);};
    put("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
    put("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
    put("xl/workbook.xml","<workbook xmlns=\""+ns+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Vocabulary\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
    put("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
    int row=0;var sheet=new XElement(ns+"worksheet",new XElement(ns+"sheetViews",new XElement(ns+"sheetView",new XAttribute("workbookViewId",0),new XElement(ns+"pane",new XAttribute("ySplit",1),new XAttribute("topLeftCell","A2"),new XAttribute("state","frozen")))),new XElement(ns+"cols",Enumerable.Range(1,13).Select(i=>new XElement(ns+"col",new XAttribute("min",i),new XAttribute("max",i),new XAttribute("width",i<3?28:35),new XAttribute("customWidth",1)))),new XElement(ns+"sheetData",data.Select(r=>new XElement(ns+"row",new XAttribute("r",++row),r.Select((v,i)=>new XElement(ns+"c",new XAttribute("r",Column(i)+row),new XAttribute("t","inlineStr"),new XElement(ns+"is",new XElement(ns+"t",new XAttribute(XNamespace.Xml+"space","preserve"),v??""))))))));put("xl/worksheets/sheet1.xml",new XDocument(sheet).ToString());
   }
  }
  public static List<List<string>> ReadFile(string file){var ext=Path.GetExtension(file).ToLowerInvariant();return ext==".xlsx"?ReadXlsx(file):ext==".xls"?Biff.Read(file):Parse(File.ReadAllText(file));}
  public static List<List<string>> ReadXlsx(string file)
  {
   using(var zip=ZipFile.OpenRead(file)){
    Func<string,XDocument> xml=name=>{var entry=zip.GetEntry(name);if(entry==null)return null;using(var s=entry.Open())return XDocument.Load(s);};
    var strings=xml("xl/sharedStrings.xml");var shared=strings==null?new List<string>():strings.Descendants(ns+"si").Select(si=>String.Concat(si.Descendants(ns+"t").Select(t=>(string)t))).ToList();
    var book=xml("xl/workbook.xml");var rels=xml("xl/_rels/workbook.xml.rels");XNamespace rn="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    var first=book.Descendants(ns+"sheet").First();var target=rels.Root.Elements().First(e=>(string)e.Attribute("Id")== (string)first.Attribute(rn+"id")).Attribute("Target").Value;var uri=new Uri(new Uri("http://local/xl/"),target);string sheetPath=uri.AbsolutePath.TrimStart('/');var sheet=xml(sheetPath);if(sheet==null)throw new InvalidDataException("The first worksheet is missing.");
    var rows=new List<List<string>>();foreach(var row in sheet.Descendants(ns+"row")){var result=new List<string>();foreach(var cell in row.Elements(ns+"c")){var address=(string)cell.Attribute("r")??"A1";int col=0;foreach(char c in address.TakeWhile(Char.IsLetter))col=col*26+Char.ToUpperInvariant(c)-'A'+1;if(col>20000)throw new InvalidDataException("Worksheet is too wide.");while(result.Count<col)result.Add("");var type=(string)cell.Attribute("t");var v=(string)cell.Element(ns+"v")??"";int index;if(type=="s" && Int32.TryParse(v,out index) && index>=0 && index<shared.Count)v=shared[index];else if(type=="inlineStr")v=String.Concat(cell.Descendants(ns+"t").Select(t=>(string)t));if(col>0)result[col-1]=v;}if(result.Any(v=>v.Length>0))rows.Add(result);}return rows;
   }
  }
 }
}

