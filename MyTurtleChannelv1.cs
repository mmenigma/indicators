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
	public class MyTurtleChannel : Indicator
	{
		private Series<double> upperSeries;
		private Series<double> lowerSeries;
		private Series<double> supSeries;
		private Series<double> sdownSeries;
		private Series<double> trendLineSeries;
		private Series<double> exitLineSeries;
		
		private Series<bool> buySignalSeries;
		private Series<bool> sellSignalSeries;
		private Series<bool> buyExitSeries;
		private Series<bool> sellExitSeries;
		
		private Series<int> barsSinceBuySignalSeries;
		private Series<int> barsSinceSellSignalSeries;
		private Series<int> barsSinceBuyExitSeries;
		private Series<int> barsSinceSellExitSeries;
        
        // Add series to track trend state for transitions
        private Series<bool> isLongTrendSeries;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Turtle Trade Channels Indicator based on the original Turtle Trading system";
				Name										= "MyTurtleChannel";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				IsAutoScale                                 = false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				// Default parameter values
				EntryLength                                 = 12;
				ExitLength                                  = 5;
				ShowEntrySignals                            = true;
				ShowExitSignals                             = true;
                ShowEntryText                               = true;  // New property
                ShowExitText                                = true;  // New property
				
				// Default colors
				UpperColor                                  = Brushes.Transparent;
				LowerColor                                  = Brushes.Transparent;
				TrendLineColor                              = Brushes.Red;      // Used for Short trend
				LongTrendLineColor                          = Brushes.Green;    // New property for Long trend
				ExitLineColor                               = Brushes.DimGray;
				LongEntryColor                              = Brushes.Green;
				ShortEntryColor                             = Brushes.Red;
				ExitColor                                   = Brushes.CornflowerBlue;
			}
			else if (State == State.Configure)
			{
				// Add plot series
				AddPlot(new Stroke(UpperColor, 1), PlotStyle.Line, "Upper");
				AddPlot(new Stroke(LowerColor, 1), PlotStyle.Line, "Lower");
				AddPlot(new Stroke(TrendLineColor, 2), PlotStyle.Line, "TrendLine");
				
				// Create stroke with dash style
				Stroke dashStroke = new Stroke(ExitLineColor, 1);
				dashStroke.DashStyleHelper = DashStyleHelper.Dash;
				AddPlot(dashStroke, PlotStyle.Line, "ExitLine");
			}
			else if (State == State.DataLoaded)
			{
				// Initialize series
				upperSeries = new Series<double>(this);
				lowerSeries = new Series<double>(this);
				supSeries = new Series<double>(this);
				sdownSeries = new Series<double>(this);
				trendLineSeries = new Series<double>(this);
				exitLineSeries = new Series<double>(this);
				
				buySignalSeries = new Series<bool>(this);
				sellSignalSeries = new Series<bool>(this);
				buyExitSeries = new Series<bool>(this);
				sellExitSeries = new Series<bool>(this);
				
				barsSinceBuySignalSeries = new Series<int>(this);
				barsSinceSellSignalSeries = new Series<int>(this);
				barsSinceBuyExitSeries = new Series<int>(this);
				barsSinceSellExitSeries = new Series<int>(this);
                
                // Initialize trend tracking series
                isLongTrendSeries = new Series<bool>(this);
				
				// Initialize all series with default values to prevent null references
				for (int i = 0; i < 100 && i < BarsArray[0].Count; i++)
				{
					barsSinceBuySignalSeries[i] = int.MaxValue;
					barsSinceSellSignalSeries[i] = int.MaxValue;
					barsSinceBuyExitSeries[i] = int.MaxValue;
					barsSinceSellExitSeries[i] = int.MaxValue;
                    isLongTrendSeries[i] = false;  // Default to short trend
				}
			}
		}

		protected override void OnBarUpdate()
		{
			// For early bars, just display the current price
			if (CurrentBar < Math.Max(EntryLength, ExitLength))
			{
				// Basic values for early bars
				upperSeries[0] = High[0];
				lowerSeries[0] = Low[0];
				trendLineSeries[0] = Close[0];
				exitLineSeries[0] = Close[0];
                isLongTrendSeries[0] = Close[0] > Open[0];  // Simple assumption for early bars
				
				// Set plot values
				Values[0][0] = upperSeries[0];     // Upper
				Values[1][0] = lowerSeries[0];     // Lower
				Values[2][0] = trendLineSeries[0]; // TrendLine
				Values[3][0] = exitLineSeries[0];  // ExitLine
				
				return;
			}
				
			// Calculate highest high and lowest low
			double highestHigh = double.MinValue;
			double lowestLow = double.MaxValue;
			
			for (int i = 0; i < EntryLength && CurrentBar >= i; i++)
			{
				if (High[i] > highestHigh)
					highestHigh = High[i];
				
				if (Low[i] < lowestLow)
					lowestLow = Low[i];
			}
			
			upperSeries[0] = highestHigh;
			lowerSeries[0] = lowestLow;
			
			// Calculate highest high and lowest low for exit
			double highestExit = double.MinValue;
			double lowestExit = double.MaxValue;
			
			for (int i = 0; i < ExitLength && CurrentBar >= i; i++)
			{
				if (High[i] > highestExit)
					highestExit = High[i];
				
				if (Low[i] < lowestExit)
					lowestExit = Low[i];
			}
			
			supSeries[0] = highestExit;
			sdownSeries[0] = lowestExit;
			
			// Detect signals - entry
			buySignalSeries[0] = High[0] >= upperSeries[1] || (High[0] > upperSeries[1] && High[1] <= upperSeries[1]);
			sellSignalSeries[0] = Low[0] <= lowerSeries[1] || (Low[0] < lowerSeries[1] && Low[1] >= lowerSeries[1]);
			
			// Detect signals - exit
			buyExitSeries[0] = Low[0] <= sdownSeries[1] || (Low[0] < sdownSeries[1] && Low[1] >= sdownSeries[1]);
			sellExitSeries[0] = High[0] >= supSeries[1] || (High[0] > supSeries[1] && High[1] <= supSeries[1]);
			
			// Count bars since signals
			barsSinceBuySignalSeries[0] = buySignalSeries[0] ? 0 : barsSinceBuySignalSeries[1] + 1;
			barsSinceSellSignalSeries[0] = sellSignalSeries[0] ? 0 : barsSinceSellSignalSeries[1] + 1;
			barsSinceBuyExitSeries[0] = buyExitSeries[0] ? 0 : barsSinceBuyExitSeries[1] + 1;
			barsSinceSellExitSeries[0] = sellExitSeries[0] ? 0 : barsSinceSellExitSeries[1] + 1;
			
			// Determine trend line (which channel to use)
			int barsSinceHigh = int.MaxValue;
			int barsSinceLow = int.MaxValue;
			
			for (int i = 0; i < CurrentBar; i++)
			{
				if (High[i] >= upperSeries[0])
				{
					barsSinceHigh = i;
					break;
				}
			}
			
			for (int i = 0; i < CurrentBar; i++)
			{
				if (Low[i] <= lowerSeries[0])
				{
					barsSinceLow = i;
					break;
				}
			}
			
			// K1 in Pine script - which band to use for trend line
			trendLineSeries[0] = barsSinceHigh <= barsSinceLow ? lowerSeries[0] : upperSeries[0];
			
			// K2 in Pine script - which band to use for exit line
			exitLineSeries[0] = barsSinceHigh <= barsSinceLow ? sdownSeries[0] : supSeries[0];
            
            // Determine if we're in a long trend
            bool isLongTrend = barsSinceHigh <= barsSinceLow;
            isLongTrendSeries[0] = isLongTrend;
            
            // Check for trend transition
            bool isTrendTransition = false;
            if (CurrentBar > 0)
            {
                isTrendTransition = isLongTrendSeries[0] != isLongTrendSeries[1];
            }
			
			// Set plot values
			Values[0][0] = upperSeries[0];     // Upper
			Values[1][0] = lowerSeries[0];     // Lower
			Values[2][0] = trendLineSeries[0]; // TrendLine
			Values[3][0] = exitLineSeries[0];  // ExitLine
            
            // Set dynamic colors for trend line and exit line
            if (isTrendTransition)
            {
                // During transition, make lines transparent
                PlotBrushes[2][0] = Brushes.Transparent;
                PlotBrushes[3][0] = Brushes.Transparent;
            }
            else
            {
                // Set appropriate color based on trend direction
                PlotBrushes[2][0] = isLongTrend ? LongTrendLineColor : TrendLineColor;
                PlotBrushes[3][0] = ExitLineColor;
            }
			
			// Draw signals on chart
			if (ShowEntrySignals || ShowExitSignals)
			{
				if (ShowEntrySignals)
				{
					// Long entry signal
					if (buySignalSeries[0] && barsSinceBuyExitSeries[0] < barsSinceBuySignalSeries[1])
					{
						Draw.Diamond(this, "LongEntry" + CurrentBar, false, 0, lowerSeries[0], LongEntryColor);
						
                        // Only draw text if ShowEntryText is enabled
                        if (ShowEntryText)
                        {
                            Draw.Text(this, "LongEntryText" + CurrentBar, "Long Entry", 0, lowerSeries[0] - TickSize * 5, LongEntryColor);
                        }
					}
					
					// Short entry signal
					if (sellSignalSeries[0] && barsSinceSellExitSeries[0] < barsSinceSellSignalSeries[1])
					{
						Draw.Diamond(this, "ShortEntry" + CurrentBar, false, 0, upperSeries[0], ShortEntryColor);
						
                        // Only draw text if ShowEntryText is enabled
                        if (ShowEntryText)
                        {
                            Draw.Text(this, "ShortEntryText" + CurrentBar, "Short Entry", 0, upperSeries[0] + TickSize * 20, ShortEntryColor);
                        }
					}
				}
				
				if (ShowExitSignals)
				{
					// Long exit signal
					if (buyExitSeries[0] && barsSinceBuySignalSeries[0] < barsSinceBuyExitSeries[1])
					{
						Draw.Diamond(this, "LongExit" + CurrentBar, false, 0, upperSeries[0], ExitColor);
						
                        // Only draw text if ShowExitText is enabled
                        if (ShowExitText)
                        {
                            Draw.Text(this, "LongExitText" + CurrentBar, "Exit Long", 0, upperSeries[0] + TickSize * 20, ExitColor);
                        }
					}
					
					// Short exit signal
					if (sellExitSeries[0] && barsSinceSellSignalSeries[0] < barsSinceSellExitSeries[1])
					{
						Draw.Diamond(this, "ShortExit" + CurrentBar, false, 0, lowerSeries[0], ExitColor);
						
                        // Only draw text if ShowExitText is enabled
                        if (ShowExitText)
                        {
                            Draw.Text(this, "ShortExitText" + CurrentBar, "Exit Short", 0, lowerSeries[0] - TickSize * 5, ExitColor);
                        }
					}
				}
			}
		}
		
		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Entry Length", Order=1, GroupName="Parameters")]
		public int EntryLength { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Exit Length", Order=2, GroupName="Parameters")]
		public int ExitLength { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Show Entry Signals", Order=3, GroupName="Parameters")]
		public bool ShowEntrySignals { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Show Exit Signals", Order=4, GroupName="Parameters")]
		public bool ShowExitSignals { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name="Show Entry Text", Order=5, GroupName="Parameters")]
        public bool ShowEntryText { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name="Show Exit Text", Order=6, GroupName="Parameters")]
        public bool ShowExitText { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Upper Line Color", Order=1, GroupName="Colors")]
		public Brush UpperColor { get; set; }
		
		[Browsable(false)]
		public string UpperColorSerializable
		{
			get { return Serialize.BrushToString(UpperColor); }
			set { UpperColor = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Lower Line Color", Order=2, GroupName="Colors")]
		public Brush LowerColor { get; set; }
		
		[Browsable(false)]
		public string LowerColorSerializable
		{
			get { return Serialize.BrushToString(LowerColor); }
			set { LowerColor = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Short Trend Line Color", Order=3, GroupName="Colors")]
		public Brush TrendLineColor { get; set; }
		
		[Browsable(false)]
		public string TrendLineColorSerializable
		{
			get { return Serialize.BrushToString(TrendLineColor); }
			set { TrendLineColor = Serialize.StringToBrush(value); }
		}
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Long Trend Line Color", Order=4, GroupName="Colors")]
        public Brush LongTrendLineColor { get; set; }
        
        [Browsable(false)]
        public string LongTrendLineColorSerializable
        {
            get { return Serialize.BrushToString(LongTrendLineColor); }
            set { LongTrendLineColor = Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Exit Line Color", Order=5, GroupName="Colors")]
		public Brush ExitLineColor { get; set; }
		
		[Browsable(false)]
		public string ExitLineColorSerializable
		{
			get { return Serialize.BrushToString(ExitLineColor); }
			set { ExitLineColor = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Long Entry Color", Order=6, GroupName="Colors")]
		public Brush LongEntryColor { get; set; }
		
		[Browsable(false)]
		public string LongEntryColorSerializable
		{
			get { return Serialize.BrushToString(LongEntryColor); }
			set { LongEntryColor = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Short Entry Color", Order=7, GroupName="Colors")]
		public Brush ShortEntryColor { get; set; }
		
		[Browsable(false)]
		public string ShortEntryColorSerializable
		{
			get { return Serialize.BrushToString(ShortEntryColor); }
			set { ShortEntryColor = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Exit Signal Color", Order=8, GroupName="Colors")]
		public Brush ExitColor { get; set; }
		
		[Browsable(false)]
		public string ExitColorSerializable
		{
			get { return Serialize.BrushToString(ExitColor); }
			set { ExitColor = Serialize.StringToBrush(value); }
		}
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.MyTurtleChannel[] cacheMyTurtleChannel;
		public Myindicators.MyTurtleChannel MyTurtleChannel(int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return MyTurtleChannel(Input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}

		public Myindicators.MyTurtleChannel MyTurtleChannel(ISeries<double> input, int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			if (cacheMyTurtleChannel != null)
				for (int idx = 0; idx < cacheMyTurtleChannel.Length; idx++)
					if (cacheMyTurtleChannel[idx] != null && cacheMyTurtleChannel[idx].EntryLength == entryLength && cacheMyTurtleChannel[idx].ExitLength == exitLength && cacheMyTurtleChannel[idx].ShowEntrySignals == showEntrySignals && cacheMyTurtleChannel[idx].ShowExitSignals == showExitSignals && cacheMyTurtleChannel[idx].ShowEntryText == showEntryText && cacheMyTurtleChannel[idx].ShowExitText == showExitText && cacheMyTurtleChannel[idx].UpperColor == upperColor && cacheMyTurtleChannel[idx].LowerColor == lowerColor && cacheMyTurtleChannel[idx].TrendLineColor == trendLineColor && cacheMyTurtleChannel[idx].LongTrendLineColor == longTrendLineColor && cacheMyTurtleChannel[idx].ExitLineColor == exitLineColor && cacheMyTurtleChannel[idx].LongEntryColor == longEntryColor && cacheMyTurtleChannel[idx].ShortEntryColor == shortEntryColor && cacheMyTurtleChannel[idx].ExitColor == exitColor && cacheMyTurtleChannel[idx].EqualsInput(input))
						return cacheMyTurtleChannel[idx];
			return CacheIndicator<Myindicators.MyTurtleChannel>(new Myindicators.MyTurtleChannel(){ EntryLength = entryLength, ExitLength = exitLength, ShowEntrySignals = showEntrySignals, ShowExitSignals = showExitSignals, ShowEntryText = showEntryText, ShowExitText = showExitText, UpperColor = upperColor, LowerColor = lowerColor, TrendLineColor = trendLineColor, LongTrendLineColor = longTrendLineColor, ExitLineColor = exitLineColor, LongEntryColor = longEntryColor, ShortEntryColor = shortEntryColor, ExitColor = exitColor }, input, ref cacheMyTurtleChannel);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(Input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}

		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(ISeries<double> input , int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(Input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}

		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(ISeries<double> input , int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}
	}
}

#endregion
