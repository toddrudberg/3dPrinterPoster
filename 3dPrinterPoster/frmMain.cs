using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static System.Windows.Forms.Design.AxImporter;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Printing;


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
      if (!string.IsNullOrEmpty(formSettings.LastSettingsFile))
      {
        try
        {
          //string json = File.ReadAllText(formSettings.LastSettingsFile);
          currentSettings = PrintSettings.Load(formSettings.LastSettingsFile); //JsonConvert.DeserializeObject<PrintSettings>(json);
          MessageBox.Show($"Loaded settings profile: {currentSettings.ProfileName}",
              "Settings Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
          MessageBox.Show($"Failed to load settings:\n{ex.Message}",
              "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
    }

    public frmMain()
    {
      InitializeComponent();
    }

    private void btnOpenFile_Click(object sender, EventArgs e)
    {
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
          ModifyPPG.DoTheThing(path, currentSettings, chkIncludeG29.Checked);

        }
      }
    }



    private void btnOpenSettingsFile_Click(object sender, EventArgs e)
    {
      using (OpenFileDialog openFileDialog = new OpenFileDialog())
      {

        openFileDialog.Filter = "Settings files (*.settings.json)|*.settings.json|All files (*.*)|*.*";
        openFileDialog.Title = "Open Print Settings File";

        // If there's a last file, set its folder and filename
        if (!string.IsNullOrEmpty(formSettings.LastSettingsFile))
        {
          openFileDialog.InitialDirectory = Path.GetDirectoryName(formSettings.LastSettingsFile);
          openFileDialog.FileName = Path.GetFileName(formSettings.LastSettingsFile);
        }

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
          try
          {
            string json = File.ReadAllText(openFileDialog.FileName);
            currentSettings = JsonConvert.DeserializeObject<PrintSettings>(json);
            formSettings.LastSettingsFile = openFileDialog.FileName;
            formSettings.SaveFormSettings(); // Save the last settings file path

            MessageBox.Show($"Loaded settings profile: {currentSettings.ProfileName}",
                "Settings Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
          }
          catch (Exception ex)
          {
            MessageBox.Show($"Failed to load settings:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          }
        }
      }
    }

    private void btnSaveSettingsFileAs_Click(object sender, EventArgs e)
    {
      using (SaveFileDialog saveFileDialog = new SaveFileDialog())
      {
        saveFileDialog.Title = "Save Settings File As";
        saveFileDialog.Filter = "Settings files (*.settings.json)|*.settings.json|All files (*.*)|*.*";
        saveFileDialog.DefaultExt = "settings.json";

        // Default to current settings file folder if available
        if (!string.IsNullOrEmpty(formSettings.LastSettingsFile))
        {
          saveFileDialog.InitialDirectory = Path.GetDirectoryName(formSettings.LastSettingsFile);
          saveFileDialog.FileName = Path.GetFileName(formSettings.LastSettingsFile);
        }
        else
        {
          saveFileDialog.FileName = "newProfile.settings.json";
        }

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
          try
          {
            // Serialize current settings
            string json = JsonConvert.SerializeObject(currentSettings, Formatting.Indented);
            File.WriteAllText(saveFileDialog.FileName, json);

            // Update formSettings
            formSettings.LastSettingsFile = saveFileDialog.FileName;

            MessageBox.Show("Settings saved successfully.", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
          }
          catch (Exception ex)
          {
            MessageBox.Show($"Failed to save settings:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
          }
        }
      }
    }

    private void btnTest_Click(object sender, EventArgs e)
    {
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


          //string[] lines = File.ReadAllLines(path);

          List<string> lines = File.ReadAllLines(path).ToList();
          int numSupportInterfaceFeatures = 0;

          foreach (string line in lines)
          {
            if( line.Contains("FEATURE:"))
            {
              string sFeature = line.Replace(" ", "");
              string output = line.Substring(sFeature.IndexOf("FEATURE:"));

              if( sFeature.Contains("Supportinterface"))
              {
                numSupportInterfaceFeatures++;
              }
              Console.WriteLine(output);
            }
          }
          Console.WriteLine($"Support Interface Count: {numSupportInterfaceFeatures}");
        }
      }
    }
  }
}
