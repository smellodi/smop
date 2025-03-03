namespace Smop.MainApp.Controllers;

public class SetupSettings
{
    /// <summary>
    /// Dilution ratio denominator, ie. "x" in "1:x"
    /// </summary>
    public float DilutionRatio
    {
        get => Properties.Settings.Default.Setup_DilutionRatio;
        set
        {
            if (Properties.Settings.Default.Setup_DilutionRatio != value)
            {
                Properties.Settings.Default.Setup_DilutionRatio = value;
                Properties.Settings.Default.Save();
            }
        }
    }
}
