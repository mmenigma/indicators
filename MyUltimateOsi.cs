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
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
	/// <summary>
	/// V25 The Ultimate Oscillator is the weighted sum of three oscillators of different time periods.
	/// The typical time periods are 7, 14 and 28. The values of the Ultimate Oscillator range
	/// from zero to 100. Values over 70 indicate overbought conditions, and values under 30 indicate
	/// oversold conditions. Also look for agreement/divergence with the price to confirm a trend or signal the end of a trend.
	/// </summary>
	public class MyUltimateOsi : Indicator
	{
		private Series<double>	buyingPressure;
		private double			constant1;
		private double			constant2;
		private double			constant3;
		private SUM				sumBpFast;
		private SUM				sumBpIntermediate;
		private SUM				sumBpSlow;
		private SUM				sumTrFast;
		private SUM				sumTrIntermediate;
		private SUM				sumTrSlow;
		private Series<double>	trueRange;
		
		// Signal tracking variables
		private bool			inLongTrend;
		private bool			inShortTrend;
		
		// Filter variables for deep oversold/overbought
		private bool			reachedOversoldLevel;
		private bool			reachedOverboughtLevel;
		
		// Data logging variables
		private string			currentLogFile;
		private bool			headerWritten;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "Ultimate Oscillator for Renko Bars";
				Name						= "MyUltimateOsi";
				IsSuspendedWhileInactive	= true;
				Fast						= 7;
				Intermediate				= 14;
				Slow						= 28;

				AddPlot(Brushes.DodgerBlue,		"MyUltimateOsi");

				AddLine(Brushes.Lime,	30,	"Oversold");
				AddLine(Brushes.DarkGray,	50, "Neutral");
				AddLine(Brushes.Red,	70,	"Overbought");
				
				// Signal Settings
				ShowEntrySignal = false;
				LongOn = "LongEntry";
				LongEntryColor = Brushes.Lime;
				ShortOn = "ShortEntry";
				ShortEntryColor = Brushes.Red;

				ShowExitSignal = false;
				LongOff = "LongExit";
				ShortOff = "ShortExit";
				ExitColor = Brushes.DimGray;
						
				Signal_Offset = 5;
				
				// Filter thresholds - optimized for quality over quantity
				OversoldThreshold = 15;
				OverboughtThreshold = 85;
			}
			else if (State == State.Configure)
			{
				constant1			= Slow / Fast;
				constant2			= Slow / Intermediate;
				constant3			= constant1 + constant2 + 1;
			}
			else if (State == State.DataLoaded)
			{
				buyingPressure		= new Series<double>(this);
				trueRange			= new Series<double>(this);
				sumBpFast			= SUM(buyingPressure, Fast);
				sumBpIntermediate	= SUM(buyingPressure, Intermediate);
				sumBpSlow			= SUM(buyingPressure, Slow);
				sumTrFast			= SUM(trueRange, Fast);
				sumTrIntermediate	= SUM(trueRange, Intermediate);
				sumTrSlow			= SUM(trueRange, Slow);
				
				// Initialize trend tracking
				inLongTrend = false;
				inShortTrend = false;
				
				// Initialize filter flags
				reachedOversoldLevel = false;
				reachedOverboughtLevel = false;
				
				// Initialize data logging
				currentLogFile = "";
				headerWritten = false;
				
				// Make the 50 line dashed
				Lines[1].DashStyleHelper = DashStyleHelper.Dash;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar == 0)
			{
				Value[0] = 0;
				return;
			}

			double high0	= High[0];
			double low0		= Low[0];
			double close0	= Close[0];
			double close1	= Close[1];

			buyingPressure[0] 	= close0 - Math.Min(low0, close1);
			trueRange[0] 		= Math.Max(Math.Max(high0 - low0, high0 - close1), close1 - low0);

			// Use previous value if we get into trouble
			if (sumTrFast[0] == 0 || sumTrIntermediate[0] == 0 || sumTrSlow[0] == 0)
			{
				Value[0] = Value[1];
				return;
			}

			Value[0] = (((sumBpFast[0] / sumTrFast[0]) * constant1
							+ (sumBpIntermediate[0] / sumTrIntermediate[0]) * constant2
							+ (sumBpSlow[0] / sumTrSlow[0]))
							/ (constant3)) * 100;

			// Signal Logic
			if (CurrentBar < 1) return; // Need at least 2 bars for signals
			
			// Track when we reach deep oversold/overbought levels
			if (Value[0] <= OversoldThreshold)
				reachedOversoldLevel = true;
				
			if (Value[0] >= OverboughtThreshold)
				reachedOverboughtLevel = true;
			
			// Long Entry: UO crosses above 30 AND we previously reached deep oversold
			bool longSignalTriggered = false;
			if (CrossAbove(Value, 30, 1) && ShowEntrySignal)
			{
				if (reachedOversoldLevel)
				{
					Draw.ArrowUp(this, LongOn + CurrentBar.ToString(), true, 0, Low[0] - Signal_Offset * TickSize, LongEntryColor);
					inLongTrend = true;
					inShortTrend = false;
					longSignalTriggered = true;
				}
			}

			// Short Entry: UO crosses below 70 AND we previously reached deep overbought
			bool shortSignalTriggered = false;
			if (CrossBelow(Value, 70, 1) && ShowEntrySignal)
			{
				if (reachedOverboughtLevel)
				{
					Draw.ArrowDown(this, ShortOn + CurrentBar.ToString(), true, 0, High[0] + Signal_Offset * TickSize, ShortEntryColor);
					inShortTrend = true;
					inLongTrend = false;
					shortSignalTriggered = true;
				}
			}

			// Exit Signals - Trend Change Logic
			if (ShowExitSignal && CurrentBar >= 1)
			{
				// Long Exit: In long trend and current bar closes lower than previous bar
				if (inLongTrend && Close[0] < Close[1])
				{
					Draw.Text(this, LongOff + CurrentBar.ToString(), "o", 0, High[0] + Signal_Offset * TickSize * 5, ExitColor);
					inLongTrend = false;
				}
				
				// Short Exit: In short trend and current bar closes higher than previous bar
				if (inShortTrend && Close[0] > Close[1])
				{
					Draw.Text(this, ShortOff + CurrentBar.ToString(), "o", 0, Low[0] - Signal_Offset * TickSize, ExitColor);
					inShortTrend = false;
				}
			}
			
			// Data logging for analysis (9:30 AM to 4:00 PM EST) - AFTER all logic
			LogDataForAnalysis();
			
			// Reset flags AFTER logging to preserve them for the CSV
			if (longSignalTriggered)
				reachedOversoldLevel = false;
			if (shortSignalTriggered)
				reachedOverboughtLevel = false;
		}
		
		private void LogDataForAnalysis()
		{
			try
			{
				// Only log during trading hours (9:30 AM to 4:00 PM EST)
				DateTime barTime = Time[0];
				TimeSpan startTime = new TimeSpan(9, 30, 0);   // 9:30 AM
				TimeSpan endTime = new TimeSpan(16, 0, 0);     // 4:00 PM
				
				if (barTime.TimeOfDay < startTime || barTime.TimeOfDay >= endTime)
					return;
				
				// Create daily file name
				string dateString = barTime.ToString("yyyy-MM-dd");
				string fileName = $"UO_Data_{dateString}.csv";
				string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				string filePath = Path.Combine(documentsPath, fileName);
				
				// Write header if new file
				if (currentLogFile != filePath)
				{
					currentLogFile = filePath;
					headerWritten = false;
				}
				
				if (!headerWritten)
				{
					using (StreamWriter writer = new StreamWriter(filePath, false))
					{
						writer.WriteLine("DateTime,UO_Value,High,Low,Close,Open,ReachedOversold,ReachedOverbought,InLongTrend,InShortTrend");
					}
					headerWritten = true;
				}
				
				// Append data - CRITICAL: Log the actual C# boolean variables, not computed values
				using (StreamWriter writer = new StreamWriter(filePath, true))
				{
					string logLine = $"{barTime:yyyy-MM-dd HH:mm:ss},{Value[0]:F2},{High[0]:F2},{Low[0]:F2},{Close[0]:F2},{Open[0]:F2},{reachedOversoldLevel},{reachedOverboughtLevel},{inLongTrend},{inShortTrend}";
					writer.WriteLine(logLine);
				}
			}
			catch (Exception ex)
			{
				// Silently handle any file I/O errors to prevent indicator from crashing
				Print($"Data logging error: {ex.Message}");
			}
		}

		#region Properties
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Fast", GroupName = "NinjaScriptParameters", Order = 0)]
		public int Fast
		{ get; set; }

		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Intermediate", GroupName = "NinjaScriptParameters", Order = 1)]
		public int Intermediate
		{ get; set; }

		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Slow", GroupName = "NinjaScriptParameters", Order = 2)]
		public int Slow
		{ get; set; }
		
		// Signal Properties
		[NinjaScriptProperty]
		[Display(Name = "Show Entry Signals", Order = 1, GroupName = "Entry Signal Settings")]
		public bool ShowEntrySignal { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Long On", GroupName = "Entry Signal Settings", Order = 2)]
		public string LongOn { get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Long Entry Color", Description = "Color for the long entry arrow", Order = 3, GroupName = "Entry Signal Settings")]
		public Brush LongEntryColor { get; set; }
		        
		[Browsable(false)]
		public string LongEntryColorSerializable
		{
			get { return Serialize.BrushToString(LongEntryColor); }
			set { LongEntryColor = Serialize.StringToBrush(value); }
		}
			
		[NinjaScriptProperty]
		[Display(Name = "Short On", GroupName = "Entry Signal Settings", Order = 4)]
		public string ShortOn { get; set; }
			
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Short Entry Color", Description = "Color for the short entry arrow", Order = 5, GroupName = "Entry Signal Settings")]
		public Brush ShortEntryColor { get; set; }
			
		[Browsable(false)]
		public string ShortEntryColorSerializable
		{
			get { return Serialize.BrushToString(ShortEntryColor); }
			set { ShortEntryColor = Serialize.StringToBrush(value); }
		}
		        
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Signals Offset", GroupName = "Entry Signal Settings", Order = 6)]
		public double Signal_Offset { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Exit Signals", GroupName = "Exit Signal Settings", Order = 7,
		Description = "When enabled, shows exit signals on trend change")]
		public bool ShowExitSignal { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Long Off", GroupName = "Exit Signal Settings", Order = 8)]
		public string LongOff { get; set; }
		        
		[NinjaScriptProperty]
		[Display(Name = "Short Off", GroupName = "Exit Signal Settings", Order = 9)]
		public string ShortOff { get; set; }
			
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Exit Color", Description = "Color for exit signals", Order = 10, GroupName = "Exit Signal Settings")]
		public Brush ExitColor { get; set; }

		[Browsable(false)]
		public string ExitColorSerializable
		{
			get { return Serialize.BrushToString(ExitColor); }
			set { ExitColor = Serialize.StringToBrush(value); }
		}
		
		// Filter Threshold Properties
		[Range(5, 30), NinjaScriptProperty]
		[Display(Name = "Oversold Threshold", GroupName = "Filter Settings", Order = 11,
		Description = "UO must reach this level or below before long signals are allowed (lower = more restrictive)")]
		public int OversoldThreshold { get; set; }

		[Range(70, 95), NinjaScriptProperty]
		[Display(Name = "Overbought Threshold", GroupName = "Filter Settings", Order = 12,
		Description = "UO must reach this level or above before short signals are allowed (higher = more restrictive)")]
		public int OverboughtThreshold { get; set; }
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.MyUltimateOsi[] cacheMyUltimateOsi;
		public Myindicators.MyUltimateOsi MyUltimateOsi(int fast, int intermediate, int slow, bool showEntrySignal, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signal_Offset, bool showExitSignal, string longOff, string shortOff, Brush exitColor, int oversoldThreshold, int overboughtThreshold)
		{
			return MyUltimateOsi(Input, fast, intermediate, slow, showEntrySignal, longOn, longEntryColor, shortOn, shortEntryColor, signal_Offset, showExitSignal, longOff, shortOff, exitColor, oversoldThreshold, overboughtThreshold);
		}

		public Myindicators.MyUltimateOsi MyUltimateOsi(ISeries<double> input, int fast, int intermediate, int slow, bool showEntrySignal, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signal_Offset, bool showExitSignal, string longOff, string shortOff, Brush exitColor, int oversoldThreshold, int overboughtThreshold)
		{
			if (cacheMyUltimateOsi != null)
				for (int idx = 0; idx < cacheMyUltimateOsi.Length; idx++)
					if (cacheMyUltimateOsi[idx] != null && cacheMyUltimateOsi[idx].Fast == fast && cacheMyUltimateOsi[idx].Intermediate == intermediate && cacheMyUltimateOsi[idx].Slow == slow && cacheMyUltimateOsi[idx].ShowEntrySignal == showEntrySignal && cacheMyUltimateOsi[idx].LongOn == longOn && cacheMyUltimateOsi[idx].LongEntryColor == longEntryColor && cacheMyUltimateOsi[idx].ShortOn == shortOn && cacheMyUltimateOsi[idx].ShortEntryColor == shortEntryColor && cacheMyUltimateOsi[idx].Signal_Offset == signal_Offset && cacheMyUltimateOsi[idx].ShowExitSignal == showExitSignal && cacheMyUltimateOsi[idx].LongOff == longOff && cacheMyUltimateOsi[idx].ShortOff == shortOff && cacheMyUltimateOsi[idx].ExitColor == exitColor && cacheMyUltimateOsi[idx].OversoldThreshold == oversoldThreshold && cacheMyUltimateOsi[idx].OverboughtThreshold == overboughtThreshold && cacheMyUltimateOsi[idx].EqualsInput(input))
						return cacheMyUltimateOsi[idx];
			return CacheIndicator<Myindicators.MyUltimateOsi>(new Myindicators.MyUltimateOsi(){ Fast = fast, Intermediate = intermediate, Slow = slow, ShowEntrySignal = showEntrySignal, LongOn = longOn, LongEntryColor = longEntryColor, ShortOn = shortOn, ShortEntryColor = shortEntryColor, Signal_Offset = signal_Offset, ShowExitSignal = showExitSignal, LongOff = longOff, ShortOff = shortOff, ExitColor = exitColor, OversoldThreshold = oversoldThreshold, OverboughtThreshold = overboughtThreshold }, input, ref cacheMyUltimateOsi);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MyUltimateOsi MyUltimateOsi(int fast, int intermediate, int slow, bool showEntrySignal, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signal_Offset, bool showExitSignal, string longOff, string shortOff, Brush exitColor, int oversoldThreshold, int overboughtThreshold)
		{
			return indicator.MyUltimateOsi(Input, fast, intermediate, slow, showEntrySignal, longOn, longEntryColor, shortOn, shortEntryColor, signal_Offset, showExitSignal, longOff, shortOff, exitColor, oversoldThreshold, overboughtThreshold);
		}

		public Indicators.Myindicators.MyUltimateOsi MyUltimateOsi(ISeries<double> input , int fast, int intermediate, int slow, bool showEntrySignal, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signal_Offset, bool showExitSignal, string longOff, string shortOff, Brush exitColor, int oversoldThreshold, int overboughtThreshold)
		{
			return indicator.MyUltimateOsi(input, fast, intermediate, slow, showEntrySignal, longOn, longEntryColor, shortOn, shortEntryColor, signal_Offset, showExitSignal, longOff, shortOff, exitColor, oversoldThreshold, overboughtThreshold);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MyUltimateOsi MyUltimateOsi(int fast, int intermediate, int slow, bool showEntrySignal, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signal_Offset, bool showExitSignal, string longOff, string shortOff, Brush exitColor, int oversoldThreshold, int overboughtThreshold)
		{
			return indicator.MyUltimateOsi(Input, fast, intermediate, slow, showEntrySignal, longOn, longEntryColor, shortOn, shortEntryColor, signal_Offset, showExitSignal, longOff, shortOff, exitColor, oversoldThreshold, overboughtThreshold);
		}

		public Indicators.Myindicators.MyUltimateOsi MyUltimateOsi(ISeries<double> input , int fast, int intermediate, int slow, bool showEntrySignal, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signal_Offset, bool showExitSignal, string longOff, string shortOff, Brush exitColor, int oversoldThreshold, int overboughtThreshold)
		{
			return indicator.MyUltimateOsi(input, fast, intermediate, slow, showEntrySignal, longOn, longEntryColor, shortOn, shortEntryColor, signal_Offset, showExitSignal, longOff, shortOff, exitColor, oversoldThreshold, overboughtThreshold);
		}
	}
}

#endregion
