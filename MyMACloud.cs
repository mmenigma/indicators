#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// Move enum outside all namespaces so it's globally accessible
public enum MATypeEnum
{
    SMA,
    EMA
}

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    [Gui.CategoryOrder("Parameters", 1)]
    [Gui.CategoryOrder("Display", 2)]
    [Gui.CategoryOrder("Signals", 3)]
    [Gui.CategoryOrder("Alerts", 4)]
    public class MyMACloud : Indicator
    {
        #region Members
        private int _regionTrend;
        private int _regionStartBar;
        private Series<bool> _longEntrySignal;
        private Series<bool> _shortEntrySignal;
        #endregion

        #region Indicator methods
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description									= @"Bullish/bearish cloud based on two moving averages.";
                Name										= "MyMACloud";
                Calculate									= Calculate.OnPriceChange;
                IsOverlay									= true;
                DisplayInDataBox							= true;
                DrawOnPricePanel							= true;
                DrawHorizontalGridLines						= true;
                DrawVerticalGridLines						= true;
                PaintPriceMarkers							= false;
                ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
                //Disable this property if your indicator requires custom values that cumulate with each new market data event. 
                //See Help Guide for additional information.
                MAType										= MATypeEnum.EMA;
                FastPeriod									= 9;
                SlowPeriod									= 21;
                BullishCloudBrush							= Brushes.Green;
                BearishCloudBrush							= Brushes.Red;
                CloudOpacity								= 40;
                EnableSignals								= false;
                SignalOffset								= 10;
                SignalPrefix								= "MyMACloud_";
                EnableAlerts								= false;
                AlertSoundsPath                             = DefaultAlertFilePath();
                LongEntryAlert                              = "LongEntry.wav";
                ShortEntryAlert								= "ShortEntry.wav";
                AddPlot(new Stroke(Brushes.Green, 1), PlotStyle.PriceBox, "FastMA");
                AddPlot(new Stroke(Brushes.Red, 1), PlotStyle.PriceBox, "SlowMA");
                AddPlot(Brushes.Transparent, "Signals");
            }
            else if (State == State.Configure)
            {
                // Disable IsSuspendedWhileInactive if alerts are enabled.
                IsSuspendedWhileInactive = !EnableAlerts;

                SetZOrder(-1); // Draw behind price bars.
                _regionTrend = 0;
                _regionStartBar = 0;
                _longEntrySignal = new Series<bool>(this);
                _shortEntrySignal = new Series<bool>(this);
            }
        }

        public override string DisplayName
        {
            get { return Name + "(" + MAType + "," + FastPeriod + "," + SlowPeriod + ")"; }
        }

        protected override void OnBarUpdate()
        {
            _longEntrySignal[0] = false;
            _shortEntrySignal[0] = false;
            Signals[0] = 0;

            if (CurrentBar < Math.Max(FastPeriod, SlowPeriod))
                return;

            FastMA[0] = (MAType == MATypeEnum.EMA) ? (EMA(Close, FastPeriod)[0]) : (SMA(Close, FastPeriod)[0]);
            SlowMA[0] = (MAType == MATypeEnum.EMA) ? (EMA(Close, SlowPeriod)[0]) : (SMA(Close, SlowPeriod)[0]);

            if (!IsVisible)
                return;

            DrawCloud(this, "MyMACloud_");

            // Signals
            if (EnableSignals)
            {
                var barTime = Time[0];
                string longEntrySignalTag = string.Format("{0}LongEntry{1}", SignalPrefix, CurrentBar);
                string shortEntrySignalTag = string.Format("{0}ShortEntry{1}", SignalPrefix, CurrentBar);

                if (FastMA[0] >= SlowMA[0] && FastMA[1] <= SlowMA[1] && Close[0] > Close[1])
                {
                    // LONG entry signal.
                    RemoveDrawObject(shortEntrySignalTag);
                    _longEntrySignal[0] = true;
                    Signals[0] = 1;
                    Draw.Diamond(this,
                        longEntrySignalTag,
                        true,
                        barTime,
                        Math.Min(Math.Min(FastMA[0], SlowMA[0]), Low[0]) - (SignalOffset * TickSize),
                        BullishCloudBrush);
                }
                else if (FastMA[0] <= SlowMA[0] && FastMA[1] >= SlowMA[1] && Close[0] < Close[1])
                {
                    // SHORT entry signal.
                    RemoveDrawObject(longEntrySignalTag);
                    _shortEntrySignal[0] = true;
                    Signals[0] = -1;
                    Draw.Diamond(this,
                        shortEntrySignalTag,
                        true,
                        barTime,
                        Math.Max(Math.Max(FastMA[0], SlowMA[0]), High[0]) + (SignalOffset * TickSize),
                        BearishCloudBrush);
                }
                else
                {
                    // No signal.
                    //RemoveDrawObject(longEntrySignalTag);
                    //RemoveDrawObject(shortEntrySignalTag);
                }

                // Alerts
                if (EnableAlerts && EnableSignals && (State == State.Realtime) && IsFirstTickOfBar)
                {
                    if (Signals[1] > 0 && !string.IsNullOrWhiteSpace(LongEntryAlert))
                    {
                        // Long entry alert.
                        string audioFile = ResolveAlertFilePath(LongEntryAlert, AlertSoundsPath);
                        Alert("LongEntryAlert", Priority.High, "Long Entry - Bullish cross", audioFile, 10, Brushes.Black, BullishCloudBrush);
                    }
                    if (Signals[1] < 0 && !string.IsNullOrWhiteSpace(ShortEntryAlert))
                    {
                        // Short entry alert.
                        string audioFile = ResolveAlertFilePath(ShortEntryAlert, AlertSoundsPath);
                        Alert("ShortEntryAlert", Priority.High, "Short Entry - Bearish cross", audioFile, 10, Brushes.Black, BearishCloudBrush);
                    }
                }
            }
        }

        private string DefaultAlertFilePath()
        {
            return NinjaTrader.Core.Globals.UserDataDir + "sounds\\";
        }

        private string ResolveAlertFilePath(string filename, string directory)
        {
            if (string.IsNullOrEmpty(filename))
                return string.Empty;
            
            if (filename.Contains("\\") || filename.Contains("/"))
                return filename;
            
            if (!string.IsNullOrEmpty(directory) && !directory.EndsWith("\\"))
                directory += "\\";
            
            return directory + filename;
        }
        #endregion

        #region Public methods
        public void DrawCloud(NinjaScriptBase owner, string drawingObjectPrefix)
        {
            int currTrend = (FastMA[0] > SlowMA[0]) ? 1 : ((FastMA[0] < SlowMA[0]) ? -1 : 0);
            int prevTrend = (FastMA[1] > SlowMA[1]) ? 1 : ((FastMA[1] < SlowMA[1]) ? -1 : 0);

            if (currTrend != 0)
            {
                if (currTrend != _regionTrend)
                {
                    // New cloud region starting.
                    _regionStartBar = CurrentBar - 1; // Extend back 1 bar to fill gap at start of cloud region.
                    _regionTrend = currTrend;
                }

                Brush areaBrush = currTrend > 0 ? BullishCloudBrush : (currTrend < 0 ? BearishCloudBrush : Brushes.Transparent);
                string regionTag = string.Format("{0}{1}Cloud{2}", drawingObjectPrefix, (_regionTrend > 0 ? "Bullish" : "Bearish"), _regionStartBar);
                int startBarsAgo = CurrentBar - _regionStartBar;

                Draw.Region(owner, regionTag, startBarsAgo, 0, FastMA, SlowMA, Brushes.Transparent, areaBrush, CloudOpacity);
            }
            else
            {
                // MAs must be exactly overlapping, close current region.
                _regionTrend = 0;
                _regionStartBar = 0;
            }
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "MA Type", Description = "The type of Moving Average.", Order = 1, GroupName = "Parameters")]
        public MATypeEnum MAType { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="FastPeriod", Description="The period of the fast Moving Average.", Order=2, GroupName="Parameters")]
        public int FastPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="SlowPeriod", Description="The period of the slow Moving Average.", Order=3, GroupName="Parameters")]
        public int SlowPeriod { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Bullish Cloud Color", Description="Color used for the bullish cloud.", Order=1, GroupName="Display")]
        public Brush BullishCloudBrush { get; set; }

        [Browsable(false)]
        public string BullishCloudBrushSerializable
        {
            get { return Serialize.BrushToString(BullishCloudBrush); }
            set { BullishCloudBrush = Serialize.StringToBrush(value); }
        }			

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Bearish Cloud Color", Description="Color used for the bearish cloud.", Order=2, GroupName= "Display")]
        public Brush BearishCloudBrush { get; set; }

        [Browsable(false)]
        public string BearishCloudBrushSerializable
        {
            get { return Serialize.BrushToString(BearishCloudBrush); }
            set { BearishCloudBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Cloud Opacity", Description = "The opacity of the cloud (0 = completely transparent, 100 = no opacity).", Order = 3, GroupName = "Display")]
        public int CloudOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Signals", Description = "Display entry signals on the chart.", Order = 1, GroupName = "Signals")]
        public bool EnableSignals { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Signal Offset", Description = "The vertical offset between signal and bar.", Order = 2, GroupName = "Signals")]
        public int SignalOffset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SignalPrefix", Description = "Prefix used when naming signal drawing objects.", Order = 3, GroupName = "Signals")]
        public string SignalPrefix { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Alerts", Description = "Trigger alerts for confirmed signals.", Order = 1, GroupName = "Alerts")]
        public bool EnableAlerts { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alert Sounds Path", Description = "Location of alert audio files used for confirmed signals.", Order = 2, GroupName = "Alerts")]
        public string AlertSoundsPath { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Entry Alert", Description = "Alert sound used for confirmed LONG entry signals.", Order = 3, GroupName = "Alerts")]
        public string LongEntryAlert { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short Entry Alert", Description = "Alert sound used for confirmed SHORT entry signals.", Order = 4, GroupName = "Alerts")]
        public string ShortEntryAlert { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> FastMA
        {
            get { return Values[0]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> SlowMA
        {
            get { return Values[1]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Signals
        {
            get { return Values[2]; }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.MyMACloud[] cacheMyMACloud;
		public Myindicators.MyMACloud MyMACloud(MATypeEnum mAType, int fastPeriod, int slowPeriod, Brush bullishCloudBrush, Brush bearishCloudBrush, int cloudOpacity, bool enableSignals, int signalOffset, string signalPrefix, bool enableAlerts, string alertSoundsPath, string longEntryAlert, string shortEntryAlert)
		{
			return MyMACloud(Input, mAType, fastPeriod, slowPeriod, bullishCloudBrush, bearishCloudBrush, cloudOpacity, enableSignals, signalOffset, signalPrefix, enableAlerts, alertSoundsPath, longEntryAlert, shortEntryAlert);
		}

		public Myindicators.MyMACloud MyMACloud(ISeries<double> input, MATypeEnum mAType, int fastPeriod, int slowPeriod, Brush bullishCloudBrush, Brush bearishCloudBrush, int cloudOpacity, bool enableSignals, int signalOffset, string signalPrefix, bool enableAlerts, string alertSoundsPath, string longEntryAlert, string shortEntryAlert)
		{
			if (cacheMyMACloud != null)
				for (int idx = 0; idx < cacheMyMACloud.Length; idx++)
					if (cacheMyMACloud[idx] != null && cacheMyMACloud[idx].MAType == mAType && cacheMyMACloud[idx].FastPeriod == fastPeriod && cacheMyMACloud[idx].SlowPeriod == slowPeriod && cacheMyMACloud[idx].BullishCloudBrush == bullishCloudBrush && cacheMyMACloud[idx].BearishCloudBrush == bearishCloudBrush && cacheMyMACloud[idx].CloudOpacity == cloudOpacity && cacheMyMACloud[idx].EnableSignals == enableSignals && cacheMyMACloud[idx].SignalOffset == signalOffset && cacheMyMACloud[idx].SignalPrefix == signalPrefix && cacheMyMACloud[idx].EnableAlerts == enableAlerts && cacheMyMACloud[idx].AlertSoundsPath == alertSoundsPath && cacheMyMACloud[idx].LongEntryAlert == longEntryAlert && cacheMyMACloud[idx].ShortEntryAlert == shortEntryAlert && cacheMyMACloud[idx].EqualsInput(input))
						return cacheMyMACloud[idx];
			return CacheIndicator<Myindicators.MyMACloud>(new Myindicators.MyMACloud(){ MAType = mAType, FastPeriod = fastPeriod, SlowPeriod = slowPeriod, BullishCloudBrush = bullishCloudBrush, BearishCloudBrush = bearishCloudBrush, CloudOpacity = cloudOpacity, EnableSignals = enableSignals, SignalOffset = signalOffset, SignalPrefix = signalPrefix, EnableAlerts = enableAlerts, AlertSoundsPath = alertSoundsPath, LongEntryAlert = longEntryAlert, ShortEntryAlert = shortEntryAlert }, input, ref cacheMyMACloud);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MyMACloud MyMACloud(MATypeEnum mAType, int fastPeriod, int slowPeriod, Brush bullishCloudBrush, Brush bearishCloudBrush, int cloudOpacity, bool enableSignals, int signalOffset, string signalPrefix, bool enableAlerts, string alertSoundsPath, string longEntryAlert, string shortEntryAlert)
		{
			return indicator.MyMACloud(Input, mAType, fastPeriod, slowPeriod, bullishCloudBrush, bearishCloudBrush, cloudOpacity, enableSignals, signalOffset, signalPrefix, enableAlerts, alertSoundsPath, longEntryAlert, shortEntryAlert);
		}

		public Indicators.Myindicators.MyMACloud MyMACloud(ISeries<double> input , MATypeEnum mAType, int fastPeriod, int slowPeriod, Brush bullishCloudBrush, Brush bearishCloudBrush, int cloudOpacity, bool enableSignals, int signalOffset, string signalPrefix, bool enableAlerts, string alertSoundsPath, string longEntryAlert, string shortEntryAlert)
		{
			return indicator.MyMACloud(input, mAType, fastPeriod, slowPeriod, bullishCloudBrush, bearishCloudBrush, cloudOpacity, enableSignals, signalOffset, signalPrefix, enableAlerts, alertSoundsPath, longEntryAlert, shortEntryAlert);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MyMACloud MyMACloud(MATypeEnum mAType, int fastPeriod, int slowPeriod, Brush bullishCloudBrush, Brush bearishCloudBrush, int cloudOpacity, bool enableSignals, int signalOffset, string signalPrefix, bool enableAlerts, string alertSoundsPath, string longEntryAlert, string shortEntryAlert)
		{
			return indicator.MyMACloud(Input, mAType, fastPeriod, slowPeriod, bullishCloudBrush, bearishCloudBrush, cloudOpacity, enableSignals, signalOffset, signalPrefix, enableAlerts, alertSoundsPath, longEntryAlert, shortEntryAlert);
		}

		public Indicators.Myindicators.MyMACloud MyMACloud(ISeries<double> input , MATypeEnum mAType, int fastPeriod, int slowPeriod, Brush bullishCloudBrush, Brush bearishCloudBrush, int cloudOpacity, bool enableSignals, int signalOffset, string signalPrefix, bool enableAlerts, string alertSoundsPath, string longEntryAlert, string shortEntryAlert)
		{
			return indicator.MyMACloud(input, mAType, fastPeriod, slowPeriod, bullishCloudBrush, bearishCloudBrush, cloudOpacity, enableSignals, signalOffset, signalPrefix, enableAlerts, alertSoundsPath, longEntryAlert, shortEntryAlert);
		}
	}
}

#endregion
