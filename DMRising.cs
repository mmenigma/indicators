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
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// This namespace holds indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
	/// <summary>
	/// Directional Movement (DM). This is the same indicator as the ADX,
	/// with the addition of the two directional movement indicators +DI
	/// and -DI. +DI and -DI measure upward and downward momentum. A buy
	/// signal is generated when +DI crosses -DI to the upside.
	/// A sell signal is generated when -DI crosses +DI to the downside.
	/// </summary>
	public class DMRising : Indicator
	{
		private Series<double> dmPlus;
		private Series<double> dmMinus;
		private Series<double> sumDmPlus;
		private Series<double> sumDmMinus;
		private Series<double> sumTr;
		private Series<double> tr;
		
		// Signal tracking variables
		private bool adxWasRising = false;
		private bool inLongTrade = false;
		private bool inShortTrade = false;
		private bool hasSignaledThisRise = false;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "Signals when DI lines a diverging and ADX is rising";
				Name						= "DMRising";
				IsSuspendedWhileInactive	= true;
				Period						= 14;

				AddPlot(new Stroke(Brushes.Gray, DashStyleHelper.Dash, 1), PlotStyle.Line, "ADX");
				AddPlot(new Stroke(Brushes.Lime, 1), PlotStyle.Line,			"+DI");
				AddPlot(new Stroke(Brushes.Red, 1), PlotStyle.Line,			"-DI");

				AddLine(Brushes.DarkCyan,						15,				"Lower ADX Threshold");
				AddLine(Brushes.DarkCyan,						75,				"Upper");
				
				ShowEntrySignals = false;
                LongOn = "LongEntry";
                ShortOn = "ShortEntry";

                ShowExitSignals = false;
                LongOff = "LongExit";
                ShortOff = "ShortExit";
				
				Signal_Offset = 5;
				
				// Signal Colors
				LongEntryColor = Brushes.Lime;
				ShortEntryColor = Brushes.Red;
				ExitColor = Brushes.DimGray;
			}

			else if (State == State.DataLoaded)
			{
				dmPlus		= new Series<double>(this);
				dmMinus		= new Series<double>(this);
				sumDmPlus	= new Series<double>(this);
				sumDmMinus	= new Series<double>(this);
				sumTr		= new Series<double>(this);
				tr			= new Series<double>(this);
			}
		}

		protected override void OnBarUpdate()
		{
			double high0		= High[0];
			double low0			= Low[0];
			double trueRange	= high0 - low0;

			if (CurrentBar == 0)
			{
				tr[0]			= trueRange;
				dmPlus[0]		= 0;
				dmMinus[0]		= 0;
				sumTr[0]		= tr[0];
				sumDmPlus[0]	= dmPlus[0];
				sumDmMinus[0]	= dmMinus[0];
				ADXPlot[0]		= 50;
			}
			else
			{
				double low1			= Low[1];
				double high1		= High[1];
				double close1		= Close[1];

				tr[0]				= Math.Max(Math.Abs(low0 - close1), Math.Max(trueRange, Math.Abs(high0 - close1)));
				dmPlus[0]			= high0 - high1 > low1 - low0 ? Math.Max(high0 - high1, 0) : 0;
				dmMinus[0]			= low1 - low0 > high0 - high1 ? Math.Max(low1 - low0, 0) : 0;

				double sumDmPlus1	= sumDmPlus[1];
				double sumDmMinus1	= sumDmMinus[1];
				double sumTr1		= sumTr[1];

				if (CurrentBar < Period)
				{
					sumTr[0]		= sumTr1 + tr[0];
					sumDmPlus[0]	= sumDmPlus1 + dmPlus[0];
					sumDmMinus[0]	= sumDmMinus1 + dmMinus[0];
				}
				else
				{
					sumTr[0]		= sumTr1 - sumTr[1] / Period + tr[0];
					sumDmPlus[0]	= sumDmPlus1 - sumDmPlus1 / Period + dmPlus[0];
					sumDmMinus[0]	= sumDmMinus1 - sumDmMinus1 / Period + dmMinus[0];
				}

				double diPlus	= 100 * (sumTr[0] == 0 ? 0 : sumDmPlus[0] / sumTr[0]);
				double diMinus	= 100 * (sumTr[0] == 0 ? 0 : sumDmMinus[0] / sumTr[0]);
				double diff		= Math.Abs(diPlus - diMinus);
				double sum		= diPlus + diMinus;

				ADXPlot[0]		= sum == 0 ? 50 : ((Period - 1) * ADXPlot[1] + 100 * diff / sum) / Period;
				DiPlus[0]		= diPlus;
				DiMinus[0]		= diMinus;
				
				// Signal Logic - only process if we have enough data
				if (CurrentBar > Period)
				{
					ProcessSignals();
				}
			}
		}
		
		private void ProcessSignals()
		{
			bool adxIsRising = ADXPlot[0] > ADXPlot[1];
			bool adxAboveThreshold = ADXPlot[0] > Lines[0].Value; // Lower line threshold
			
			// Check for exit conditions first (renko bar direction change)
			if (inLongTrade && Close[0] < Open[0]) // Down renko brick
			{
				if (ShowExitSignals)
				{
					Draw.Text(this, LongOff + CurrentBar, "x", 0, High[0] + Signal_Offset * TickSize * 3, ExitColor);
				}
				inLongTrade = false;
				hasSignaledThisRise = false; // Reset for new signals
			}
			else if (inShortTrade && Close[0] > Open[0]) // Up renko brick
			{
				if (ShowExitSignals)
				{
					Draw.Text(this, ShortOff + CurrentBar, "x", 0, Low[0] - Signal_Offset * TickSize * 3, ExitColor);
				}
				inShortTrade = false;
				hasSignaledThisRise = false; // Reset for new signals
			}
			
			// Check for entry conditions
			if (ShowEntrySignals && adxAboveThreshold)
			{
				// ADX started rising and we haven't signaled for this rise yet
				if (adxIsRising && !adxWasRising && !hasSignaledThisRise)
				{
					// Determine trend direction from DI lines
					if (DiPlus[0] > DiMinus[0] && !inLongTrade && !inShortTrade)
					{
						// Long signal
						Draw.TriangleUp(this, LongOn + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, LongEntryColor);
						inLongTrade = true;
						hasSignaledThisRise = true;
					}
					else if (DiMinus[0] > DiPlus[0] && !inLongTrade && !inShortTrade)
					{
						// Short signal
						Draw.TriangleDown(this, ShortOn + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, ShortEntryColor);
						inShortTrade = true;
						hasSignaledThisRise = true;
					}
				}
			}
			
			// Reset hasSignaledThisRise when ADX stops rising (for future signals)
			if (!adxIsRising && adxWasRising && !inLongTrade && !inShortTrade)
			{
				hasSignaledThisRise = false;
			}
			
			// Track ADX rising state for next bar
			adxWasRising = adxIsRising;
		}

		#region Properties
		[Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
		[XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
		public Series<double> ADXPlot
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> DiPlus
		{
			get { return Values[1]; }
		}

		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> DiMinus
		{
			get { return Values[2]; }
		}

		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Period", GroupName = "NinjaScriptParameters", Order = 0)]
		public int Period
		{ get; set; }
		
		// Signal Settings
		[NinjaScriptProperty]
        [Display(Name = "Show Entry Signals", Order = 0, GroupName = "Signal Settings")]
        public bool ShowEntrySignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Entry", GroupName = "Signal Settings", Order = 1)]
        public string LongOn { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Long Entry Color", GroupName = "Signal Settings", Order = 2)]
		public Brush LongEntryColor { get; set; }
		
		[Browsable(false)]
		public string LongEntryColorSerializable
		{
			get { return Serialize.BrushToString(LongEntryColor); }
			set { LongEntryColor = Serialize.StringToBrush(value); }
		}

        [NinjaScriptProperty]
        [Display(Name = "Short Entry", GroupName = "Signal Settings", Order = 3)]
        public string ShortOn { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Short Entry Color", GroupName = "Signal Settings", Order = 4)]
		public Brush ShortEntryColor { get; set; }
		
		[Browsable(false)]
		public string ShortEntryColorSerializable
		{
			get { return Serialize.BrushToString(ShortEntryColor); }
			set { ShortEntryColor = Serialize.StringToBrush(value); }
		}

        [NinjaScriptProperty]
        [Display(Name = "Show Exit Signals", GroupName = "Signal Settings", Order = 5)]
        public bool ShowExitSignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Exit", GroupName = "Signal Settings", Order = 6)]
        public string LongOff { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short Exit", GroupName = "Signal Settings", Order = 7)]
        public string ShortOff { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Exit Signal Color", GroupName = "Signal Settings", Order = 8)]
		public Brush ExitColor { get; set; }
		
		[Browsable(false)]
		public string ExitColorSerializable
		{
			get { return Serialize.BrushToString(ExitColor); }
			set { ExitColor = Serialize.StringToBrush(value); }
		}

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signals Offset", GroupName = "Signal Settings", Order = 9)]
        public double Signal_Offset { get; set; }
		
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.DMRising[] cacheDMRising;
		public Myindicators.DMRising DMRising(int period, bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, bool showExitSignals, string longOff, string shortOff, Brush exitColor, double signal_Offset)
		{
			return DMRising(Input, period, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, showExitSignals, longOff, shortOff, exitColor, signal_Offset);
		}

		public Myindicators.DMRising DMRising(ISeries<double> input, int period, bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, bool showExitSignals, string longOff, string shortOff, Brush exitColor, double signal_Offset)
		{
			if (cacheDMRising != null)
				for (int idx = 0; idx < cacheDMRising.Length; idx++)
					if (cacheDMRising[idx] != null && cacheDMRising[idx].Period == period && cacheDMRising[idx].ShowEntrySignals == showEntrySignals && cacheDMRising[idx].LongOn == longOn && cacheDMRising[idx].LongEntryColor == longEntryColor && cacheDMRising[idx].ShortOn == shortOn && cacheDMRising[idx].ShortEntryColor == shortEntryColor && cacheDMRising[idx].ShowExitSignals == showExitSignals && cacheDMRising[idx].LongOff == longOff && cacheDMRising[idx].ShortOff == shortOff && cacheDMRising[idx].ExitColor == exitColor && cacheDMRising[idx].Signal_Offset == signal_Offset && cacheDMRising[idx].EqualsInput(input))
						return cacheDMRising[idx];
			return CacheIndicator<Myindicators.DMRising>(new Myindicators.DMRising(){ Period = period, ShowEntrySignals = showEntrySignals, LongOn = longOn, LongEntryColor = longEntryColor, ShortOn = shortOn, ShortEntryColor = shortEntryColor, ShowExitSignals = showExitSignals, LongOff = longOff, ShortOff = shortOff, ExitColor = exitColor, Signal_Offset = signal_Offset }, input, ref cacheDMRising);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.DMRising DMRising(int period, bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, bool showExitSignals, string longOff, string shortOff, Brush exitColor, double signal_Offset)
		{
			return indicator.DMRising(Input, period, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, showExitSignals, longOff, shortOff, exitColor, signal_Offset);
		}

		public Indicators.Myindicators.DMRising DMRising(ISeries<double> input , int period, bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, bool showExitSignals, string longOff, string shortOff, Brush exitColor, double signal_Offset)
		{
			return indicator.DMRising(input, period, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, showExitSignals, longOff, shortOff, exitColor, signal_Offset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.DMRising DMRising(int period, bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, bool showExitSignals, string longOff, string shortOff, Brush exitColor, double signal_Offset)
		{
			return indicator.DMRising(Input, period, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, showExitSignals, longOff, shortOff, exitColor, signal_Offset);
		}

		public Indicators.Myindicators.DMRising DMRising(ISeries<double> input , int period, bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, bool showExitSignals, string longOff, string shortOff, Brush exitColor, double signal_Offset)
		{
			return indicator.DMRising(input, period, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, showExitSignals, longOff, shortOff, exitColor, signal_Offset);
		}
	}
}

#endregion
