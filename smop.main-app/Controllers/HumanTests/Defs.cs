using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Smop.MainApp.Controllers.HumanTests;

public enum Stage
{
    Initial,
    WaitingMixture,
    Ready,
    SniffingMixture,
    Question,
    TimedPause,
    UserControlledPause,
    Finished,
}

public class TrialStage(Stage stage, int mixtureId)
{
    public Stage Stage { get; set; } = stage;
    public int MixtureId { get; set; } = mixtureId;
}

internal static class Brushes
{
    public static Brush Inactive { get; } = (Brush)Application.Current.FindResource("BrushHTButtonInactive");
    public static Brush Active { get; } = (Brush)Application.Current.FindResource("BrushHTButtonActive");
    public static Brush Done { get; } = (Brush)Application.Current.FindResource("BrushHTButtonDone");
    public static Brush Clickable { get; } = (Brush)Application.Current.FindResource("BrushHTButtonClickable");
}

internal class Triplet(Mixture mixture1, Mixture mixture2, Mixture mixture3)
{
    public Mixture[] Mixtures { get; } = [mixture1, mixture2, mixture3];
    public int Answer { get; set; } = 0;
    public int OneOutID =>
        mixture1.Name == mixture2.Name ? 3 :
        mixture1.Name == mixture3.Name ? 2 : 1;
    public bool IsCorrect => Answer == OneOutID;

    public override string ToString() => $"{mixture1.Name}\t{mixture2.Name}\t{mixture3.Name}\t{OneOutID}\t{Answer}\t{IsCorrect}";
}

internal class OneOutSession
{
    public Triplet[] Triplets { get; }

    public OneOutSession(Settings settings)
    {
        var r = new Random();

        var triplets = new List<Triplet>();
        if (settings.IsPracticingProcedure)
        {
            var empty = new Mixture("empty", [], settings.Channels);

            for (int i = 0; i < settings.PracticingTrialCount; i++)
                triplets.Add(new Triplet(empty, empty, empty));
        }
        else
        {
            var allMixtures = Settings.Mode switch
            {
                HumanTestsMode.StressControl => OdorDisplayHelper.GetStressControlMixtures(settings.Channels),
                HumanTestsMode.Demo => OdorDisplayHelper.GetDemoMixtures(settings.Channels),
                _ => throw new NotImplementedException($"Mode '{Settings.Mode}' is not implemented yet.")
            };

            for (int i = 0; i < allMixtures.Length; i++)
            {
                var mixSame = allMixtures[i];
                for (int j = 0; j < allMixtures.Length; j++)
                {
                    if (i != j)
                    {
                        var mixDiff = allMixtures[j];
                        Mixture[] arr = [mixSame, mixSame, mixDiff];
                        if (settings.IsRandomized)
                        {
                            r.Shuffle(arr);
                        }
                        triplets.Add(new Triplet(arr[0], arr[1], arr[2]));
                    }
                }
            }
        }

        Triplets = triplets.ToArray();

        if (settings.IsRandomized)
        {
            r.Shuffle(Triplets);
        }
    }
}
