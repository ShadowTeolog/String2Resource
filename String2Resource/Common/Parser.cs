using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
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
                using (StreamReader inFile = new StreamReader(fi.FullName))
                {
                    string inLine;
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
        internal static bool AddToResourceFile(List<FileInfo> files, FileInfo selectedResource, Dictionary<FileInfo, List<ParseResult>> parsedFiles /* List<ParseResult> replacements */ ,
                                                     ref List<string> findTemplates, RegexOptions findOptions, ref List<string> excludeTemplates, RegexOptions ignoreOptions, ref ProgressBar progressBarFile, ref ProgressBar progressBarAll)
        {
            Hashtable resxEntries = GetResourceEntries(ref selectedResource);

            string resourcePrefix = string.Empty;
            bool succes = true;
            Int32 lineNumber = 0;
            Int32 lineCount = 0;
            Int32 resourceId = 0;
            List<ParseResult> replacements;

            progressBarAll.Maximum = files.Count;
            progressBarAll.Value = 0;


            foreach (FileInfo fi in files)
            {

                lineNumber = 0;
                lineCount = 0;
                string inLine = string.Empty;

                // use the form name as a resource prefix
                resourcePrefix = fi.Name.Replace(fi.Extension, string.Empty).Replace(".", "_") + "_";

                lineCount = BackupSourceFile(fi);
                ++progressBarAll.Value;
                progressBarAll.Update();
                progressBarFile.Maximum = lineCount + 1;
                progressBarFile.Value = 0;

                var preParsed = parsedFiles.FirstOrDefault(c => c.Key == fi);
                if (preParsed.Value == null)
                    replacements = GetStrings(fi, findTemplates, excludeTemplates, findOptions, ignoreOptions);
                else
                    replacements = preParsed.Value;

                if (replacements.Count > 0)
                {
                    //existing source file (backup)
                    using (StreamReader backupFile = new StreamReader(fi.FullName + ".bak"))
                    {
                        //new sourcefile
                        using (TextWriter sourceCodeFile = new StreamWriter(fi.FullName, false))
                        {
                            while ((inLine = backupFile.ReadLine()) != null)
                            {
                                ParseResult moveToResource = replacements.FirstOrDefault(c => c.LineNumber == lineNumber);

                                if (moveToResource != null && moveToResource.ReplaceFinds.Count > 0)
                                    resxEntries = ReplaceStringsInCode(resxEntries, fi.Extension, ref inLine, ref resourceId, ref resourcePrefix, ref moveToResource);

                                sourceCodeFile.WriteLine(inLine);

                                ++progressBarFile.Value;
                                progressBarFile.Update();
                                ++lineNumber;
                            }
                            // done, close source code file
                            sourceCodeFile.Flush();
                            sourceCodeFile.Close();
                        }
                        // done, close source code file
                        backupFile.Close();
                    }
                }
            }

            // (re-)write the resource file...
            using (ResXResourceWriter resxFile = new ResXResourceWriter(selectedResource.FullName))
            {
                foreach (String key in resxEntries.Keys)
                    resxFile.AddResource(key, resxEntries[key]);

                resxFile.Generate();
                resxFile.Close();
            }

            return succes;
        }

        /// <summary>
        /// Replace hard coded string with resource reference.
        /// If the resource file allready contains a similar string the existing resource string is referenced.
        /// </summary>
        /// <param name="resxEntries">Resource file contents</param>
        /// <param name="ext">File extention (vb or cs replacement?)</param>
        /// <param name="inLine">Line to parse</param>
        /// <param name="resourceId">serial number for resource key</param>
        /// <param name="resxKeyName">resource key name</param>
        /// <param name="moveToResource">Replacement strings</param>
        /// <returns></returns>
        private static Hashtable ReplaceStringsInCode(Hashtable resxEntries, string ext, ref string inLine, ref Int32 resourceId, ref string resxKeyName, ref ParseResult moveToResource)
        {
            string cleanReplace = string.Empty;
            string resourceKey = string.Empty;
            bool keyExists = false;

            foreach (string replacment in moveToResource.ReplaceFinds)
            {
                resourceKey = string.Empty;
                cleanReplace = replacment.Trim('"'); //.Replace("\"", "");    // Resource value is string without quotes!

                keyExists = resxEntries.ContainsValue(cleanReplace);

                if (keyExists)
                {
                    resourceKey = resxEntries.Keys.OfType<String>().Where(obj => resxEntries[obj] is string).FirstOrDefault(s => (string)resxEntries[s] == cleanReplace);
                }
                else
                {
                    resourceKey = string.Format("{0}{1:#0000}", resxKeyName, ++resourceId);
                    resxEntries.Add(resourceKey, cleanReplace);
                }

                var replacementString = (ext.ToLower() == "vb") ? "My.Resources.{0}" : "Properties.Resources.ResourceManager.GetString(\"{0}\")"; ;
                inLine = inLine.Replace(replacment, string.Format(replacementString,resourceKey));

            }

            return resxEntries;
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
            using (StreamReader sourceFile = new StreamReader(fi.FullName))
            {
                string inLine;
                while ((inLine = sourceFile.ReadLine()) != null) ++lineCount;
            }
            if (File.Exists(fi.FullName + ".bak")) File.Delete(fi.FullName + ".bak");
            File.Copy(fi.FullName, fi.FullName + ".bak");
            return lineCount;
        }


        /// <summary>
        /// Get existing resources to move to newly created resource file
        /// </summary>
        /// <param name="selectedResource">(existing?) resource file to migrate strings to</param>
        /// <returns></returns>
        static Hashtable GetResourceEntries(ref FileInfo selectedResource)
        {
            Hashtable entries = new Hashtable();

            if (File.Exists(selectedResource.FullName))
            {
                // obtain the existing resources, _after_ creating a backup
                if (File.Exists(selectedResource.FullName + ".bak")) File.Delete(selectedResource.FullName + ".bak");
                File.Copy(selectedResource.FullName, selectedResource.FullName + ".bak");

                ResXResourceReader rdr = new ResXResourceReader(selectedResource.FullName);
                rdr.BasePath = selectedResource.FullName.Replace(selectedResource.Name, string.Empty);
                foreach (DictionaryEntry de in rdr)
                    entries.Add(de.Key.ToString(), de.Value);
                rdr.Close();

            }

            return entries;
        }




    }
}
