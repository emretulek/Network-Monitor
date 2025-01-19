using System.ComponentModel;
using System.Windows;
using Widgets.Common;

namespace Network_Monitor
{
    public partial class MainWindow : Window, IWidgetWindow
    {
        public readonly static string WidgetName = "Network Monitor";
        public readonly static string SettingsFile = "settings.networkmonitor.json";
        private readonly Config config = new(SettingsFile);

        public NetworkViewModel ViewModel { get; set; }
        private NetworkViewModel.SettingsStruct Settings = NetworkViewModel.Default;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            ViewModel = new NetworkViewModel()
            {
                Settings = Settings
            };
            DataContext = ViewModel;
            _ = ViewModel.Start();
            Logger.Info($"{WidgetName} is started");
        }

        public void LoadSettings()
        {
            try
            {
                Settings.ReceivedColor = PropertyParser.ToString(config.GetValue("received_color"), Settings.ReceivedColor);
                Settings.SentColor = PropertyParser.ToString(config.GetValue("sent_color"), Settings.SentColor);
                Settings.TimeLine = PropertyParser.ToFloat(config.GetValue("graphic_timeline"), Settings.TimeLine);
                Settings.AdaptorId = PropertyParser.ToInt(config.GetValue("adaptor_id"), Settings.AdaptorId);

                UsageText.FontSize = PropertyParser.ToFloat(config.GetValue("usage_font_size"));
                UsageText.Foreground = PropertyParser.ToColorBrush(config.GetValue("usage_foreground"));
                TextR.Foreground = PropertyParser.ToColorBrush(Settings.ReceivedColor);
                TextS.Foreground = PropertyParser.ToColorBrush(Settings.SentColor);
                MaxBandText.Foreground = UsageText.Foreground;
                MaxBandText.FontSize = UsageText.FontSize / 2;
            }
            catch (Exception)
            {
                config.Add("usage_font_size", UsageText.FontSize);
                config.Add("usage_foreground", UsageText.Foreground);
                config.Add("received_color", Settings.ReceivedColor);
                config.Add("sent_color", Settings.SentColor);
                config.Add("graphic_timeline", Settings.TimeLine);
                config.Add("adaptor_id", Settings.AdaptorId);

                config.Save();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            ViewModel.Dispose();
            Logger.Info($"{WidgetName} is closed");
        }

        public WidgetWindow WidgetWindow()
        {
            return new WidgetWindow(this);
        }

        public static WidgetDefaultStruct WidgetDefaultStruct()
        {
            return new WidgetDefaultStruct();
        }
    }
}
