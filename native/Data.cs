using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Vocab
{
    public class Line { public string speaker {get;set;} public string text {get;set;} public Line(){speaker="A";text="";} }
    public class Dialogue { public List<Line> lines {get;set;} public string a {get;set;} public string b {get;set;} public Dialogue(){lines=new List<Line>();} }
    public class Attachment {public string id{get;set;} public string name{get;set;} public string type{get;set;} public long size{get;set;} }
    public class Card
    {
        public string id{get;set;} public string english{get;set;} public string russian{get;set;}
        public string explanation{get;set;} public string example{get;set;} public string exampleTr{get;set;}
        public string synonyms{get;set;} public string antonyms{get;set;} public string grammar{get;set;}
        public string register{get;set;} public string level{get;set;} public string status{get;set;}
        public bool favorite{get;set;} public long createdAt{get;set;}
        public List<string> sections{get;set;} public List<string> topics{get;set;}
        public List<Dialogue> dialogs{get;set;} public List<Attachment> attachments{get;set;}
        public Card(){id=Guid.NewGuid().ToString("N");english=russian=explanation=example=exampleTr=synonyms=antonyms=grammar=register=level="";status="new";createdAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();sections=new List<string>();topics=new List<string>();dialogs=new List<Dialogue>();attachments=new List<Attachment>();}
        public Card Copy(){return Json.Read<Card>(Json.Write(this));}
        public void Normalize(){sections=sections??new List<string>();topics=topics??new List<string>();attachments=attachments??new List<Attachment>();dialogs=dialogs??new List<Dialogue>();foreach(var d in dialogs){if(d.lines==null || d.lines.Count==0){d.lines=new List<Line>();if(!String.IsNullOrEmpty(d.a))d.lines.Add(new Line{speaker="A",text=d.a});if(!String.IsNullOrEmpty(d.b))d.lines.Add(new Line{speaker="B",text=d.b});}foreach(var l in d.lines){l.speaker=l.speaker=="B"?"B":"A";l.text=l.text??"";}}}
    }
    public class UserSettings {public List<string> customSections{get;set;} public List<string> customTopics{get;set;} public UserSettings(){customSections=new List<string>();customTopics=new List<string>();} }
    public class Database { public int version{get;set;} public List<Card> cards{get;set;} public UserSettings settings{get;set;} public Database(){version=2;cards=new List<Card>();settings=new UserSettings();} }
    public static class Json
    {
        public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue,RecursionLimit=200};}
        public static string Write(object value){return Serializer().Serialize(value);}
        public static T Read<T>(string text){return Serializer().Deserialize<T>(text);}
    }
    public sealed class Store
    {
        public readonly string Root;
        public Database Data;
        public readonly string FileName;
        public Store(string folder){Root=Path.GetFullPath(folder);FileName=Path.Combine(Root,"vocabulary.json");Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.Combine(Root,"images"));Data=File.Exists(FileName)?Json.Read<Database>(File.ReadAllText(FileName)):new Database();if(Data==null || Data.cards==null)throw new InvalidDataException("The vocabulary file could not be read. Your data has been kept.");Data.settings=Data.settings??new UserSettings();Data.settings.customSections=Data.settings.customSections??new List<string>();Data.settings.customTopics=Data.settings.customTopics??new List<string>();foreach(var c in Data.cards)c.Normalize();}
        public void Save(){var temp=FileName+".tmp";File.WriteAllText(temp,Json.Write(Data),new UTF8Encoding(false));if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".previous",true);else File.Move(temp,FileName);}
        public string ImagePath(string id){if(String.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || id.Contains("..") || id.Contains('/') || id.Contains('\\'))throw new InvalidDataException("Invalid image ID.");return Path.Combine(Root,"images",id);}
        public string[] Sections(){return Defaults.Sections.Concat(Data.settings.customSections).Distinct().ToArray();}
        public string[] Topics(){return Defaults.Topics.Concat(Data.settings.customTopics).Distinct().ToArray();}
        public void RegisterTags(Card c){foreach(var v in c.sections)if(!Sections().Contains(v))Data.settings.customSections.Add(v);foreach(var v in c.topics)if(!Topics().Contains(v))Data.settings.customTopics.Add(v);}
        public List<Card> Filter(string query,string level,string status,bool favorite,string section,string topic){query=(query??"").Trim();return Data.cards.Where(c=>(!favorite || c.favorite)&&(String.IsNullOrEmpty(level)||c.level==level)&&(String.IsNullOrEmpty(status)||c.status==status)&&(String.IsNullOrEmpty(section)||c.sections.Contains(section))&&(String.IsNullOrEmpty(topic)||c.topics.Contains(topic))&&(query.Length==0 || String.Join(" ",new[]{c.english,c.russian,c.explanation,c.example,c.synonyms,c.antonyms,String.Join(" ",c.sections),String.Join(" ",c.topics)}).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToList();}
    }
    public static class Defaults
    {
        public static readonly string[] Sections={"Phrases & Expressions","Verbs","Phrasal Verbs","Idioms","Slang","Adjectives","Nouns","Adverbs","Prepositions","Discourse Markers","Collocations","Grammar","Questions","Answers","Errors & Traps","Pronunciation","Mini-Dialogues"};
        public static readonly string[] Topics={"Studies","Lesson with a Teacher","Everyday Communication","Politeness","Requests & Commands","Agreement & Refusal","Opinion & Arguments","Emotions","Irritation","Humor & Irony","Relationships","Describing People","Appearance","Health","Food","Home","Shopping","Money","Work","Travel","Transport","Time","Cause & Result","Secrets & Gossip","Lies & Truth","Conflict","Compliments","Small Talk","Correspondence","Books / Writing","Arts","Sci-Fi / Space / Science","Texts & Articles","Video / Subtitles"};
        public static readonly string[] Levels={"","A1","A2","B1","B2","C1"};
        public static readonly string[] Registers={"","Neutral","Informal","Formal","Rude","Slang"};
        public static readonly string[] Statuses={"new","learning","known"};
        public static string Status(string s){return s=="known"?"Known":s=="learning"?"Learning":"New";}
    }
}
