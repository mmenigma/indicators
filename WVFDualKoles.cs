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
	public class WVFDualKoles : Indicator
	{
        // Bottom-finding series (original WVF)
        private Series<double> wvfBottomSeries;
        private Series<bool> isFilteredEntrySeries;
        
        // Top-finding series (new addition)
        private Series<double> wvfTopSeries;
        private Series<bool> isFilteredTopSeries;
        
        // Long signal tracking
        private Series<bool> hasLongSetupMarkSeries;
        private Series<bool> longEntrySignalPlaced;
        
        // Short signal tracking
        private Series<bool> hasShortSetupMarkSeries;
        private Series<bool> shortEntrySignalPlaced;

        // Declare variables for price action strength and lookback periods
        private int str = 3; // Entry price action strength
        private int ltLB = 40; // Long-term lookback period
        private int mtLB = 14; // Medium-term lookback period

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Williams Vix Fix Dual with both top and bottom identification with signals for Koles";
                Name = "WVFDualKoles";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Inputs for WVF calculation - Bottom detection parameters
                BottomPeriod = 22;
                BottomBBLength = 20;
                BottomBBDeviation = 2;
                BottomLookback = 50;
                BottomHighestMultiplier = 0.85;
                BottomLowestMultiplier = 1.01;
                
                // Inputs for WVF calculation - Top detection parameters
                TopPeriod = 22;
                TopBBLength = 20;
                TopBBDeviation = 2;
                TopLookback = 50;
                TopHighestMultiplier = 1.01;
                TopLowestMultiplier = 0.85;
				
				// Long Signal Settings
                ShowLongSignal = true;
                LongOn = "LongOn";
                LongSetupMark = 0.1;
				LongEntryMark = .09;
                EntryArrowColor = Brushes.Yellow;
				
				// Short Signal Settings
				ShowShortSignal = true;
				ShortOn = "ShortOn";
				ShortSetupMark = -0.1;
				ShortEntryMark = -0.09;
				ShortArrowColor = Brushes.BlueViolet;
				
				// Exit Signal Settings
				ShowExitSignal = false;
                LongOff = "LongOff";
                LongExitMark = .3;
                ShortOff = "ShortOff";
				ShortExitMark = - 0.3;
                ExitOColor = Brushes.DimGray;

                Signal_Offset = 5;

                // Inputs for bar coloring
                HighlightWhite = true;
                HighlightYellow = true;

                // Add a plot for the Bottom WVF histogram (lime/gray)
                AddPlot(new Stroke(Brushes.Firebrick, 2), PlotStyle.Bar, "BottomVixFix");
                
                // Add a plot for the Top WVF histogram (red/darkred)
                AddPlot(new Stroke(Brushes.Lime, 2), PlotStyle.Bar, "TopVixFix");
                
                // Zero line for reference
                AddPlot(new Stroke(Brushes.White, 1), PlotStyle.Line, "ZeroLine");
            }
            else if (State == State.Configure)
            {
                // Initialize series for tracking filtered entries only
                isFilteredEntrySeries = new Series<bool>(this);
                isFilteredTopSeries = new Series<bool>(this);
                
                // Initialize series for tracking long signals
                hasLongSetupMarkSeries = new Series<bool>(this);
                longEntrySignalPlaced = new Series<bool>(this);
                
                // Initialize series for tracking short signals
                hasShortSetupMarkSeries = new Series<bool>(this);
                shortEntrySignalPlaced = new Series<bool>(this);
            }
            else if (State == State.DataLoaded)
            {
                // Initialize the series
                wvfBottomSeries = new Series<double>(this);
                wvfTopSeries = new Series<double>(this);
            }
        }

		protected override void OnBarUpdate()
		{
		    // Ensure we have enough bars for both indicators
		    if (CurrentBar < Math.Max(BottomLookback, TopLookback) || 
		        CurrentBar < Math.Max(BottomPeriod, TopPeriod))
		        return;
		
		    // Set the zero line plot value
		    Values[2][0] = 0;
		
		    // Define price action variables
		    bool isBullishBar = Close[0] > Open[0];
		    bool isBearishBar = Close[0] < Open[0];
		    bool wasLastBarBullish = CurrentBar > 0 && Close[1] > Open[1];
		    bool wasLastBarBearish = CurrentBar > 0 && Close[1] < Open[1];
		    bool trendChangeToBearish = wasLastBarBullish && isBearishBar;
		    bool trendChangeToBullish = wasLastBarBearish && isBullishBar;
		
		    // ==================== BOTTOM IDENTIFICATION (Original WVF) ====================
		    
		    // Calculate Bottom WVF (original formula)
		    wvfBottomSeries[0] = ((MAX(Close, BottomPeriod)[0] - Low[0]) / MAX(Close, BottomPeriod)[0]) * 100;
		
		    // Calculate Bollinger Bands and Range High/Low for Bottom WVF
		    double bottomSDev = BottomBBDeviation * StdDev(wvfBottomSeries, BottomBBLength)[0];
		    double bottomMidLine = SMA(wvfBottomSeries, BottomBBLength)[0];
		    double bottomUpperBand = bottomMidLine + bottomSDev;
		    double bottomRangeHigh = BottomHighestMultiplier * MAX(wvfBottomSeries, BottomLookback)[0];
		
		    // Store whether Bottom WVF is above upper band/range high
		    bool isBottomWvfHigh = wvfBottomSeries[0] >= bottomUpperBand || wvfBottomSeries[0] >= bottomRangeHigh;
		    bool wasBottomWvfHigh = CurrentBar > 0 && (wvfBottomSeries[1] >= bottomUpperBand || wvfBottomSeries[1] >= bottomRangeHigh);
		
		    // Set the plot value for the Bottom WVF histogram
		    Values[0][0] = -wvfBottomSeries[0];
		
		    // ==================== SHORT SETUP MARK DETECTION ====================
		
		    // Check if BottomVixFix has crossed the Short Setup Mark
		    bool isShortSetupMark = (wvfBottomSeries[0] >= Math.Abs(ShortSetupMark)) && 
		                            (CurrentBar == 0 || wvfBottomSeries[1] < Math.Abs(ShortSetupMark));
		
		    // Apply color to the Bottom WVF histogram for setup mark
		    if (isShortSetupMark)
		    {
		        PlotBrushes[0][0] = Brushes.Cyan;
		    }
		    else
		    {
		        // Use the original coloring logic
		        PlotBrushes[0][0] = isBottomWvfHigh ? Brushes.Firebrick : Brushes.Gray;
		    }
		
		    // ==================== TOP IDENTIFICATION (New WVF Top formula) ====================
		    
		    // Calculate Top WVF (inverted formula to match Pine script)
		    wvfTopSeries[0] = ((MIN(Close, TopPeriod)[0] - High[0]) / MIN(Close, TopPeriod)[0]) * 100;
		    
		    // Calculate Bollinger Bands and Range High/Low for Top WVF
		    double topSDev = TopBBDeviation * StdDev(wvfTopSeries, TopBBLength)[0];
		    double topMidLine = SMA(wvfTopSeries, TopBBLength)[0];
		    double topLowerBand = topMidLine - topSDev;
		    double topRangeLow = TopLowestMultiplier * MIN(wvfTopSeries, TopLookback)[0];
		    
		    // Store whether Top WVF is below lower band/range low (inverted from bottom logic)
		    bool isTopWvfHigh = wvfTopSeries[0] <= topLowerBand || wvfTopSeries[0] <= topRangeLow;
		    bool wasTopWvfHigh = CurrentBar > 0 && (wvfTopSeries[1] <= topLowerBand || wvfTopSeries[1] <= topRangeLow);
		    
		    // Set the plot value for the Top WVF histogram
		    Values[1][0] = -wvfTopSeries[0]; // Already negative from the formula
		    
		    // ==================== LONG SETUP MARK DETECTION ====================
		    
		    // Check if TopVixFix has crossed the Long Setup Mark
		    bool isLongSetupMark = (-wvfTopSeries[0] >= LongSetupMark) && 
		                          (CurrentBar == 0 || -wvfTopSeries[1] < LongSetupMark);
		    
		    // Apply color to the Top WVF histogram
		    if (isLongSetupMark)
		    {
		        PlotBrushes[1][0] = Brushes.DarkOrange;
		    }
		    else
		    {
		        PlotBrushes[1][0] = isTopWvfHigh ? Brushes.Lime : Brushes.SlateGray;
		    }
		    
		    // ==================== SIGNAL TRACKING LOGIC ====================
		    
		    // Initialize tracking variables for the first bar or carry forward state
		    if (CurrentBar == 0)
		    {
		        hasLongSetupMarkSeries[0] = false;
		        longEntrySignalPlaced[0] = false;
		        hasShortSetupMarkSeries[0] = false;
		        shortEntrySignalPlaced[0] = false;
		    }
		    else
		    {
		        // Default behavior is to carry forward the state from the previous bar
		        hasLongSetupMarkSeries[0] = hasLongSetupMarkSeries[1];
		        longEntrySignalPlaced[0] = longEntrySignalPlaced[1];
		        hasShortSetupMarkSeries[0] = hasShortSetupMarkSeries[1];
		        shortEntrySignalPlaced[0] = shortEntrySignalPlaced[1];
		    }
		    
		    // Update long setup tracking
		    if (isLongSetupMark)
		    {
		        hasLongSetupMarkSeries[0] = true;
		        longEntrySignalPlaced[0] = false;
		    }
		    
		    // Update short setup tracking
		    if (isShortSetupMark)
		    {
		        hasShortSetupMarkSeries[0] = true;
		        shortEntrySignalPlaced[0] = false;
		    }
		    
		    // ==================== ENTRY SIGNAL LOGIC ====================
		    
		    // Check for entry signal conditions
		    bool isLongEntryCrossed = (-wvfTopSeries[0] <= LongEntryMark) && 
		                             (CurrentBar > 0 && -wvfTopSeries[1] > LongEntryMark);
		    
		    bool isShortEntryCrossed = (wvfBottomSeries[0] <= Math.Abs(ShortEntryMark)) && 
		                               (CurrentBar > 0 && wvfBottomSeries[1] > Math.Abs(ShortEntryMark));
		    
		    // Long entry signal
		    if (ShowLongSignal && 
		        hasLongSetupMarkSeries[0] && 
		        !longEntrySignalPlaced[0] && 
		        isLongEntryCrossed &&
		        isBullishBar)
		    {
		        // Draw the yellow up arrow for long entry
		        Draw.ArrowUp(this, LongOn + CurrentBar.ToString(), true, 0, 
		                     Low[0] - Signal_Offset * TickSize, EntryArrowColor);
		        
		        // Mark that we've placed a signal
		        longEntrySignalPlaced[0] = true;
		    }
		    
		    // Short entry signal
		    if (ShowShortSignal && 
		        hasShortSetupMarkSeries[0] && 
		        !shortEntrySignalPlaced[0] && 
		        isShortEntryCrossed &&
		        isBearishBar)
		    {
		        // Draw the down arrow for short entry
		        Draw.ArrowDown(this, ShortOn + CurrentBar.ToString(), true, 0, 
		                      High[0] + Signal_Offset * TickSize, ShortArrowColor);
		        
		        // Mark that we've placed a signal
		        shortEntrySignalPlaced[0] = true;
		    }
		    
		    // ==================== EXIT SIGNAL LOGIC ====================
		    
		    // Only process exit signals if they're enabled
		    if (ShowExitSignal)
		    {
		        // LONG EXIT SIGNAL - First bearish bar that ends a bullish trend
		        if ((longEntrySignalPlaced[0] || (CurrentBar > 0 && longEntrySignalPlaced[1])) && trendChangeToBearish)
		        {
		            // Draw the 'o' for long exit
		            Draw.Text(this, LongOff + CurrentBar.ToString(), "o", 0, 
		                     High[0] + (Signal_Offset * 1) * TickSize, ExitOColor);
		        }
		        
		        // SHORT EXIT SIGNAL - First bullish bar that ends a bearish trend
		        if ((shortEntrySignalPlaced[0] || (CurrentBar > 0 && shortEntrySignalPlaced[1])) && trendChangeToBullish)
		        {
		            // Draw the 'o' for short exit
		            Draw.Text(this, ShortOff + CurrentBar.ToString(), "o", 0, 
		                     Low[0] - (Signal_Offset * 1) * TickSize, ExitOColor);
		        }
		    }
		    
		    // ==================== RESET SIGNAL LOGIC ====================
		    
		    // Reset long tracking when trend changes from bullish to bearish
		    if (trendChangeToBearish && hasLongSetupMarkSeries[0])
		    {
		        hasLongSetupMarkSeries[0] = false;
		        longEntrySignalPlaced[0] = false;
		    }
		    
		    // Reset short tracking when trend changes from bearish to bullish
		    if (trendChangeToBullish && hasShortSetupMarkSeries[0])
		    {
		        hasShortSetupMarkSeries[0] = false;
		        shortEntrySignalPlaced[0] = false;
		    }
		    
		    // ==================== BAR COLORING LOGIC ====================
		    
		    // Reset filtered entry flags for this bar
		    isFilteredEntrySeries[0] = false;
		    isFilteredTopSeries[0] = false;
		    
		    // Price action criteria for filtered bottom entries
		    bool upRange = Low[0] > Low[1] && Close[0] > High[1];
		    bool strengthCriteria = Close[0] > Close[str] && (Close[0] < Close[ltLB] || Close[0] < Close[mtLB]);
		    
		    // Price action criteria for filtered top entries - reversed from bottom entries
		    bool downRange = High[0] < High[1] && Close[0] < Low[1];
		    bool topStrengthCriteria = Close[0] < Close[str] && (Close[0] > Close[ltLB] || Close[0] > Close[mtLB]);
		    
		    // Filtered Entry - White
		    bool isFilteredEntry = false;
		    if (wasBottomWvfHigh && !isBottomWvfHigh && upRange && strengthCriteria)
		    {
		        isFilteredEntry = true;
		    }
		    
		    // Filtered Top Entry - Yellow
		    bool isFilteredTopEntry = false;
		    if (wasTopWvfHigh && !isTopWvfHigh && downRange && topStrengthCriteria)
		    {
		        isFilteredTopEntry = true;
		    }
		
		    // Apply colors - Bottom entries (White) and Top entries (Yellow)
		    if (HighlightWhite && isFilteredEntry && !isFilteredEntrySeries[1])
		    {
		        BarBrushes[0] = Brushes.White;
		        isFilteredEntrySeries[0] = true;
		    }
		    else if (HighlightYellow && isFilteredTopEntry && !isFilteredTopSeries[1])
		    {
		        BarBrushes[0] = Brushes.Yellow;
		        isFilteredTopSeries[0] = true;
		    }
		    else
		    {
		        BarBrushes[0] = null; // Default chart color
		    }
		}

        #region Properties
		// Signal settings
        [NinjaScriptProperty]
        [Display(Name = "Show Long Signals", Order = 1, GroupName = "Entry Signal Settings")]
        public bool ShowLongSignal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", GroupName = "Entry Signal Settings", Order = 2)]
        public string LongOn { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Long Setup Mark", GroupName = "Entry Signal Settings", Order = 3,
		Description = "TopVixFix value that triggers setup for long signals")]
		public double LongSetupMark { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Long Entry Mark", GroupName = "Entry Signal Settings", Order = 4,
		Description = "TopVixFix value that triggers long entry signals")]
		public double LongEntryMark { get; set; }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Arrow Color", Description = "Color for the long entry arrow", Order = 5, GroupName = "Entry Signal Settings")]
        public Brush EntryArrowColor { get; set; }
        
        [Browsable(false)]
        public string EntryArrowColorSerializable
        {
            get { return Serialize.BrushToString(EntryArrowColor); }
            set { EntryArrowColor = Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
        [Display(Name = "Show Short Signals", Order = 6, GroupName = "Entry Signal Settings")]
        public bool ShowShortSignal { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short On", GroupName = "Entry Signal Settings", Order = 7)]
		public string ShortOn { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short Setup Mark", GroupName = "Entry Signal Settings", Order = 8,
		Description = "TopVixFix value that triggers long entry signals")]
		public double ShortSetupMark { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short Entry Mark", GroupName = "Entry Signal Settings", Order = 9)]
		public double ShortEntryMark { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Short Arrow Color", Description = "Color for the short entry arrow", Order = 10, GroupName = "Entry Signal Settings")]
		public Brush ShortArrowColor { get; set; }
		
		[Browsable(false)]
		public string ShortArrowColorSerializable
		{
		    get { return Serialize.BrushToString(ShortArrowColor); }
		    set { ShortArrowColor = Serialize.StringToBrush(value); }
		}
		
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signals Offset", GroupName = "Entry Signal Settings", Order = 11)]
        public double Signal_Offset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Exit Signals", GroupName = "Exit Signal Settings", Order = 12,
        Description = "When enabled, shows Exit Mark set by user")]
        public bool ShowExitSignal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Off", GroupName = "Exit Signal Settings", Order = 13)]
        public string LongOff { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Long Exit Mark", GroupName = "Exit Signal Settings", Order = 14,
         Description = "WVF value that triggers exit signals")]
        public double LongExitMark { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short Off", GroupName = "Exit Signal Settings", Order = 15)]
		public string ShortOff { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short Exit Mark", GroupName = "Exit Signal Settings", Order = 16,
		Description = "WVF value that triggers short exit signals")]
		public double ShortExitMark { get; set; }
        
        [Browsable(false)]
        public string ExitOColorSerializable
        {
            get { return Serialize.BrushToString(ExitOColor); }
            set { ExitOColor = Serialize.StringToBrush(value); }
        }
		    
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Exit 'o' Color", Description = "Color for the exit o", Order = 17, GroupName = "Exit Signal Settings")]
        public Brush ExitOColor { get; set; }
		
        // Bottom WVF parameters
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Bottom Period", Description = "Lookback Period for Bottom WVF", Order = 1, GroupName = "Bottom Detection Parameters")]
        public int BottomPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Bottom BB Length", Description = "Bollinger Band Length for Bottom WVF", Order = 2, GroupName = "Bottom Detection Parameters")]
        public int BottomBBLength { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "Bottom BB Deviation", Description = "Bollinger Band Standard Deviation for Bottom WVF", Order = 3, GroupName = "Bottom Detection Parameters")]
        public double BottomBBDeviation { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Bottom Lookback", Description = "Lookback Period for Bottom Percentile", Order = 4, GroupName = "Bottom Detection Parameters")]
        public int BottomLookback { get; set; }

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Bottom Highest Multiplier", Description = "Highest Percentile Multiplier: 0.85 = 85%", Order = 5, GroupName = "Bottom Detection Parameters")]
        public double BottomHighestMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(1.01, double.MaxValue)]
        [Display(Name = "Bottom Lowest Multiplier", Description = "Lowest Percentile Multiplier: 1.01 = 99%", Order = 6, GroupName = "Bottom Detection Parameters")]
        public double BottomLowestMultiplier { get; set; }
        
        // Top WVF parameters
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Top Period", Description = "Lookback Period for Top WVF", Order = 1, GroupName = "Top Detection Parameters")]
        public int TopPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Top BB Length", Description = "Bollinger Band Length for Top WVF", Order = 2, GroupName = "Top Detection Parameters")]
        public int TopBBLength { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "Top BB Deviation", Description = "Bollinger Band Standard Deviation for Top WVF", Order = 3, GroupName = "Top Detection Parameters")]
        public double TopBBDeviation { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Top Lookback", Description = "Lookback Period for Top Percentile", Order = 4, GroupName = "Top Detection Parameters")]
        public int TopLookback { get; set; }

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Top Highest Multiplier", Description = "Highest Percentile Multiplier for Tops: 1.01 = 99%", Order = 5, GroupName = "Top Detection Parameters")]
        public double TopHighestMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Top Lowest Multiplier", Description = "Lowest Percentile Multiplier for Tops: 0.85 = 85%", Order = 6, GroupName = "Top Detection Parameters")]
        public double TopLowestMultiplier { get; set; }

        // Bar coloring parameters
        [NinjaScriptProperty]
        [Display(Name = "Highlight White", Description = "Highlight bars in White for filtered bottom entry signals", Order = 1, GroupName = "Bar Coloring")]
        public bool HighlightWhite { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Highlight Yellow", Description = "Highlight bars in Yellow for filtered top entry signals", Order = 2, GroupName = "Bar Coloring")]
        public bool HighlightYellow { get; set; }

        // Series access properties
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> BottomVixFix
        {
            get { return Values[0]; }
        }
        
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> TopVixFix
        {
            get { return Values[1]; }
        }
        
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> ZeroLine
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
		private Myindicators.WVFDualKoles[] cacheWVFDualKoles;
		public Myindicators.WVFDualKoles WVFDualKoles(bool showLongSignal, string longOn, double longSetupMark, double longEntryMark, Brush entryArrowColor, bool showShortSignal, string shortOn, double shortSetupMark, double shortEntryMark, Brush shortArrowColor, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor, int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightWhite, bool highlightYellow)
		{
			return WVFDualKoles(Input, showLongSignal, longOn, longSetupMark, longEntryMark, entryArrowColor, showShortSignal, shortOn, shortSetupMark, shortEntryMark, shortArrowColor, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightWhite, highlightYellow);
		}

		public Myindicators.WVFDualKoles WVFDualKoles(ISeries<double> input, bool showLongSignal, string longOn, double longSetupMark, double longEntryMark, Brush entryArrowColor, bool showShortSignal, string shortOn, double shortSetupMark, double shortEntryMark, Brush shortArrowColor, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor, int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightWhite, bool highlightYellow)
		{
			if (cacheWVFDualKoles != null)
				for (int idx = 0; idx < cacheWVFDualKoles.Length; idx++)
					if (cacheWVFDualKoles[idx] != null && cacheWVFDualKoles[idx].ShowLongSignal == showLongSignal && cacheWVFDualKoles[idx].LongOn == longOn && cacheWVFDualKoles[idx].LongSetupMark == longSetupMark && cacheWVFDualKoles[idx].LongEntryMark == longEntryMark && cacheWVFDualKoles[idx].EntryArrowColor == entryArrowColor && cacheWVFDualKoles[idx].ShowShortSignal == showShortSignal && cacheWVFDualKoles[idx].ShortOn == shortOn && cacheWVFDualKoles[idx].ShortSetupMark == shortSetupMark && cacheWVFDualKoles[idx].ShortEntryMark == shortEntryMark && cacheWVFDualKoles[idx].ShortArrowColor == shortArrowColor && cacheWVFDualKoles[idx].Signal_Offset == signal_Offset && cacheWVFDualKoles[idx].ShowExitSignal == showExitSignal && cacheWVFDualKoles[idx].LongOff == longOff && cacheWVFDualKoles[idx].LongExitMark == longExitMark && cacheWVFDualKoles[idx].ShortOff == shortOff && cacheWVFDualKoles[idx].ShortExitMark == shortExitMark && cacheWVFDualKoles[idx].ExitOColor == exitOColor && cacheWVFDualKoles[idx].BottomPeriod == bottomPeriod && cacheWVFDualKoles[idx].BottomBBLength == bottomBBLength && cacheWVFDualKoles[idx].BottomBBDeviation == bottomBBDeviation && cacheWVFDualKoles[idx].BottomLookback == bottomLookback && cacheWVFDualKoles[idx].BottomHighestMultiplier == bottomHighestMultiplier && cacheWVFDualKoles[idx].BottomLowestMultiplier == bottomLowestMultiplier && cacheWVFDualKoles[idx].TopPeriod == topPeriod && cacheWVFDualKoles[idx].TopBBLength == topBBLength && cacheWVFDualKoles[idx].TopBBDeviation == topBBDeviation && cacheWVFDualKoles[idx].TopLookback == topLookback && cacheWVFDualKoles[idx].TopHighestMultiplier == topHighestMultiplier && cacheWVFDualKoles[idx].TopLowestMultiplier == topLowestMultiplier && cacheWVFDualKoles[idx].HighlightWhite == highlightWhite && cacheWVFDualKoles[idx].HighlightYellow == highlightYellow && cacheWVFDualKoles[idx].EqualsInput(input))
						return cacheWVFDualKoles[idx];
			return CacheIndicator<Myindicators.WVFDualKoles>(new Myindicators.WVFDualKoles(){ ShowLongSignal = showLongSignal, LongOn = longOn, LongSetupMark = longSetupMark, LongEntryMark = longEntryMark, EntryArrowColor = entryArrowColor, ShowShortSignal = showShortSignal, ShortOn = shortOn, ShortSetupMark = shortSetupMark, ShortEntryMark = shortEntryMark, ShortArrowColor = shortArrowColor, Signal_Offset = signal_Offset, ShowExitSignal = showExitSignal, LongOff = longOff, LongExitMark = longExitMark, ShortOff = shortOff, ShortExitMark = shortExitMark, ExitOColor = exitOColor, BottomPeriod = bottomPeriod, BottomBBLength = bottomBBLength, BottomBBDeviation = bottomBBDeviation, BottomLookback = bottomLookback, BottomHighestMultiplier = bottomHighestMultiplier, BottomLowestMultiplier = bottomLowestMultiplier, TopPeriod = topPeriod, TopBBLength = topBBLength, TopBBDeviation = topBBDeviation, TopLookback = topLookback, TopHighestMultiplier = topHighestMultiplier, TopLowestMultiplier = topLowestMultiplier, HighlightWhite = highlightWhite, HighlightYellow = highlightYellow }, input, ref cacheWVFDualKoles);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.WVFDualKoles WVFDualKoles(bool showLongSignal, string longOn, double longSetupMark, double longEntryMark, Brush entryArrowColor, bool showShortSignal, string shortOn, double shortSetupMark, double shortEntryMark, Brush shortArrowColor, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor, int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightWhite, bool highlightYellow)
		{
			return indicator.WVFDualKoles(Input, showLongSignal, longOn, longSetupMark, longEntryMark, entryArrowColor, showShortSignal, shortOn, shortSetupMark, shortEntryMark, shortArrowColor, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightWhite, highlightYellow);
		}

		public Indicators.Myindicators.WVFDualKoles WVFDualKoles(ISeries<double> input , bool showLongSignal, string longOn, double longSetupMark, double longEntryMark, Brush entryArrowColor, bool showShortSignal, string shortOn, double shortSetupMark, double shortEntryMark, Brush shortArrowColor, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor, int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightWhite, bool highlightYellow)
		{
			return indicator.WVFDualKoles(input, showLongSignal, longOn, longSetupMark, longEntryMark, entryArrowColor, showShortSignal, shortOn, shortSetupMark, shortEntryMark, shortArrowColor, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightWhite, highlightYellow);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.WVFDualKoles WVFDualKoles(bool showLongSignal, string longOn, double longSetupMark, double longEntryMark, Brush entryArrowColor, bool showShortSignal, string shortOn, double shortSetupMark, double shortEntryMark, Brush shortArrowColor, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor, int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightWhite, bool highlightYellow)
		{
			return indicator.WVFDualKoles(Input, showLongSignal, longOn, longSetupMark, longEntryMark, entryArrowColor, showShortSignal, shortOn, shortSetupMark, shortEntryMark, shortArrowColor, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightWhite, highlightYellow);
		}

		public Indicators.Myindicators.WVFDualKoles WVFDualKoles(ISeries<double> input , bool showLongSignal, string longOn, double longSetupMark, double longEntryMark, Brush entryArrowColor, bool showShortSignal, string shortOn, double shortSetupMark, double shortEntryMark, Brush shortArrowColor, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor, int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightWhite, bool highlightYellow)
		{
			return indicator.WVFDualKoles(input, showLongSignal, longOn, longSetupMark, longEntryMark, entryArrowColor, showShortSignal, shortOn, shortSetupMark, shortEntryMark, shortArrowColor, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightWhite, highlightYellow);
		}
	}
}

#endregion
