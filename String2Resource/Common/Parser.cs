using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace String2Resources
{
    public static class Parser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="patterns"></param>
        /// <returns></returns>
        public static List<FileInfo> GetAllFiles(string path, string[] patterns)
        {

            var files = new List<string>();
            var fileInfos = new List<FileInfo>();

            foreach (string pattern in patterns)
                files.AddRange(Directory.GetFiles(path, pattern, SearchOption.AllDirectories  /* SearchOption.TopDirectoryOnly */));

            foreach (string file in files.Distinct())
                fileInfos.Add(new FileInfo(file));

            return fileInfos;
        }

        /// <summary>
        /// Parse file to extract strings
        /// </summary>
        /// <param name="fi">File to parse</param>
        /// <param name="findTemplates">Regexes finding strings</param>
        /// <param name="excludeTemplates">Regexes to exclude from search</param>
        /// <param name="findOptions">Case sensitive finding?</param>
        /// <param name="ignoreOptions">Case sensitive ignoring?</param>
        /// <returns></returns>
        public static List<ParseResult> GetStrings(FileInfo fi, List<string> findTemplates, List<string> excludeTemplates, RegexOptions findOptions, RegexOptions ignoreOptions)
        {
            var results = new List<ParseResult>();
            try
            {

                Int32 lineNumber = 0;
                using var inFile = new StreamReader(fi.FullName);
                string? inLine;
                while ((inLine = inFile.ReadLine()) != null)
                {

                    if (inLine.TrimStart().StartsWith("'") || inLine.TrimStart().StartsWith("////"))
                    {
                        // commented line, ignore
                        ++lineNumber;
                        continue;
                    }

                    if (inLine.Contains('"'))
                    {
                        bool replace = true;
                        var line = new ParseResult() { LineContent = inLine, LineNumber = lineNumber, Quotes = new List<Int32>() };

                        // get qoute locations
                        foreach (Match match in Regex.Matches(inLine, "\"")) line.Quotes.Add(match.Index);

                        if (findTemplates.Count > 0)
                        {
                            // check if we ignore this line
                            foreach (var ignore in excludeTemplates)
                            {
                                var contains = Regex.Matches(inLine, ignore, ignoreOptions);
                                if (contains.Count > 0)
                                {
                                    replace = false;
                                    break;
                                }
                            }

                            // does the line contain what we looking for?
                            if (replace)
                            {
                                replace = false;
                                foreach (string find in findTemplates)
                                {
                                    if (Regex.Matches(inLine, find, findOptions).Count > 0)
                                    {
                                        replace = true;
                                        break;
                                    }
                                }
                            }

                            // if so, get all string from that line
                            if (replace) ParseLine(ref line);
                        }
                        else
                        {
                            ParseLine(ref line);
                        }
                        results.Add(line);
                    }
                    ++lineNumber;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Parse error");
            }

            return results;
        }


        /// <summary>
        /// Get hard coded strings from source code line
        /// </summary>
        /// <param name="result"></param>
        public static void ParseLine(ref ParseResult result)
        {
            foreach (Match str in Regex.Matches(result.LineContent, "\"[^\"]*[\"]"))
            {
                if (!string.IsNullOrWhiteSpace(str.Value) && str.Value != "\"\"")
                {
                    if (!result.ReplaceFinds.Contains(str.Value))
                    {
                        ++result.ReplaceCount;
                        result.ReplaceFinds.Add(str.Value);
                        result.ToResource = true;
                    }
                }

            }
        }


        /// <summary>
        /// Add selected hard coded strings to seleced resource file
        /// </summary>
        /// <param name="files">Files selected to be parsed</param>
        /// <param name="selectedResource">Resource file to be used</param>
        /// <param name="parsedFiles">Preparsed (possibly manually changed) sources</param>
        /// <param name="findTemplates">Regexes for finding replacements</param>
        /// <param name="findOptions">Case sensitive find?</param>
        /// <param name="excludeTemplates">Regexes for ignoring lines</param>
        /// <param name="ignoreOptions">Case sensitive ignore?</param>
        /// <param name="progressBarFile">Progress bar for files progress</param>
        /// <param name="progressBarAll">Progress bar for filecontent progress</param>
        /// <returns></returns>
        internal static bool AddToResourceFile(List<FileInfo> files, FileInfo selectedResource, Dictionary<FileInfo, List<ParseResult>> parsedFiles,
                                                     List<string> findTemplates, RegexOptions findOptions, List<string> excludeTemplates, RegexOptions ignoreOptions, ProgressBar progressBarFile, ProgressBar progressBarAll)
        {

            var filesToSwap = new List<(string original, string tmp)>();
            try
            {
                string targetResourceFilePath = selectedResource.FullName;
                string resourceFileName = Path.GetFileNameWithoutExtension(targetResourceFilePath);
                string temporaryFilePath = Path.GetTempFileName(); //temp resource file path
                using ResXResourceReader originalResource = new(targetResourceFilePath)
                {
                    BasePath = Path.GetDirectoryName(targetResourceFilePath),
                    UseResXDataNodes = true
                };
                List<(string key, string value)> newResourceStrings = [];



                progressBarAll.Maximum = files.Count;
                progressBarAll.Value = 0;


                foreach (FileInfo fi in files)
                {
                    int lineNumber = 0;
                    int lineCount = 0;


                    // use the form name as a resource prefix
                    var resourcePrefix = fi.Name.Replace(fi.Extension, string.Empty).Replace(".", "_") + "_";

                    lineCount = BackupSourceFile(fi);
                    ++progressBarAll.Value;
                    progressBarAll.Update();
                    progressBarFile.Maximum = lineCount + 1;
                    progressBarFile.Value = 0;

                    var preParsed = parsedFiles.FirstOrDefault(c => c.Key == fi);
                    List<ParseResult> replacements;
                    if (preParsed.Value == null)
                        replacements = GetStrings(fi, findTemplates, excludeTemplates, findOptions, ignoreOptions);
                    else
                        replacements = preParsed.Value;

                    if (replacements.Count > 0)
                    {
                        //existing source file (backup)
                        var originalpath = fi.FullName;
                        var temporarysourcepath = Path.GetTempFileName();
                        using var backupFile = new StreamReader(originalpath);
                        //new sourcefile
                        using var sourceCodeFile = new StreamWriter(temporarysourcepath, false);
                        string? inLine;
                        while ((inLine = backupFile.ReadLine()) != null)
                        {
                            ParseResult? moveToResource = replacements.FirstOrDefault(c => c.LineNumber == lineNumber);

                            if (moveToResource != null && moveToResource.ReplaceFinds.Count > 0)
                                ReplaceStringsInCode(originalResource, newResourceStrings, fi.Extension, ref inLine, resourcePrefix, ref moveToResource, resourceFileName);

                            sourceCodeFile.WriteLine(inLine);

                            ++progressBarFile.Value;
                            progressBarFile.Update();
                            ++lineNumber;
                        }
                        filesToSwap.Add((originalpath, temporarysourcepath));
                    }
                }
                using var updatedResource = new ResXResourceWriter(temporaryFilePath);
                updatedResource.BasePath = originalResource.BasePath;
                foreach (DictionaryEntry resource in originalResource)
                    updatedResource.AddResource((ResXDataNode)resource.Value);

                foreach (var (key, value) in newResourceStrings)
                    updatedResource.AddResource(new ResXDataNode(key, value));

                filesToSwap.Add((targetResourceFilePath, temporaryFilePath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
            //move old resource file to backup and place new tmp file to vacant place
            return ReplaceOriginalFiles(filesToSwap);
        }

        /// <summary>
        /// Apply file changes in one batch
        /// </summary>
        /// <param name="filesToSwap"></param>
        /// <returns></returns>
        private static bool ReplaceOriginalFiles(List<(string original, string tmp)> filesToSwap)
        {
            try
            {
                foreach (var (original, tmp) in filesToSwap)
                {
                    var resourceBackup = original + ".bak";
                    File.Delete(resourceBackup);
                    File.Move(original, resourceBackup);
                    File.Move(tmp, original);
                    File.SetLastWriteTimeUtc(original, DateTime.UtcNow); //push update time
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
            return true;
        }

        static string? SearchInOriginalResources(ResXResourceReader original, string value)
        {
            foreach (DictionaryEntry resource in original)
            {
                var resourcevalue = resource.Value;
                if (resourcevalue is ResXDataNode content)
                {
                    if ((string?)content?.GetValue((ITypeResolutionService?)null) == value)
                        return (string)resource.Key;
                }
            }
            return null;
        }
        static string? SearchInNewResources(List<(string key, string value)> newResourceStrings, string value)
        {
            return newResourceStrings.FirstOrDefault(i => i.value == value).key;
        }
        private static void ReplaceStringsInCode(ResXResourceReader original, List<(string key, string value)> newResourceStrings, string ext, ref string inLine, string resxKeyName, ref ParseResult moveToResource, string resourceFileName)
        {
            foreach (string replacment in moveToResource.ReplaceFinds)
            {
                var cleanReplace = replacment.Trim('"'); //.Replace("\"", "");    // Resource value is string without quotes!

                var resourceKey = SearchInNewResources(newResourceStrings, cleanReplace);
                resourceKey ??= SearchInOriginalResources(original, cleanReplace);
                if (resourceKey == null)
                {
                    resourceKey = NewResourceKey(resxKeyName, cleanReplace, newResourceStrings);
                    newResourceStrings.Add((resourceKey, cleanReplace));
                }

                var replacementString = ext.Equals("vb", StringComparison.CurrentCultureIgnoreCase)
                    ? $"My.Resources.{resourceKey}"
                    : $"Properties.{resourceFileName}.{resourceKey}";
                inLine = inLine.Replace(replacment, replacementString);

            }
        }

        private static string NewResourceKey(string resxKeyName, string cleanReplace, List<(string key, string value)> resxEntries)
        {
            var cleankey = cleanReplace.Replace(' ', '_');
            cleankey = new string(cleankey.Where(c => char.IsAsciiLetterOrDigit(c) || c == '_').ToArray());

            if (IsValidMemberName(cleankey)) //chek result is valid C# member name
            {
                var defaultid = $"{resxKeyName}_{cleankey}";
                if (!resxEntries.Any(i => i.key == defaultid))
                    return defaultid;
                int resourceId = 0;
                while (true)
                {
                    var testkey = $"{defaultid}{++resourceId:#0000}";
                    if (!resxEntries.Any(i => i.key == testkey))
                        return testkey;
                }
            }
            else
            {
                int resourceId = 0;
                while (true)
                {
                    var testkey = $"{resxKeyName}_{++resourceId:#0000}";
                    if (!resxEntries.Any(i => i.key == testkey))
                        return testkey;
                }
            }
            throw new Exception("can't valid generate resource id");
            static bool IsValidMemberName(string name)
            {
                return SyntaxFacts.IsValidIdentifier(name) &&
                       SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;
            }
        }


        /// <summary>
        /// Backup sourcefile
        /// </summary>        
        /// <param name="fi"></param>
        /// <returns></returns>
        private static int BackupSourceFile(FileInfo fi)
        {
            Int32 lineCount = 0;
            // for using the progress bar..
            using (var sourceFile = new StreamReader(fi.FullName))
            {
                string? inLine;
                while ((inLine = sourceFile.ReadLine()) != null) ++lineCount;
            }
            if (File.Exists(fi.FullName + ".bak")) File.Delete(fi.FullName + ".bak");
            File.Copy(fi.FullName, fi.FullName + ".bak");
            return lineCount;
        }

    }
}
