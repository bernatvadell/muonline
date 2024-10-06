using System.Diagnostics;

namespace Client.Editor
{
    public partial class FMain : Form
    {
        public FMain()
        {
            InitializeComponent();
        }

        private void FMain_Load(object sender, EventArgs e)
        {
            menuOpenDataFolder.Click += OnOpenDataFolder;
            treeView1.NodeMouseDoubleClick += OnNodeMouseDoubleClick;
        }

        private void OnNodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Nodes.Count > 0)
                return;

            var filePath = e.Node.Tag as string;

            if (string.IsNullOrEmpty(filePath))
                return;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            switch (ext)
            {
                case ".ozj":
                case ".ozt":
                    {
                        var editor = new CTextureEditor();
                        editor.Dock = DockStyle.Fill;
                        editor.Init(filePath);
                        panel1.Controls.Clear();
                        panel1.Controls.Add(editor);
                    }
                    break;
            }
        }

        private void OnOpenDataFolder(object? sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            var path = dialog.SelectedPath;

            treeView1.Nodes.Clear();
            LoadDirectory(path, treeView1.Nodes);
            treeView1.Nodes[0].Expand();
        }

        private void LoadDirectory(string dir, TreeNodeCollection nodes)
        {
            var directoryNode = new TreeNode(Path.GetFileName(dir))
            {
                Tag = dir
            };

            nodes.Add(directoryNode);

            foreach (var subDir in Directory.GetDirectories(dir))
                LoadDirectory(subDir, directoryNode.Nodes);

            foreach (var file in Directory.GetFiles(dir))
            {
                var fileNode = new TreeNode(Path.GetFileName(file))
                {
                    Tag = file
                };
                directoryNode.Nodes.Add(fileNode);
            }
        }
    }
}
