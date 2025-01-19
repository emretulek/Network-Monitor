using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Widgets.Common;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace Network_Monitor
{
    public partial class NetworkViewModel : INotifyPropertyChanged,IDisposable
    {
        private readonly Schedule schedule = new();
        private string scheduleID = "";
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly List<int> BandwdithSteps = [10,50,100,250,500,1000];

        public struct SettingsStruct
        {
            public float TimeLine { get; set; }
            public int AdaptorId { get; set; }
            public string ReceivedColor { get; set; }
            public string SentColor { get; set; }
        }

        public static SettingsStruct Default = new()
        {
            TimeLine = 200,
            AdaptorId = 0,
            ReceivedColor = "#9D0A0A",
            SentColor = "#fc8403",
        };

        public required SettingsStruct Settings = Default;

        private PerformanceCounter? bytesReceivedCounter;
        private PerformanceCounter? bytesSentCounter;
        private AreaSeries? receivedSeries;
        private AreaSeries? sentSeries;
        private int timeCounter;

        private PlotModel? _plotModel;
        public PlotModel? NetworkPlotModel
        {
            get { return _plotModel; }
            set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(NetworkPlotModel));
            }
        }

        private string _maxbandText = "0 Mbps";
        public string MaxBandText
        {
            get { return _maxbandText; }
            set
            {
                _maxbandText = value;
                OnPropertyChanged(nameof(MaxBandText));
            }
        }

        private string _receivedUsageText = "0";
        public string ReceivedUsageText
        {
            get { return _receivedUsageText; }
            set
            {
                _receivedUsageText = value;
                OnPropertyChanged(nameof(ReceivedUsageText));
            }
        }

        private string _sentUsageText = "0";
        public string SentUsageText
        {
            get { return _sentUsageText; }
            set
            {
                _sentUsageText = value;
                OnPropertyChanged(nameof(SentUsageText));
            }
        }

        public async Task Start()
        {
            try
            {
                await Task.Run(() =>
                {
                    PerformanceCounterCategory category = new("Network Interface");
                    String[] instancenames = category.GetInstanceNames();
            
                    if (instancenames.Length > 0)
                    {
                        bytesReceivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instancenames[Settings.AdaptorId]);
                        bytesSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instancenames[Settings.AdaptorId]);
                        return;
                    }

                    throw new Exception("Invalid Ethernet Adaptor.");
                   
                }, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.Message);
                return;
            }

            CreatePlot();
            scheduleID = schedule.Secondly(UpdateUsage, 1);
        }

        /// <summary>
        /// 
        /// </summary>
        private void CreatePlot()
        {
            NetworkPlotModel = new PlotModel
            {
                PlotAreaBorderThickness = new OxyThickness(0),
                PlotAreaBorderColor = OxyColors.Transparent,
                Padding = new OxyThickness(0)
            };

            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                IsAxisVisible = false,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                MaximumPadding = 0,
                MinimumPadding = 0
            };

            var yAxis = new LinearAxis
            {
                Minimum = 0,
                Maximum = BandwdithSteps.Min(),
                Position = AxisPosition.Left,
                IsAxisVisible = false,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                MaximumPadding = 0,
                MinimumPadding = 0
            };

            NetworkPlotModel.Axes.Add(xAxis);
            NetworkPlotModel.Axes.Add(yAxis);

            receivedSeries = new AreaSeries
            {
                LineStyle = LineStyle.Solid,
                StrokeThickness = 1,
                Color = OxyColor.Parse(Settings.ReceivedColor),
            };

            sentSeries = new AreaSeries
            {
                LineStyle = LineStyle.Solid,
                StrokeThickness = 1,
                Color = OxyColor.Parse(Settings.SentColor),
            };

            NetworkPlotModel.Series.Add(receivedSeries);
            NetworkPlotModel.Series.Add(sentSeries);
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateUsage()
        {
            if(bytesReceivedCounter is null || bytesSentCounter is null ||
                sentSeries is null || receivedSeries is null || NetworkPlotModel is null) return;

            float bytesReceived = bytesReceivedCounter.NextValue() * 8 / 1024 / 1024;
            float bytesSent = bytesSentCounter.NextValue() * 8 / 1024 / 1024;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    sentSeries.Points.Add(new DataPoint(timeCounter, bytesSent));
                    sentSeries.Points2.Add(new DataPoint(timeCounter, 0));
                    receivedSeries.Points.Add(new DataPoint(timeCounter, bytesReceived + bytesSent));
                    receivedSeries.Points2.Add(new DataPoint(timeCounter, bytesSent));

                    var maxY = receivedSeries.Points.Max(point => point.Y);
                    var currentBandwidth = BandwdithSteps.Min();
                    NetworkPlotModel.Axes[1].Maximum = currentBandwidth;

                    foreach(int BandwidthStep in BandwdithSteps.OrderByDescending(x => x))
                    {
                        if (maxY < BandwidthStep)
                        {
                            currentBandwidth = BandwidthStep;
                            NetworkPlotModel.Axes[1].Maximum = currentBandwidth;
                        }
                    }

                    MaxBandText = $"{currentBandwidth:F1} Mbps";
                    ReceivedUsageText = $"R: {bytesReceived:F1} ";
                    SentUsageText = $"S: {bytesSent:F1} Mbps";

                    timeCounter++;

                    if (receivedSeries.Points.Count > Settings.TimeLine)
                    {
                        receivedSeries.Points.RemoveAt(0);
                        receivedSeries.Points2.RemoveAt(0);
                        sentSeries.Points.RemoveAt(0);
                        sentSeries.Points2.RemoveAt(0);
                    }

                    NetworkPlotModel.InvalidatePlot(true);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                }
            });
        }

        public void Dispose()
        {
            schedule.Stop(scheduleID);
            cancellationTokenSource.Cancel();
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
