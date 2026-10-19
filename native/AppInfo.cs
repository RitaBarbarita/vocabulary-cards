using System;
using System.IO;

namespace Vocab
{
    internal static class AppInfo
    {
        internal const string Name = "Vocabulary Database";
        internal const string Version = "1.1";
        internal const string AssemblyVersion = "1.1.0.0";
        internal const string ExecutableName = "VocabularyDatabase.exe";
        internal const string Contact = "systemfolder.dev@gmail.com";
        internal const string LegacyName = "English Vocab DB";
        internal const string LegacyExecutableName = "EnglishVocabDB.exe";
        // Keep the storage and registry identities so upgrades retain existing data.
        internal const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EnglishVocabDB";
        internal static string DataDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyName);
            }
        }

        internal static string InstallDirectory(string programsDirectory)
        {
            string legacy = Path.Combine(programsDirectory, LegacyName);
            return Directory.Exists(legacy) ? legacy : Path.Combine(programsDirectory, Name);
        }
    }
}
