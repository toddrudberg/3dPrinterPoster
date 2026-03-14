namespace _3dPrinterPoster
{
    partial class frmMain
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
      btnOpenFile = new Button();
      linkLabel1 = new LinkLabel();
      SuspendLayout();
      // 
      // btnOpenFile
      // 
      btnOpenFile.Location = new Point(676, 60);
      btnOpenFile.Name = "btnOpenFile";
      btnOpenFile.Size = new Size(438, 46);
      btnOpenFile.TabIndex = 0;
      btnOpenFile.Text = "Open File GCode File";
      btnOpenFile.UseVisualStyleBackColor = true;
      btnOpenFile.Click += btnOpenFile_Click;
      // 
      // linkLabel1
      // 
      linkLabel1.Location = new Point(147, 109);
      linkLabel1.Name = "linkLabel1";
      linkLabel1.Size = new Size(1822, 43);
      linkLabel1.TabIndex = 5;
      linkLabel1.TabStop = true;
      linkLabel1.Text = "linkLabel1";
      linkLabel1.TextAlign = ContentAlignment.MiddleCenter;
      linkLabel1.MouseUp += linkLabel1_MouseUp;
      // 
      // frmMain
      // 
      AutoScaleDimensions = new SizeF(13F, 32F);
      AutoScaleMode = AutoScaleMode.Font;
      BackgroundImage = Properties.Resources._3dConverter;
      BackgroundImageLayout = ImageLayout.Center;
      ClientSize = new Size(2042, 1448);
      Controls.Add(linkLabel1);
      Controls.Add(btnOpenFile);
      Name = "frmMain";
      Text = "Form1";
      Load += frmMain_Load;
      ResumeLayout(false);
    }

    #endregion

    private Button btnOpenFile;
    private LinkLabel linkLabel1;
  }
}
