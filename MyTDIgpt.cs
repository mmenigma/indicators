#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    public class MyTDIgpt : Indicator
    {
        private SMA rsiSignal;
        private RSI rsi;
        private Bollinger bb;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Traders Dynamic Index (TDI)";
                Name = "MyTDIgpt";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;

                // Define RSI and Signal Line
                AddPlot(Brushes.LimeGreen, "RSI Base Line");  
                AddPlot(Brushes.Red, "Signal Line"); 

                // Define Bollinger Bands
                AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Dash, 1), PlotStyle.Line, "BB Upper");
                AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Dash, 1), PlotStyle.Line, "BB Lower");
                AddPlot(Brushes.Yellow, "BB Middle");  

                // Add Horizontal Guide Lines
                AddLine(Brushes.Green, 68, "Extremely Bought");
                AddLine(new Stroke(Brushes.Gray, DashStyleHelper.Dash, 1), 63, "Over Bought");
                AddLine(Brushes.BlueViolet, 50, "Base Line");
                AddLine(new Stroke(Brushes.Gray, DashStyleHelper.Dash, 1), 37, "Over Sold");
                AddLine(Brushes.Red, 32, "Extremely Sold");

                // Signal Settings
                LongOn = "LongOn";
                ShortOn = "ShortOn";
                Signal_Offset = 5;
                MeanReversion = true;
            }
            else if (State == State.DataLoaded)
            {
                rsi = RSI(RsiPeriod, 3);
                bb = Bollinger(rsi, BBStdDev, BBPeriod);
                rsiSignal = SMA(rsi, SignalSmoothing);
            }
        }

protected override void OnBarUpdate()
{
    if (CurrentBar < BBPeriod) return; // Ensure enough bars for calculation

    // RSI Base Line
    double rsiValue = rsi[0];

    // Bollinger Bands on RSI
    double bbUpper = double.IsNaN(bb.Upper[0]) ? 50 : bb.Upper[0];
    double bbLower = double.IsNaN(bb.Lower[0]) ? 50 : bb.Lower[0];
    double bbMiddle = double.IsNaN(bb.Middle[0]) ? 50 : bb.Middle[0]; // Middle Band (SMA)

    // Signal Line
    double signalLine = rsiSignal[0];

    // Assign values to plots
    Values[0][0] = rsiValue;  // RSI Base Line
    Values[1][0] = signalLine; // Signal Line
    Values[2][0] = bbUpper;    // BB Upper
    Values[3][0] = bbLower;    // BB Lower
    Values[4][0] = bbMiddle;   // BB Middle (New Yellow Line)

    // Mean Reversion Strategy Logic
    if (MeanReversion)
    {
        // Check for Long Signal (Oversold Reversal)
        if (rsiValue < bbLower && rsi[1] >= bbLower && rsiValue > signalLine)
        {
            // Trigger Long Signal
            Draw.ArrowUp(this, LongOn + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, Brushes.LimeGreen);
           
        }

        // Check for Short Signal (Overbought Reversal)
        if (rsi[1] > bb.Upper[1] && rsiValue <= bbUpper && rsiValue < signalLine)
        {
            // Trigger Short Signal
            Draw.ArrowDown(this, ShortOn + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, Brushes.Red);
        }
    }
}
        #region Properties

        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        public int RsiPeriod { get; set; } = 13;  // RSI Period

        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        public int SignalSmoothing { get; set; } = 7; // Smoothing for Signal Line

        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        public int BBPeriod { get; set; } = 34; // Bollinger Bands Period

        [Range(0.1, double.MaxValue)]
        [NinjaScriptProperty]
        public double BBStdDev { get; set; } = 1.6185; // Bollinger Bands Standard Deviation

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> RsiBaseLine { get { return Values[0]; } }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> BBUpper { get { return Values[1]; } }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> BBLower { get { return Values[2]; } }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> SignalLine { get { return Values[3]; } }

        [NinjaScriptProperty]
        [Display(Name = "Mean Reversion ↑↓", GroupName = "SignalSettings", Order = 1)]
        public bool MeanReversion { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", GroupName = "SignalSettings", Order = 4)]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short On", GroupName = "SignalSettings", Order = 5)]
        public string ShortOn { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal_Offset", GroupName = "SignalSettings", Order = 6)]
        public double Signal_Offset { get; set; }
    }
}
#endregion

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.MyTDIgpt[] cacheMyTDIgpt;
		public Myindicators.MyTDIgpt MyTDIgpt(int rsiPeriod, int signalSmoothing, int bBPeriod, double bBStdDev, bool meanReversion, string longOn, string shortOn, double signal_Offset)
		{
			return MyTDIgpt(Input, rsiPeriod, signalSmoothing, bBPeriod, bBStdDev, meanReversion, longOn, shortOn, signal_Offset);
		}

		public Myindicators.MyTDIgpt MyTDIgpt(ISeries<double> input, int rsiPeriod, int signalSmoothing, int bBPeriod, double bBStdDev, bool meanReversion, string longOn, string shortOn, double signal_Offset)
		{
			if (cacheMyTDIgpt != null)
				for (int idx = 0; idx < cacheMyTDIgpt.Length; idx++)
					if (cacheMyTDIgpt[idx] != null && cacheMyTDIgpt[idx].RsiPeriod == rsiPeriod && cacheMyTDIgpt[idx].SignalSmoothing == signalSmoothing && cacheMyTDIgpt[idx].BBPeriod == bBPeriod && cacheMyTDIgpt[idx].BBStdDev == bBStdDev && cacheMyTDIgpt[idx].MeanReversion == meanReversion && cacheMyTDIgpt[idx].LongOn == longOn && cacheMyTDIgpt[idx].ShortOn == shortOn && cacheMyTDIgpt[idx].Signal_Offset == signal_Offset && cacheMyTDIgpt[idx].EqualsInput(input))
						return cacheMyTDIgpt[idx];
			return CacheIndicator<Myindicators.MyTDIgpt>(new Myindicators.MyTDIgpt(){ RsiPeriod = rsiPeriod, SignalSmoothing = signalSmoothing, BBPeriod = bBPeriod, BBStdDev = bBStdDev, MeanReversion = meanReversion, LongOn = longOn, ShortOn = shortOn, Signal_Offset = signal_Offset }, input, ref cacheMyTDIgpt);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MyTDIgpt MyTDIgpt(int rsiPeriod, int signalSmoothing, int bBPeriod, double bBStdDev, bool meanReversion, string longOn, string shortOn, double signal_Offset)
		{
			return indicator.MyTDIgpt(Input, rsiPeriod, signalSmoothing, bBPeriod, bBStdDev, meanReversion, longOn, shortOn, signal_Offset);
		}

		public Indicators.Myindicators.MyTDIgpt MyTDIgpt(ISeries<double> input , int rsiPeriod, int signalSmoothing, int bBPeriod, double bBStdDev, bool meanReversion, string longOn, string shortOn, double signal_Offset)
		{
			return indicator.MyTDIgpt(input, rsiPeriod, signalSmoothing, bBPeriod, bBStdDev, meanReversion, longOn, shortOn, signal_Offset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MyTDIgpt MyTDIgpt(int rsiPeriod, int signalSmoothing, int bBPeriod, double bBStdDev, bool meanReversion, string longOn, string shortOn, double signal_Offset)
		{
			return indicator.MyTDIgpt(Input, rsiPeriod, signalSmoothing, bBPeriod, bBStdDev, meanReversion, longOn, shortOn, signal_Offset);
		}

		public Indicators.Myindicators.MyTDIgpt MyTDIgpt(ISeries<double> input , int rsiPeriod, int signalSmoothing, int bBPeriod, double bBStdDev, bool meanReversion, string longOn, string shortOn, double signal_Offset)
		{
			return indicator.MyTDIgpt(input, rsiPeriod, signalSmoothing, bBPeriod, bBStdDev, meanReversion, longOn, shortOn, signal_Offset);
		}
	}
}

#endregion
