using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _3dPrinterPoster
{
  public partial class TuningDialog : Form
  {


    //private readonly PrintSettings _original;
    //public PrintSettings Options { get; private set; }


    private readonly PrintSettings _original;
    public PrintSettings Options { get; private set; }

    public TuningDialog(PrintSettings settings)
    {
      InitializeComponent();

      _original = settings;
      Options = settings.Clone();   // <-- edit a copy

      propertyGrid1.SelectedObject = Options;

      Text = "Program Tuning";
      AcceptButton = btnOK; CancelButton = btnCancel;
    }

    //public TuningDialog(PrintSettings options)
    //{
    //  InitializeComponent();
    //  _original = options;
    //  Options = Clone(options);                 // work on a copy
    //  propertyGrid1.SelectedObject = Options;
    //  Text = "Program Tuning";
    //  AcceptButton = btnOK; CancelButton = btnCancel;
    //}

    private void btnOK_Click(object sender, EventArgs e) => DialogResult = DialogResult.OK;
    private void btnCancel_Click(object sender, EventArgs e) => DialogResult = DialogResult.Cancel;

    private void btnReset_Click(object sender, EventArgs e)
    {
      Options = new PrintSettings();     // reset to defaults
      propertyGrid1.SelectedObject = Options;
    }

    private static PrintSettings Clone(PrintSettings src)
    {
      var json = System.Text.Json.JsonSerializer.Serialize(src);
      return System.Text.Json.JsonSerializer.Deserialize<PrintSettings>(json)!;
    }

    protected override void OnLoad(EventArgs e)
    {
      base.OnLoad(e);
      EnsureOnScreen(this);
      ForceVisible(this);
    }

    static void EnsureOnScreen(Form f)
    {
      var r = f.Bounds;
      var screen = Screen.FromRectangle(r).WorkingArea;
      int nx = Math.Max(screen.Left, Math.Min(r.Left, screen.Right - f.Width));
      int ny = Math.Max(screen.Top, Math.Min(r.Top, screen.Bottom - f.Height));
      f.Location = new Point(nx, ny);
    }
    static void ForceVisible(Form f)
    {
      f.WindowState = FormWindowState.Normal;
      f.Opacity = 1;
      f.ShowInTaskbar = false;

      // Clamp to ANY connected screen
      var union = Screen.AllScreens.Select(s => s.WorkingArea)
                                   .Aggregate(Rectangle.Union);
      var b = f.Bounds;

      int x = Math.Max(union.Left, Math.Min(b.Left, union.Right - b.Width));
      int y = Math.Max(union.Top, Math.Min(b.Top, union.Bottom - b.Height));
      if (x != b.Left || y != b.Top) f.Location = new Point(x, y);

      // If still no intersection (e.g., zero size), center on primary with sane size
      if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(f.Bounds)))
      {
        var wa = Screen.PrimaryScreen.WorkingArea;
        f.Size = new Size(Math.Min(900, wa.Width - 100), Math.Min(650, wa.Height - 100));
        f.Location = new Point(wa.Left + (wa.Width - f.Width) / 2,
                               wa.Top + (wa.Height - f.Height) / 2);
      }

      f.TopMost = true;      // temporary: ensure it’s above the main form
      f.Activate();
      f.BringToFront();
    }

    private void TuningDialog_Load(object sender, EventArgs e)
    {
      this.StartPosition = FormStartPosition.CenterParent;
    }
  }
}
