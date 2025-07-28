using HBPakEditor;

namespace HBPakEdtior
{
    partial class MainWindow
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            closeToolStripMenuItem = new ToolStripMenuItem();
            darkModeToolStripMenuItem = new ToolStripMenuItem();
            tabControl1 = new RenamableTabControl();
            tabTemplate = new TabPage();
            splitContainer1 = new SplitContainer();
            spriteTreeView = new TreeView();
            splitContainer2 = new SplitContainer();
            pbSpriteImage = new PictureBox();
            pbImageControlBar = new MenuStrip();
            zoomToolStripMenuItem = new ToolStripMenuItem();
            menuStrip1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabTemplate.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pbSpriteImage).BeginInit();
            pbImageControlBar.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, darkModeToolStripMenuItem });
            menuStrip1.Location = new Point(5, 5);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(840, 28);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { closeToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(46, 24);
            fileToolStripMenuItem.Text = "File";
            // 
            // closeToolStripMenuItem
            // 
            closeToolStripMenuItem.Name = "closeToolStripMenuItem";
            closeToolStripMenuItem.Size = new Size(128, 26);
            closeToolStripMenuItem.Text = "Close";
            closeToolStripMenuItem.Click += closeToolStripMenuItem_Click;
            // 
            // darkModeToolStripMenuItem
            // 
            darkModeToolStripMenuItem.Alignment = ToolStripItemAlignment.Right;
            darkModeToolStripMenuItem.CheckOnClick = true;
            darkModeToolStripMenuItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
            darkModeToolStripMenuItem.Name = "darkModeToolStripMenuItem";
            darkModeToolStripMenuItem.Size = new Size(97, 24);
            darkModeToolStripMenuItem.Text = "Dark Mode";
            // 
            // tabControl1
            // 
            tabControl1.AfterRename = null;
            tabControl1.Appearance = TabAppearance.FlatButtons;
            tabControl1.BeforeRename = null;
            tabControl1.Controls.Add(tabTemplate);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(5, 33);
            tabControl1.Multiline = true;
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(840, 415);
            tabControl1.TabIndex = 2;
            // 
            // tabTemplate
            // 
            tabTemplate.Controls.Add(splitContainer1);
            tabTemplate.Location = new Point(4, 32);
            tabTemplate.Name = "tabTemplate";
            tabTemplate.Padding = new Padding(3);
            tabTemplate.Size = new Size(832, 379);
            tabTemplate.TabIndex = 0;
            tabTemplate.Text = "tabTemplate";
            tabTemplate.UseVisualStyleBackColor = true;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(3, 3);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(spriteTreeView);
            splitContainer1.Panel1MinSize = 280;
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(splitContainer2);
            splitContainer1.Size = new Size(826, 373);
            splitContainer1.SplitterDistance = 280;
            splitContainer1.TabIndex = 1;
            // 
            // spriteTreeView
            // 
            spriteTreeView.BorderStyle = BorderStyle.FixedSingle;
            spriteTreeView.Dock = DockStyle.Fill;
            spriteTreeView.Location = new Point(0, 0);
            spriteTreeView.Name = "spriteTreeView";
            spriteTreeView.Size = new Size(280, 373);
            spriteTreeView.TabIndex = 0;
            // 
            // splitContainer2
            // 
            splitContainer2.BorderStyle = BorderStyle.FixedSingle;
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            splitContainer2.Orientation = Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(pbSpriteImage);
            splitContainer2.Panel1.Controls.Add(pbImageControlBar);
            splitContainer2.Panel2MinSize = 128;
            splitContainer2.Size = new Size(542, 373);
            splitContainer2.SplitterDistance = 241;
            splitContainer2.TabIndex = 0;
            // 
            // pbSpriteImage
            // 
            pbSpriteImage.BackgroundImageLayout = ImageLayout.Zoom;
            pbSpriteImage.Dock = DockStyle.Fill;
            pbSpriteImage.Location = new Point(0, 28);
            pbSpriteImage.Name = "pbSpriteImage";
            pbSpriteImage.Padding = new Padding(5);
            pbSpriteImage.Size = new Size(540, 211);
            pbSpriteImage.TabIndex = 0;
            pbSpriteImage.TabStop = false;
            // 
            // pbImageControlBar
            // 
            pbImageControlBar.ImageScalingSize = new Size(20, 20);
            pbImageControlBar.Items.AddRange(new ToolStripItem[] { zoomToolStripMenuItem });
            pbImageControlBar.Location = new Point(0, 0);
            pbImageControlBar.Name = "pbImageControlBar";
            pbImageControlBar.Size = new Size(540, 28);
            pbImageControlBar.TabIndex = 1;
            pbImageControlBar.Text = "menuStrip2";
            // 
            // zoomToolStripMenuItem
            // 
            zoomToolStripMenuItem.CheckOnClick = true;
            zoomToolStripMenuItem.Name = "zoomToolStripMenuItem";
            zoomToolStripMenuItem.Size = new Size(63, 24);
            zoomToolStripMenuItem.Text = "Zoom";
            // 
            // MainWindow
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(850, 453);
            Controls.Add(tabControl1);
            Controls.Add(menuStrip1);
            DoubleBuffered = true;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(868, 500);
            Name = "MainWindow";
            Padding = new Padding(5);
            Text = "PAK Editor";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabTemplate.ResumeLayout(false);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pbSpriteImage).EndInit();
            pbImageControlBar.ResumeLayout(false);
            pbImageControlBar.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem closeToolStripMenuItem;
        private ToolStripMenuItem darkModeToolStripMenuItem;
        private RenamableTabControl tabControl1;
        private TabPage tabTemplate;
        private SplitContainer splitContainer1;
        private TreeView spriteTreeView;
        private SplitContainer splitContainer2;
        private PictureBox pbSpriteImage;
        private MenuStrip pbImageControlBar;
        private ToolStripMenuItem zoomToolStripMenuItem;
    }
}
