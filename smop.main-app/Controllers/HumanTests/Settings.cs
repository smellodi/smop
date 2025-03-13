using System.Collections.Generic;

namespace Smop.MainApp.Controllers.HumanTests;

public class Settings
{
    public int Repetitions
    {
        get => Properties.Settings.Default.HumanTest_Repetitions;
        set
        {
            Properties.Settings.Default.HumanTest_Repetitions = value;
            Properties.Settings.Default.Save();
        }
    }

    public bool IsRandomized
    {
        get => Properties.Settings.Default.HumanTest_Randomize;
        set
        {
            Properties.Settings.Default.HumanTest_Randomize = value;
            Properties.Settings.Default.Save();
        }
    }

    public int PracticingTrialCount
    {
        get => Properties.Settings.Default.HumanTest_PracticingTrialCount;
        set
        {
            Properties.Settings.Default.HumanTest_PracticingTrialCount = value;
            Properties.Settings.Default.Save();
        }
    }

    public bool IsPracticingProcedure { get; set; } = true;
    public Language Language { get; set; } = Language.Finnish;

    public Dictionary<OdorDisplay.Device.ID, string> Channels { get; } = new();
}
