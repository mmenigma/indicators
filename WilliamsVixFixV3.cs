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
	public class WilliamsVixFixV3 : Indicator
	{
        private Series<double> wvfSeries;
        private Series<bool> isAboveUpperBandSeries;
        private Series<bool> isBelowLowerBandSeries;
        private Series<bool> isSimpleEntrySeries; // Added for tracking simple entries
        private Series<bool> isFilteredEntrySeries; // Added for tracking filtered entries

        // Declare variables for price action strength and lookback periods
        private int str = 3; // Entry price action strength
        private int ltLB = 40; // Long-term lookback period
        private int mtLB = 14; // Medium-term lookback period
		
		// Variables for tracking trade signals
		private bool hasGeneratedEntrySignal = false;
		private bool hasGeneratedExitSignal = false;
		private int lastBearishBar = -1;
		private int lastEntrySignalBar = -1;
		private int lastExitSignalBar = -1;
		private int entryConditionBarIndex = -1; // Tracks the bar where entry condition was first met
        private int exitConditionBarIndex = -1; // Tracks the bar where exit condition was first met

        protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        Description = @"Williams Vix Fix V3 with dual reset logic";
        Name = "WilliamsVixFixV3";
        Calculate = Calculate.OnBarClose;
        IsOverlay = false;
        DisplayInDataBox = true;
        DrawOnPricePanel = true;
        DrawHorizontalGridLines = true;
        DrawVerticalGridLines = true;
        PaintPriceMarkers = true;
        ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
        IsSuspendedWhileInactive = true;

        // Inputs for WVF calculation
        SDHigh = 22;
        BBLength = 20;
        SDUp = 2;
        PHigh = 50;
        HP = 0.85;
        LP = 1.01;

        // Inputs for bar coloring
        HighlightFuchsia = true;
        HighlightWhite = true;

        // Add a plot for the WVF histogram
        AddPlot(new Stroke(Brushes.Lime, 2), PlotStyle.Bar, "VixFix");
        
        // Signal Settings
        ShowEntrySignal = false;
        LongOn = "LongOn";
        // ShortOn = "ShortOn";
        LongEntryMark = .08;
        ConfirmationBars = 0;
		EntryArrowColor = Brushes.Yellow;  // Default yellow for entry arrows


        ShowExitSignal = false;
        LongOff = "LongOff";
        // ShortOff = "ShortOff";
        LongExitMark = .3;
		ExitXColor = Brushes.White;       // Changed to white for testing (was Black)

        Signal_Offset = 5;
    }
    else if (State == State.Configure)
    {
        // Initialize series for tracking previous conditions
        isAboveUpperBandSeries = new Series<bool>(this);
        isBelowLowerBandSeries = new Series<bool>(this);
        isSimpleEntrySeries = new Series<bool>(this); // Initialize simple entry series
        isFilteredEntrySeries = new Series<bool>(this); // Initialize filtered entry series
    }
    else if (State == State.DataLoaded)
    {
        // Initialize the wvf series
        wvfSeries = new Series<double>(this);
        
        // Initialize signal tracking variables
        hasGeneratedEntrySignal = false;
        hasGeneratedExitSignal = false;
        lastBearishBar = -1;
        lastEntrySignalBar = -1;
        lastExitSignalBar = -1;
        entryConditionBarIndex = -1;
        exitConditionBarIndex = -1;
    }
}
protected override void OnBarUpdate()
{
    // Ensure we have enough bars for the indicator
    if (CurrentBar < PHigh || CurrentBar < SDHigh) return;

    // Calculate WVF
    wvfSeries[0] = ((MAX(Close, SDHigh)[0] - Low[0]) / MAX(Close, SDHigh)[0]) * 100;

    // Calculate Bollinger Bands and Range High/Low
    double sDev = SDUp * StdDev(wvfSeries, BBLength)[0];
    double midLine = SMA(wvfSeries, BBLength)[0];
    double lowerBand = midLine - sDev;
    double upperBand = midLine + sDev;
    double rangeHigh = HP * MAX(wvfSeries, PHigh)[0];
    double rangeLow = LP * MIN(wvfSeries, PHigh)[0];

    // Store whether WVF is above upper band/range high
    bool isWvfHigh = wvfSeries[0] >= upperBand || wvfSeries[0] >= rangeHigh;
    bool wasWvfHigh = wvfSeries[1] >= upperBand || wvfSeries[1] >= rangeHigh;
    
    // Store current conditions in series for next bar
    isAboveUpperBandSeries[0] = isWvfHigh;
    isBelowLowerBandSeries[0] = wvfSeries[0] <= lowerBand || wvfSeries[0] <= rangeLow;

    // Set the plot value for the WVF histogram
    Values[0][0] = wvfSeries[0];

    // Apply color to the WVF histogram (indicator bars)
    PlotBrushes[0][0] = isWvfHigh ? Brushes.Lime : Brushes.Gray;

    // Reset entry flags
    isSimpleEntrySeries[0] = false;
    isFilteredEntrySeries[0] = false;
    
    // Price action criteria - matching .pin logic more closely
    bool upRange = Low[0] > Low[1] && Close[0] > High[1];
    bool strengthCriteria = Close[0] > Close[str] && (Close[0] < Close[ltLB] || Close[0] < Close[mtLB]);
    
    // Simple Entry - Fuchsia (bFiltered in .pin)
    bool isSimpleEntry = false;
    if (wasWvfHigh && !isWvfHigh) // WVF crossed below upperBand/rangeHigh
    {
        isSimpleEntry = true;
    }
    
    // Filtered Entry - White (.pin alert3 logic)
    bool isFilteredEntry = false;
    if (wasWvfHigh && !isWvfHigh && upRange && strengthCriteria)
    {
        isFilteredEntry = true;
    }

    // Apply colors - prioritize Filtered (White) over Simple (Fuchsia)
    if (HighlightWhite && isFilteredEntry && !isFilteredEntrySeries[1] && !isSimpleEntrySeries[1])
    {
        BarBrushes[0] = Brushes.White;
        isFilteredEntrySeries[0] = true;
    }
    else if (HighlightFuchsia && isSimpleEntry && !isSimpleEntrySeries[1] && !isFilteredEntrySeries[1])
    {
        BarBrushes[0] = Brushes.Fuchsia;
        isSimpleEntrySeries[0] = true;
    }
    else
    {
        BarBrushes[0] = null; // Default chart color
    }
    
    // ==================== MODIFIED TRADE SIGNALS LOGIC ====================
    
    // Track if we've had a bearish bar 
    bool isBearishBar = Close[0] < Open[0];
    
    // Check for exit condition based on WVF value
    bool isExitConditionMet = wvfSeries[0] >= LongExitMark;
    
    // Long Entry Signal Logic 
    // Check for meeting the entry condition (WVF <= LongEntryMark)
    if (ShowEntrySignal && wvfSeries[0] <= LongEntryMark)
    {
        // MODIFIED: Different reset logic based on whether exit signals are shown
        bool canGenerateNewEntrySignal = false;
        
        if (ShowExitSignal)
        {
            // When exit signals enabled: Reset based on exit condition (LongExitMark)
            canGenerateNewEntrySignal = !hasGeneratedEntrySignal || 
                                       (hasGeneratedExitSignal && lastExitSignalBar > lastEntrySignalBar);
        }
        else
        {
            // When exit signals disabled: Reset based on bearish bars
            canGenerateNewEntrySignal = !hasGeneratedEntrySignal || 
                                       (lastBearishBar > lastEntrySignalBar);
        }
        
        // Check if we can generate a new entry signal
        if (canGenerateNewEntrySignal)
        {
            // Store the bar where the condition was first met
            if (entryConditionBarIndex == -1)
            {
                entryConditionBarIndex = CurrentBar;
            }
            
            // Check if we've reached the confirmation bar
            if (CurrentBar >= entryConditionBarIndex + ConfirmationBars)
            {
                // Draw the yellow up arrow for long entry with the LongOn text as identifier
                Draw.ArrowUp(this, LongOn + CurrentBar.ToString(), true, 0, 
                            Low[0] - Signal_Offset * TickSize, EntryArrowColor);
                
                // Mark that we've generated an entry signal
                hasGeneratedEntrySignal = true;
                lastEntrySignalBar = CurrentBar;
                entryConditionBarIndex = -1; // Reset the condition bar index
            }
        }
    }
    else 
    {
        // Reset entry condition bar index if we no longer meet the condition
        entryConditionBarIndex = -1;
    }
    
    // MODIFIED: Exit Signal Logic based on mode
    if (ShowExitSignal)
    {
        // Mode 1: Use Long Exit Mark when exit signals are enabled
        if (isExitConditionMet)
        {
            if (hasGeneratedEntrySignal && CurrentBar > lastEntrySignalBar && 
                (!hasGeneratedExitSignal || lastEntrySignalBar > lastExitSignalBar))
            {
                // Store the bar where the exit condition was first met
                if (exitConditionBarIndex == -1)
                {
                    exitConditionBarIndex = CurrentBar;
                }
                
                // Draw white X for exit
                Draw.Text(this, LongOff + CurrentBar.ToString(), "×", 0, 
                          High[0] + (Signal_Offset * 5) * TickSize, ExitXColor);
                    
                // Mark that we've generated an exit signal
                hasGeneratedExitSignal = true;
                lastExitSignalBar = CurrentBar;
                exitConditionBarIndex = -1; // Reset the condition bar index
            }
        }
        else
        {
            // Reset exit condition bar index if we no longer meet the condition
            exitConditionBarIndex = -1;
        }
    }
    else
    {
        // Mode 2: Use bearish bars for exit when exit signals are disabled
        if (isBearishBar)
        {
            lastBearishBar = CurrentBar;
            
            // Draw exit signal on first bearish bar after an entry
            if (hasGeneratedEntrySignal && CurrentBar > lastEntrySignalBar && 
                (!hasGeneratedExitSignal || lastEntrySignalBar > lastExitSignalBar))
            {
                // Draw white X for exit 
                Draw.Text(this, LongOff + CurrentBar.ToString(), "×", 0, 
                          High[0] + (Signal_Offset * 5) * TickSize, ExitXColor);
                    
                // Mark that we've generated an exit signal
                hasGeneratedExitSignal = true;
                lastExitSignalBar = CurrentBar;
            }
        }
    }
}
        #region Properties
		[NinjaScriptProperty]
        [Display(Name = "Show Entry Signals", Order = 0, GroupName = "Signal Settings")]
        public bool ShowEntrySignal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", GroupName = "Signal Settings", Order = 1)]
        public string LongOn { get; set; }

        //[NinjaScriptProperty]
       // [Display(Name = "Short On", GroupName = "Signal Settings", Order = 2)]
       // public string ShortOn { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Long Entry Mark", GroupName = "Signal Settings", Order = 3)]
        public double LongEntryMark { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Entry Arrow Color", Description = "Color for the long entry arrow", Order = 4, GroupName = "Signal Settings")]
		public Brush EntryArrowColor { get; set; }
		
		[Browsable(false)]
		public string EntryArrowColorSerializable
		{
		    get { return Serialize.BrushToString(EntryArrowColor); }
		    set { EntryArrowColor = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
        [Display(Name = "Enter After x Bars", GroupName = "Signal Settings", Order = 5)]
        public int ConfirmationBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Exit Signals", GroupName = "Signal Settings", Order = 6,
                Description = "When enabled, uses Long Exit Mark for resetting signals instead of bearish bars")]
        public bool ShowExitSignal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Off", GroupName = "Signal Settings", Order = 7)]
        public string LongOff { get; set; }

        //[NinjaScriptProperty]
       // [Display(Name = "Short Off", GroupName = "Signal Settings", Order = 7)]
       // public string ShortOff { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Long Exit Mark", GroupName = "Signal Settings", Order = 8,
                Description = "WVF value that triggers exit signals (when Show Exit Signals is enabled)")]
        public double LongExitMark { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Exit X Color", Description = "Color for the exit X", Order = 10, GroupName = "Signal Settings")]
		public Brush ExitXColor { get; set; }
		
		[Browsable(false)]
		public string ExitXColorSerializable
		{
		    get { return Serialize.BrushToString(ExitXColor); }
		    set { ExitXColor = Serialize.StringToBrush(value); }
		}
		
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signals Offset", GroupName = "Signal Settings", Order = 11)]
        public double Signal_Offset { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SDHigh", Description = "LookBack Period Standard Deviation High", Order = 1, GroupName = "Parameters")]
        public int SDHigh { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "BBLength", Description = "Bollinger Band Length", Order = 2, GroupName = "Parameters")]
        public int BBLength { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SDUp", Description = "Bollinger Band Standard Deviation Up", Order = 3, GroupName = "Parameters")]
        public int SDUp { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "PHigh", Description = "LookBack Period Percentile High", Order = 4, GroupName = "Parameters")]
        public int PHigh { get; set; }

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "HP", Description = "Highest Percentile: 0.90 = 90%, 0.95=95%, 0.99=99%", Order = 5, GroupName = "Parameters")]
        public double HP { get; set; }

        [NinjaScriptProperty]
        [Range(1.01, double.MaxValue)]
        [Display(Name = "LP", Description = "Lowest Percentile: 1.10 = 90%, 1.05 = 95%, 1.01 = 99%", Order = 6, GroupName = "Parameters")]
        public double LP { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Highlight Fuchsia", Description = "Highlight bars in Fuchsia for simple entry signals", Order = 7, GroupName = "Bar Coloring")]
        public bool HighlightFuchsia { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Highlight White", Description = "Highlight bars in White for filtered entry signals", Order = 8, GroupName = "Bar Coloring")]
        public bool HighlightWhite { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> VixFix
        {
            get { return Values[0]; }
        }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.WilliamsVixFixV3[] cacheWilliamsVixFixV3;
		public Myindicators.WilliamsVixFixV3 WilliamsVixFixV3(bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, int confirmationBars, bool showExitSignal, string longOff, double longExitMark, Brush exitXColor, double signal_Offset, int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return WilliamsVixFixV3(Input, showEntrySignal, longOn, longEntryMark, entryArrowColor, confirmationBars, showExitSignal, longOff, longExitMark, exitXColor, signal_Offset, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}

		public Myindicators.WilliamsVixFixV3 WilliamsVixFixV3(ISeries<double> input, bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, int confirmationBars, bool showExitSignal, string longOff, double longExitMark, Brush exitXColor, double signal_Offset, int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			if (cacheWilliamsVixFixV3 != null)
				for (int idx = 0; idx < cacheWilliamsVixFixV3.Length; idx++)
					if (cacheWilliamsVixFixV3[idx] != null && cacheWilliamsVixFixV3[idx].ShowEntrySignal == showEntrySignal && cacheWilliamsVixFixV3[idx].LongOn == longOn && cacheWilliamsVixFixV3[idx].LongEntryMark == longEntryMark && cacheWilliamsVixFixV3[idx].EntryArrowColor == entryArrowColor && cacheWilliamsVixFixV3[idx].ConfirmationBars == confirmationBars && cacheWilliamsVixFixV3[idx].ShowExitSignal == showExitSignal && cacheWilliamsVixFixV3[idx].LongOff == longOff && cacheWilliamsVixFixV3[idx].LongExitMark == longExitMark && cacheWilliamsVixFixV3[idx].ExitXColor == exitXColor && cacheWilliamsVixFixV3[idx].Signal_Offset == signal_Offset && cacheWilliamsVixFixV3[idx].SDHigh == sDHigh && cacheWilliamsVixFixV3[idx].BBLength == bBLength && cacheWilliamsVixFixV3[idx].SDUp == sDUp && cacheWilliamsVixFixV3[idx].PHigh == pHigh && cacheWilliamsVixFixV3[idx].HP == hP && cacheWilliamsVixFixV3[idx].LP == lP && cacheWilliamsVixFixV3[idx].HighlightFuchsia == highlightFuchsia && cacheWilliamsVixFixV3[idx].HighlightWhite == highlightWhite && cacheWilliamsVixFixV3[idx].EqualsInput(input))
						return cacheWilliamsVixFixV3[idx];
			return CacheIndicator<Myindicators.WilliamsVixFixV3>(new Myindicators.WilliamsVixFixV3(){ ShowEntrySignal = showEntrySignal, LongOn = longOn, LongEntryMark = longEntryMark, EntryArrowColor = entryArrowColor, ConfirmationBars = confirmationBars, ShowExitSignal = showExitSignal, LongOff = longOff, LongExitMark = longExitMark, ExitXColor = exitXColor, Signal_Offset = signal_Offset, SDHigh = sDHigh, BBLength = bBLength, SDUp = sDUp, PHigh = pHigh, HP = hP, LP = lP, HighlightFuchsia = highlightFuchsia, HighlightWhite = highlightWhite }, input, ref cacheWilliamsVixFixV3);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.WilliamsVixFixV3 WilliamsVixFixV3(bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, int confirmationBars, bool showExitSignal, string longOff, double longExitMark, Brush exitXColor, double signal_Offset, int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV3(Input, showEntrySignal, longOn, longEntryMark, entryArrowColor, confirmationBars, showExitSignal, longOff, longExitMark, exitXColor, signal_Offset, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}

		public Indicators.Myindicators.WilliamsVixFixV3 WilliamsVixFixV3(ISeries<double> input , bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, int confirmationBars, bool showExitSignal, string longOff, double longExitMark, Brush exitXColor, double signal_Offset, int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV3(input, showEntrySignal, longOn, longEntryMark, entryArrowColor, confirmationBars, showExitSignal, longOff, longExitMark, exitXColor, signal_Offset, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.WilliamsVixFixV3 WilliamsVixFixV3(bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, int confirmationBars, bool showExitSignal, string longOff, double longExitMark, Brush exitXColor, double signal_Offset, int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV3(Input, showEntrySignal, longOn, longEntryMark, entryArrowColor, confirmationBars, showExitSignal, longOff, longExitMark, exitXColor, signal_Offset, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}

		public Indicators.Myindicators.WilliamsVixFixV3 WilliamsVixFixV3(ISeries<double> input , bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, int confirmationBars, bool showExitSignal, string longOff, double longExitMark, Brush exitXColor, double signal_Offset, int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV3(input, showEntrySignal, longOn, longEntryMark, entryArrowColor, confirmationBars, showExitSignal, longOff, longExitMark, exitXColor, signal_Offset, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}
	}
}

#endregion
