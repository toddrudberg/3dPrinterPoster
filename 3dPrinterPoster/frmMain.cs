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
      ToddUtils.FileParser.cFileParse fp = new ();

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
            currentSettings.CopyFrom(dlg.Options);   // commit changes into existing instance
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

          try
          {
            string[] lines = File.ReadAllLines(path);

            Console.WriteLine("File loaded with " + lines.Length + " lines.");

            int bedTemp = currentSettings.BedTempByLayer[0].Temp; // Get the first layer bed temperature
            int nozzleTemp = currentSettings.NozzleTempByLayer[0].Temp; // Get the first layer nozzle temperature
            int chamberTemp = currentSettings.ChamberTempC ?? 0; // Get chamber temp, default to 0 if not set

            // Step 1: Initial cleanup
            var cleanedLines = lines.Where(line =>
            {
              string trimmed = line.TrimStart();

              // Remove any lines that control temps — we'll handle them later
              if (trimmed.StartsWith("M104")) return false;
              if (trimmed.StartsWith("M140")) return false;
              if (trimmed.StartsWith("M109")) return false;
              if (trimmed.StartsWith("M190")) return false;
              if (trimmed.StartsWith("M191")) return false;
              if (trimmed.StartsWith("M141")) return false;

              // G29 is optional via checkbox
              if (trimmed.StartsWith("G29") && !chkIncludeG29.Checked) return false;

              return true;
            }).ToList();


            bool isFromQidiStudio = cleanedLines.Any(line => line.Contains("QIDIStudio"));
            string layerTag = isFromQidiStudio ? ";Z_HEIGHT:" : ";Z:";

            // Step 2: Apply layer-aware edits
            var modifiedLines = new List<string>();
            double currentZ = 0.0;
            int currentLayer = 0;
            double lastZ = 0;
            bool inWipeEnd = false;
            bool inE1command = false;

            double layerHeight = currentSettings.LayerHeight;
            bool nozzleTempSet = false;
            int? lastNozzleTemp = null;
            int? lastBedTemp = null;
            int? lastChamberTemp = null;
            bool ExecutionEnd = false;

            foreach (var line in cleanedLines)
            {
              string modifiedLine = line;

              if (ExecutionEnd)
              {
                modifiedLines.Add(line);
                continue; // Skip further processing if execution has ended
              }

              if (currentSettings.Printer == PrinterType.QIDI_X_MAX_3 && chkIncludeG29.Checked && line.Trim().Contains("G29"))
                {
                  modifiedLines.Add($"M190 S{bedTemp} ; Wait for bed temp before G29 - inserted by 3D Printer Poster");
                  modifiedLines.Add(line + " ; G29 included by User Preference.");
                  continue;
                }

              // Handle initial nozzle temperature before any extrusion (E > 0)
              if (!nozzleTempSet)
              {
                if (line.Contains("G0") || line.Contains("G1"))
                {
                  if (fp.GetArgument(line, "E", out double evalue) && evalue > 0)
                  {
                    modifiedLines.Add($"M140 S{bedTemp} ; start bed heating (non-blocking)");
                    modifiedLines.Add($"M104 S{nozzleTemp} ; start nozzle preheat (non-blocking)");
                    if (chamberTemp > 0)
                      modifiedLines.Add($"M141 S{chamberTemp} ; start chamber heating (non-blocking)");
                    modifiedLines.Add($"M190 S{bedTemp} ; wait for bed");
                    modifiedLines.Add($"M109 S{nozzleTemp} ; wait for nozzle");
                    if (chamberTemp > 0)
                      modifiedLines.Add($"M191 S{chamberTemp} ; wait for chamber");

                    nozzleTempSet = true;
                  }
                }
              }


              if (currentSettings.Printer == PrinterType.QIDI_Q1_Pro && line.IndexOf("PRINT_START", StringComparison.OrdinalIgnoreCase) >= 0)
              {

                if (chkIncludeG29.Checked)
                {
                  // Build PRINT_START with only the params we have
                  if (chamberTemp > 0)
                    modifiedLines.Add($"M141 S{chamberTemp} ; start chamber heating (non-blocking)");
                  var parts = new List<string>
                  {
                    $"BED={bedTemp}",
                    $"HOTEND={nozzleTemp}"
                  };
                  if (chamberTemp > 0) parts.Add($"CHAMBER={chamberTemp}");

                  string newPrintStart = "PRINT_START " + string.Join(" ", parts);
                  modifiedLines.Add(newPrintStart + " ; modified by 3D Printer Poster");

                  // IMPORTANT: do NOT emit your own M140/M104/M141/M190/M109/M191 here
                  // if your PRINT_START macro already handles heating/mesh/homing.
                }
                else
                {
                  // Manual warm-up path (no PRINT_START)
                  modifiedLines.Add($"M140 S{bedTemp} ; start bed heating (non-blocking)");
                  modifiedLines.Add($"M104 S{nozzleTemp} ; start nozzle preheat (non-blocking)");
                  if (chamberTemp > 0)
                    modifiedLines.Add($"M141 S{chamberTemp} ; start chamber heating (non-blocking)");

                  modifiedLines.Add($"M190 S{bedTemp} ; wait for bed");
                  modifiedLines.Add($"M109 S{nozzleTemp} ; wait for nozzle");
                  if (chamberTemp > 0)
                    modifiedLines.Add($"M191 S{chamberTemp} ; wait for chamber");

                  modifiedLines.Add("; " + line); // keep original as comment for traceability
                }
                continue;
              }


              // handle temps for layer changes
              string lineTrimmed = Regex.Replace(line, @"\s+", "");
              if (lineTrimmed.StartsWith(layerTag))
              {
                lineTrimmed = isFromQidiStudio ? lineTrimmed.Replace("Z_HEIGHT:", "Z:") : lineTrimmed;
                string ZArgument = "Z:";
                currentZ = fp.GetArgument2(lineTrimmed, ZArgument);

                currentLayer = (int)Math.Round(currentZ / layerHeight);
                if (currentLayer < 1) currentLayer = 1;

                modifiedLines.Add(modifiedLine + $" ; layer {currentLayer}");

                int? nozzleTarget = currentSettings?.GetNozzleTempForLayer(currentLayer);
                int? bedTarget = currentSettings?.GetBedTempForLayer(currentLayer);

                // Single chamber value (optional)
                int? chamberTarget = (currentSettings?.ChamberTempC is > 0) ? currentSettings.ChamberTempC : null;

                // Emit only if present AND changed since last time
                bool changeNozzle = nozzleTarget.HasValue && nozzleTarget != lastNozzleTemp;
                bool changeBed = bedTarget.HasValue && bedTarget != lastBedTemp;
                bool changeChamber = chamberTarget.HasValue && chamberTarget != lastChamberTemp;

                if (changeNozzle || changeBed || changeChamber)
                {
                  modifiedLines.Add($"; apply temperature setpoints for layer {currentLayer}");

                  // Start all heaters in parallel (non-blocking)
                  if (changeBed) modifiedLines.Add($"M140 S{bedTarget} ; bed start (non-blocking)");
                  if (changeNozzle) modifiedLines.Add($"M104 S{nozzleTarget} ; nozzle start (non-blocking)");
                  if (changeChamber) modifiedLines.Add($"M141 S{chamberTarget} ; chamber start (non-blocking)");

                  // On the first printable layer, wait before proceeding
                  if (currentLayer == 1)
                  {
                    if (changeBed) modifiedLines.Add($"M190 S{bedTarget} ; wait for bed");
                    if (changeNozzle) modifiedLines.Add($"M109 S{nozzleTarget} ; wait for nozzle");
                    if (changeChamber) modifiedLines.Add($"M191 S{chamberTarget} ; wait for chamber");
                  }

                  if (changeNozzle) lastNozzleTemp = nozzleTarget;
                  if (changeBed) lastBedTemp = bedTarget;
                  if (changeChamber) lastChamberTemp = chamberTarget;
                }

                continue;
              }



              //Override Feedrates
              if (line.StartsWith("G1"))
              {
                double argument;
                bool farg = fp.GetArgument(line, "F", out argument);
                double fCmd = argument;

                if (farg)
                {
                  bool xarg = fp.GetArgument(line, "X", out argument);
                  bool yarg = fp.GetArgument(line, "Y", out argument);
                  bool earg = fp.GetArgument(line, "E", out argument);

                  if (line.StartsWith("G1 F") || (xarg && yarg && earg))
                  {
                    int currentSpeed = currentSettings.GetSpeedForLayer(currentLayer);

                    if (fCmd > currentSpeed * 60)
                    {
                      fp.ReplaceArgument(modifiedLine, "F", currentSpeed * 60, out modifiedLine, "F0");

                      var rule = currentSettings.GetSpeedRuleForLayer(currentLayer);
                      int ruleLayer = rule?.Layer ?? -1;

                      string where = ruleLayer > 0 ? $"(rule L{ruleLayer}+)" : "(default rule)";

                      modifiedLine +=
                        $" ; feedrate capped to {currentSpeed * 60} mm/min ({currentSpeed} mm/s) at layer {currentLayer} ({Ordinal(currentLayer)}) {where} - modified by 3D Printer Poster";

                      modifiedLines.Add(modifiedLine);
                      continue;
                    }
                  }
                }
              }


              if (currentLayer > 0)
              {
                inWipeEnd = line.Contains("WIPE_END") || inWipeEnd;

                int gcmd = (int)fp.GetArgument2(line, "G");
                if (gcmd == 0 || gcmd == 1)
                {
                  if (line.Contains("Z"))
                  {
                    double zcmd = fp.GetArgument2(line, "Z");
                    if (zcmd - lastZ > .2 && inWipeEnd)
                    {
                      modifiedLines.Add(modifiedLine + " ; Z hop detected");
                      //Console.WriteLine("Z hop detected");
                      inWipeEnd = false; // reset after handling Z hop
                      continue;
                    }
                    lastZ = zcmd;
                  }
                }
              }

              if (line.Contains("EXECUTABLE_BLOCK_END"))
              {
                modifiedLines.Add("M140 S0 ; Shut down the bed, non-blocking");
                modifiedLines.Add("M141 S0 ; Shut down the chamber, non-blocking");
                modifiedLines.Add("M109 S0 ; Shut down the extruder, blocking");
                modifiedLines.Add("M106 S0 ; Shut off fans");
                ExecutionEnd = true;
              }

              // Default: add line unchanged
              modifiedLines.Add(line);
            }

            string originalPath = openFileDialog.FileName;
            string directory = Path.GetDirectoryName(path);
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(path);
            string newPath = Path.Combine(directory, $"{filenameWithoutExt}_mod.gcode");

            File.WriteAllLines(newPath, modifiedLines);

            Console.WriteLine($"Modified file saved as:\n{newPath}", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

          }
          catch (Exception ex)
          {
            MessageBox.Show("Error reading file: " + ex.Message);
          }
        }
      }
    }

    private static string Ordinal(int n)
    {
      if (n <= 0) return n.ToString();
      int mod100 = n % 100;
      if (mod100 is 11 or 12 or 13) return $"{n}th";
      return (n % 10) switch
      {
        1 => $"{n}st",
        2 => $"{n}nd",
        3 => $"{n}rd",
        _ => $"{n}th"
      };
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
  }
}
