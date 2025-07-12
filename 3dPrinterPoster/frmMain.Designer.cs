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
      btnOpenSettingsFile = new Button();
      btnSaveSettings = new Button();
      btnSaveSettingsFileAs = new Button();
      chkIncludeG29 = new CheckBox();
      SuspendLayout();
      // 
      // btnOpenFile
      // 
      btnOpenFile.Location = new Point(192, 314);
      btnOpenFile.Name = "btnOpenFile";
      btnOpenFile.Size = new Size(438, 46);
      btnOpenFile.TabIndex = 0;
      btnOpenFile.Text = "Open File GCode File";
      btnOpenFile.UseVisualStyleBackColor = true;
      btnOpenFile.Click += btnOpenFile_Click;
      // 
      // btnOpenSettingsFile
      // 
      btnOpenSettingsFile.Location = new Point(192, 117);
      btnOpenSettingsFile.Name = "btnOpenSettingsFile";
      btnOpenSettingsFile.Size = new Size(438, 46);
      btnOpenSettingsFile.TabIndex = 1;
      btnOpenSettingsFile.Text = "Open Settings File";
      btnOpenSettingsFile.UseVisualStyleBackColor = true;
      btnOpenSettingsFile.Click += btnOpenSettingsFile_Click;
      // 
      // btnSaveSettings
      // 
      btnSaveSettings.Location = new Point(192, 169);
      btnSaveSettings.Name = "btnSaveSettings";
      btnSaveSettings.Size = new Size(438, 46);
      btnSaveSettings.TabIndex = 2;
      btnSaveSettings.Text = "Save Settings File";
      btnSaveSettings.UseVisualStyleBackColor = true;
      // 
      // btnSaveSettingsFileAs
      // 
      btnSaveSettingsFileAs.Location = new Point(192, 221);
      btnSaveSettingsFileAs.Name = "btnSaveSettingsFileAs";
      btnSaveSettingsFileAs.Size = new Size(438, 46);
      btnSaveSettingsFileAs.TabIndex = 3;
      btnSaveSettingsFileAs.Text = "Save Settings File As,,,";
      btnSaveSettingsFileAs.UseVisualStyleBackColor = true;
      btnSaveSettingsFileAs.Click += btnSaveSettingsFileAs_Click;
      // 
      // chkIncludeG29
      // 
      chkIncludeG29.AutoSize = true;
      chkIncludeG29.Location = new Point(691, 328);
      chkIncludeG29.Name = "chkIncludeG29";
      chkIncludeG29.Size = new Size(331, 36);
      chkIncludeG29.TabIndex = 4;
      chkIncludeG29.Text = "Include G29 (Bed Leveling)";
      chkIncludeG29.UseVisualStyleBackColor = true;
      // 
      // frmMain
      // 
      AutoScaleDimensions = new SizeF(13F, 32F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(2042, 1448);
      Controls.Add(chkIncludeG29);
      Controls.Add(btnSaveSettingsFileAs);
      Controls.Add(btnSaveSettings);
      Controls.Add(btnOpenSettingsFile);
      Controls.Add(btnOpenFile);
      Name = "frmMain";
      Text = "Form1";
      Load += frmMain_Load;
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private Button btnOpenFile;
    private Button btnOpenSettingsFile;
    private Button btnSaveSettings;
    private Button btnSaveSettingsFileAs;
    private CheckBox chkIncludeG29;
  }
}
