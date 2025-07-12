using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3dPrinterPoster
{
  public class PrintSettings
  {
    public string ProfileName { get; set; } = "default";

    // Layer-based bed temperature settings
    public List<LayerTempSetting> BedTempByLayer { get; set; } = new();
    public List<LayerTempSetting> NozzleTempByLayer { get; set; } = new();

    // Layer-based speed settings
    public List<LayerSpeedSetting> SpeedByLayer { get; set; } = new();
  
    // Mass-based control
    public List<MassBasedFeedrate> FeedrateByMass { get; set; } = new();

    // Zone-based XY feedrate limiter
    public List<SlowZone> FeedrateZones { get; set; } = new();

    // Fan and other custom commands (optional for now)
    public List<CustomCommand> CustomCommands { get; set; } = new();
  }
  public class LayerTempSetting
  {
    public int Layer { get; set; }          // Use -1 to represent "end - N"
    public int Temp { get; set; }
  }

  public class LayerSpeedSetting
  {
    public int Layer { get; set; }          // Use -1 to represent "end - N"
    public int Speed { get; set; }
  }

  public class MassBasedFeedrate
  {
    public double MinMassG { get; set; }
    public double Feedrate { get; set; }
  }

  public class SlowZone
  {
    public double XMin { get; set; }
    public double XMax { get; set; }
    public double YMin { get; set; }
    public double YMax { get; set; }
    public double FeedrateLimit { get; set; }
  }

  public class CustomCommand
  {
    public int Layer { get; set; }
    public string Gcode { get; set; }
    public string Comment { get; set; } = "";
  }

}
