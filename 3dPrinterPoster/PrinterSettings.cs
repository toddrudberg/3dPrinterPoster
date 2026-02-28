using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms.Design;

namespace _3dPrinterPoster
{
  public class PrintSettings
  {
    [Category("Identification")]
    [DisplayName("Profile Name")]
    public string ProfileName { get; set; } = "default";

    [Category("Identification")]
    public MaterialType Material { get; set; } = MaterialType.Unknown;

    [Category("Identification")]
    public PrinterType Printer { get; set; } = PrinterType.Unknown;

    [Category("LayerHeight (mm)")]
    [DefaultValue(0.2)]
    public double LayerHeight { get; set; } = 0.3;

    [Category("Thermals")]
    [DisplayName("Chamber Temp (°C)")]
    [Description("Single chamber target in °C. Leave blank (null) to not set.")]
    public int? ChamberTempC { get; set; } = null;

    [Category("Thermals")]
    [DisplayName("Bed Temp by Layer")]
    [Editor(typeof(CollectionEditor), typeof(UITypeEditor))]
    [Description("Layer-indexed bed temperature targets.")]
    public List<LayerTempSetting> BedTempByLayer { get; set; } = new();

    [Category("Thermals")]
    [DisplayName("Nozzle Temp by Layer")]
    [Editor(typeof(CollectionEditor), typeof(UITypeEditor))]
    [Description("Layer-indexed nozzle temperature targets.")]
    public List<LayerTempSetting> NozzleTempByLayer { get; set; } = new();

    [Category("Motion")]
    [DisplayName("Speed by Layer")]
    [Editor(typeof(CollectionEditor), typeof(UITypeEditor))]
    [Description("Layer-indexed speed targets (mm/s).")]
    public List<LayerSpeedSetting> SpeedByLayer { get; set; } = new();

    // --- Add these settings (optional but recommended) ---

    [Category("Flow")]
    [DisplayName("Assumed Line Width (mm)")]
    [Description("Used for flow preview calculations. Set this to your typical line width (e.g., 0.65 for a 0.6 nozzle).")]
    [DefaultValue(0.65)]
    public double AssumedLineWidthMm { get; set; } = 0.65;

    [Category("Flow")]
    [DisplayName("Recommended Max Flow (mm³/s)")]
    [Description("Your personal recommended ceiling for this material/printer/hotend combo. Used for preview only.")]
    [DefaultValue(20.0)]
    public double RecommendedMaxFlowMm3PerSec { get; set; } = 20.0;

    // --- Previews ---

    [Category("Preview")]
    [DisplayName("Max Speed (from ramp)")]
    [ReadOnly(true)]
    public int MaxSpeedPreviewMmPerSec =>
      (SpeedByLayer == null || SpeedByLayer.Count == 0)
        ? 0
        : SpeedByLayer.OrderBy(s => s.Layer).Last().Speed;

    [Category("Preview")]
    [DisplayName("Flow @ Max Speed")]
    [ReadOnly(true)]
    public string FlowAtMaxSpeedPreview
    {
      get
      {
        int vmax = MaxSpeedPreviewMmPerSec;
        if (vmax <= 0) return "(no speed ramp)";
        if (LayerHeight <= 0) return "(invalid LayerHeight)";
        if (AssumedLineWidthMm <= 0) return "(invalid line width)";

        double flow = AssumedLineWidthMm * LayerHeight * vmax; // mm^3/s
        string verdict = (RecommendedMaxFlowMm3PerSec > 0)
          ? (flow <= RecommendedMaxFlowMm3PerSec ? "OK" : "HIGH")
          : "—";

        return $"{flow:0.0} mm³/s  (w={AssumedLineWidthMm:0.###}, h={LayerHeight:0.###}, v={vmax} mm/s)  [{verdict}]";
      }
    }

    [Category("Preview")]
    [DisplayName("Recommended Max Flow")]
    [ReadOnly(true)]
    public string RecommendedMaxFlowPreview =>
      (RecommendedMaxFlowMm3PerSec > 0)
        ? $"{RecommendedMaxFlowMm3PerSec:0.0} mm³/s  (material/printer guideline)"
        : "(not set)";

    // Optional: keeps things tidy if you want a deterministic view/save
    public void SortAll()
    {
      BedTempByLayer = BedTempByLayer.OrderBy(x => x.Layer).ToList();
      NozzleTempByLayer = NozzleTempByLayer.OrderBy(x => x.Layer).ToList();
      SpeedByLayer = SpeedByLayer.OrderBy(x => x.Layer).ToList();
    }

    [Category("Ramp Summary")]
    [DisplayName("Speed Ramp")]
    [ReadOnly(true)]
    public string SpeedRampPreview => RampPreview(
  SpeedByLayer?.OrderBy(x => x.Layer).Select(x => $"L{x.Layer}:{x.Speed}mm/s")
);

    [Category("Ramp Summary")]
    [DisplayName("Nozzle Ramp")]
    [ReadOnly(true)]
    public string NozzleRampPreview => RampPreview(
      NozzleTempByLayer?.OrderBy(x => x.Layer).Select(x => $"L{x.Layer}:{x.Temp}°C")
    );

    [Category("Ramp Summary")]
    [DisplayName("Bed Ramp")]
    [ReadOnly(true)]
    public string BedRampPreview => RampPreview(
      BedTempByLayer?.OrderBy(x => x.Layer).Select(x => $"L{x.Layer}:{x.Temp}°C")
    );

    private static string RampPreview(IEnumerable<string>? items, int maxChars = 180)
    {
      if (items == null) return "(none)";
      string s = string.Join(", ", items);
      if (string.IsNullOrWhiteSpace(s)) return "(none)";
      return s.Length <= maxChars ? s : s.Substring(0, maxChars) + "…";
    }


    public PrintSettings Clone()
    {
      return new PrintSettings
      {
        ProfileName = this.ProfileName,
        Material = this.Material,
        Printer = this.Printer,
        LayerHeight = this.LayerHeight,
        ChamberTempC = this.ChamberTempC,
        BedTempByLayer = this.BedTempByLayer
              .Select(x => new LayerTempSetting { Layer = x.Layer, Temp = x.Temp })
              .ToList(),
        NozzleTempByLayer = this.NozzleTempByLayer
              .Select(x => new LayerTempSetting { Layer = x.Layer, Temp = x.Temp })
              .ToList(),
        SpeedByLayer = this.SpeedByLayer
              .Select(x => new LayerSpeedSetting { Layer = x.Layer, Speed = x.Speed })
              .ToList()
      };
    }

    public void CopyFrom(PrintSettings other)
    {
      if (other == null) throw new ArgumentNullException(nameof(other));

      ProfileName = other.ProfileName;
      Material = other.Material;
      Printer = other.Printer;
      LayerHeight = other.LayerHeight;
      ChamberTempC = other.ChamberTempC;

      BedTempByLayer = other.BedTempByLayer.Select(x => new LayerTempSetting { Layer = x.Layer, Temp = x.Temp }).ToList();
      NozzleTempByLayer = other.NozzleTempByLayer.Select(x => new LayerTempSetting { Layer = x.Layer, Temp = x.Temp }).ToList();
      SpeedByLayer = other.SpeedByLayer.Select(x => new LayerSpeedSetting { Layer = x.Layer, Speed = x.Speed }).ToList();
    }

    public int GetSpeedForLayer(int layer)
    {
      if (SpeedByLayer == null || SpeedByLayer.Count == 0)
        throw new InvalidOperationException("No speed settings defined.");

      var match = SpeedByLayer
          .Where(s => s.Layer <= layer)
          .OrderBy(s => s.Layer)
          .LastOrDefault();

      return match?.Speed ?? SpeedByLayer.OrderBy(s => s.Layer).First().Speed;
    }

    public int? GetNozzleTempForLayer(int layerNumber)
    {
      if (NozzleTempByLayer == null || NozzleTempByLayer.Count == 0) return null;
      return NozzleTempByLayer
          .Where(t => t.Layer <= layerNumber)
          .OrderBy(t => t.Layer)
          .LastOrDefault()
          ?.Temp;
    }

    public int? GetBedTempForLayer(int layerNumber)
    {
      if (BedTempByLayer == null || BedTempByLayer.Count == 0) return null;
      return BedTempByLayer
          .Where(t => t.Layer <= layerNumber)
          .OrderBy(t => t.Layer)
          .LastOrDefault()
          ?.Temp;
    }

    public LayerSpeedSetting? GetSpeedRuleForLayer(int layerNumber)
    {
      if (SpeedByLayer == null || SpeedByLayer.Count == 0) return null;

      return SpeedByLayer
          .Where(s => s.Layer <= layerNumber)
          .OrderBy(s => s.Layer)
          .LastOrDefault();
    }

    public static PrintSettings Load(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        throw new ArgumentException("Invalid config path.");

      if (!File.Exists(path))
        return new PrintSettings(); // return defaults

      var options = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
          new JsonStringEnumConverter()
        }
      };

      string json = File.ReadAllText(path);

      var settings = JsonSerializer.Deserialize<PrintSettings>(json, options)
                     ?? new PrintSettings();

      settings.SortAll(); // keep layer entries ordered

      return settings;
    }

    public void Save(string path)
    {
      var options = new JsonSerializerOptions
      {
        WriteIndented = true,
        Converters =
        {
          new JsonStringEnumConverter()
        }
      };

      SortAll();

      string json = JsonSerializer.Serialize(this, options);
      File.WriteAllText(path, json);
    }

    public static void SaveAs(string path, PrintSettings optionsIn)
    {
      var options = new JsonSerializerOptions
      {
        WriteIndented = true,
        Converters =
        {
          new JsonStringEnumConverter()
        }
      };

      string json = JsonSerializer.Serialize(optionsIn, options);
      File.WriteAllText(path, json);
    }
  }

  [TypeConverter(typeof(ExpandableObjectConverter))]
  public class LayerTempSetting
  {
    [DisplayName("Layer #")]
    public int Layer { get; set; }

    [DisplayName("Temp (°C)")]
    public int Temp { get; set; }

    public override string ToString() => $"L{Layer} → {Temp}°C";
  }

  [TypeConverter(typeof(ExpandableObjectConverter))]
  public class LayerSpeedSetting
  {
    [DisplayName("Layer #")]
    public int Layer { get; set; }

    [DisplayName("Speed (mm/s)")]
    public int Speed { get; set; }

    public override string ToString() => $"L{Layer} → {Speed} mm/s";

  }

  public enum MaterialType { Unknown = 0, PLA, PETG, ABS, ASA, PA12_CF, PA12_GF, Nylon, PC, TPU }
  public enum PrinterType { Unknown = 0, QIDI_Q1_Pro, QIDI_X_MAX_3, Bambu_X1C, Prusa_MK4, Other }
}