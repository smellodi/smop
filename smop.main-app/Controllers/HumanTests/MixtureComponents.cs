using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Smop.MainApp.Controllers.HumanTests;

public class MixtureComponents : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public float Limonene
    {
        get => field;
        set
        {
            field = value;
            UpdateNameList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Limonene)));
        }
    }
    public float Cyclohexanone
    {
        get => field;
        set
        {
            field = value;
            UpdateNameList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cyclohexanone)));
        }
    }
    public float CitronellylAcetate
    {
        get => field;
        set
        {
            field = value;
            UpdateNameList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CitronellylAcetate)));
        }
    }
    public float Carene
    {
        get => field;
        set
        {
            field = value;
            UpdateNameList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Carene)));
        }
    }

    public string[] ShortOdorNames => _shortOdorNames.ToArray();
    public bool CanBeLoadedFromServer { get; set; } = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MixtureComponents()
    {
        UpdateNameList();
    }

    public string GetComponentName(string knownOdor) => knownOdor switch
    {
        CARENE => nameof(Carene),
        CITRONEL => nameof(CitronellylAcetate),
        CYCLOHEX => nameof(Cyclohexanone),
        LIMONENE => nameof(Limonene),
        _ => throw new NotImplementedException($"Odor name '{knownOdor}' does not match any member of MixtureComponents")
    };

    public void SetComponent(string odorName, float flow)
    {
        if (odorName.Contains(CARENE, StringComparison.CurrentCultureIgnoreCase))
            Carene = flow;
        else if (odorName.Contains(CITRONEL, StringComparison.CurrentCultureIgnoreCase))
            CitronellylAcetate = flow;
        else if (odorName.Contains(CYCLOHEX, StringComparison.CurrentCultureIgnoreCase))
            Cyclohexanone = flow;
        else if (odorName.Contains(LIMONENE, StringComparison.CurrentCultureIgnoreCase))
            Limonene = flow;
        else
            throw new NotImplementedException($"Odor name '{odorName}' does not match any member of MixtureComponents");
    }

    public KeyValuePair<string, float>[] GetPairs(HumanTestsMode mode)
    {
        Dictionary<string, float> result = new()
        {
            { LIMONENE, Limonene },
            { CITRONEL, CitronellylAcetate }
        };

        if (mode == HumanTestsMode.Demo)
            result.Add(CARENE, Carene);
        else if (mode == HumanTestsMode.StressControl)
            result.Add(CYCLOHEX, Cyclohexanone);

        return result.ToArray();
    }

    // Internal

    const string CARENE = "caren";
    const string CITRONEL = "citron";
    const string CYCLOHEX = "hex";
    const string LIMONENE = "limon";

    List<string> _shortOdorNames = new();

    private void UpdateNameList()
    {
        _shortOdorNames.Clear();

        var knownOdors = new KnownOdors();

        foreach (var odor in knownOdors)
        {
            if (odor.FullKnownName.Contains(LIMONENE, StringComparison.CurrentCultureIgnoreCase) && Limonene > 0)
            {
                _shortOdorNames.Add(odor.ShortKnownName);
            }
            else if (odor.FullKnownName.Contains(CYCLOHEX, StringComparison.CurrentCultureIgnoreCase) && Cyclohexanone > 0)
            {
                _shortOdorNames.Add(odor.ShortKnownName);
            }
            else if (odor.FullKnownName.Contains(CITRONEL, StringComparison.CurrentCultureIgnoreCase) && CitronellylAcetate > 0)
            {
                _shortOdorNames.Add(odor.ShortKnownName);
            }
            else if (odor.FullKnownName.Contains(CARENE, StringComparison.CurrentCultureIgnoreCase) && Carene > 0)
            {
                _shortOdorNames.Add(odor.ShortKnownName);
            }
        }
    }
}
