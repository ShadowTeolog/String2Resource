using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace String2Resources
{
    public partial class Form1 : Form
    {

        FileInfo _selectedSourceCode;
        FileInfo _selectedResource;
        //List<ParseResult> _parseResult;
        Dictionary<FileInfo, List<ParseResult>> _parseResult = new Dictionary<FileInfo, List<ParseResult>>();

        String _path;


        public Form1()
        {
            InitializeComponent();
            CustomInitializeComponent();
        }

        void CustomInitializeComponent()
        {

            treeView1.AfterCheck += (s, e) =>
                {
                    if (e.Node.Nodes.Count > 0)
                    {
                        foreach (TreeNode node in e.Node.Nodes)
                            node.Checked = e.Node.Checked;
                    }
                };


            treeView1.NodeMouseClick += (s, e) =>
                {                    
                    if (e.Node.Tag is FileInfo)
                    {
                        dataGridView1.DataSource = null;
                        treeView1.SelectedNode = e.Node;
                        button2_Click(treeView1, e);
                    }
                };

            listBox1.SelectedIndexChanged += listBox1_SelectedIndexChanged;
            listBox1.ValueMember = "this";
            listBox1.DisplayMember = "Name";

            var cb = new DataGridViewCheckBoxColumn() { Name = "ToResource", HeaderText = "move", DataPropertyName = "ToResource", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader };
            var nr = new DataGridViewTextBoxColumn() { Name = "LineNumber", HeaderText = "#", DataPropertyName = "LineNumber", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader, ReadOnly = true };
            var cnt = new DataGridViewTextBoxColumn() { Name = "StringCount", HeaderText = "cnt", DataPropertyName = "StringCount", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader };
            var repl = new DataGridViewTextBoxColumn() { Name = "ReplaceCount", HeaderText = "repl", DataPropertyName = "ReplaceCount", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader };
            var txt = new DataGridViewTextBoxColumn() { Name = "LineContent", HeaderText = "Line content", DataPropertyName = "LineContent", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.Columns.AddRange(cb, nr, cnt, repl, txt);
            dataGridView1.CellMouseDown += dataGridView1_CellMouseDown;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;


            List<string> find = new List<string>();
            List<string> ignore = new List<string>();

            using (IsolatedStorage isolatedStorage = new IsolatedStorage())
            {
                var lists = isolatedStorage.Load();
                find = lists.Item1;
                ignore = lists.Item2;
            }

            dataGridView2.DefaultCellStyle.Font = new Font("Courier New", 12);
            foreach (string str in find) dataGridView2.Rows.Add(str);
            
            dataGridView3.DefaultCellStyle.Font = new Font("Courier New", 12);
            foreach (string str in ignore) dataGridView3.Rows.Add(str);
            

            _path = string.Empty;
            treeView1.Nodes.Clear();
            _selectedSourceCode = null;
            label7.Text = string.Empty;
            label3.Text = string.Empty;

            this.FormClosing += (s, e) =>
            {
                List<string> findTemplates = FindTemplates();
                List<string> excludeTemplates = ExcludeTemplates();
                using (IsolatedStorage isolatedStorage = new IsolatedStorage())
                {
                    isolatedStorage.Save(findTemplates, excludeTemplates);
                }
            };

        }

        void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selectedSourceCode = (FileInfo)listBox1.SelectedValue;
            TreeNode selectedNode = null;

            foreach (TreeNode findNode in treeView1.Nodes)
            {
                FindTreeNodeByTag(_selectedSourceCode, findNode, ref selectedNode);
                if (selectedNode != null) break;
            }

            if (selectedNode != null)
            {
                treeView1.SelectedNode = selectedNode;
                TreeNodeMouseClickEventArgs arg = new TreeNodeMouseClickEventArgs(selectedNode, MouseButtons.Left, 1, 0, 0);
                button2_Click(treeView1, arg);
            }

        }



        void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0)
            {
                if (dataGridView1.Rows[e.RowIndex] != null)
                {
                    var data = dataGridView1.Rows[e.RowIndex].DataBoundItem as ParseResult;


                    if (data != null)
                    {
                        data.ToResource = !data.ToResource;
                        data.ReplaceCount = 0;
                        data.ReplaceFinds = new List<string>();
                        if (data.ToResource) Parser.ParseLine(ref data);
                    }
                    dataGridView1.EndEdit();
                    dataGridView1.InvalidateRow(e.RowIndex);
                }
            }
        }


        private void dataGridView1_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (dataGridView1.Rows[e.RowIndex] != null)
                {
                    var data = dataGridView1.Rows[e.RowIndex].DataBoundItem as ParseResult;
                    if (data != null && data.ReplaceFinds.Count > 0)
                    {
                        string msg = string.Empty;
                        foreach (string replacement in data.ReplaceFinds) msg += replacement + Environment.NewLine;
                        MessageBox.Show(msg, "To replace:");
                    }
                }
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            _path = string.Empty;
            treeView1.Nodes.Clear();
            _selectedSourceCode = null;
            label7.Text = string.Empty;
            label3.Text = string.Empty;
            progressBar1.Value = 0;
            progressBar2.Value = 0;

            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;
                dlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                dlg.ShowNewFolderButton = false;

                var result = dlg.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    _path = dlg.SelectedPath;
                    LoadTreeView1(_path, ".vb");
                }
            }

            label7.Text = _path;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.DefaultExt = ".resx";
                dlg.FileName = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Resources.nl-NL.resx";
                dlg.OverwritePrompt = false;
                var result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    _selectedResource = new FileInfo(dlg.FileName);
                    textBox1.Text = _selectedResource.FullName;
                }
            }
        }


        private void LoadTreeView1(string basePath, string ext)
        {

            var pattern = @"*" + ext;
            var rootFolder = new DirectoryInfo(basePath).Name;
            var nodeKey = string.Empty;
            TreeNode lastNode = null;

            var files = Parser.GetAllFiles(new List<FileInfo>(), basePath, pattern);

            treeView1.Nodes.Clear();
            treeView1.CheckBoxes = true;
            treeView1.PathSeparator = @"\";

            if (files.Count == 0)
            {
                MessageBox.Show(string.Format("No files with extension \"{0}\" found.", ext));
                return;
            }

            foreach (FileInfo fi in files)
            {
                nodeKey = string.Empty;
                var root = fi.FullName.Replace(basePath, string.Empty);

                foreach (string subPath in root.Split('\\'))
                {
                    var pathPart = (subPath.Length == 0) ? rootFolder : subPath;
                    nodeKey += pathPart + @"\";

                    TreeNode[] nodes = treeView1.Nodes.Find(nodeKey, true);
                    if (nodes.Length == 0)
                        if (lastNode == null)
                        {
                            lastNode = treeView1.Nodes.Add(nodeKey, pathPart);
                            lastNode.Checked = true;
                            if (subPath.EndsWith(ext)) lastNode.Tag = fi;
                        }
                        else
                        {
                            lastNode = lastNode.Nodes.Add(nodeKey, pathPart);
                            if (subPath.EndsWith(ext)) lastNode.Tag = fi;
                            lastNode.Checked = true;
                        }

                    else
                    {
                        lastNode = nodes[0];
                    }


                }
                lastNode = null;
            }

            treeView1.Nodes[0].Expand();

        }

        private void button2_Click(object sender, EventArgs e)
        {

            TreeNode node;
            bool isTreeviewSelect = (e is TreeNodeMouseClickEventArgs);


            if (!isTreeviewSelect)                              // refresh button
                node = treeView1.SelectedNode;
            else                                                // treeview mouseclick
                node = ((TreeNodeMouseClickEventArgs)e).Node;


            if (node == null || !(node.Tag is FileInfo))
            {
                MessageBox.Show("No selected file");
                return;
            }



            _selectedSourceCode = (FileInfo)node.Tag;
            List<ParseResult> parsed = null;

            var preParsed = _parseResult.FirstOrDefault(c => c.Key == _selectedSourceCode);


            if (preParsed.Value != null && (e is TreeNodeMouseClickEventArgs)) parsed = preParsed.Value;

            if (parsed == null)
            {
                List<string> findTemplates = FindTemplates();
                List<string> excludeTemplates = ExcludeTemplates();
                RegexOptions findOptions = (checkBox1.Checked) ? RegexOptions.None : RegexOptions.IgnoreCase;
                RegexOptions ignoreOptions = (checkBox2.Checked) ? RegexOptions.None : RegexOptions.IgnoreCase;
                parsed = Parser.GetStrings(_selectedSourceCode, findTemplates, excludeTemplates,findOptions,ignoreOptions);
                if (preParsed.Value != null /* && (e is TreeNodeMouseClickEventArgs) */) _parseResult.Remove(_selectedSourceCode);
                _parseResult.Add(_selectedSourceCode, parsed);
            }

            dataGridView1.DataSource = parsed;
            label3.Text = string.Format("Selected: {0}", _selectedSourceCode.FullName);
            tabControl1.SelectedTab = tabPage2;


            if (isTreeviewSelect) listBox1.SelectedIndexChanged -= listBox1_SelectedIndexChanged;

            var editted = _parseResult.Keys.ToList<FileInfo>().OrderBy(c => c.Name);
            listBox1.DataSource = new BindingSource(editted, null);
            var selected = editted.FirstOrDefault(c => c == _selectedSourceCode);
            listBox1.SelectedItem = selected;

            if (isTreeviewSelect) listBox1.SelectedIndexChanged += listBox1_SelectedIndexChanged;



        }

        private void button4_Click(object sender, EventArgs e)
        {

            if (_selectedSourceCode == null)
            {
                MessageBox.Show("No source file selected");
                return;
            }

            if (_parseResult == null)
            {
                MessageBox.Show("Could not parse source file");
                return;
            }

            if (_selectedResource == null)
            {
                MessageBox.Show("No resource file selected");
                return;
            }

            this.Cursor = Cursors.WaitCursor;

            List<string> findTemplates = FindTemplates();
            List<string> excludeTemplates = ExcludeTemplates();
            RegexOptions findOptions = (checkBox1.Checked) ? RegexOptions.None : RegexOptions.IgnoreCase;
            RegexOptions ignoreOptions = (checkBox2.Checked) ? RegexOptions.None : RegexOptions.IgnoreCase;
            if (Parser.CreateMultipleResource(new List<FileInfo>() { _selectedSourceCode }, _selectedResource, _parseResult, ref findTemplates, findOptions, ref excludeTemplates, ignoreOptions, ref progressBar1, ref progressBar2))
                Process.Start(_selectedSourceCode.FullName.Replace(_selectedSourceCode.Name, string.Empty));

            this.Cursor = Cursors.Default;

        }

        private void button5_Click(object sender, EventArgs e)
        {
            List<FileInfo> files = new List<FileInfo>();
            foreach (TreeNode node in treeView1.Nodes) GetSelectedFiles(node, ref files);

            if (files.Count < 1)
            {
                MessageBox.Show("No checked files");
                return;
            }

            if (_selectedResource == null)
            {
                MessageBox.Show("No resource file selected");
                return;
            }

            List<string> findTemplates = FindTemplates();
            List<string> excludeTemplates = ExcludeTemplates();
            RegexOptions findOptions = (checkBox1.Checked) ? RegexOptions.None : RegexOptions.IgnoreCase;
            RegexOptions ignoreOptions = (checkBox2.Checked) ? RegexOptions.None : RegexOptions.IgnoreCase;
            if (Parser.CreateMultipleResource(files, _selectedResource, _parseResult /* new List<ParseResult>() */, ref findTemplates, findOptions, ref excludeTemplates, ignoreOptions, ref progressBar1, ref progressBar2))
                Process.Start(_path);

        }

        private void GetSelectedFiles(TreeNode node, ref List<FileInfo> files)
        {
            foreach (TreeNode tn in node.Nodes) GetSelectedFiles(tn, ref files);
            if (node.Checked && node.Tag != null) files.Add((FileInfo)node.Tag);
        }

        private void FindTreeNodeByTag(object findTag, TreeNode node, ref TreeNode selectNode)
        {
            if (node.Tag == findTag) selectNode = node;

            if (selectNode == null)
            {
                foreach (TreeNode tn in node.Nodes)
                {
                    if (node.Tag == findTag) break;
                    FindTreeNodeByTag(findTag, tn, ref selectNode);
                }
            }
        }




        private List<string> FindTemplates()
        {
            List<string> regexList = new List<string>();
            foreach (DataGridViewRow dgvr in dataGridView2.Rows)
            {
                if (dgvr.Cells[0].Value != null && !string.IsNullOrWhiteSpace(dgvr.Cells[0].Value.ToString()))
                    regexList.Add(dgvr.Cells[0].Value.ToString());
            }
            return regexList;
        }

        private List<string> ExcludeTemplates()
        {
            List<string> regexList = new List<string>();
            foreach (DataGridViewRow dgvr in dataGridView3.Rows)
            {
                if (dgvr.Cells[0].Value != null && !string.IsNullOrWhiteSpace(dgvr.Cells[0].Value.ToString()))
                    regexList.Add(dgvr.Cells[0].Value.ToString());
            }
            return regexList;
        }


    }
}
