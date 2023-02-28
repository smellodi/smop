using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SMOP.Comm;
using IndicatorDataSource = SMOP.Controls.ChannelIndicator.DataSource;

namespace SMOP.Pages
{
    public partial class Home : Page, IPage<Tests.Test>
    {
        public event EventHandler<Tests.Test>? Next;

        public Home()
        {
            InitializeComponent();

            DataContext = this;

            txbFreshAir.ToolTip = Utils.L10n.T("CleanAir") + " (" + Utils.L10n.T("NumNotNeg") + ")\n" + Utils.L10n.T("EnterToSet");
            txbOdor.ToolTip = Utils.L10n.T("ScentedAir") + " (" + Utils.L10n.T("NumNotNeg") + ")\n" + Utils.L10n.T("EnterToSet");

            LoadSettings();

            _com.Closed += COM_Closed;
            _com.Opened += COM_Opened;

            long startTs = Utils.Timestamp.Ms;
        }


        // Internal

        private const double PID_UPDATE_INTERVAL = 0.2;     // seconds

        readonly Storage _storage = Storage.Instance;
        readonly CommPort _com = CommPort.Instance;
        readonly MFC _mfc = MFC.Instance;
        readonly PID _pid = PID.Instance;

        Controls.ChannelIndicator? _currentIndicator = null;
        DeviceSample? _lastSample = null;

        MFCScheduler? _mfcScheduler = null;

        private void LoadSettings()
        {
            var settings = Properties.Settings.Default;
            txbFreshAir.Text = settings.MFC_FreshAir.ToString();
        }

        private void SaveSettings()
        {
            var settings = Properties.Settings.Default;
            try
            {
                settings.MFC_FreshAir = double.Parse(txbFreshAir.Text);
            }
            catch { }
            settings.Save();
        }

        private void UpdateDeviceStateUI()
        {
            txbFreshAir.Text = Math.Max(0, _mfc.FreshAirSpeed).ToString("F1");
            txbOdor.Text = Math.Max(0, _mfc.OdorSpeed).ToString("F1");
        }

        private void ClearIndicators()
        {
            foreach (Controls.ChannelIndicator chi in stpIndicators.Children)
            {
                chi.Value = 0;
            }

            if (_currentIndicator != null)
            {
                _currentIndicator.IsActive = false;
                _currentIndicator = null;
                lmsGraph.Empty();
            }
        }

        private void UpdateGraph(IndicatorDataSource dataSource, double timestamp, double value)
        {
            if (_currentIndicator?.Source == dataSource)
            {
                lmsGraph.Add(timestamp, value);
            }
        }

        private void ResetGraph(IndicatorDataSource dataSource, double baseValue)
        {
            if (_currentIndicator?.Source == dataSource)
            {
                lmsGraph.Reset(PID_UPDATE_INTERVAL, baseValue);
            }
        }

        private void UpdateIndicators(DeviceSample sample)
        {
            chiSourceTemperature.Value = sample.SystemTemperature;

            double timestamp = Utils.Timestamp.Sec; //sample.Time;
            UpdateGraph(IndicatorDataSource.MFC, timestamp, sample.SystemPID);
        }

        private void ResetIndicators(DeviceSample? sample)
        {
            ResetGraph(IndicatorDataSource.MFC, sample?.SystemPID ?? 0);

            rctBreathingStage.Fill = null;
            lblBreathingStage.Text = "";
        }

        private void HandleError(Result result)
        {
            if (result.Error == Error.Success)
            {
                return; // no error
            }
            else if (result.Error == Error.CRC || result.Error == Error.WrongDevice)
            {
                return; // these are not critical errors, just continue
            }
            else if (result.Error.HasFlag(Error.DeviceError))
            {
                var deviceError = (Packet.Result)((int)result.Error & ~(int)Error.DeviceError);
                Utils.MsgBox.Error(Title, Utils.L10n.T("DeviceError") + $"\n[{deviceError}] {result.Reason}");
                return; // hopefully, these are not critical errors, just continue
            }

            Utils.MsgBox.Error(Title, Utils.L10n.T("CriticalError") + $"\n{result}\n\n" + Utils.L10n.T("AppTerminated"));
            Application.Current.Shutdown();
        }


        // Event handlers

        private void PID_Sample(object? s, DeviceSample sample)
        {
            _mfcScheduler?.FeedPID(sample.SystemPID);

            Dispatcher.Invoke(() =>
            {
                _lastSample = sample;
                UpdateIndicators(sample);
            });
        }

        private void MFC_ParamsChanged(object? s, EventArgs e) => Dispatcher.Invoke(() =>
        {
            UpdateDeviceStateUI();
        });

        private void COM_Opened(object? s, EventArgs e) => Utils.DispatchOnce.Do(0.5, () => Dispatcher.Invoke(() =>
        {
            var result = _mfc.ReadState();
            HandleError(result);

            if (result.Error == Error.Success)
            {
                if (double.TryParse(txbFreshAir.Text, out double freshAirSpeed))
                {
                    _mfc.FreshAirSpeed = freshAirSpeed;
                }

                UpdateDeviceStateUI();
            }
        }));

        private void COM_Closed(object? s, EventArgs e) => 
            ClearIndicators();


        // UI events

        private void Page_Loaded(object? sender, RoutedEventArgs e)
        {
            _storage
                .BindScaleToZoomLevel(sctScale)
                .BindVisibilityToDebug(lblDebug);

            if (Focusable)
            {
                Focus();
            }

            _pid.Sample += PID_Sample;
            _mfc.ParamsChanged += MFC_ParamsChanged;

            if (_com.IsOpen && _pid.ArePIDsOn)
            {
                UpdateDeviceStateUI();

                if (_lastSample == null)
                {
                    var result = _pid.GetSample(out DeviceSample sample);
                    HandleError(result);

                    if (result.Error == Error.Success)
                    {
                        _lastSample = sample;
                    }
                }

                ResetIndicators(_lastSample);

                HandleError(_pid.EnableSampling(PID_UPDATE_INTERVAL));
            }
        }

        private void Page_Unloaded(object? sender, RoutedEventArgs e)
        {
            _storage
                .UnbindScaleToZoomLevel(sctScale)
                .UnbindVisibilityToDebug(lblDebug);

            _pid.Sample -= PID_Sample;
            _mfc.ParamsChanged -= MFC_ParamsChanged;

            SaveSettings();

            HandleError(_pid.EnableSampling(0));
            
            _lastSample = null;
        }

        private void Page_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F4)
            {
                HandleError(_pid.EnableSampling(0));

                Next?.Invoke(this, Tests.Test.OdorProduction);
            }
        }

        private void FreshAir_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2 && txbFreshAir.IsReadOnly)
            {
                txbFreshAir.IsReadOnly = false;
            }
            else if (e.Key == Key.Enter)
            {
                if (Utils.Validation.Do(txbFreshAir, 0, 10, (object? s, double value) => _mfc.FreshAirSpeed = value))
                {
                    txbFreshAir.MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
                }
                else
                {
                    txbFreshAir.Undo();
                }
            }
        }

        private void Odor_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Utils.Validation.Do(txbOdor, 0, 160, (object? s, double value) => _mfc.OdorSpeed = value))
                {
                    txbOdor.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
                }
                else
                {
                    txbOdor.Undo();
                }
            }
        }

        private void ChannelIndicator_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            var chi = sender as Controls.ChannelIndicator;
            if (!chi?.IsActive ?? false)
            {
                if (_currentIndicator != null)
                {
                    _currentIndicator.IsActive = false;
                }

                _currentIndicator = chi;
                _currentIndicator!.IsActive = true;
                
                ResetIndicators(_lastSample);
            }
        }
    }
}
