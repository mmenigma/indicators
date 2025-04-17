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
	public class WVFDualv3 : Indicator
	{
        // Bottom-finding series (original WVF)
        private Series<double> wvfBottomSeries;
        private Series<bool> isAboveUpperBandSeries;
        private Series<bool> isBelowLowerBandSeries;
        private Series<bool> isSimpleEntrySeries;
        private Series<bool> isFilteredEntrySeries;
        
        // Top-finding series (new addition)
        private Series<double> wvfTopSeries;
        private Series<bool> isAboveTopsUpperBandSeries;
        private Series<bool> isBelowTopsLowerBandSeries;
        private Series<bool> isSimpleTopSeries;
        private Series<bool> isFilteredTopSeries;

        // Declare variables for price action strength and lookback periods
        private int str = 3; // Entry price action strength
        private int ltLB = 40; // Long-term lookback period
        private int mtLB = 14; // Medium-term lookback period
		
		// Variables for tracking long trade signals
		private bool hasGeneratedEntrySignal = false;
		private bool hasGeneratedExitSignal = false;
		private int lastBearishBar = -1;
		private int lastEntrySignalBar = -1;
		private int lastExitSignalBar = -1;
		private int entryConditionBarIndex = -1; // Tracks the bar where entry condition was first met
        private int exitConditionBarIndex = -1; // Tracks the bar where exit condition was first met
		
		// Variables for tracking short trade signals
		private bool hasGeneratedShortEntrySignal = false;
		private bool hasGeneratedShortExitSignal = false;
		private int lastBullishBar = -1;
		private int lastShortEntrySignalBar = -1;
		private int lastShortExitSignalBar = -1;
		private int shortEntryConditionBarIndex = -1; // Tracks the bar where short entry condition was first met
		private int shortExitConditionBarIndex = -1; // Tracks the bar where short exit condition was first met

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Williams Vix Fix Dual with both top and bottom identification";
                Name = "WVFDualv3";
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

                // Inputs for bar coloring
                HighlightFuchsia = true;
                HighlightWhite = true;
                HighlightCyan = true;
                HighlightYellow = true;

                // Add a plot for the Bottom WVF histogram (lime/gray)
                AddPlot(new Stroke(Brushes.Firebrick, 2), PlotStyle.Bar, "BottomVixFix");
                
                // Add a plot for the Top WVF histogram (red/darkred)
                AddPlot(new Stroke(Brushes.Lime, 2), PlotStyle.Bar, "TopVixFix");
                
                // Zero line for reference
                AddPlot(new Stroke(Brushes.White, 1), PlotStyle.Line, "ZeroLine");
                
                // Signal Settings
                ShowEntrySignal = true;
                LongOn = "LongOn";
                LongEntryMark = .1;
                ConfirmationBars = 0;
                EntryArrowColor = Brushes.Yellow;

                ShowExitSignal = false;
                LongOff = "LongOff";
                LongExitMark = .3;
                ExitOColor = Brushes.DimGray;
				
				// Short Signal Settings
				ShortOn = "ShortOn";
				ShortEntryMark = -0.1;
				ShortArrowColor = Brushes.BlueViolet;
				ShortOff = "ShortOff";
				ShortExitMark = - 0.3;

                Signal_Offset = 5;
            }
            else if (State == State.Configure)
            {
                // Initialize series for tracking bottom identification
                isAboveUpperBandSeries = new Series<bool>(this);
                isBelowLowerBandSeries = new Series<bool>(this);
                isSimpleEntrySeries = new Series<bool>(this);
                isFilteredEntrySeries = new Series<bool>(this);
                
                // Initialize series for tracking top identification
                isAboveTopsUpperBandSeries = new Series<bool>(this);
                isBelowTopsLowerBandSeries = new Series<bool>(this);
                isSimpleTopSeries = new Series<bool>(this);
                isFilteredTopSeries = new Series<bool>(this);
            }
            else if (State == State.DataLoaded)
            {
                // Initialize the series
                wvfBottomSeries = new Series<double>(this);
                wvfTopSeries = new Series<double>(this);
                
                // Initialize long signal tracking variables
                hasGeneratedEntrySignal = false;
                hasGeneratedExitSignal = false;
                lastBearishBar = -1;
                lastEntrySignalBar = -1;
                lastExitSignalBar = -1;
                entryConditionBarIndex = -1;
                exitConditionBarIndex = -1;
				
				// Initialize short signal tracking variables
				hasGeneratedShortEntrySignal = false;
				hasGeneratedShortExitSignal = false;
				lastBullishBar = -1;
				lastShortEntrySignalBar = -1;
				lastShortExitSignalBar = -1;
				shortEntryConditionBarIndex = -1;
				shortExitConditionBarIndex = -1;
            }
        }

        protected override void OnBarUpdate()
        {
			   bool isBullishBar = Close[0] > Open[0];
               bool isBearishBar = Close[0] < Open[0];
			
            // Ensure we have enough bars for both indicators
            if (CurrentBar < Math.Max(BottomLookback, TopLookback) || 
                CurrentBar < Math.Max(BottomPeriod, TopPeriod))
                return;

            // Set the zero line plot value
            Values[2][0] = 0;

            // ==================== BOTTOM IDENTIFICATION (Original WVF) ====================
            
            // Calculate Bottom WVF (original formula)
            wvfBottomSeries[0] = ((MAX(Close, BottomPeriod)[0] - Low[0]) / MAX(Close, BottomPeriod)[0]) * 100;

            // Calculate Bollinger Bands and Range High/Low for Bottom WVF
            double bottomSDev = BottomBBDeviation * StdDev(wvfBottomSeries, BottomBBLength)[0];
            double bottomMidLine = SMA(wvfBottomSeries, BottomBBLength)[0];
            double bottomLowerBand = bottomMidLine - bottomSDev;
            double bottomUpperBand = bottomMidLine + bottomSDev;
            double bottomRangeHigh = BottomHighestMultiplier * MAX(wvfBottomSeries, BottomLookback)[0];
            double bottomRangeLow = BottomLowestMultiplier * MIN(wvfBottomSeries, BottomLookback)[0];

            // Store whether Bottom WVF is above upper band/range high
            bool isBottomWvfHigh = wvfBottomSeries[0] >= bottomUpperBand || wvfBottomSeries[0] >= bottomRangeHigh;
            bool wasBottomWvfHigh = CurrentBar > 0 && (wvfBottomSeries[1] >= bottomUpperBand || wvfBottomSeries[1] >= bottomRangeHigh);
            
            // Store current conditions in series for next bar
            isAboveUpperBandSeries[0] = isBottomWvfHigh;
            isBelowLowerBandSeries[0] = wvfBottomSeries[0] <= bottomLowerBand || wvfBottomSeries[0] <= bottomRangeLow;

            // Set the plot value for the Bottom WVF histogram
            Values[0][0] = -wvfBottomSeries[0];

            // Apply color to the Bottom WVF histogram
            PlotBrushes[0][0] = isBottomWvfHigh ? Brushes.Firebrick : Brushes.Gray;

            // ==================== TOP IDENTIFICATION (New WVF Top formula) ====================
            
			// Calculate Top WVF (inverted formula to match Pine script)
			wvfTopSeries[0] = ((MIN(Close, TopPeriod)[0] - High[0]) / MIN(Close, TopPeriod)[0]) * 100;
			
			// Calculate Bollinger Bands and Range High/Low for Top WVF
			double topSDev = TopBBDeviation * StdDev(wvfTopSeries, TopBBLength)[0];
			double topMidLine = SMA(wvfTopSeries, TopBBLength)[0];
			double topLowerBand = topMidLine - topSDev;
			double topUpperBand = topMidLine + topSDev;
			double topRangeHigh = TopHighestMultiplier * MAX(wvfTopSeries, TopLookback)[0];
			double topRangeLow = TopLowestMultiplier * MIN(wvfTopSeries, TopLookback)[0];
			
			// Store whether Top WVF is below lower band/range low (inverted from bottom logic)
			bool isTopWvfHigh = wvfTopSeries[0] <= topLowerBand || wvfTopSeries[0] <= topRangeLow;
			bool wasTopWvfHigh = CurrentBar > 0 && (wvfTopSeries[1] <= topLowerBand || wvfTopSeries[1] <= topRangeLow);
			
			// Store current conditions in series for next bar
			isAboveTopsUpperBandSeries[0] = wvfTopSeries[0] >= topUpperBand || wvfTopSeries[0] >= topRangeHigh;
			isBelowTopsLowerBandSeries[0] = isTopWvfHigh;
			
			// Set the plot value for the Top WVF histogram
			Values[1][0] = -wvfTopSeries[0]; // Already negative from the formula
			
			// Apply color to the Top WVF histogram
			PlotBrushes[1][0] = isTopWvfHigh ? Brushes.Lime : Brushes.SlateGray;

            // ==================== ENTRY/EXIT LOGIC FOR BOTTOM WVF ====================
            
            // Reset entry flags
            isSimpleEntrySeries[0] = false;
            isFilteredEntrySeries[0] = false;
            
            // Price action criteria for bottom entries
            bool upRange = Low[0] > Low[1] && Close[0] > High[1];
            bool strengthCriteria = Close[0] > Close[str] && (Close[0] < Close[ltLB] || Close[0] < Close[mtLB]);
            
            // Simple Entry - Fuchsia (bFiltered in .pin)
            bool isSimpleEntry = false;
            if (wasBottomWvfHigh && !isBottomWvfHigh) // WVF crossed below upperBand/rangeHigh
            {
                isSimpleEntry = true;
            }
            
            // Filtered Entry - White (.pin alert3 logic)
            bool isFilteredEntry = false;
            if (wasBottomWvfHigh && !isBottomWvfHigh && upRange && strengthCriteria)
            {
                isFilteredEntry = true;
            }

            // ==================== ENTRY/EXIT LOGIC FOR TOP WVF ====================
            
            // Reset top entry flags
            isSimpleTopSeries[0] = false;
            isFilteredTopSeries[0] = false;
            
            // Price action criteria for top entries - reversed from bottom entries
            bool downRange = High[0] < High[1] && Close[0] < Low[1];
            bool topStrengthCriteria = Close[0] < Close[str] && (Close[0] > Close[ltLB] || Close[0] > Close[mtLB]);
            
            // Simple Top Entry - Cyan
            bool isSimpleTopEntry = false;
            if (wasTopWvfHigh && !isTopWvfHigh) // Top WVF crossed below upperBand/rangeHigh
            {
                isSimpleTopEntry = true;
            }
            
            // Filtered Top Entry - Yellow
            bool isFilteredTopEntry = false;
            if (wasTopWvfHigh && !isTopWvfHigh && downRange && topStrengthCriteria)
            {
                isFilteredTopEntry = true;
            }

            // ==================== BAR COLORING LOGIC ====================
            
            // Apply colors - Bottom entries (prioritize Filtered/White over Simple/Fuchsia)
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
            // Apply colors - Top entries (prioritize Filtered/Yellow over Simple/Cyan)
            else if (HighlightYellow && isFilteredTopEntry && !isFilteredTopSeries[1] && !isSimpleTopSeries[1])
            {
                BarBrushes[0] = Brushes.Yellow;
                isFilteredTopSeries[0] = true;
            }
            else if (HighlightCyan && isSimpleTopEntry && !isSimpleTopSeries[1] && !isFilteredTopSeries[1])
            {
                BarBrushes[0] = Brushes.Cyan;
                isSimpleTopSeries[0] = true;
            }
            else
            {
                BarBrushes[0] = null; // Default chart color
            }
            
            // ==================== TRADE SIGNALS LOGIC ====================
            
            // Track if we've had a bearish bar 
            isBearishBar = Close[0] < Open[0];
            
            // Check for exit condition based on WVF value
            bool isExitConditionMet = wvfBottomSeries[0] >= LongExitMark;
            
            // Long Entry Signal Logic 
            if (ShowEntrySignal && wvfBottomSeries[0] <= LongEntryMark)
            {
                // Add check for bullish bar
                isBullishBar = Close[0] > Open[0];
                
                // Check histogram pattern: current bar lower than previous 2 bars
                // and 2 bars back higher than 1 bar back
                bool histogramPattern = CurrentBar >= 3 && 
                                        wvfBottomSeries[0] < wvfBottomSeries[1] && 
                                        wvfBottomSeries[0] < wvfBottomSeries[2] && 
                                        wvfBottomSeries[2] > wvfBottomSeries[1];
                
                // Only proceed if we have a bullish bar and the histogram pattern is valid
                if (isBullishBar && histogramPattern)
                {
                    // Different reset logic based on whether exit signals are shown
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
                    // Reset entry condition bar index if not a bullish bar
                    entryConditionBarIndex = -1;
                }
            }
                
            // Exit Signal Logic based on mode
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
                        Draw.Text(this, LongOff + CurrentBar.ToString(), "o", 0, 
                                  High[0] + (Signal_Offset * 4) * TickSize, ExitOColor);
                            
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
                        Draw.Text(this, LongOff + CurrentBar.ToString(), "o", 0, 
                                  High[0] + (Signal_Offset * 4) * TickSize, ExitOColor);
                            
                        // Mark that we've generated an exit signal
                        hasGeneratedExitSignal = true;
                        lastExitSignalBar = CurrentBar;
                    }
                }
            }
		       
		// exit signal logic for short positions
		// Track if we've had a bullish bar 
		isBullishBar = Close[0] > Open[0];
		
		// Check for short exit condition based on WVF value
		bool isShortExitConditionMet = wvfTopSeries[0] <= ShortExitMark;
		
		// Short Entry Signal Logic 
		if (ShowEntrySignal && wvfTopSeries[0] >= ShortEntryMark)
		{
		    // Add check for bearish bar
		    isBearishBar = Close[0] < Open[0];
		    
		    // Check histogram pattern: current bar higher than previous 2 bars
		    // and 2 bars back lower than 1 bar back
		    bool shortHistogramPattern = CurrentBar >= 3 && 
		                               wvfTopSeries[0] > wvfTopSeries[1] && 
		                               wvfTopSeries[0] > wvfTopSeries[2] && 
		                               wvfTopSeries[2] < wvfTopSeries[1];
		    
		    // Only proceed if we have a bearish bar and the histogram pattern is valid
		    if (isBearishBar && shortHistogramPattern)
		    {
		        // Different reset logic based on whether exit signals are shown
		        bool canGenerateNewShortEntrySignal = false;
		        
		        if (ShowExitSignal)
		        {
		            // When exit signals enabled: Reset based on exit condition (ShortExitMark)
		            canGenerateNewShortEntrySignal = !hasGeneratedShortEntrySignal || 
		                                           (hasGeneratedShortExitSignal && lastShortExitSignalBar > lastShortEntrySignalBar);
		        }
		        else
		        {
		            // When exit signals disabled: Reset based on bullish bars
		            canGenerateNewShortEntrySignal = !hasGeneratedShortEntrySignal || 
		                                           (lastBullishBar > lastShortEntrySignalBar);
		        }
		        
		        // Check if we can generate a new short entry signal
		        if (canGenerateNewShortEntrySignal)
		        {
		            // Store the bar where the condition was first met
		            if (shortEntryConditionBarIndex == -1)
		            {
		                shortEntryConditionBarIndex = CurrentBar;
		            }
		            
		            // Check if we've reached the confirmation bar
		            if (CurrentBar >= shortEntryConditionBarIndex + ConfirmationBars)
		            {
		                // Draw the BlueViolet down arrow for short entry with the ShortOn text as identifier
		                Draw.ArrowDown(this, ShortOn + CurrentBar.ToString(), true, 0, 
		                              High[0] + Signal_Offset * TickSize, ShortArrowColor);
		                
		                // Mark that we've generated a short entry signal
		                hasGeneratedShortEntrySignal = true;
		                lastShortEntrySignalBar = CurrentBar;
		                shortEntryConditionBarIndex = -1; // Reset the condition bar index
		            }
		        }
		    }
		    else
		    {
		        // Reset short entry condition bar index if not a bearish bar
		        shortEntryConditionBarIndex = -1;
		    }
		}
		    
		// Short Exit Signal Logic based on mode
		if (ShowExitSignal)
		{
		    // Mode 1: Use Short Exit Mark when exit signals are enabled
		    if (isShortExitConditionMet)
		    {
		        if (hasGeneratedShortEntrySignal && CurrentBar > lastShortEntrySignalBar && 
		            (!hasGeneratedShortExitSignal || lastShortEntrySignalBar > lastShortExitSignalBar))
		        {
		            // Store the bar where the exit condition was first met
		            if (shortExitConditionBarIndex == -1)
		            {
		                shortExitConditionBarIndex = CurrentBar;
		            }
		            
		            // Draw white O for exit
		            Draw.Text(this, ShortOff + CurrentBar.ToString(), "o", 0, 
		                      Low[0] - (Signal_Offset * 3) * TickSize, ExitOColor);
		                
		            // Mark that we've generated a short exit signal
		            hasGeneratedShortExitSignal = true;
		            lastShortExitSignalBar = CurrentBar;
		            shortExitConditionBarIndex = -1; // Reset the condition bar index
		        }
		    }
		    else
		    {
		        // Reset short exit condition bar index if we no longer meet the condition
		        shortExitConditionBarIndex = -1;
		    }
		}
		else
		{
		    // Mode 2: Use bullish bars for exit when exit signals are disabled
		    if (isBullishBar)
		    {
		        lastBullishBar = CurrentBar;
		        
		        // Draw exit signal on first bullish bar after a short entry
		        if (hasGeneratedShortEntrySignal && CurrentBar > lastShortEntrySignalBar && 
		            (!hasGeneratedShortExitSignal || lastShortEntrySignalBar > lastShortExitSignalBar))
		        {
		            // Draw white O for exit 
		            Draw.Text(this, ShortOff + CurrentBar.ToString(), "o", 0, 
		                      Low[0] - (Signal_Offset * 3 ) * TickSize, ExitOColor);
		                
		            // Mark that we've generated a short exit signal
		            hasGeneratedShortExitSignal = true;
		            lastShortExitSignalBar = CurrentBar;
		        }
		    }
		  }
		}
		
		

        #region Properties
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
        [Display(Name = "Highlight Fuchsia", Description = "Highlight bars in Fuchsia for simple bottom entry signals", Order = 1, GroupName = "Bar Coloring")]
        public bool HighlightFuchsia { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Highlight White", Description = "Highlight bars in White for filtered bottom entry signals", Order = 2, GroupName = "Bar Coloring")]
        public bool HighlightWhite { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Highlight Cyan", Description = "Highlight bars in Cyan for simple top entry signals", Order = 3, GroupName = "Bar Coloring")]
        public bool HighlightCyan { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Highlight Yellow", Description = "Highlight bars in Yellow for filtered top entry signals", Order = 4, GroupName = "Bar Coloring")]
        public bool HighlightYellow { get; set; }

        // Signal settings
        [NinjaScriptProperty]
        [Display(Name = "Show Entry Signals", Order = 1, GroupName = "Entry Signal Settings")]
        public bool ShowEntrySignal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", GroupName = "Entry Signal Settings", Order = 2)]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Entry Mark", GroupName = "Entry Signal Settings", Order = 3)]
        public double LongEntryMark { get; set; }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Arrow Color", Description = "Color for the long entry arrow", Order = 4, GroupName = "Entry Signal Settings")]
        public Brush EntryArrowColor { get; set; }
        
        [Browsable(false)]
        public string EntryArrowColorSerializable
        {
            get { return Serialize.BrushToString(EntryArrowColor); }
            set { EntryArrowColor = Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
		[Display(Name = "Short On", GroupName = "Entry Signal Settings", Order = 5)]
		public string ShortOn { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short Entry Mark", GroupName = "Entry Signal Settings", Order = 6)]
		public double ShortEntryMark { get; set; }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Short Arrow Color", Description = "Color for the short entry arrow", Order = 7, GroupName = "Entry Signal Settings")]
		public Brush ShortArrowColor { get; set; }
		
		[Browsable(false)]
		public string ShortArrowColorSerializable
		{
		    get { return Serialize.BrushToString(ShortArrowColor); }
		    set { ShortArrowColor = Serialize.StringToBrush(value); }
		}
        
        [NinjaScriptProperty]
        [Display(Name = "Confirmation Bars", GroupName = "Entry Signal Settings", Order = 7)]
        public int ConfirmationBars { get; set; }
		
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signals Offset", GroupName = "Entry Signal Settings", Order = 8)]
        public double Signal_Offset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Exit Signals", GroupName = "Exit Signal Settings", Order = 9,
        Description = "When enabled, shows Exit Mark set by user")]
        public bool ShowExitSignal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Off", GroupName = "Exit Signal Settings", Order = 10)]
        public string LongOff { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Long Exit Mark", GroupName = "Exit Signal Settings", Order = 11,
         Description = "WVF value that triggers exit signals")]
        public double LongExitMark { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short Off", GroupName = "Exit Signal Settings", Order = 12)]
		public string ShortOff { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Short Exit Mark", GroupName = "Exit Signal Settings", Order = 13,
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
        [Display(Name = "Exit 'o' Color", Description = "Color for the exit o", Order = 15, GroupName = "Exit Signal Settings")]
        public Brush ExitOColor { get; set; }


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
		private Myindicators.WVFDualv3[] cacheWVFDualv3;
		public Myindicators.WVFDualv3 WVFDualv3(int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightFuchsia, bool highlightWhite, bool highlightCyan, bool highlightYellow, bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, string shortOn, double shortEntryMark, Brush shortArrowColor, int confirmationBars, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor)
		{
			return WVFDualv3(Input, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightFuchsia, highlightWhite, highlightCyan, highlightYellow, showEntrySignal, longOn, longEntryMark, entryArrowColor, shortOn, shortEntryMark, shortArrowColor, confirmationBars, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor);
		}

		public Myindicators.WVFDualv3 WVFDualv3(ISeries<double> input, int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightFuchsia, bool highlightWhite, bool highlightCyan, bool highlightYellow, bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, string shortOn, double shortEntryMark, Brush shortArrowColor, int confirmationBars, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor)
		{
			if (cacheWVFDualv3 != null)
				for (int idx = 0; idx < cacheWVFDualv3.Length; idx++)
					if (cacheWVFDualv3[idx] != null && cacheWVFDualv3[idx].BottomPeriod == bottomPeriod && cacheWVFDualv3[idx].BottomBBLength == bottomBBLength && cacheWVFDualv3[idx].BottomBBDeviation == bottomBBDeviation && cacheWVFDualv3[idx].BottomLookback == bottomLookback && cacheWVFDualv3[idx].BottomHighestMultiplier == bottomHighestMultiplier && cacheWVFDualv3[idx].BottomLowestMultiplier == bottomLowestMultiplier && cacheWVFDualv3[idx].TopPeriod == topPeriod && cacheWVFDualv3[idx].TopBBLength == topBBLength && cacheWVFDualv3[idx].TopBBDeviation == topBBDeviation && cacheWVFDualv3[idx].TopLookback == topLookback && cacheWVFDualv3[idx].TopHighestMultiplier == topHighestMultiplier && cacheWVFDualv3[idx].TopLowestMultiplier == topLowestMultiplier && cacheWVFDualv3[idx].HighlightFuchsia == highlightFuchsia && cacheWVFDualv3[idx].HighlightWhite == highlightWhite && cacheWVFDualv3[idx].HighlightCyan == highlightCyan && cacheWVFDualv3[idx].HighlightYellow == highlightYellow && cacheWVFDualv3[idx].ShowEntrySignal == showEntrySignal && cacheWVFDualv3[idx].LongOn == longOn && cacheWVFDualv3[idx].LongEntryMark == longEntryMark && cacheWVFDualv3[idx].EntryArrowColor == entryArrowColor && cacheWVFDualv3[idx].ShortOn == shortOn && cacheWVFDualv3[idx].ShortEntryMark == shortEntryMark && cacheWVFDualv3[idx].ShortArrowColor == shortArrowColor && cacheWVFDualv3[idx].ConfirmationBars == confirmationBars && cacheWVFDualv3[idx].Signal_Offset == signal_Offset && cacheWVFDualv3[idx].ShowExitSignal == showExitSignal && cacheWVFDualv3[idx].LongOff == longOff && cacheWVFDualv3[idx].LongExitMark == longExitMark && cacheWVFDualv3[idx].ShortOff == shortOff && cacheWVFDualv3[idx].ShortExitMark == shortExitMark && cacheWVFDualv3[idx].ExitOColor == exitOColor && cacheWVFDualv3[idx].EqualsInput(input))
						return cacheWVFDualv3[idx];
			return CacheIndicator<Myindicators.WVFDualv3>(new Myindicators.WVFDualv3(){ BottomPeriod = bottomPeriod, BottomBBLength = bottomBBLength, BottomBBDeviation = bottomBBDeviation, BottomLookback = bottomLookback, BottomHighestMultiplier = bottomHighestMultiplier, BottomLowestMultiplier = bottomLowestMultiplier, TopPeriod = topPeriod, TopBBLength = topBBLength, TopBBDeviation = topBBDeviation, TopLookback = topLookback, TopHighestMultiplier = topHighestMultiplier, TopLowestMultiplier = topLowestMultiplier, HighlightFuchsia = highlightFuchsia, HighlightWhite = highlightWhite, HighlightCyan = highlightCyan, HighlightYellow = highlightYellow, ShowEntrySignal = showEntrySignal, LongOn = longOn, LongEntryMark = longEntryMark, EntryArrowColor = entryArrowColor, ShortOn = shortOn, ShortEntryMark = shortEntryMark, ShortArrowColor = shortArrowColor, ConfirmationBars = confirmationBars, Signal_Offset = signal_Offset, ShowExitSignal = showExitSignal, LongOff = longOff, LongExitMark = longExitMark, ShortOff = shortOff, ShortExitMark = shortExitMark, ExitOColor = exitOColor }, input, ref cacheWVFDualv3);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.WVFDualv3 WVFDualv3(int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightFuchsia, bool highlightWhite, bool highlightCyan, bool highlightYellow, bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, string shortOn, double shortEntryMark, Brush shortArrowColor, int confirmationBars, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor)
		{
			return indicator.WVFDualv3(Input, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightFuchsia, highlightWhite, highlightCyan, highlightYellow, showEntrySignal, longOn, longEntryMark, entryArrowColor, shortOn, shortEntryMark, shortArrowColor, confirmationBars, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor);
		}

		public Indicators.Myindicators.WVFDualv3 WVFDualv3(ISeries<double> input , int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightFuchsia, bool highlightWhite, bool highlightCyan, bool highlightYellow, bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, string shortOn, double shortEntryMark, Brush shortArrowColor, int confirmationBars, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor)
		{
			return indicator.WVFDualv3(input, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightFuchsia, highlightWhite, highlightCyan, highlightYellow, showEntrySignal, longOn, longEntryMark, entryArrowColor, shortOn, shortEntryMark, shortArrowColor, confirmationBars, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.WVFDualv3 WVFDualv3(int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightFuchsia, bool highlightWhite, bool highlightCyan, bool highlightYellow, bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, string shortOn, double shortEntryMark, Brush shortArrowColor, int confirmationBars, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor)
		{
			return indicator.WVFDualv3(Input, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightFuchsia, highlightWhite, highlightCyan, highlightYellow, showEntrySignal, longOn, longEntryMark, entryArrowColor, shortOn, shortEntryMark, shortArrowColor, confirmationBars, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor);
		}

		public Indicators.Myindicators.WVFDualv3 WVFDualv3(ISeries<double> input , int bottomPeriod, int bottomBBLength, double bottomBBDeviation, int bottomLookback, double bottomHighestMultiplier, double bottomLowestMultiplier, int topPeriod, int topBBLength, double topBBDeviation, int topLookback, double topHighestMultiplier, double topLowestMultiplier, bool highlightFuchsia, bool highlightWhite, bool highlightCyan, bool highlightYellow, bool showEntrySignal, string longOn, double longEntryMark, Brush entryArrowColor, string shortOn, double shortEntryMark, Brush shortArrowColor, int confirmationBars, double signal_Offset, bool showExitSignal, string longOff, double longExitMark, string shortOff, double shortExitMark, Brush exitOColor)
		{
			return indicator.WVFDualv3(input, bottomPeriod, bottomBBLength, bottomBBDeviation, bottomLookback, bottomHighestMultiplier, bottomLowestMultiplier, topPeriod, topBBLength, topBBDeviation, topLookback, topHighestMultiplier, topLowestMultiplier, highlightFuchsia, highlightWhite, highlightCyan, highlightYellow, showEntrySignal, longOn, longEntryMark, entryArrowColor, shortOn, shortEntryMark, shortArrowColor, confirmationBars, signal_Offset, showExitSignal, longOff, longExitMark, shortOff, shortExitMark, exitOColor);
		}
	}
}

#endregion
