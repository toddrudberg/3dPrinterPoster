using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace _3dPrinterPoster
{

  public class FormSettings
  {
    private static readonly string settingsFolder = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
              "3dPrinterPoster" // Change this to your actual app name
              );

    private static readonly string settingsPath = Path.Combine(settingsFolder, "formsettings.json");
    public string LastSettingsFile { get; set; } = string.Empty;
    public string LastGcodeFile { get; set; } = string.Empty;

    public FormSettings LoadFormSettings()
    {
      if (File.Exists(settingsPath))
      {
        FormSettings formSettings;
        try
        {

          string json = File.ReadAllText(settingsPath);
          formSettings = JsonConvert.DeserializeObject<FormSettings>(json) ?? new FormSettings();
        }
        catch
        {
          formSettings = new FormSettings(); // fallback on error
        }
        return formSettings;
      }
      else
      {
        // Ensure the settings folder exists
        Directory.CreateDirectory(settingsFolder);
        return new FormSettings(); // Return a new instance if file doesn't exist
      }
    }

    public void SaveFormSettings()
    {
      try
      {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(settingsPath, json);
      }
      catch (Exception ex)
      {
        MessageBox.Show("Failed to save form settings:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }
  }
}
