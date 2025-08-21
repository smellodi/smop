using System.Collections.Generic;
using System.Text.Json;

namespace Smop.MainApp.Controllers.HumanTests;

// Can be set only in the code
public enum HumanTestsMode
{
    StressControl,
    Demo
}

public class Settings
{
    public static HumanTestsMode Mode { get; set; } = HumanTestsMode.Demo; // hardcoded!

    public int ComparisonBlockCount
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

    public double PauseBetweenBlocks
    {
        get => Properties.Settings.Default.HumanTest_PauseBetweenBlocks;
        set
        {
            Properties.Settings.Default.HumanTest_PauseBetweenBlocks = value;
            Properties.Settings.Default.Save();
        }
    }

    public double PauseBetweenTrials
    {
        get => Properties.Settings.Default.HumanTest_PauseBetweenTrials;
        set
        {
            Properties.Settings.Default.HumanTest_PauseBetweenTrials = value;
            Properties.Settings.Default.Save();
        }
    }

    public bool AllowEmptyRatings
    {
        get => Properties.Settings.Default.HumanTest_AllowEmptyRatings;
        set
        {
            Properties.Settings.Default.HumanTest_AllowEmptyRatings = value;
            Properties.Settings.Default.Save();
        }
    }

    public bool IsPracticingProcedure { get; set; } = true;
    public Language Language { get; set; } = Language.Finnish;
    public int ParticipantID { get; set; } = 1;

    /// <summary>
    /// Mixture preparation interval, in seconds
    /// </summary>
    public double WaitingInterval
    {
        get => Properties.Settings.Default.HumanTest_WaitingInterval;
        set
        {
            Properties.Settings.Default.HumanTest_WaitingInterval = value;
            Properties.Settings.Default.Save();
        }
    }

    /// <summary>
    /// Mixture sniffing interval, in seconds
    /// </summary>
    public double SniffingInterval
    {
        get => Properties.Settings.Default.HumanTest_SniffingInterval;
        set
        {
            Properties.Settings.Default.HumanTest_SniffingInterval = value;
            Properties.Settings.Default.Save();
        }
    }

    public MixtureComponents[] MixtureComponents => _mixComponenets;

    public Dictionary<OdorDisplay.Device.ID, string> Channels { get; } = new();

    // Internal

    static Settings()
    {
        try
        {
            MixtureComponents[]? mc = null;

            var savedMode = (HumanTestsMode)Properties.Settings.Default.HumanTest_Mode;
            if (savedMode == Mode && !string.IsNullOrEmpty(Properties.Settings.Default.HumanTest_Mixtures))
            {
                mc = JsonSerializer.Deserialize<MixtureComponents[]>(Properties.Settings.Default.HumanTest_Mixtures);
            }
            
            if (mc == null)
            {
                mc = Mode switch
                {
                    HumanTestsMode.StressControl => STRESS_CONTROL_MIX_COMPONENTS,
                    HumanTestsMode.Demo => DEMO_MIX_COMPONENTS,
                    _ => throw new System.NotImplementedException($"Mode '{Mode}' is not implemented yet.")
                };
            }

            _mixComponenets = mc ?? [];
        }
        catch
        {
            _mixComponenets = [];
        }

        ((App)System.Windows.Application.Current).AddCleanupAction(() =>
        {
            var json = JsonSerializer.Serialize(_mixComponenets);
            Properties.Settings.Default.HumanTest_Mixtures = json;
            Properties.Settings.Default.HumanTest_Mode = (int)Mode;
            Properties.Settings.Default.Save();
        });
    }

    static MixtureComponents[] _mixComponenets;

    static readonly MixtureComponents[] STRESS_CONTROL_MIX_COMPONENTS =
    [
        new MixtureComponents() { Name = "Control", Limonene = 8.3f, Cyclohexanone = 2.0f, CitronellylAcetate = 100f },
        new MixtureComponents() { Name = "Stress", Limonene = 10.1f, Cyclohexanone = 3.0f, CitronellylAcetate = 62.5f },
        new MixtureComponents() { Name = "Stress close", Limonene = 9.5f, Cyclohexanone = 2.66f, CitronellylAcetate = 50f },
        new MixtureComponents() { Name = "Stress medium", Limonene = 7.75f, Cyclohexanone = 3.22f, CitronellylAcetate = 70f },
        new MixtureComponents() { Name = "Stress far", Limonene = 17.8f, Cyclohexanone = 3.22f, CitronellylAcetate = 52.5f },
    ];

    static readonly MixtureComponents[] DEMO_MIX_COMPONENTS =
    [
        new MixtureComponents() { Name = "OM1", Limonene = 100f, Carene = 5f, CitronellylAcetate = 5f },
        new MixtureComponents() { Name = "OM2", Limonene = 5f, Carene = 100f, CitronellylAcetate = 5f },
        new MixtureComponents() { Name = "OM3", Limonene = 5f, Carene = 5f, CitronellylAcetate = 100f },
        new MixtureComponents() { Name = "RM1", Limonene = 100f, Carene = 5f, CitronellylAcetate = 5f, CanBeLoadedFromServer = true },
        new MixtureComponents() { Name = "RM2", Limonene = 5f, Carene = 100f, CitronellylAcetate = 5f, CanBeLoadedFromServer = true },
        new MixtureComponents() { Name = "RM3", Limonene = 5f, Carene = 5f, CitronellylAcetate = 100f, CanBeLoadedFromServer = true },
    ];
}
