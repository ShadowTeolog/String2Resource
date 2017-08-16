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


        public static List<FileInfo> GetAllFiles(List<FileInfo> fileInfos, string path, string pattern)
        {

            var files = new List<string>();

            try
            {
                files.AddRange(Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly));

                foreach (string file in files)
                    fileInfos.Add(new FileInfo(file));

                foreach (var directory in Directory.GetDirectories(path))
                    GetAllFiles(fileInfos, directory, pattern);
            }
            catch (UnauthorizedAccessException) { /* can't go here, just ignore */ }

            return fileInfos;
        }

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

                            foreach (Match match in Regex.Matches(inLine, "\"")) line.Quotes.Add(match.Index);

                            if (findTemplates.Count > 0)
                            {
                                foreach (string find in findTemplates)
                                {
                                    foreach (Match match in Regex.Matches(inLine, find, findOptions))
                                    {

                                        foreach (string ignore in excludeTemplates)
                                        {
                                            /*
                                            if (inLine.Contains(ignore))
                                            {
                                                replace = false;
                                                break;
                                            }
                                            */
                                            //var contains = Regex.Matches(match.Value, ignore);
                                            var contains = Regex.Matches(inLine, ignore, ignoreOptions);
                                            if (contains.Count > 0)
                                            {
                                                replace = false;
                                                break;
                                            }
                                        }

                                        if (replace)
                                            ParseLine(ref line);

                                    }




                                }
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


        public static void ParseLine(ref ParseResult result)
        {

            foreach (Match str in Regex.Matches(result.LineContent, "\"[^\"]*\""))
            {
                if (!result.ReplaceFinds.Contains(str.Value))
                {
                    ++result.ReplaceCount;
                    result.ReplaceFinds.Add(str.Value);
                    result.ToResource = true;
                }
            }

        }





        internal static bool CreateMultipleResource(List<FileInfo> files, FileInfo selectedResource, Dictionary<FileInfo, List<ParseResult>> parsedFiles /* List<ParseResult> replacements */ ,
                                                     ref List<string> findTemplates, RegexOptions findOptions, ref List<string> excludeTemplates, RegexOptions ignoreOptions, ref ProgressBar progressBarFile, ref ProgressBar progressBarAll)
        {
            Hashtable resxEntries = GetResourceEntries(ref selectedResource);

            string resourcePrefix = string.Empty;
            bool succes = true;
            //bool dynamicReplace = (replacements.Count == 0);
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

                lineCount = BackupSourceFile(lineCount, fi);
                ++progressBarAll.Value;
                progressBarAll.Update();
                progressBarFile.Maximum = lineCount + 1;
                progressBarFile.Value = 0;

                var preParsed = parsedFiles.FirstOrDefault(c => c.Key == fi);
                if (preParsed.Value == null)
                    replacements = GetStrings(fi, findTemplates, excludeTemplates,findOptions,ignoreOptions);
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
                                    resxEntries = ReplaceStringsInCode(resxEntries, ref inLine, ref resourceId, ref resourcePrefix, ref moveToResource);

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

            using (ResXResourceWriter resxFile = new ResXResourceWriter(selectedResource.FullName))
            {
                foreach (String key in resxEntries.Keys)
                    resxFile.AddResource(key, resxEntries[key]);

                resxFile.Generate();
                resxFile.Close();
            }

            return succes;
        }


        private static Hashtable ReplaceStringsInCode(Hashtable resxEntries, ref string inLine, ref Int32 resourceId, ref string resourcePrefix, ref ParseResult moveToResource)
        {
            string cleanReplace = string.Empty;
            string resourceKey = string.Empty;
            bool keyExists = false;

            foreach (string replacment in moveToResource.ReplaceFinds)
            {
                resourceKey = string.Empty;
                cleanReplace = replacment.Replace("\"", "");

                keyExists = resxEntries.ContainsValue(cleanReplace);

                if (keyExists)
                {
                    resourceKey = resxEntries.Keys.OfType<String>().FirstOrDefault(s => resxEntries[s] == cleanReplace);
                }
                else
                {
                    resourceKey = string.Format("{0}{1:#0000}", resourcePrefix, ++resourceId);
                    resxEntries.Add(resourceKey, cleanReplace);
                }

                inLine = inLine.Replace(replacment, "My.Resources." + resourceKey);

            }

            return resxEntries;
        }

        private static int BackupSourceFile(Int32 lineCount, FileInfo fi)
        {
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


        static Hashtable GetResourceEntries(ref FileInfo selectedResource)
        {
            Hashtable entries = new Hashtable();

            if (File.Exists(selectedResource.FullName))
            {
                // obtain the existing resources, after creating a backup
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
