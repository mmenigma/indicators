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

// This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    public class RMAC : Indicator
    {
        private Series<double> fastEma;
        private Series<double> slowEma;
        private Series<double> macdSignal;
        private Series<double> gain;
        private Series<double> loss;
        private double constant1;
        private double constant2;
        private double constant3;
        private double constant4;
        private double constant5;
        private double constant6;
		private bool LongOnSignalActive = false;
        private bool ShortOnSignalActive = false;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"MACD/RSI mash up";
                Name = "RMAC";
                Calculate = Calculate.OnBarClose;

                // MACD Settings
                Fast = 2;
                IsSuspendedWhileInactive = true;
                Slow = 20;
                Smooth = 20;

                // RSI Settings
                RsiPeriod = 14;

                // MACD Plots (overlay, no scale)
                AddPlot(Brushes.DarkCyan, "MACD Line"); // MACD Line
                AddPlot(Brushes.BlueViolet, "MACD Signal Line"); // MACD Signal Line
                AddLine(new Stroke(Brushes.Yellow, 1) { DashStyleHelper = DashStyleHelper.Dash }, 50, "Zero Line"); // Dashed Zero Line (centered at 50)

                // RSI Middle Line (60)
                Stroke rsiMiddleLineStroke = new Stroke(Brushes.Red, 1);
                rsiMiddleLineStroke.DashStyleHelper = DashStyleHelper.Dash;
                AddLine(rsiMiddleLineStroke, 60, "RSI Middle Line");

                // Signal Settings
                ShowEntrySignals = false;
                LongOn = "LongOn";
                ShortOn = "ShortOn";

                ShowExitSignals = false;
                LongOff = "LongOff";
                ShortOff = "ShortOff";

                Signal_Offset = 5;
            }
            else if (State == State.Configure)
            {
                constant1 = 2.0 / (1 + Fast);
                constant2 = 1 - (2.0 / (1 + Fast));
                constant3 = 2.0 / (1 + Slow);
                constant4 = 1 - (2.0 / (1 + Slow));
                constant5 = 2.0 / (1 + Smooth);
                constant6 = 1 - (2.0 / (1 + Smooth));

                gain = new Series<double>(this);
                loss = new Series<double>(this);
                macdSignal = new Series<double>(this);
            }
            else if (State == State.DataLoaded)
            {
                fastEma = new Series<double>(this);
                slowEma = new Series<double>(this);
            }
        }

		protected override void OnBarUpdate()
		{
	    double input0 = Input[0];
	
	    if (CurrentBar == 0)
	    {
	        fastEma[0] = input0;
	        slowEma[0] = input0;
	        gain[0] = 0;
	        loss[0] = 0;
	        Values[0][0] = 50; // MACD Line (normalized to RSI scale)
	        Values[1][0] = 50; // MACD Signal Line (normalized to RSI scale)
	    }
	    else
	    {
        // MACD Calculation
        double fastEma0 = constant1 * input0 + constant2 * fastEma[1];
        double slowEma0 = constant3 * input0 + constant4 * slowEma[1];
        double macd = fastEma0 - slowEma0;
        double macdAvg = constant5 * macd + constant6 * macdSignal[1];

        fastEma[0] = fastEma0;
        slowEma[0] = slowEma0;
        macdSignal[0] = macdAvg;

        // Normalize MACD values to RSI scale (0 to 100)
        double macdNormalized = (macd + 100) / 2; // Adjust MACD to fit RSI scale
        double macdAvgNormalized = (macdAvg + 100) / 2; // Adjust MACD Avg to fit RSI scale

        Values[0][0] = macdNormalized; // MACD Line
        Values[1][0] = macdAvgNormalized; // MACD Signal Line

       // LongOn Signal Conditions
		if (Values[0][0] > Values[1][0] && Values[0][0] > 60) // MACD Line > MACD Signal Line AND MACD Line > RSI Middle Line (60)
		{
		    if (!LongOnSignalActive) // Only print the first signal
		    {
		        Draw.ArrowUp(this, LongOn + CurrentBar, true, 0, Low[0] - TickSize * Signal_Offset, Brushes.Lime);
		        LongOnSignalActive = true; // Set signal active
		    }
		}
		
		// ShortOn Signal Conditions
		if (Values[0][0] < Values[1][0] && Values[0][0] < 50) // MACD Line < MACD Signal Line AND MACD Line < MACD Zero Line (50)
		{
		    if (!ShortOnSignalActive) // Only print the first signal
		    {
		        Draw.ArrowDown(this, ShortOn + CurrentBar, true, 0, High[0] + TickSize * Signal_Offset, Brushes.Red);
		        ShortOnSignalActive = true; // Set signal active
		    }
		}
		
		// LongOff Exit Signal Conditions
		if (ShowExitSignals && LongOnSignalActive && CrossBelow(Values[0], Values[1], 1)) // MACD Line crosses below MACD Signal Line
		{
		    Draw.ArrowDown(this, LongOff + CurrentBar, true, 0, High[0] + TickSize * (Signal_Offset + 30), Brushes.Transparent); // Add 10-tick offset
		    LongOnSignalActive = false; // Reset LongOn signal
		}
		
		// ShortOff Exit Signal Conditions
		if (ShowExitSignals && ShortOnSignalActive && CrossAbove(Values[0], Values[1], 1)) // MACD Line crosses above MACD Signal Line
		{
		    Draw.ArrowUp(this, ShortOff + CurrentBar, true, 0, Low[0] - TickSize * (Signal_Offset + 30), Brushes.Transparent); // Add 10-tick offset
		    ShortOnSignalActive = false; // Reset ShortOn signal
		}
	}
}

        #region Properties
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> MACDLine
        {
            get { return Values[0]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> MACDSignalLine
        {
            get { return Values[1]; }
        }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "MACD Fast", GroupName = "MACD/RSI Settings", Order = 0)]
        public int Fast { get; set; }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "MACD Slow", GroupName = "MACD/RSI Settings", Order = 1)]
        public int Slow { get; set; }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "MACD Smooth", GroupName = "MACD/RSI Settings", Order = 2)]
        public int Smooth { get; set; }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(Name = "RSI Period", GroupName = "MACD/RSI Settings", Order = 3)]
        public int RsiPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show ENTRY Signals", Order = 0, GroupName = "Signal Settings")]
        public bool ShowEntrySignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", GroupName = "Signal Settings", Order = 1)]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short On", GroupName = "Signal Settings", Order = 2)]
        public string ShortOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Exit Signals", GroupName = "Signal Settings", Order = 3)]
        public bool ShowExitSignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Off", GroupName = "Signal Settings", Order = 4)]
        public string LongOff { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short Off", GroupName = "Signal Settings", Order = 5)]
        public string ShortOff { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signals Offset", GroupName = "Signal Settings", Order = 6)]
        public double Signal_Offset { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.RMAC[] cacheRMAC;
		public Myindicators.RMAC RMAC(int fast, int slow, int smooth, int rsiPeriod, bool showEntrySignals, string longOn, string shortOn, bool showExitSignals, string longOff, string shortOff, double signal_Offset)
		{
			return RMAC(Input, fast, slow, smooth, rsiPeriod, showEntrySignals, longOn, shortOn, showExitSignals, longOff, shortOff, signal_Offset);
		}

		public Myindicators.RMAC RMAC(ISeries<double> input, int fast, int slow, int smooth, int rsiPeriod, bool showEntrySignals, string longOn, string shortOn, bool showExitSignals, string longOff, string shortOff, double signal_Offset)
		{
			if (cacheRMAC != null)
				for (int idx = 0; idx < cacheRMAC.Length; idx++)
					if (cacheRMAC[idx] != null && cacheRMAC[idx].Fast == fast && cacheRMAC[idx].Slow == slow && cacheRMAC[idx].Smooth == smooth && cacheRMAC[idx].RsiPeriod == rsiPeriod && cacheRMAC[idx].ShowEntrySignals == showEntrySignals && cacheRMAC[idx].LongOn == longOn && cacheRMAC[idx].ShortOn == shortOn && cacheRMAC[idx].ShowExitSignals == showExitSignals && cacheRMAC[idx].LongOff == longOff && cacheRMAC[idx].ShortOff == shortOff && cacheRMAC[idx].Signal_Offset == signal_Offset && cacheRMAC[idx].EqualsInput(input))
						return cacheRMAC[idx];
			return CacheIndicator<Myindicators.RMAC>(new Myindicators.RMAC(){ Fast = fast, Slow = slow, Smooth = smooth, RsiPeriod = rsiPeriod, ShowEntrySignals = showEntrySignals, LongOn = longOn, ShortOn = shortOn, ShowExitSignals = showExitSignals, LongOff = longOff, ShortOff = shortOff, Signal_Offset = signal_Offset }, input, ref cacheRMAC);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.RMAC RMAC(int fast, int slow, int smooth, int rsiPeriod, bool showEntrySignals, string longOn, string shortOn, bool showExitSignals, string longOff, string shortOff, double signal_Offset)
		{
			return indicator.RMAC(Input, fast, slow, smooth, rsiPeriod, showEntrySignals, longOn, shortOn, showExitSignals, longOff, shortOff, signal_Offset);
		}

		public Indicators.Myindicators.RMAC RMAC(ISeries<double> input , int fast, int slow, int smooth, int rsiPeriod, bool showEntrySignals, string longOn, string shortOn, bool showExitSignals, string longOff, string shortOff, double signal_Offset)
		{
			return indicator.RMAC(input, fast, slow, smooth, rsiPeriod, showEntrySignals, longOn, shortOn, showExitSignals, longOff, shortOff, signal_Offset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.RMAC RMAC(int fast, int slow, int smooth, int rsiPeriod, bool showEntrySignals, string longOn, string shortOn, bool showExitSignals, string longOff, string shortOff, double signal_Offset)
		{
			return indicator.RMAC(Input, fast, slow, smooth, rsiPeriod, showEntrySignals, longOn, shortOn, showExitSignals, longOff, shortOff, signal_Offset);
		}

		public Indicators.Myindicators.RMAC RMAC(ISeries<double> input , int fast, int slow, int smooth, int rsiPeriod, bool showEntrySignals, string longOn, string shortOn, bool showExitSignals, string longOff, string shortOff, double signal_Offset)
		{
			return indicator.RMAC(input, fast, slow, smooth, rsiPeriod, showEntrySignals, longOn, shortOn, showExitSignals, longOff, shortOff, signal_Offset);
		}
	}
}

#endregion
