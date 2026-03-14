using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms;
using static System.Windows.Forms.Design.AxImporter;


namespace _3dPrinterPoster
{
  public partial class frmMain : Form
  {

    private PrintSettings currentSettings;
    private FormSettings formSettings = new FormSettings();
    //public TuningDialog(PrintSettings options)
    //{
    //  InitializeComponent();
    //  _original = options;
    //  Options = Clone(options);                 // work on a copy
    //  propertyGrid1.SelectedObject = Options;
    //  Text = "Program Tuning";
    //  AcceptButton = btnOK; CancelButton = btnCancel;
    //}

    [DllImport("kernel32.dll")]
    static extern bool AllocConsole();

    private void frmMain_Load(object sender, EventArgs e)
    {
      AllocConsole();

      formSettings = formSettings.LoadFormSettings();

      void CenterHorizontally(Control ctrl)
      {
        if (ctrl.Parent == null) return;

        ctrl.Left = (ctrl.Parent.ClientSize.Width - ctrl.Width) / 2;
      }

      CenterHorizontally(btnOpenFile);
      CenterHorizontally(linkLabel1);
      linkLabel1.Visible = false;


    }

    public frmMain()
    {
      InitializeComponent();
    }

    private void btnOpenFile_Click(object sender, EventArgs e)
    {
      this.Enabled = false;
      ToddUtils.FileParser.cFileParse fp = new();

      using (var openFileDialog = new OpenFileDialog())
      {
        openFileDialog.Filter = "Settings files (*.settings.json)|*.settings.json|All files (*.*)|*.*";
        openFileDialog.Title = "Open Print Settings File";

        // If there's a last file, set its folder and filename
        if (!string.IsNullOrEmpty(formSettings.LastSettingsFile))
        {
          var lastDir = Path.GetDirectoryName(formSettings.LastSettingsFile);
          if (!string.IsNullOrEmpty(lastDir) && Directory.Exists(lastDir))
            openFileDialog.InitialDirectory = lastDir;

          openFileDialog.FileName = Path.GetFileName(formSettings.LastSettingsFile);
        }

        if (openFileDialog.ShowDialog() != DialogResult.OK)
        {
          this.Enabled = true;
          return; // cancelled
        }

        string settingsPath = openFileDialog.FileName;

        try
        {
          // Load selected config (single source of truth)
          currentSettings = PrintSettings.Load(settingsPath);

          // Remember path
          formSettings.LastSettingsFile = settingsPath;
          formSettings.SaveFormSettings();

          using var dlg = new TuningDialog(currentSettings);
          if (dlg.ShowDialog(this) == DialogResult.OK)
          {
            currentSettings.Save(settingsPath);

            MessageBox.Show(
              $"Saved settings profile: {currentSettings.ProfileName}",
              "Settings Saved",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          }
          // else: do nothing (cancel means no changes)
        }
        catch (Exception ex)
        {
          MessageBox.Show(
            $"Failed to load/save settings:\n{ex.Message}",
            "Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

          this.Enabled = true;
          return;
        }
      }

      using (OpenFileDialog openFileDialog = new OpenFileDialog())
      {
        openFileDialog.Filter = "GCode files (*.gcode)|*.gcode|All files (*.*)|*.*";
        openFileDialog.Title = "Open GCode File";

        // If there's a last file, set its folder and filename
        if (!string.IsNullOrEmpty(formSettings.LastGcodeFile))
        {
          openFileDialog.InitialDirectory = Path.GetDirectoryName(formSettings.LastGcodeFile);
          openFileDialog.FileName = Path.GetFileName(formSettings.LastGcodeFile);
        }

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
          string path = openFileDialog.FileName;
          formSettings.LastGcodeFile = path;
          formSettings.SaveFormSettings(); // Save the last Gcode file path

          //Hop out and do the thing:

          string directory = Path.GetDirectoryName(path);
          string filenameWithoutExt = Path.GetFileNameWithoutExtension(path);
          string newPath = Path.Combine(directory, $"{filenameWithoutExt}_mod.gcode");

          ModifyPPG.DoTheThing(path, currentSettings, newPath);
          linkLabel1.Text = newPath;
          linkLabel1.Visible = true;
          Console.WriteLine($"Modified file saved as:\n{newPath}", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
      }
      this.Enabled = true;
    }

    private void linkLabel1_MouseUp(object sender, MouseEventArgs e)
    {
      string path = linkLabel1.Text;

      if (!File.Exists(path)) return;

      if (e.Button == MouseButtons.Left)
      {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
      }
      else if (e.Button == MouseButtons.Right)
      {
        Process.Start("explorer.exe", "/select,\"" + path + "\"");
      }
    }
  }
}
