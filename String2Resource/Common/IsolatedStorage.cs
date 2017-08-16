using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace String2Resources
{
    public class IsolatedStorage : IDisposable
    {
        private IsolatedStorageFile isf;

        public IsolatedStorage()
        {
            isf = IsolatedStorageFile.GetUserStoreForAssembly();
        }


        public void Save(List<string> find, List<string> ignore)
        {

            using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream("String2Resource.xml", FileMode.Create, isf))
            {
                XmlWriterSettings settings = new XmlWriterSettings() { Indent = true };
                using (XmlWriter writer = XmlWriter.Create(isfs, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteComment("  Copyright © Smartcoding 2017  ");
                    writer.WriteComment("  Regex entries encoded because of extensive use of reserved characters  ");
                    writer.WriteStartElement("RegexList");
                    writer.WriteStartElement("Find");
                    foreach (string str in find) writer.WriteElementString("Regex", XmlConvert.EncodeName(str));
                    writer.WriteEndElement();
                    writer.WriteStartElement("Ignore");
                    foreach (string str in ignore) writer.WriteElementString("Regex", XmlConvert.EncodeName(str));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                }
            }
        }

        public Tuple<List<string>, List<string>> Load()
        {

            /*
            
             * Dim ifs As IsolatedStorageFile = IsolatedStorageFile.GetUserStoreForAssembly()
            If ifs.FileExists(frm.Name + ".v2.xml") Then
                Using isoStream As IsolatedStorageFileStream = New IsolatedStorageFileStream(frm.Name + ".v2.xml", FileMode.Open, ifs)
                    Using reader As XmlReader = XmlReader.Create(isoStream)
                        reader.MoveToContent()
                        While reader.Read
                            If reader.NodeType = XmlNodeType.Element Then
                                If reader.Name = "Dock" Then dockdata = reader.ReadString()
                                If reader.Name = "X" Then x = Integer.Parse(reader.ReadString())
                                If reader.Name = "Y" Then y = Integer.Parse(reader.ReadString())
                                If reader.Name = "H" Then h = Integer.Parse(reader.ReadString())
                                If reader.Name = "W" Then w = Integer.Parse(reader.ReadString())
                            End If
                        End While
                    End Using
                End Using
            End If
             */
            List<string> find = new List<string>();
            List<string> ignore = new List<string>();
            IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForAssembly();

            if (isf.FileExists("String2Resource.xml"))
            {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream("String2Resource.xml", FileMode.Open, isf))
                {
                    using (XmlReader rdr = XmlReader.Create(isfs))
                    {
                        short addRegex = 0;
                        rdr.MoveToContent();

                        while (rdr.Read())
                        {
                            if (rdr.NodeType == XmlNodeType.Element)
                            {
                                if (rdr.Name == "Find") addRegex = 1;
                                else if (rdr.Name == "Ignore") addRegex = 2;
                                else if (rdr.Name != "Regex") addRegex = 0;                                
                            }

                            if (rdr.Name == "Regex" && addRegex > 0 )
                            {
                                if (addRegex == 1)
                                    find.Add(XmlConvert.DecodeName(rdr.ReadElementString()));
                                else
                                    ignore.Add(XmlConvert.DecodeName(rdr.ReadElementString()));
                            }
                        }
                    }
                }               
            }
            else
            {
                ListDefaults(ref find, ref ignore);
            }

            return new Tuple<List<string>, List<string>>(find,ignore);
            
            
        }

        private void ListDefaults(ref List<string> find, ref List<string> ignore)
        {
            find.Add(".*[\\+]$");                    //  .*[\+]$             -- line ends with "+" (string concat?)
            find.Add("\\\"[^\\\"]*\\\",");           //  \"[^\"]*\",         -- lines containing "xxx",
            find.Add("Format?\\(\\\"[^\\\"].*");     //  Format\(\"[^\"].*   -- contains (String.) Format(
            find.Add("Show?\\(\\\"[^\\\"].*");       //  Show?\(?\"[^\"].*   -- contains (Messagebox.) Show( 
            find.Add("Text = \\\"[^\\\"]*\\\"");     //  Text =?\"[^\"]*\"   -- line contains (Control.)Text = "xxx"
            find.Add("Text \\+= \\\"[^\\\"]*\\\"");  //  Text =?\"[^\"]*\"   -- line contains (Control.)Text += "xxx"
            find.Add("(A|a)s String = \\\".*");      //  As String = \".*    -- line contains vb String declaration

            ignore.Add("(data source=)");              //  data source=.*       -- string containing "data source="
            ignore.Add("(Cipher)");                    //  Cipher               -- string containing "Cipher"
            ignore.Add("(LogEntry)");                  //  LogEntry             -- string containing "LogEntry"

        }




        #region IDisposable
        private void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
        }

        protected virtual void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
                isf = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~IsolatedStorage()
        {
            Dispose(false);
        }
        #endregion
    }
}
