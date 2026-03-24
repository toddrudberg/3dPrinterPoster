using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace _3dPrinterPoster
{
  internal class ModifyPPG
  {

    public static void DoTheThing(string filename, PrintSettings options, string newPath)
    {
      List<string> lines = File.ReadAllLines(filename).ToList();
      bool isFromQidiStudio = lines.Any(line => line.Contains("QIDIStudio"));
      ToddUtils.FileParser.cFileParse fp = new ToddUtils.FileParser.cFileParse();

      #region Utilities
      string ClearWhiteSpace(string line )
      {
        var sb = new StringBuilder(line.Length);
        foreach (char c in line)
        {
          if (!char.IsWhiteSpace(c))
            sb.Append(c);
        }
        return sb.ToString();
      }

      int CheckCurrentLayer(PrintSettings options, string line)
      {
        int currentLayer = 0;
        string layerTag = isFromQidiStudio ? ";Z_HEIGHT:" : ";Z:";
        ToddUtils.FileParser.cFileParse fp = new ToddUtils.FileParser.cFileParse();
        string lineNoWS = ClearWhiteSpace(line);
        
        if (lineNoWS.StartsWith(layerTag))
        {
          lineNoWS = isFromQidiStudio ? lineNoWS.Replace("Z_HEIGHT:", "Z:") : lineNoWS;
          string ZArgument = "Z:";
          double currentZ = fp.GetArgument2(lineNoWS, ZArgument);
          currentLayer = (int)Math.Round(currentZ / options.LayerHeight);
          if (currentLayer < 1) currentLayer = 1;
        }

        return currentLayer;
      }

      #endregion

      List<string> InitialAndGlobalSettings(List<string> inputLines, PrintSettings options)
      {
        List<string> result = new List<string>();
        #region GlobalSettings
        string[] lines = inputLines.ToArray();
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
          if (trimmed.StartsWith("G29") && !options.bedLeveling) return false;

          return true;
        }).ToList();
        #endregion

        bool nozzleTempSet = false;
        int? nozzleTarget = options?.GetNozzleTempForLayer(1);
        int? bedTarget = options?.GetBedTempForLayer(1);
        int? chamberTarget = (options?.ChamberTempC is > 0) ? options.ChamberTempC : null;
        bool startUpSetingsComplete = false;
        bool executionEnd = false;

        for(int i = 0; i < cleanedLines.Count; i++) 
        {
          string line = cleanedLines[i];
          if (!startUpSetingsComplete)
          {
            if (options.Printer == PrinterType.QIDI_X_MAX_3)
            {
              if (options.bedLeveling && line.Trim().Contains("G29"))
              {
                result.Add($"M190 S{bedTarget} ; Wait for bed temp before G29 - inserted by 3D Printer Poster");
                result.Add(line + " ; G29 included by User Preference.");
              }

              // Handle initial nozzle temperature before any extrusion (E > 0)
              if (!nozzleTempSet)
              {
                if (line.Contains("G0") || line.Contains("G1"))
                {
                  if (fp.GetArgument(line, "E", out double evalue) && evalue > 0)
                  {
                    result.Add($"M140 S{bedTarget} ; start bed heating (non-blocking)");
                    if (chamberTarget.Value > 0)
                      result.Add($"M141 S{chamberTarget} ; start chamber heating (non-blocking)");
                    result.Add($"M190 S{bedTarget} ; wait for bed");
                    if (chamberTarget > 0)
                    {
                      if( chamberTarget > 40)
                        result.Add($"M191 S{40} ; wait for chamber");
                      else
                        result.Add($"M191 S{chamberTarget} ; wait for chamber");

                      result.Add($"M141 S{chamberTarget} ; start chamber heating (non-blocking)");
                    }
                    if (options.ChamberHold > 0)
                      result.Add($"G4 P{options.ChamberHold * 60 * 1000}");

                    result.Add($"M104 S{nozzleTarget} ; start nozzle preheat (non-blocking)");
                    result.Add($"M109 S{nozzleTarget} ; wait for nozzle");

                    nozzleTempSet = true;
                    startUpSetingsComplete = true;
                  }
                }
              }
              continue;
            }

            if (options.Printer == PrinterType.QIDI_Q1_Pro)
            {
              if (line.IndexOf("PRINT_START", StringComparison.OrdinalIgnoreCase) >= 0)
              {
                if (options.bedLeveling)
                {
                  // Build PRINT_START with only the params we have
                  if (chamberTarget > 0)
                  {
                    if (chamberTarget > bedTarget || chamberTarget > 40)
                    {
                      int? target = bedTarget < 40 ? bedTarget - 5 : 40;
                      result.Add($"M141 S{target} ; start chamber heating (non-blocking)");
                    }
                    else
                      result.Add($"M141 S{chamberTarget} ; start chamber heating (non-blocking)");
                  }
                  var parts = new List<string>
                  {
                    $"BED={bedTarget}",
                    $"HOTEND={nozzleTarget}"
                  };
                  if (chamberTarget > 0)
                  {
                    if (chamberTarget > bedTarget || chamberTarget > 40)
                    {
                      int? target = bedTarget < 40 ? bedTarget - 5 : 40;
                      parts.Add($"CHAMBER={target}");
                    }
                    else
                      parts.Add($"CHAMBER={chamberTarget}");
                  }

                  string newPrintStart = "PRINT_START " + string.Join(" ", parts);
                  result.Add(newPrintStart + " ; modified by 3D Printer Poster");
                }

                result.Add("M104 S100 ; Set Nozzle to a low temp while the bed and chamber catchup (non-blocking).");
                // Manual warm-up path (no PRINT_START)
                result.Add($"M140 S{bedTarget} ; start bed heating (non-blocking)");
                if (chamberTarget > 0)
                {
                  if (chamberTarget > bedTarget || chamberTarget > 40)
                  {
                    int? target = bedTarget < 40 ? bedTarget - 5 : 40;

                    result.Add($"M191 S{target} ; wait for chamber");
                  }
                  else
                    result.Add($"M191 S{chamberTarget} ; wait for chamber");

                  //result.Add($"M141 S{chamberTarget} ; start chamber heating (non-blocking)");
                }

                result.Add($"M190 S{bedTarget} ; wait for bed");
                //if (chamberTarget > 0)
                //  result.Add($"M191 S{chamberTarget} ; wait for chamber");

                if (options.ChamberHold > 0)
                {
                  result.Add($"G4 P{options.ChamberHold * 60 * 1000}");
                }

                result.Add($"M104 S{nozzleTarget} ; start nozzle preheat (non-blocking)");

                result.Add($"M109 S{nozzleTarget} ; wait for nozzle");

                line = " ; " + line;
                
                startUpSetingsComplete = true;
              }
            }
          }
          
          if (line.Contains("EXECUTABLE_BLOCK_END"))
          {
            result.Add("M140 S0 ; Shut down the bed, non-blocking");
            result.Add("M141 S0 ; Shut down the chamber, non-blocking");
            result.Add("M109 S0 ; Shut down the extruder, blocking");
            result.Add("M106 S0 ; Shut off fans");
            executionEnd = true;
          }

          result.Add(line);
        }
        return result;
      }
      List<string> SetFeedRatesByLayer(List<string> inputLines, PrintSettings options)
      {
        List<string> result = new List<string>();
        int currentLayer = 0;
        

        for (int ii = 0; ii < inputLines.Count; ii++)
        {
          string line = inputLines[ii];

          if( line.Contains("Z_HEIGHT:"))
            currentLayer = CheckCurrentLayer(options, line);
          
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
                  fp.ReplaceArgument(line, "F", currentSpeed * 60, out line, "F0");

                  var rule = options.GetSpeedRuleForLayer(currentLayer);
                  int ruleLayer = rule?.Layer ?? -1;

                  string where = ruleLayer > 0 ? $"(rule L{ruleLayer}+)" : "(default rule)";

                  line +=
                    $" ; feedrate capped to {currentSpeed * 60} mm/min ({currentSpeed} mm/s)";// at layer {currentLayer} ({Ordinal(currentLayer)}) {where} - modified by 3D Printer Poster";

                  result.Add(line);
                  continue;
                }
              }
            }
          }
          result.Add(line);
        }
        return result;
      }
      List<string> SetNozzleTempsByLayer(List<string> inputLines, PrintSettings options)
      {
        List<string> result = new List<string>();
        int currentLayer = 0;
        int lastLayer = 0;
        double lastBedTemp = 0;
        double lastChamberTemp = 0;
        double lastNozzleTemp = 0;
        for (int ii = 0; ii < inputLines.Count; ii++)
        {
          string line = inputLines[ii];

          if(line.Contains("Z_HEIGHT:"))
            currentLayer = CheckCurrentLayer(options, line);

          if (currentLayer != lastLayer)
          {
            lastLayer = currentLayer;

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
              result.Add($"; apply temperature setpoints for layer {currentLayer}");

              // Start all heaters in parallel (non-blocking)
              if (changeBed) result.Add($"M140 S{bedTarget} ; bed start (non-blocking)");
              if (changeNozzle) result.Add($"M104 S{nozzleTarget} ; nozzle start (non-blocking)");
              if (changeChamber) result.Add($"M141 S{chamberTarget} ; chamber start (non-blocking)");

              // On the first printable layer, wait before proceeding You've done this above!
              //if (currentLayer == 1)
              //{
              //  if (changeBed) result.Add($"M190 S{bedTarget} ; wait for bed");
              //  if (changeNozzle) result.Add($"M140 S{nozzleTarget} ; wait for nozzle");
              //  if (changeChamber) result.Add($"M141 S{chamberTarget} ; set chamber and go");
              //}

              if (changeNozzle) lastNozzleTemp = nozzleTarget.Value;
              if (changeBed) lastBedTemp = bedTarget.Value;
              if (changeChamber) lastChamberTemp = chamberTarget.Value;
            }
          }
          result.Add(line);
        }
        return result;
      }
      List<string> SetSupportInterfaceTemp(List<string> inputLines, PrintSettings options)
      {
        List<string> result = new List<string>();

        ToddUtils.FileParser.cFileParse fp = new ToddUtils.FileParser.cFileParse();

        if (options.ApplySupportInterfaceNozzleTemp)
        {
          int currentNozzleTemp;
          string lastNozzleTempCommand = "";
          bool inSupportInterfaceFeature = false;

          for (int ii = 0; ii < inputLines.Count; ii++)
          {
            string line = inputLines[ii];
            result.Add(line);

            if (line.Contains("M104") || line.Contains("M109"))
            {
              fp.GetArgument(line, "S", out double dNozzleTemp, true);
              currentNozzleTemp = (int)dNozzleTemp;
              lastNozzleTempCommand = line;
            }

            if (line.Contains("FEATURE: "))
            {
              line = line.Replace(" ", "");
              if (line.Contains("Supportinterface"))
              {
                if (!inSupportInterfaceFeature)
                {
                  result.Add($"M104 S{options.SupportInterfaceNozzleTemp} ; (Lowering temp for support interface)");
                  inSupportInterfaceFeature |= true;
                }
              }
              else
              {
                if (inSupportInterfaceFeature)
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
      List<string> SetCoolingFanLevels(List<string> input, PrintSettings options)
      {
        List<string> result = new List<string>();
        ToddUtils.FileParser.cFileParse fp = new ToddUtils.FileParser.cFileParse();
        int currentLayer = 1;
        foreach (string line in input)
        {
          if (line.Contains("Z_HEIGHT:"))
            currentLayer = CheckCurrentLayer(options, line);
          int currentSpeed = options.EnablePartCoolingFan ? options.GetFanSpeedForLayer(currentLayer) : 0;
          string thisline = line;
          if (line.Contains("M106"))
          {
            if (line.Contains("P"))
            {
              fp.GetArgument(line, "P", out double pcode, false);

              switch ((int)pcode)
              {
                case 0:
                  {
                    if (options.EnablePartCoolingFan)
                    {
                      fp.ReplaceArgument(line, "S", $"{currentSpeed * 255 / 100:F0}", out string newline);
                      var rule = options.GetFanSpeedRuleForLayer(currentLayer);
                      int ruleLayer = rule?.Layer ?? -1;
                      thisline = newline + $" ; fan speed {currentSpeed:F0}% for {ruleLayer}+"; // do nothing
                    }
                    else
                    {
                      thisline = "M106 P0 S0"; //turn part fan off!
                    }
                    break;
                  }
                case 1: { break; }
                case 2: { break; }
                case 3:
                  {
                    fp.GetArgument(line, "S", out double scode, false);
                    if (scode > 0)
                    {
                      fp.ReplaceArgument(thisline, "S", options.ChamberFanPercent * 255.0 / 100.0, out thisline, "F0");
                    }
                    break;
                  }
              }
            }
            else
            {
              fp.GetArgument(line, "S", out double scode, false);
              if (scode > 0)
              {
                if (options.EnablePartCoolingFan)
                {
                  fp.ReplaceArgument(line, "S", $"{currentSpeed * 255 / 100:F0}", out string newline);
                  var rule = options.GetFanSpeedRuleForLayer(currentLayer);
                  int ruleLayer = rule?.Layer ?? -1;
                  thisline = newline + $" ; fan speed {currentSpeed:F0}% for {ruleLayer}+"; // do nothing
                }
                else
                {
                  thisline = "M106 S0";
                }
              }
            }
          }
          result.Add(thisline);
        }
        return result;
      }
      List<string> InsertOperatorMessages(List<string> input, PrintSettings options)
      {
        List<string> CreateOperatorMessages(string line)
        {
          List<string> messages = new List<string>();

          int zIndex = line.IndexOf("Z_HEIGHT:");
          int layerIndex = line.IndexOf("layer");

          if (zIndex == -1)
            return messages;

          int currentLayer = CheckCurrentLayer(options, line);

          // Extract Z height
          int zStart = zIndex + "Z_HEIGHT:".Length;
          int zEnd = line.Length;
          string zHeight = line.Substring(zStart, zEnd - zStart).Trim();

          //messages.Add($"M118 Layer {currentLayer} T={zHeight}mm");
          var rule = options.GetSpeedRuleForLayer(currentLayer);
          int ruleLayer = rule?.Layer ?? -1;

          string where = ruleLayer > 0 ? $"(rule L{ruleLayer}+)" : "(default rule)";

          //messages.Add($"M118 SPEED {rule:F1} at {where}");

          int? nozzleTarget = options?.GetNozzleTempForLayer(currentLayer);
          int? bedTarget = options?.GetBedTempForLayer(currentLayer);

          messages.Add($"M118 L{currentLayer} Z{zHeight} F{rule:F0} N{nozzleTarget ?? 0} B{bedTarget ?? 0}");
          return messages;
        }

        List<string> result = new List<string>();
        ToddUtils.FileParser.cFileParse fp = new ToddUtils.FileParser.cFileParse();

        foreach (string line in input)
        {
          if (string.IsNullOrWhiteSpace(line))
          {
            result.Add(line);
            continue;
          }

          if (line.StartsWith("G4 P"))
          {
            fp.GetArgument(line, "P", out double pcode, false);
            result.Add($"M118 Pausing for {pcode / 1000 / 60:F1} minutes");
          }

          if (line.Contains("Z_HEIGHT"))
          {
            result.AddRange(CreateOperatorMessages(line));
          }

          result.Add(line);
        }

        return result;
      }
      List<string> CleanUpKnownBadCommands(List<string> input, PrintSettings options)
      {
        List<string> result = new List<string>();
        int i = 1;
        foreach( string s in input)
        {
          bool unknownCode = false;
          if (s == null)
            Console.WriteLine($"badline @ {i}");

          unknownCode = unknownCode || s == null;
          unknownCode = unknownCode || s.Contains("G17");
          unknownCode = unknownCode || s.Contains("M73");
          if(!unknownCode)
            result.Add(s);

          i++;
        }
        return result;
      }

      List<string> output = InitialAndGlobalSettings(lines, options);
      output = SetFeedRatesByLayer(output, options);
      output = SetNozzleTempsByLayer(output, options);
      output = SetSupportInterfaceTemp(output, options);
      output = SetCoolingFanLevels(output, options);
      output = InsertOperatorMessages(output, options);
      output = CleanUpKnownBadCommands(output, options);

      File.WriteAllLines(newPath, output);
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
