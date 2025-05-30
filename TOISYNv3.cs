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
using NinjaTrader.NinjaScript.Indicators;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    public class TOISYNv3 : Indicator
    {
        private bool inLongTrend = false;
        private bool inShortTrend = false;
        
        // Series for Heiken Ashi calculations
        private Series<double> haOpen;
        private Series<double> haClose;
        private Series<double> haHigh;
        private Series<double> haLow;
        private Series<double> haDotsState;  // 1 = green, -1 = red, 0 = yellow
        
        // Series for LaguerreRSI calculations
        private Series<double> _l0Series;
        private Series<double> _l1Series;
        private Series<double> _l2Series;
        private Series<double> _l3Series;
        private Series<double> lrsiValue;
        
        // Series for WAE calculations
        private Series<double> waeTrendUp;
        private Series<double> waeTrendDown;
        private Series<double> waeExplosionLine;
		
		// ZLMACD filter variables
		private ZLMAcd zlMacd;
		private Series<double> zlMacdValue;
		private Series<double> zlMacdAvg;
		private int atrPeriod = 14;
		private double zlMacdMultiplier = 0.5;
        
        // Debug plot values
        private Series<double> tfcDebug;
        private Series<double> haDotsDebug;
        private Series<double> lrsiDebug;
        private Series<double> waeDebug;
		
		// Add signal lockout variables
		private int lastLongSignalBar = -9999;
		private int lastShortSignalBar = -9999;
		private int signalLockoutPeriod = 10; // Adjust based on testing
        
        protected override void OnStateChange()
{
	    if (State == State.SetDefaults)
	    {
	        Description = @"TOISYNv3 - Predator Signals";
	        Name = "TOISYNv3";
	        Calculate = Calculate.OnBarClose;
	        IsOverlay = true;
	        DisplayInDataBox = true;
	        DrawOnPricePanel = true;
	        IsAutoScale = false;
	        
	        // Set default values for parameters
	        WAESensitivity = 150;
	        LongArrowColor = Brushes.DarkTurquoise;
	        ShortArrowColor = Brushes.Maroon;
	        ShowLabels = false;
	        
	        // Timeframe selection parameters
	        Use5Min = true;
	        Use15Min = true;
	        Use30Min = false;
	        Use1Hour = false;
	        Use4Hour = false;
	        Use1Day = false;
	        
	        // Enable/disable individual indicators
	        UseTFC = true;
	        UseHADots = true;
	        UseLRSI = true;
	        UseWAE = true;
	        
	        // Set ZLMACD default value
	        ZLMACDMultiplier = 0.5;
			UseZLMACDFilter = true;
	        
	        // Add debug details
	        ShowDebugInfo = false;
	        
	        // Add plots
	        AddPlot(Brushes.Transparent, "Signals");
	        
	        // Debug plots
	        AddPlot(Brushes.White, "TFC");
	        AddPlot(Brushes.Yellow, "HADots");
	        AddPlot(Brushes.Cyan, "LRSI");
	        AddPlot(Brushes.Magenta, "WAE");
	    }
	    else if (State == State.Configure)
	    {
	        // Initialize Heiken Ashi series
	        haOpen = new Series<double>(this);
	        haClose = new Series<double>(this);
	        haHigh = new Series<double>(this);
	        haLow = new Series<double>(this);
	        haDotsState = new Series<double>(this);
	        
	        // Initialize LaguerreRSI series
	        _l0Series = new Series<double>(this);
	        _l1Series = new Series<double>(this);
	        _l2Series = new Series<double>(this);
	        _l3Series = new Series<double>(this);
	        lrsiValue = new Series<double>(this);
	        
	        // Initialize WAE series
	        waeTrendUp = new Series<double>(this);
	        waeTrendDown = new Series<double>(this);
	        waeExplosionLine = new Series<double>(this);
	        
	        // Initialize ZLMACD components - moved here from SetDefaults
	        zlMacd = ZLMAcd(12, 26, 9, 0.7); // Your settings from the screenshot
	        zlMacdValue = new Series<double>(this);
	        zlMacdAvg = new Series<double>(this);
	        
	        // Connect parameter to variable
	        zlMacdMultiplier = ZLMACDMultiplier;
	        
	        // Initialize debug series
	        tfcDebug = new Series<double>(this);
	        haDotsDebug = new Series<double>(this);
	        lrsiDebug = new Series<double>(this);
	        waeDebug = new Series<double>(this);
	        
	        // Set initial zero values
	        for (int i = 1; i < 5; i++)
	        {
	            Values[i].Reset();
	        }
	        
	        // Add additional timeframe data if needed
	        if (Use5Min && (BarsPeriod.BarsPeriodType != BarsPeriodType.Minute || BarsPeriod.Value != 5))
	            AddDataSeries(BarsPeriodType.Minute, 5);
	        
	        if (Use15Min && (BarsPeriod.BarsPeriodType != BarsPeriodType.Minute || BarsPeriod.Value != 15))
	            AddDataSeries(BarsPeriodType.Minute, 15);
	        
	        if (Use30Min && (BarsPeriod.BarsPeriodType != BarsPeriodType.Minute || BarsPeriod.Value != 30))
	            AddDataSeries(BarsPeriodType.Minute, 30);
	        
	        if (Use1Hour && (BarsPeriod.BarsPeriodType != BarsPeriodType.Minute || BarsPeriod.Value != 60))
	            AddDataSeries(BarsPeriodType.Minute, 60);
	        
	        if (Use4Hour && (BarsPeriod.BarsPeriodType != BarsPeriodType.Minute || BarsPeriod.Value != 240))
	            AddDataSeries(BarsPeriodType.Minute, 240);
	        
	        if (Use1Day && (BarsPeriod.BarsPeriodType != BarsPeriodType.Day || BarsPeriod.Value != 1))
	            AddDataSeries(BarsPeriodType.Day, 1);
	    }
	    else if (State == State.Historical)
	    {
	        // Initialize debug plot values to avoid nulls
	        if (ShowDebugInfo && CurrentBar == 0)
	        {
	            tfcDebug[0] = 0;
	            haDotsDebug[0] = 0;
	            lrsiDebug[0] = 0;
	            waeDebug[0] = 0;
	            
	            // Also initialize plot values
	            if (Values != null && Values.Length >= 5)
	            {
	                for (int i = 1; i < 5; i++)
	                {
	                    if (Values[i] != null)
	                        Values[i][0] = 0;
	                }
	            }
	        }
	    }
	}
        
        protected override void OnBarUpdate()
        {
            try
            {
                // Skip if not enough bars
                if (CurrentBar < 5)
                    return;
                
                // Determine which data series we're updating
                if (BarsInProgress == 0) // Primary data series
                {
                    // Update other indicator components
                    UpdateHeikenAshiValues();
                    UpdateLaguerreRSI();
                    UpdateWAEValues();
					
					// Update ZLMACD values
					zlMacdValue[0] = zlMacd.Value[0];
					zlMacdAvg[0] = zlMacd.Avg[0];
					
					// Calculate momentum and normalize by ATR
					double macdMomentum = Math.Abs(zlMacdValue[0] - zlMacdAvg[0]);
					double atr = ATR(atrPeriod)[0];
					double normalizedMomentum = macdMomentum / atr;
					
					// Dynamic threshold based on ATR
					bool sufficientMomentum = !UseZLMACDFilter || normalizedMomentum >= zlMacdMultiplier;
					
					// Apply lockout period filter
					bool longSignalAllowed = CurrentBar - lastLongSignalBar > signalLockoutPeriod;
					bool shortSignalAllowed = CurrentBar - lastShortSignalBar > signalLockoutPeriod;
					
					// For debugging
					if (ShowDebugInfo && IsFirstTickOfBar)
					{
					    Print($"ZLMACD Filter: Enabled={UseZLMACDFilter}, Momentum={normalizedMomentum:F6}, Threshold={zlMacdMultiplier:F6}, " +
					          $"Sufficient={sufficientMomentum}, LongAllowed={longSignalAllowed}, ShortAllowed={shortSignalAllowed}");
					}
                    
                    // Get the HA bar type for other components that need it
                    bool haGreenBar = haClose[0] > haOpen[0];
                    bool haRedBar = haClose[0] < haOpen[0];
                    
                    // BEGIN TFC SIGNAL DETECTION
                    bool tfcForLong = false;
                    bool tfcForShort = false;
                    
                    if (UseTFC)
                    {
                        // Direct calculation based on the working TFCwSignals indicator
                        bool allGreen = true;
                        bool allRed = true;
                        
                        // Check all timeframes exactly like the TFCwSignals indicator does
                        for (int idx = CurrentBars.Length - 1; idx >= 0; idx--)
                        {
                            if (CurrentBars[idx] > 0)
                            {
                                double open = Opens[idx][0];
                                double close = Closes[0][0];
                                
                                if (State == State.Historical)
                                {
                                    open = Closes[idx][0];
                                    close = Closes[0][0];
                                    
                                    if (Times[idx][0] == Times[0][0])
                                        open = Opens[idx][0];
                                }
                                
                                if (open < close)
                                {
                                    // This timeframe is bullish
                                    allRed = false;
                                }
                                else if (open > close)
                                {
                                    // This timeframe is bearish
                                    allGreen = false;
                                }
                                else
                                {
                                    // This timeframe is neutral
                                    allGreen = false;
                                    allRed = false;
                                }
                            }
                        }
                        
                        // Direct mapping: green summary = long signal, red summary = short signal
                        tfcForLong = allGreen;
                        tfcForShort = allRed;
                        
                        if (ShowDebugInfo && IsFirstTickOfBar)
                        {
                            string summaryColor = "NEUTRAL";
                            if (allGreen) summaryColor = "GREEN";
                            else if (allRed) summaryColor = "RED";
                            
                            Print($"TFC Summary: {summaryColor}, Long={tfcForLong}, Short={tfcForShort}");
                        }
                    }
                    else
                    {
                        // If TFC is disabled, always return true
                        tfcForLong = true;
                        tfcForShort = true;
                    }
                    // END TFC SIGNAL DETECTION
                    
                    // BEGIN HA DOTS SIGNAL DETECTION
                    bool haDotsForLong = false;
                    bool haDotsForShort = false;
                    
                    if (UseHADots)
                    {
                        // We already calculate this in UpdateHeikenAshiValues
                        // Green dot = continuing bullish trend
                        // Red dot = continuing bearish trend
                        
                        haDotsForLong = haDotsState[0] > 0 && haGreenBar;
                        haDotsForShort = haDotsState[0] < 0 && haRedBar;
                        
                        // Extra safety - confirm signal matches HA bar color
                        if (haDotsForLong && !haGreenBar)
                        {
                            haDotsForLong = false;
                            if (ShowDebugInfo && IsFirstTickOfBar)
                                Print($"HA DOTS SAFETY: Blocked long signal on non-green HA bar at {Time[0]}");
                        }
                        
                        if (haDotsForShort && !haRedBar)
                        {
                            haDotsForShort = false;
                            if (ShowDebugInfo && IsFirstTickOfBar)
                                Print($"HA DOTS SAFETY: Blocked short signal on non-red HA bar at {Time[0]}");
                        }
                    }
                    else
                    {
                        haDotsForLong = true;
                        haDotsForShort = true;
                    }
                    // END HA DOTS SIGNAL DETECTION
                    
                    // BEGIN LRSI SIGNAL DETECTION
                    bool lrsiForLong = false;
                    bool lrsiForShort = false;
                    
                    if (UseLRSI)
                    {
                        // New simplified LRSI rules:
                        // 1. For long: LRSI must be rising AND current bar must be green
                        // 2. For short: LRSI must be falling AND current bar must be red
                        
                        // Check if LRSI is rising or falling by comparing to previous value
                        bool isRising = lrsiValue[0] > lrsiValue[1];
                        bool isFalling = lrsiValue[0] < lrsiValue[1];
                        
                        // Apply the new rules
                        lrsiForLong = isRising && haGreenBar;  // Only long if bar is green
                        lrsiForShort = isFalling && haRedBar;  // Only short if bar is red
                        
                        // Debug information
                        if (ShowDebugInfo && IsFirstTickOfBar)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine($"LRSI Detail: Current={lrsiValue[0]:F2}, Previous={lrsiValue[1]:F2}");
                            sb.AppendLine($"  Rising: {isRising}, Falling: {isFalling}");
                            sb.AppendLine($"  Bar Color: {(haGreenBar ? "GREEN" : (haRedBar ? "RED" : "NEUTRAL"))}");
                            sb.AppendLine($"  Long Signal: {lrsiForLong}, Short Signal: {lrsiForShort}");
                            Print(sb.ToString());
                        }
                    }
                    else
                    {
                        // If LRSI is disabled, always return true
                        lrsiForLong = true;
                        lrsiForShort = true;
                    }
                    // END LRSI SIGNAL DETECTION
                    
                    // BEGIN WAE SIGNAL DETECTION
                    bool waeForLong = false;
                    bool waeForShort = false;
                    
                    if (UseWAE)
                    {
                        // Check WAE trend strength
                        bool upTrendValid = waeTrendUp[0] > 0 && waeTrendUp[0] >= waeExplosionLine[0];
                        bool downTrendValid = waeTrendDown[0] > 0 && waeTrendDown[0] >= waeExplosionLine[0];
                        
                        // Long signal requires:
                        // 1. HA green bar
                        // 2. Uptrend volume >= explosion line
                        if (haGreenBar && upTrendValid)
                        {
                            waeForLong = true;
                        }
                        
                        // Short signal requires:
                        // 1. HA red bar
                        // 2. Downtrend volume >= explosion line
                        if (haRedBar && downTrendValid)
                        {
                            waeForShort = true;
                        }
                    }
                    else
                    {
                        waeForLong = true;
                        waeForShort = true;
                    }
                    // END WAE SIGNAL DETECTION
                    
                    // BEGIN DEBUG UPDATES
                    if (ShowDebugInfo)
                    {
                        Values[1][0] = tfcForLong ? 1 : (tfcForShort ? -1 : 0);
                        Values[2][0] = haDotsForLong ? 1 : (haDotsForShort ? -1 : 0);
                        Values[3][0] = lrsiForLong ? 1 : (lrsiForShort ? -1 : 0);
                        Values[4][0] = waeForLong ? 1 : (waeForShort ? -1 : 0);
                        
                        // Also update our separate debug series
                        tfcDebug[0] = tfcForLong ? 1 : (tfcForShort ? -1 : 0);
                        haDotsDebug[0] = haDotsForLong ? 1 : (haDotsForShort ? -1 : 0);
                        lrsiDebug[0] = lrsiForLong ? 1 : (lrsiForShort ? -1 : 0);
                        waeDebug[0] = waeForLong ? 1 : (waeForShort ? -1 : 0);
                        
                        if (IsFirstTickOfBar)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("==== TOISYNv3 MASTER DEBUG ====");
                            sb.AppendLine($"Bar Time: {Time[0]}, HA Type: {(haGreenBar ? "GREEN" : (haRedBar ? "RED" : "DOJI"))}");
                            sb.AppendLine($"TFC: Long={tfcForLong}, Short={tfcForShort}");
                            sb.AppendLine($"HA Dots: Long={haDotsForLong}, Short={haDotsForShort}, State={haDotsState[0]}");
                            sb.AppendLine($"LRSI: Long={lrsiForLong}, Short={lrsiForShort}, Value={lrsiValue[0]:F2}");
                            sb.AppendLine($"WAE: Long={waeForLong}, Short={waeForShort}, Up={waeTrendUp[0]:F2}, Down={waeTrendDown[0]:F2}, Explosion={waeExplosionLine[0]:F2}");
                            Print(sb.ToString());
                        }
                    }
                    // END DEBUG UPDATES

					// BEGIN SIGNAL GENERATION
					                    
					// Check if at least one indicator is enabled before generating signals
					bool anyIndicatorEnabled = UseTFC || UseHADots || UseLRSI || UseWAE;
					
					if (!anyIndicatorEnabled)
					{
					    // If no indicators are enabled, don't generate any signals
					    Values[0][0] = 0; // No signal
					    
					    if (ShowDebugInfo && IsFirstTickOfBar)
					    {
					        Print($"No signals generated at {Time[0]}: All indicators are disabled");
					    }
					}
					else
					{
					    // Normal signal generation when at least one indicator is enabled
					    bool longSignal = tfcForLong && haDotsForLong && lrsiForLong && waeForLong;
					    bool shortSignal = tfcForShort && haDotsForShort && lrsiForShort && waeForShort;
					    
					    // CRITICAL: Additional extensive debugging at signal detection point
					    if (ShowDebugInfo && IsFirstTickOfBar)
					    {
					        StringBuilder sb = new StringBuilder();
					        sb.AppendLine($"RAW SIGNAL CONDITIONS at {Time[0]}:");
					        sb.AppendLine($"  TFC: {tfcForLong}/{tfcForShort}");
					        sb.AppendLine($"  HADots: {haDotsForLong}/{haDotsForShort}");
					        sb.AppendLine($"  LRSI: {lrsiForLong}/{lrsiForShort}");
					        sb.AppendLine($"  WAE: {waeForLong}/{waeForShort}");
					        sb.AppendLine($"  Combined: Long={longSignal}, Short={shortSignal}");
					        sb.AppendLine($"  Momentum: {normalizedMomentum:F6}, Sufficient={sufficientMomentum}");
					        sb.AppendLine($"  Lockout: LongAllowed={longSignalAllowed}, ShortAllowed={shortSignalAllowed}");
							sb.AppendLine($"  ZLMACD Filter: Enabled={UseZLMACDFilter}, Momentum={normalizedMomentum:F6}, Sufficient={sufficientMomentum}");
					        Print(sb.ToString());
					    }
					    
					    // Check for trend transitions - EXIT FIRST to reset trend flags
					    if (!longSignal && inLongTrend)
					    {
					        inLongTrend = false;
					        if (IsFirstTickOfBar && ShowDebugInfo)
					            Print($"Exit LONG trend at {Time[0]}, Price: {Close[0]}");
					    }
					    
					    if (!shortSignal && inShortTrend)
					    {
					        inShortTrend = false;
					        if (IsFirstTickOfBar && ShowDebugInfo)
					            Print($"Exit SHORT trend at {Time[0]}, Price: {Close[0]}");
					    }
					    
					    // AFTER checking exits, now check for new entries
					    if (longSignal && longSignalAllowed && sufficientMomentum && !inShortTrend && !inLongTrend) 
					    {
					        // Create signal name that includes timestamp to ensure uniqueness
					        string arrowName = "LongEntry" + CurrentBar;
					        
					        // Generate long signal with Heiken Ashi values for positioning
					        Draw.ArrowUp(this, arrowName, false, 0, haLow[0] - 5 * TickSize, LongArrowColor);
					        
					        if (ShowLabels)
					        {
					            string textName = "LongOnText_" + CurrentBar;
					            Draw.Text(this, textName, "LongOn", 0, haLow[0] - 10 * TickSize, LongArrowColor);
					        }
					        
					        Values[0][0] = 1; // Signal up
					        
					        // Update last signal bar
					        lastLongSignalBar = CurrentBar;
					        
					        if (IsFirstTickOfBar && ShowDebugInfo)
					        {
					            Print($"LONG SIGNAL DRAWN at {Time[0]}, Bar: {CurrentBar}, Price: {Close[0]}, " +
					                  $"HA: {haClose[0]}, ZLMACD Momentum: {normalizedMomentum:F6}");
					        }
					        
					        // Update trend states
					        inLongTrend = true;
					        inShortTrend = false;
					    }
					    else if (shortSignal && shortSignalAllowed && sufficientMomentum && !inLongTrend && !inShortTrend)
					    {
					        // Create signal name that includes timestamp to ensure uniqueness
					        string arrowName = "ShortEntry" + CurrentBar;
					        
					        // Generate short signal with Heiken Ashi values for positioning
					        Draw.ArrowDown(this, arrowName, false, 0, haHigh[0] + 5 * TickSize, ShortArrowColor);
					        
					        if (ShowLabels)
					        {
					            string textName = "ShortOnText_" + CurrentBar;
					            Draw.Text(this, textName, "ShortOn", 0, haHigh[0] + 10 * TickSize, ShortArrowColor);
					        }
					        
					        Values[0][0] = -1; // Signal down
					        
					        // Update last signal bar
					        lastShortSignalBar = CurrentBar;
					        
					        if (IsFirstTickOfBar && ShowDebugInfo)
					        {
					            Print($"SHORT SIGNAL DRAWN at {Time[0]}, Bar: {CurrentBar}, Price: {Close[0]}, " +
					                  $"HA: {haClose[0]}, ZLMACD Momentum: {normalizedMomentum:F6}");
					        }
					        
					        // Update trend states
					        inShortTrend = true;
					        inLongTrend = false;
					    }
					    else if (longSignal && inLongTrend)
					    {
					        // We're already in a long trend, so just update the value but don't draw the signal
					        Values[0][0] = 1;
					        
					        if (IsFirstTickOfBar && ShowDebugInfo && (!sufficientMomentum || !longSignalAllowed))
					        {
					        List<string> reasonsList = new List<string>();
							if (UseZLMACDFilter && !sufficientMomentum) reasonsList.Add("insufficient momentum");
							if (!longSignalAllowed) reasonsList.Add("in lockout period");
							            
							Print($"Long conditions met at {Time[0]}, but {string.Join(" and ", reasonsList)} - no signal drawn");
												        }
					    }
					    else if (shortSignal && inShortTrend)
					    {
					        // We're already in a short trend, so just update the value but don't draw the signal
					        Values[0][0] = -1;
					        
					        if (IsFirstTickOfBar && ShowDebugInfo && (!sufficientMomentum || !shortSignalAllowed))
					        {
					        List<string> reasonsList = new List<string>();
							if (UseZLMACDFilter && !sufficientMomentum) reasonsList.Add("insufficient momentum");
							if (!shortSignalAllowed) reasonsList.Add("in lockout period");
							            
							Print($"Short conditions met at {Time[0]}, but {string.Join(" and ", reasonsList)} - no signal drawn");
					        }
					    }
					    else
					    {
					        Values[0][0] = 0; // No signal
					    }
					}
					// END SIGNAL GENERATION
                }
            }
            catch (Exception ex)
            {
                Print("TOISYNv3 Error: " + ex.Message);
                if (ShowDebugInfo)
                    Print("Stack Trace: " + ex.StackTrace);
            }
        }
        
        // Corrected Heiken Ashi Dots implementation
        private void UpdateHeikenAshiValues()
        {
            if (CurrentBar == 0)
            {
                // Initialize HA values on the first bar
                haOpen[0] = Open[0];
                haClose[0] = Close[0];
                haHigh[0] = High[0];
                haLow[0] = Low[0];
                haDotsState[0] = 0;
                return;
            }
            
            // Calculate current Heiken Ashi values
            haClose[0] = (Open[0] + High[0] + Low[0] + Close[0]) / 4.0;
            haOpen[0] = (haOpen[1] + haClose[1]) / 2.0;
            haHigh[0] = Math.Max(High[0], Math.Max(haOpen[0], haClose[0]));
            haLow[0] = Math.Min(Low[0], Math.Min(haOpen[0], haClose[0]));
            
            // From the original HeikenAshiDots indicator, we see it's looking at:
            // 1. Current HA trend (bullish/bearish)
            // 2. Previous HA trend (bullish/bearish)
            // If they match, we know the trend is continuing
            
            // Determine current HA trend (bullish = close >= open, bearish = close < open)
            int currHATrend = (haClose[0] >= haOpen[0]) ? 1 : -1;
            int prevHATrend = (haClose[1] >= haOpen[1]) ? 1 : -1;
            
            // Set state based on trend continuation - exactly as in the original indicator
            if (currHATrend == prevHATrend && currHATrend > 0)
            {
                // Continuing bullish trend
                haDotsState[0] = 1;  // Green dot (bullish trend)
            }
            else if (currHATrend == prevHATrend && currHATrend < 0)
            {
                // Continuing bearish trend
                haDotsState[0] = -1; // Red dot (bearish trend)
            }
            else
            {
                // Changing trend (matches the yellow dots in the original)
                haDotsState[0] = 0;  // Yellow dot (changing trend)
            }
        }
        
        // BEGIN LRSI IMPLEMENTATION
        private void UpdateLaguerreRSI()
        {
            // Calculate LRSI based on the HALRSI indicator you're actually using
            if (CurrentBar == 0)
            {
                // Initialize Laguerre variables
                _l0Series[0] = 0;
                _l1Series[0] = 0;
                _l2Series[0] = 0;
                _l3Series[0] = 0;
                lrsiValue[0] = 0;
                return;
            }
            
            // Use the exact same calculation as in your HALRSI indicator
            // Note: We're using 0.7 as the default gamma value based on your indicator
            double gamma = 0.7;
            
            // Calculate Laguerre RSI
            _l0Series[0] = (1 - gamma) * Close[0] + gamma * _l0Series[1];
            _l1Series[0] = -gamma * _l0Series[0] + _l0Series[1] + gamma * _l1Series[1];
            _l2Series[0] = -gamma * _l1Series[0] + _l1Series[1] + gamma * _l2Series[1];
            _l3Series[0] = -gamma * _l2Series[0] + _l2Series[1] + gamma * _l3Series[1];
            
            double cu = 0;
            double cd = 0;
            
            if (_l0Series[0] >= _l1Series[0])
                cu = _l0Series[0] - _l1Series[0];
            else
                cd = _l1Series[0] - _l0Series[0];
            
            if (_l1Series[0] >= _l2Series[0])
                cu = cu + _l1Series[0] - _l2Series[0];
            else
                cd = cd + _l2Series[0] - _l1Series[0];
                
            if (_l2Series[0] >= _l3Series[0])
                cu = cu + _l2Series[0] - _l3Series[0];
            else
                cd = cd + _l3Series[0] - _l2Series[0];
            
            if (cu + cd != 0)
                lrsiValue[0] = 100 * cu / (cu + cd);
            else
                lrsiValue[0] = lrsiValue[1];  // Maintain previous value if denominator is zero
        }
        // END LRSI IMPLEMENTATION
        
        // BEGIN WAE IMPLEMENTATION
        private void UpdateWAEValues()
        {
            // Skip calculation if we don't have enough data
            if (CurrentBar < 3)
            {
                waeTrendUp[0] = 0;
                waeTrendDown[0] = 0;
                waeExplosionLine[0] = 0;
                return;
            }
            
            // Verify Heiken Ashi data is available (safety check)
            if (haOpen == null || haClose == null)
            {
                if (ShowDebugInfo && IsFirstTickOfBar)
                    Print("WAE Warning: Heiken Ashi data not available for WAE calculation");
                return;
            }
            
            // 1. Calculate fast and slow EMA (for MACD)
            double fastMA;
            double fastMAPrev;
            double slowMA;
            double slowMAPrev;
            
            // Match the original WAE calculation method with optional smoothing
            // Note: Since Heiken Ashi already provides smoothing, these calculations
            // work well with HA data for trend determination
            if (true) // FastSmooth enabled
            {
                fastMA = EMA(EMA(10), 9)[0];
                fastMAPrev = EMA(EMA(10), 9)[1];
            }
            else
            {
                fastMA = EMA(10)[0];
                fastMAPrev = EMA(10)[1];
            }
            
            if (true) // SlowSmooth enabled
            {
                slowMA = EMA(EMA(30), 9)[0];
                slowMAPrev = EMA(EMA(30), 9)[1];
            }
            else
            {
                slowMA = EMA(30)[0];
                slowMAPrev = EMA(30)[1];
            }
            
            // 2. Calculate MACD values
            double macd = fastMA - slowMA;
            double macdPrev = fastMAPrev - slowMAPrev;
            
            // 3. Calculate trend strength based on MACD change
            double t1 = (macd - macdPrev) * WAESensitivity;
            
            // 4. Calculate explosion line (based on Bollinger Band width)
            double basis = SMA(30)[0];
            double dev = 2.0 * StdDev(30)[0];
            waeExplosionLine[0] = (basis + dev) - (basis - dev);
            
            // 5. Set trend up/down values 
            // Note: These values will be used with Heiken Ashi bar colors
            // for signal generation in the OnBarUpdate method
            if (t1 >= 0)
            {
                waeTrendUp[0] = t1;
                waeTrendDown[0] = 0;
            }
            else
            {
                waeTrendUp[0] = 0;
                waeTrendDown[0] = Math.Abs(t1);
            }
            
            // Store the relationship between regular and HA bars for analysis
            // This helps identify potential divergences that could affect signals
            if (ShowDebugInfo && IsFirstTickOfBar && CurrentBar > 0)
            {
                bool regularGreen = Close[0] > Open[0];
                bool haGreen = haClose[0] >= haOpen[0];
                
                // Only log when there's a divergence to avoid excessive output
                if (regularGreen != haGreen)
                {
                    Print($"WAE Info: Divergence at {Time[0]} - Regular bar is {(regularGreen ? "GREEN" : "RED")} but HA bar is {(haGreen ? "GREEN" : "RED")}");
                }
            }
        }
        // END WAE IMPLEMENTATION
        
        public override string ToString()
        {
            // Make sure to check for null references
            if (!ShowDebugInfo || tfcDebug == null || haDotsDebug == null || lrsiDebug == null || waeDebug == null)
                return Name;
                
            try
            {
                return Name + 
                    string.Format(" TFC:{0} HA:{1} LRSI:{2} WAE:{3}",
                    tfcDebug[0] > 0 ? "↑" : (tfcDebug[0] < 0 ? "↓" : "-"),
                    haDotsDebug[0] > 0 ? "↑" : (haDotsDebug[0] < 0 ? "↓" : "-"),
                    lrsiDebug[0] > 0 ? "↑" : (lrsiDebug[0] < 0 ? "↓" : "-"),
                    waeDebug[0] > 0 ? "↑" : (waeDebug[0] < 0 ? "↓" : "-"));
            }
            catch
            {
                return Name; // Fallback if there's any issue
            }
        }
        
        #region Properties
        [Range(1, 1000)]
        [NinjaScriptProperty]
        [Display(Name = "WAE Sensitivity", Description = "Sensitivity multiplier for WAE calculation", Order = 1, GroupName = "Parameters")]
        public int WAESensitivity { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Description = "Display labels next to signal arrows", Order = 2, GroupName = "Parameters")]
        public bool ShowLabels { get; set; }
        
        // Enable/Disable Indicators
        [NinjaScriptProperty]
        [Display(Name = "Use TFC", Description = "Use TimeFrameContinuity for signal generation", Order = 1, GroupName = "Indicator Selection")]
        public bool UseTFC { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use HA Dots", Description = "Use Heiken Ashi Dots for signal generation", Order = 2, GroupName = "Indicator Selection")]
        public bool UseHADots { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use LRSI", Description = "Use LaguerreRSI for signal generation", Order = 3, GroupName = "Indicator Selection")]
        public bool UseLRSI { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use WAE", Description = "Use Waddah Attar Explosion for signal generation", Order = 4, GroupName = "Indicator Selection")]
        public bool UseWAE { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Use ZLMACD Filter", Description = "Enable ZLMACD momentum filter for signal generation", Order = 5, GroupName = "Indicator Selection")]
		public bool UseZLMACDFilter { get; set; }
		
		[Range(0.1, 2.0)]
		[NinjaScriptProperty]
		[Display(Name = "ZLMACD Multiplier", Description = "Multiplier for ZLMACD momentum threshold", Order = 6, GroupName = "Indicator Selection")]
		public double ZLMACDMultiplier { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Debug Info", Description = "Show debugging information", Order = 7, GroupName = "Indicator Selection")]
        public bool ShowDebugInfo { get; set; }
        
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Long Arrow Color", Description = "Color for long signal arrows", Order = 1, GroupName = "Visuals")]
        public Brush LongArrowColor { get; set; }
        
        [Browsable(false)]
        public string LongArrowColorSerializable
        {
            get { return Serialize.BrushToString(LongArrowColor); }
            set { LongArrowColor = Serialize.StringToBrush(value); }
        }
        
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Short Arrow Color", Description = "Color for short signal arrows", Order = 2, GroupName = "Visuals")]
        public Brush ShortArrowColor { get; set; }
        
        [Browsable(false)]
        public string ShortArrowColorSerializable
        {
            get { return Serialize.BrushToString(ShortArrowColor); }
            set { ShortArrowColor = Serialize.StringToBrush(value); }
        }
        
        // Timeframe Selection Properties
        [NinjaScriptProperty]
        [Display(Name = "Use 5 Minute", Description = "Include 5 minute timeframe in analysis", Order = 1, GroupName = "Timeframes")]
        public bool Use5Min { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use 15 Minute", Description = "Include 15 minute timeframe in analysis", Order = 2, GroupName = "Timeframes")]
        public bool Use15Min { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use 30 Minute", Description = "Include 30 minute timeframe in analysis", Order = 3, GroupName = "Timeframes")]
        public bool Use30Min { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use 1 Hour", Description = "Include 1 hour timeframe in analysis", Order = 4, GroupName = "Timeframes")]
        public bool Use1Hour { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use 4 Hour", Description = "Include 4 hour timeframe in analysis", Order = 5, GroupName = "Timeframes")]
        public bool Use4Hour { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use 1 Day", Description = "Include 1 day timeframe in analysis", Order = 6, GroupName = "Timeframes")]
        public bool Use1Day { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.TOISYNv3[] cacheTOISYNv3;
		public Myindicators.TOISYNv3 TOISYNv3(int wAESensitivity, bool showLabels, bool useTFC, bool useHADots, bool useLRSI, bool useWAE, bool useZLMACDFilter, double zLMACDMultiplier, bool showDebugInfo, Brush longArrowColor, Brush shortArrowColor, bool use5Min, bool use15Min, bool use30Min, bool use1Hour, bool use4Hour, bool use1Day)
		{
			return TOISYNv3(Input, wAESensitivity, showLabels, useTFC, useHADots, useLRSI, useWAE, useZLMACDFilter, zLMACDMultiplier, showDebugInfo, longArrowColor, shortArrowColor, use5Min, use15Min, use30Min, use1Hour, use4Hour, use1Day);
		}

		public Myindicators.TOISYNv3 TOISYNv3(ISeries<double> input, int wAESensitivity, bool showLabels, bool useTFC, bool useHADots, bool useLRSI, bool useWAE, bool useZLMACDFilter, double zLMACDMultiplier, bool showDebugInfo, Brush longArrowColor, Brush shortArrowColor, bool use5Min, bool use15Min, bool use30Min, bool use1Hour, bool use4Hour, bool use1Day)
		{
			if (cacheTOISYNv3 != null)
				for (int idx = 0; idx < cacheTOISYNv3.Length; idx++)
					if (cacheTOISYNv3[idx] != null && cacheTOISYNv3[idx].WAESensitivity == wAESensitivity && cacheTOISYNv3[idx].ShowLabels == showLabels && cacheTOISYNv3[idx].UseTFC == useTFC && cacheTOISYNv3[idx].UseHADots == useHADots && cacheTOISYNv3[idx].UseLRSI == useLRSI && cacheTOISYNv3[idx].UseWAE == useWAE && cacheTOISYNv3[idx].UseZLMACDFilter == useZLMACDFilter && cacheTOISYNv3[idx].ZLMACDMultiplier == zLMACDMultiplier && cacheTOISYNv3[idx].ShowDebugInfo == showDebugInfo && cacheTOISYNv3[idx].LongArrowColor == longArrowColor && cacheTOISYNv3[idx].ShortArrowColor == shortArrowColor && cacheTOISYNv3[idx].Use5Min == use5Min && cacheTOISYNv3[idx].Use15Min == use15Min && cacheTOISYNv3[idx].Use30Min == use30Min && cacheTOISYNv3[idx].Use1Hour == use1Hour && cacheTOISYNv3[idx].Use4Hour == use4Hour && cacheTOISYNv3[idx].Use1Day == use1Day && cacheTOISYNv3[idx].EqualsInput(input))
						return cacheTOISYNv3[idx];
			return CacheIndicator<Myindicators.TOISYNv3>(new Myindicators.TOISYNv3(){ WAESensitivity = wAESensitivity, ShowLabels = showLabels, UseTFC = useTFC, UseHADots = useHADots, UseLRSI = useLRSI, UseWAE = useWAE, UseZLMACDFilter = useZLMACDFilter, ZLMACDMultiplier = zLMACDMultiplier, ShowDebugInfo = showDebugInfo, LongArrowColor = longArrowColor, ShortArrowColor = shortArrowColor, Use5Min = use5Min, Use15Min = use15Min, Use30Min = use30Min, Use1Hour = use1Hour, Use4Hour = use4Hour, Use1Day = use1Day }, input, ref cacheTOISYNv3);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.TOISYNv3 TOISYNv3(int wAESensitivity, bool showLabels, bool useTFC, bool useHADots, bool useLRSI, bool useWAE, bool useZLMACDFilter, double zLMACDMultiplier, bool showDebugInfo, Brush longArrowColor, Brush shortArrowColor, bool use5Min, bool use15Min, bool use30Min, bool use1Hour, bool use4Hour, bool use1Day)
		{
			return indicator.TOISYNv3(Input, wAESensitivity, showLabels, useTFC, useHADots, useLRSI, useWAE, useZLMACDFilter, zLMACDMultiplier, showDebugInfo, longArrowColor, shortArrowColor, use5Min, use15Min, use30Min, use1Hour, use4Hour, use1Day);
		}

		public Indicators.Myindicators.TOISYNv3 TOISYNv3(ISeries<double> input , int wAESensitivity, bool showLabels, bool useTFC, bool useHADots, bool useLRSI, bool useWAE, bool useZLMACDFilter, double zLMACDMultiplier, bool showDebugInfo, Brush longArrowColor, Brush shortArrowColor, bool use5Min, bool use15Min, bool use30Min, bool use1Hour, bool use4Hour, bool use1Day)
		{
			return indicator.TOISYNv3(input, wAESensitivity, showLabels, useTFC, useHADots, useLRSI, useWAE, useZLMACDFilter, zLMACDMultiplier, showDebugInfo, longArrowColor, shortArrowColor, use5Min, use15Min, use30Min, use1Hour, use4Hour, use1Day);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.TOISYNv3 TOISYNv3(int wAESensitivity, bool showLabels, bool useTFC, bool useHADots, bool useLRSI, bool useWAE, bool useZLMACDFilter, double zLMACDMultiplier, bool showDebugInfo, Brush longArrowColor, Brush shortArrowColor, bool use5Min, bool use15Min, bool use30Min, bool use1Hour, bool use4Hour, bool use1Day)
		{
			return indicator.TOISYNv3(Input, wAESensitivity, showLabels, useTFC, useHADots, useLRSI, useWAE, useZLMACDFilter, zLMACDMultiplier, showDebugInfo, longArrowColor, shortArrowColor, use5Min, use15Min, use30Min, use1Hour, use4Hour, use1Day);
		}

		public Indicators.Myindicators.TOISYNv3 TOISYNv3(ISeries<double> input , int wAESensitivity, bool showLabels, bool useTFC, bool useHADots, bool useLRSI, bool useWAE, bool useZLMACDFilter, double zLMACDMultiplier, bool showDebugInfo, Brush longArrowColor, Brush shortArrowColor, bool use5Min, bool use15Min, bool use30Min, bool use1Hour, bool use4Hour, bool use1Day)
		{
			return indicator.TOISYNv3(input, wAESensitivity, showLabels, useTFC, useHADots, useLRSI, useWAE, useZLMACDFilter, zLMACDMultiplier, showDebugInfo, longArrowColor, shortArrowColor, use5Min, use15Min, use30Min, use1Hour, use4Hour, use1Day);
		}
	}
}

#endregion
