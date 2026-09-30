using System;
using System.Globalization;
using System.IO;
using System.Linq;

using System.Web.Script.Serialization;

public sealed class LanguageChoice
{
    public string Code;
    public string Name;
    public string Native;
    public override string ToString() { return Name == Native ? Name : Name + " — " + Native; }
}

internal static class AIPreferences
{
    internal const string FileName = "ai-preferences.json";
    internal static LanguageChoice[] Languages()
    {
        return CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .Where(c => c.Name.Length > 0)
            .Select(c => new LanguageChoice { Code=c.Name, Name=c.EnglishName, Native=c.NativeName })
            .OrderBy(c => c.Name).ToArray();
    }
    internal static LanguageChoice Read(string folder)
    {
        var languages=Languages();
        try {
            var data=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,FileName)));
            var selected=languages.FirstOrDefault(c=>c.Code==data["explanationLanguage"]);
            if(selected!=null)return selected;
        } catch(IOException) {} catch(ArgumentException) {} catch(InvalidOperationException) {} catch(System.Collections.Generic.KeyNotFoundException) {}
        // Existing installations used Russian; new installations follow the system language.
        string fallback=File.Exists(Path.Combine(folder,"english-vocab-db.html")) ? "ru" : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return languages.FirstOrDefault(c=>c.Code==fallback) ?? languages.First(c=>c.Code=="en");
    }
    internal static void Write(string folder,LanguageChoice language)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,FileName),new JavaScriptSerializer().Serialize(new { explanationLanguage=language.Code }));
    }
}


