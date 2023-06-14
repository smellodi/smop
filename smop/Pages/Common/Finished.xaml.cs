using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Smop.OdorDisplay;

namespace Smop.Pages
{
    public partial class Finished : Page, IPage<bool>, INotifyPropertyChanged
    {
        public class RequestSavingArgs : EventArgs
        {
            public SavingResult Result { get; set; }
            public RequestSavingArgs(SavingResult result)
            {
                Result = result;
            }
        }

        public event EventHandler<bool>? Next;       // true: exit, false: return to the fornt page
        public event EventHandler<RequestSavingArgs>? RequestSaving;
        public event PropertyChangedEventHandler? PropertyChanged;

        public string TestName
        {
            get => _testName;
            set
            {
                _testName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestName)));
            }
        }

        public Finished()
        {
            InitializeComponent();

            DataContext = this;
        }

        public void DisableSaving()
        {
            btnSaveData.IsEnabled = false;
        }

        // Internal

        string _testName = "";

        private bool HasDecisionAboutData()
        {
            if (!FlowLogger.Instance.HasAnyRecord)
            {
                return true;
            }

            RequestSavingArgs args = new(SavingResult.Cancel);
            RequestSaving?.Invoke(this, args);

            return args.Result != SavingResult.Cancel;
        }


        // UI events

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Storage.Instance
                .BindScaleToZoomLevel(sctScale)
                .BindVisibilityToDebug(lblDebug);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Storage.Instance
                .UnbindScaleToZoomLevel(sctScale)
                .UnbindVisibilityToDebug(lblDebug);
        }

        private void Page_GotFocus(object sender, RoutedEventArgs e)
        {
            btnSaveData.IsEnabled = true;
        }

        private void SaveData_Click(object sender, RoutedEventArgs e)
        {
            RequestSaving?.Invoke(this, new(SavingResult.Cancel));
        }

        private void Return_Click(object sender, RoutedEventArgs e)
        {
            if (HasDecisionAboutData())
            {
                Next?.Invoke(this, false);
            }
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            var procedure = Keyboard.IsKeyDown(Key.LeftCtrl)
                ? MFCScheduler.ProcedureType.CleaningFast
                : MFCScheduler.ProcedureType.Cleaning;

            var buttons = new Utils.MsgBox.Button[] { Utils.MsgBox.Button.Yes, Utils.MsgBox.Button.No };
            if (Utils.MsgBox.Warn(Title, Utils.L10n.T("IsOdorSourceRemoved"), buttons) == Utils.MsgBox.Button.No)
            {
                return;
            }

            MFCScheduler cleaning = new(procedure);

            void PID_Sample(object? s, DeviceSample sample) => cleaning.FeedPID(sample.SystemPID);

            PID.Instance.Sample += PID_Sample;

            cleaning.Finished += (s, e) => Dispatcher.Invoke(() =>
            {
                btnStartOver.IsEnabled = true;
                wtiInstruction.Hide();

                PID.Instance.Sample -= PID_Sample;
            });

            btnCleaning.IsEnabled = false;
            btnStartOver.IsEnabled = false;

            wtiInstruction.Start(cleaning.Duration);
            cleaning.Start();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (HasDecisionAboutData())
            {
                Next?.Invoke(this, true);
            }
        }
    }
}
