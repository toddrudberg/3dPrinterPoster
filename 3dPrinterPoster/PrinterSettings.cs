namespace _3dPrinterPoster
{
  public class PrintSettings
  {
    public string ProfileName { get; set; } = "default";

    // Identification
    public MaterialType Material { get; set; } = MaterialType.Unknown;
    public PrinterType Printer { get; set; } = PrinterType.Unknown;

    // Temps
    public List<LayerTempSetting> BedTempByLayer { get; set; } = new();
    public List<LayerTempSetting> NozzleTempByLayer { get; set; } = new();

    // NEW: single chamber target (°C). Null/0 means "don’t set".
    public int? ChamberTempC { get; set; } = null;

    // Speeds
    public List<LayerSpeedSetting> SpeedByLayer { get; set; } = new();

  }

  public class LayerTempSetting { public int Layer { get; set; } public int Temp { get; set; } }
  public class LayerSpeedSetting { public int Layer { get; set; } public int Speed { get; set; } }

  public enum MaterialType { Unknown = 0, PLA, PETG, ABS, ASA, PA12_CF, Nylon, PC, TPU }
  public enum PrinterType { Unknown = 0, QIDI_Q1_Pro, QIDI_X_MAX_3, Bambu_X1C, Prusa_MK4, Other }
}
