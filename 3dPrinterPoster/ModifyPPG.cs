using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace _3dPrinterPoster
{
  internal class ModifyPPG
  {

    public static void DoTheThing(string filename, PrintSettings options, bool includeG29)
    {
      List<string> lines = File.ReadAllLines(filename).ToList();


      List<string> output = MainOptionsModifier(lines, options, includeG29);
      if (output == null)
        return;

      output = SetSupportInterfaceTemp(output, options);





      string path = filename;
      string directory = Path.GetDirectoryName(path);
      string filenameWithoutExt = Path.GetFileNameWithoutExtension(path);
      string newPath = Path.Combine(directory, $"{filenameWithoutExt}_mod.gcode");

      File.WriteAllLines(newPath, output);

      Console.WriteLine($"Modified file saved as:\n{newPath}", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }


    private static List<string> SetSupportInterfaceTemp(List<string> inputLines, PrintSettings options)
    {
      List<string> result = new List<string>();

      ToddUtils.FileParser.cFileParse fp = new ToddUtils.FileParser.cFileParse();

      if(options.ApplySupportInterfaceNozzleTemp)
      {
        int currentNozzleTemp;
        string lastNozzleTempCommand = "";
        bool inSupportInterfaceFeature = false;

        for(int ii = 0; ii < inputLines.Count; ii++)
        {
          string line = inputLines[ii];
          result.Add(line);

          if( line.Contains("M104") || line.Contains("M109"))
          {
            fp.GetArgument(line, "S", out double dNozzleTemp, true);
            currentNozzleTemp = (int) dNozzleTemp;
            lastNozzleTempCommand = line;
          }

          if (line.Contains("FEATURE: "))
          {
            line = line.Replace(" ", "");
            if(line.Contains("Supportinterface"))
            {
              if (!inSupportInterfaceFeature)
              {
                result.Add($"M104 S{options.SupportInterfaceNozzleTemp} ; (Lowering temp for support interface)");
                inSupportInterfaceFeature |= true;
              }
            }
            else
            {
              if(inSupportInterfaceFeature)
              {
                result.Add(lastNozzleTempCommand.Replace("M104", "M104") + " ; (Raising temp after support interface)");
                inSupportInterfaceFeature = false;
              }
            }
          }
        }
        return result;
      }
      else
      {
        return inputLines;
      }

    }


    private static List<string> MainOptionsModifier(List<string> inputLines, PrintSettings options, bool includeG29)
    {
      ToddUtils.FileParser.cFileParse fp = new ToddUtils.FileParser.cFileParse();

      try
      {
        string[] lines = inputLines.ToArray();

        Console.WriteLine("File loaded with " + lines.Length + " lines.");

        int bedTemp = options.BedTempByLayer[0].Temp; // Get the first layer bed temperature
        int nozzleTemp = options.NozzleTempByLayer[0].Temp; // Get the first layer nozzle temperature
        int chamberTemp = options.ChamberTempC ?? 0; // Get chamber temp, default to 0 if not set

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
          if (trimmed.StartsWith("G29") && !includeG29) return false;

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

        double layerHeight = options.LayerHeight;
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

          if (options.Printer == PrinterType.QIDI_X_MAX_3 && includeG29 && line.Trim().Contains("G29"))
          {
            modifiedLines.Add($"M190 S{bedTemp} ; Wait for bed temp before G29 - inserted by 3D Printer Poster");
            modifiedLines.Add(line + " ; G29 included by User Preference.");
            continue;
          }

          // Handle initial nozzle temperature before any extrusion (E > 0)
          if (!nozzleTempSet && options.Printer != PrinterType.QIDI_Q1_Pro)
          {
            if (line.Contains("G0") || line.Contains("G1"))
            {
              if (fp.GetArgument(line, "E", out double evalue) && evalue > 0)
              {
                modifiedLines.Add($"M140 S{bedTemp} ; start bed heating (non-blocking)");
                if (chamberTemp > 0)
                  modifiedLines.Add($"M141 S{chamberTemp} ; start chamber heating (non-blocking)");
                modifiedLines.Add($"M190 S{bedTemp} ; wait for bed");
                if (chamberTemp > 0)
                  modifiedLines.Add($"M191 S{chamberTemp} ; wait for chamber");
                if (options.ChamberHold > 0)
                  modifiedLines.Add($"G4 P{options.ChamberHold * 60 * 1000}");

                modifiedLines.Add($"M104 S{nozzleTemp} ; start nozzle preheat (non-blocking)");
                modifiedLines.Add($"M109 S{nozzleTemp} ; wait for nozzle");

                nozzleTempSet = true;
              }
            }
          }


          if (options.Printer == PrinterType.QIDI_Q1_Pro && line.IndexOf("PRINT_START", StringComparison.OrdinalIgnoreCase) >= 0)
          {

            if (includeG29)
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
            //else
            {
              // Manual warm-up path (no PRINT_START)
              modifiedLines.Add($"M140 S{bedTemp} ; start bed heating (non-blocking)");
              if (chamberTemp > 0)
                modifiedLines.Add($"M141 S{chamberTemp} ; start chamber heating (non-blocking)");

              modifiedLines.Add($"M190 S{bedTemp} ; wait for bed");
              if (chamberTemp > 0)
                modifiedLines.Add($"M191 S{chamberTemp} ; wait for chamber");

              if (options.ChamberHold > 0)
              {
                modifiedLines.Add($"G4 P{options.ChamberHold * 60 * 1000}");
              }

              modifiedLines.Add($"M104 S{nozzleTemp} ; start nozzle preheat (non-blocking)");

              modifiedLines.Add($"M109 S{nozzleTemp} ; wait for nozzle");


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

            int? nozzleTarget = options?.GetNozzleTempForLayer(currentLayer);
            int? bedTarget = options?.GetBedTempForLayer(currentLayer);

            // Single chamber value (optional)
            int? chamberTarget = (options?.ChamberTempC is > 0) ? options.ChamberTempC : null;

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
                int currentSpeed = options.GetSpeedForLayer(currentLayer);

                if (fCmd > currentSpeed * 60)
                {
                  fp.ReplaceArgument(modifiedLine, "F", currentSpeed * 60, out modifiedLine, "F0");

                  var rule = options.GetSpeedRuleForLayer(currentLayer);
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

        return modifiedLines;


      }
      catch (Exception ex)
      {
        MessageBox.Show("Error reading file: " + ex.Message);
        return null;
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
  }
}
