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
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    public class TrendSpotter : Indicator
    {
        #region Variables
        private MACD macd;
        private DM dm;
        private EMA ema20;
        private ATR atr;
        private Series<double> fastMA;
        private Series<double> slowMA;
        private Series<double> macdLine;
        private Series<double> signalLine;
        
        private bool longCondition1, longCondition2, longCondition3, longCondition4, longCondition5, longCondition6;
        private bool shortCondition1, shortCondition2, shortCondition3, shortCondition4, shortCondition5, shortCondition6;
        private bool wasLongSignal, wasShortSignal;
        private bool inLongTrend, inShortTrend;
        private int longConditionCount, shortConditionCount;
        
        // Option 2 Exit Strategy variables
        private bool macdMomentumLoss;
        private bool adxWeakening;
        private bool diWeakening;
        private double previousDISpread, currentDISpread;
        private bool useOption3ForTimeframe;
        
        // Data logging variables
        private string currentLogFile;
        private bool headerWritten;
        
        // Tracking variables for analysis
        private double entryPrice;
        private double maxFavorableMove;
        private double maxAdverseMove;
        private int barsInTrend;
        
        // Optimized tracking variables
        private static double cumulativeRevenue = 0;
        
        // Time tracking variables
        private DateTime tradeEntryTime;
        private int entryHour;
        
        // Volume filter variables
        private SMA volumeSMA;
        private bool volumeConfirmed;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"TrendSpotter Trading Signal Indicator - Option 2 Exit Strategy with Optimized Goal-Oriented Tracking + Time Analysis";
                Name = "TrendSpotter";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // MACD Settings
                MacdFast = 3;
                MacdSlow = 10;
                MacdSmooth = 16;
                MacdMAType = CustomEnumNamespace.UniversalMovingAverage.SMA;

                // DM Settings  
                DmPeriod = 14;
                AdxRisingBars = 1;

                // EMA Settings
                EmaPeriod = 20;

                // Signal Settings
                ShowEntrySignals = true;
                ShowExitSignals = true;
                Signal_Offset = 5;
                LongOn = "LongEntry";
                ShortOn = "ShortEntry";
                LongOff = "LongExit";
                ShortOff = "ShortExit";

                // Background Colors
                PartialSignalColor = Brushes.Yellow;
                PartialSignalOpacity = 15;
                
                // Entry Arrow Colors
                LongEntryColor = Brushes.Lime;
                ShortEntryColor = Brushes.Red;

                // Exit Signal Color
                ExitColor = Brushes.DimGray;
                
                // Data Tracking
                EnableDataTracking = true;
                
                // Volume Filter Settings
                VolumeFilterMultiplier = 1.5;
            }
            else if (State == State.DataLoaded)
            {
                // Initialize indicators and series
                dm = DM(DmPeriod);
                ema20 = EMA(EmaPeriod);
                atr = ATR(14);
                volumeSMA = SMA(Volume, 20);
                
                if (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA)
                {
                    // Use built-in MACD (uses EMA by default)
                    macd = MACD(MacdFast, MacdSlow, MacdSmooth);
                }
                else
                {
                    // Initialize series for custom SMA-based MACD
                    fastMA = new Series<double>(this);
                    slowMA = new Series<double>(this);
                    macdLine = new Series<double>(this);
                    signalLine = new Series<double>(this);
                }
                
                // Initialize tracking variables
                currentLogFile = "";
                headerWritten = false;
                entryPrice = 0;
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 0;
                useOption3ForTimeframe = BarsPeriod.Value >= 5; // Use Option 3 for 5+ minute timeframes
                
                // Initialize time tracking variables
                tradeEntryTime = DateTime.MinValue;
                entryHour = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(MacdSlow, DmPeriod), EmaPeriod))
                return;

            // Calculate MACD values
            double macdValue, macdAvg;
            
            if (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA)
            {
                macdValue = macd.Default[0];
                macdAvg = macd.Avg[0];
            }
            else
            {
                // Custom SMA-based MACD calculation
                fastMA[0] = SMA(MacdFast)[0];
                slowMA[0] = SMA(MacdSlow)[0];
                macdLine[0] = fastMA[0] - slowMA[0];
                
                // Calculate signal line using SMA
                if (CurrentBar >= MacdSmooth - 1)
                {
                    signalLine[0] = SMA(macdLine, MacdSmooth)[0];
                }
                else
                {
                    signalLine[0] = macdLine[0];
                }
                
                macdValue = macdLine[0];
                macdAvg = signalLine[0];
            }

            // Get indicator values
            double adx = dm.ADXPlot[0];
            double diPlus = dm.DiPlus[0];
            double diMinus = dm.DiMinus[0];
            double emaValue = ema20[0];

            // Calculate DI Spread for exit conditions
            currentDISpread = Math.Abs(diPlus - diMinus);
            if (CurrentBar > 0)
                previousDISpread = Math.Abs(dm.DiPlus[1] - dm.DiMinus[1]);

            // Check if ADX is rising over specified bars
            bool adxRising = true;
            if (CurrentBar >= AdxRisingBars)
            {
                for (int i = 1; i <= AdxRisingBars; i++)
                {
                    if (dm.ADXPlot[0] <= dm.ADXPlot[i])
                    {
                        adxRising = false;
                        break;
                    }
                }
            }
            
            // Check consecutive rising ADX bars
            int consecutiveRisingBars = 0;
            for (int i = 1; i <= 5 && CurrentBar >= i; i++)
            {
                if (dm.ADXPlot[i-1] > dm.ADXPlot[i])
                    consecutiveRisingBars++;
                else
                    break;
            }
            bool adxRisingConsecutive = consecutiveRisingBars >= AdxRisingBars;

            // 6-Condition Entry System (keeping the working system)
            longCondition1 = macdValue > 0;                             // MACD above zero
            longCondition2 = macdValue > macdAvg;                       // MACD above signal
            longCondition3 = CurrentBar > 0 ? macdValue > (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1]) : true; // MACD rising
            longCondition4 = adxRising;                                 // ADX Rising
            longCondition5 = diPlus > diMinus;                          // DI is bullish (+DI > -DI)
            longCondition6 = Close[0] > emaValue;                       // Bar closes above 20EMA

            shortCondition1 = macdValue < 0;                            // MACD below zero
            shortCondition2 = macdValue < macdAvg;                      // MACD below signal
            shortCondition3 = CurrentBar > 0 ? macdValue < (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1]) : true; // MACD falling
            shortCondition4 = adxRising;                                // ADX Rising
            shortCondition5 = diMinus > diPlus;                         // DI is bearish (-DI > +DI)
            shortCondition6 = Close[0] < emaValue;                      // Bar closes below 20EMA

            // Count conditions
            longConditionCount = (longCondition1 ? 1 : 0) + (longCondition2 ? 1 : 0) + (longCondition3 ? 1 : 0) + 
                               (longCondition4 ? 1 : 0) + (longCondition5 ? 1 : 0) + (longCondition6 ? 1 : 0);
            
            shortConditionCount = (shortCondition1 ? 1 : 0) + (shortCondition2 ? 1 : 0) + (shortCondition3 ? 1 : 0) + 
                                (shortCondition4 ? 1 : 0) + (shortCondition5 ? 1 : 0) + (shortCondition6 ? 1 : 0);

            // Volume filter confirmation
            volumeConfirmed = Volume[0] > volumeSMA[0] * VolumeFilterMultiplier;

            // Current signal status with volume filter
            bool currentLongSignal = (longConditionCount == 6) && volumeConfirmed;
            bool currentShortSignal = (shortConditionCount == 6) && volumeConfirmed;

            // Dynamic Exit Strategy based on timeframe
            bool longExit, shortExit;

            if (useOption3ForTimeframe)
            {
                // Option 3: Exit when any of the 6 conditions becomes false (5+ minute timeframes)
                bool anyConditionFalse = !longCondition1 || !longCondition2 || !longCondition3 || 
                                        !longCondition4 || !longCondition5 || !longCondition6;
                bool anyShortConditionFalse = !shortCondition1 || !shortCondition2 || !shortCondition3 || 
                                             !shortCondition4 || !shortCondition5 || !shortCondition6;
                
                longExit = inLongTrend && anyConditionFalse;
                shortExit = inShortTrend && anyShortConditionFalse;
            }
            else
            {
                // Option 2: Multi-Condition Exit Logic (1-minute and shorter timeframes)
                CalculateExitConditions(macdValue);
                longExit = inLongTrend && (macdMomentumLoss && (adxWeakening || diWeakening));
                shortExit = inShortTrend && (macdMomentumLoss && (adxWeakening || diWeakening));
            }

            // Track performance during trends
            if (inLongTrend || inShortTrend)
            {
                barsInTrend++;
                
                if (inLongTrend)
                {
                    double currentMove = Close[0] - entryPrice;
                    if (currentMove > maxFavorableMove)
                        maxFavorableMove = currentMove;
                    if (currentMove < maxAdverseMove)
                        maxAdverseMove = currentMove;
                }
                else if (inShortTrend)
                {
                    double currentMove = entryPrice - Close[0];
                    if (currentMove > maxFavorableMove)
                        maxFavorableMove = currentMove;
                    if (currentMove < maxAdverseMove)
                        maxAdverseMove = currentMove;
                }
            }

            // Update trend status with time tracking
            if (currentLongSignal && !inLongTrend)
            {
                inLongTrend = true;
                inShortTrend = false;
                entryPrice = Close[0];
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 1;
                
                // Capture trade entry time
                tradeEntryTime = Time[0];
                entryHour = Time[0].Hour;
            }
            else if (currentShortSignal && !inShortTrend)
            {
                inShortTrend = true;
                inLongTrend = false;
                entryPrice = Close[0];
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 1;
                
                // Capture trade entry time
                tradeEntryTime = Time[0];
                entryHour = Time[0].Hour;
            }
            else if (longExit)
            {
                inLongTrend = false;
                barsInTrend = 0;
            }
            else if (shortExit)
            {
                inShortTrend = false;
                barsInTrend = 0;
            }

            // Draw background colors and entry signals
            if (ShowEntrySignals)
            {
                // Entry arrows - only on first bar of new signal
                if (currentLongSignal && !wasLongSignal)
                {
                    Draw.ArrowUp(this, LongOn + CurrentBar.ToString(), true, 0, Low[0] - Signal_Offset * TickSize, LongEntryColor);
                }
                else if (currentShortSignal && !wasShortSignal)
                {
                    Draw.ArrowDown(this, ShortOn + CurrentBar.ToString(), true, 0, High[0] + Signal_Offset * TickSize, ShortEntryColor);
                }

                // Background logic: Yellow for partial signals, but no background once in trend
                if (!inLongTrend && !inShortTrend && (longConditionCount == 5 || shortConditionCount == 5))
                {
                    BackBrush = CreateBrushWithOpacity(PartialSignalColor, PartialSignalOpacity);
                }
                else
                {
                    BackBrush = Brushes.Transparent;
                }
            }

            // Exit Signals using dynamic strategy
            if (ShowExitSignals && CurrentBar > 1)
            {
                // Long Exit
                if (longExit)
                {
                    Draw.Text(this, LongOff + CurrentBar, true, "o", 0, 
                             High[0] + Signal_Offset * TickSize, 0, ExitColor, 
                             new SimpleFont("Arial", 12), TextAlignment.Center, 
                             Brushes.Transparent, Brushes.Transparent, 0);
                }

                // Short Exit
                if (shortExit)
                {
                    Draw.Text(this, ShortOff + CurrentBar, true, "o", 0,
                             Low[0] - Signal_Offset * TickSize, 0, ExitColor,
                             new SimpleFont("Arial", 12), TextAlignment.Center,
                             Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            // ===== OPTIMIZED GOAL-ORIENTED TRACKING MODULE WITH TIME ANALYSIS START =====
            if (EnableDataTracking)
            {
                LogOptimizedDataForAnalysis(macdValue, macdAvg, adx, diPlus, diMinus, emaValue,
                                          currentLongSignal, currentShortSignal, longExit, shortExit);
            }
            // ===== OPTIMIZED GOAL-ORIENTED TRACKING MODULE WITH TIME ANALYSIS END =====

            // Update previous signal status - maintain during trends, reset only on exits
            if (longExit || shortExit) 
            {
                wasLongSignal = false;
                wasShortSignal = false;
            }
            else if (inLongTrend || inShortTrend)
            {
                // Maintain signal state during active trends
                wasLongSignal = inLongTrend ? true : wasLongSignal;
                wasShortSignal = inShortTrend ? true : wasShortSignal;
            }
            else
            {
                // Normal tracking when not in trend
                wasLongSignal = currentLongSignal;
                wasShortSignal = currentShortSignal;
            }
        }

        private void CalculateExitConditions(double macdValue)
        {
            // MACD Momentum Check: Current MACD ≤ Previous MACD for 2 consecutive bars
            macdMomentumLoss = false;
            if (CurrentBar >= 2)
            {
                double previousMacd1, previousMacd2;
                
                if (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA)
                {
                    previousMacd1 = macd.Default[1];
                    previousMacd2 = macd.Default[2];
                }
                else
                {
                    previousMacd1 = macdLine[1];
                    previousMacd2 = macdLine[2];
                }
                
                // Check if MACD has stopped rising for 2 consecutive bars
                macdMomentumLoss = (macdValue <= previousMacd1) && (previousMacd1 <= previousMacd2);
            }

            // ADX Weakening: ADX[0] < ADX[1] (trend strength weakening)
            adxWeakening = false;
            if (CurrentBar >= 1)
            {
                adxWeakening = dm.ADXPlot[0] < dm.ADXPlot[1];
            }

            // DI Weakening: DI_Spread decreasing (directional bias weakening)
            diWeakening = false;
            if (CurrentBar >= 1)
            {
                diWeakening = currentDISpread < previousDISpread;
            }
        }

        // ===== OPTIMIZED GOAL-ORIENTED TRACKING MODULE WITH TIME ANALYSIS START =====
        
        private void LogOptimizedDataForAnalysis(double macdValue, double macdAvg, double adx, double diPlus, double diMinus, 
                                               double emaValue, bool currentLongSignal, bool currentShortSignal, 
                                               bool longExit, bool shortExit)
        {
            try
            {
                DateTime barTime = Time[0];
                TimeSpan startTime = new TimeSpan(9, 30, 0);
                TimeSpan endTime = new TimeSpan(16, 0, 0);
                
                if (barTime.TimeOfDay < startTime || barTime.TimeOfDay >= endTime)
                    return;
                
                string instrumentName = Instrument.MasterInstrument.Name.Replace(" ", "_").Replace("/", "_");
                string chartTypeFolder = DetermineChartTypeFolder();
                string basePath = @"G:\My Drive\Trading\WIP\TrendSpotter\TSData";
                string folderPath = Path.Combine(basePath, $"TS {instrumentName} {chartTypeFolder}");
                
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                
                string dateString = barTime.ToString("yyyy-MM-dd");
                string fileName = $"TS_Optimized_{dateString}.csv";
                string filePath = Path.Combine(folderPath, fileName);
                
                if (currentLogFile != filePath)
                {
                    currentLogFile = filePath;
                    headerWritten = false;
                }
                
                if (!headerWritten)
                {
                    using (StreamWriter writer = new StreamWriter(filePath, false))
                    {
                        // CORE TRACKING COLUMNS (33 total) - Focused on Trading Goals + Time Analysis + Volume Filter
                        writer.WriteLine(
                            // Basic Bar Data (5 columns)
                            "DateTime,High,Low,Close,ATR," +
                            
                            // GOAL 1 & 4: Signal Quality & Win Rate (10 columns)  
                            "Signal_Strength_Score,LongEntry,ShortEntry,LongExit,ShortExit," +
                            "InTrend,Bars_In_Trend,Failed_Within_3_Bars," +
                            "Entry_Hour,Trade_Duration_Minutes," +  // TIME ANALYSIS COLUMNS
                            
                            // VOLUME FILTER ANALYSIS (6 columns)
                            "Volume_SMA,Current_Volume,Volume_Ratio,Volume_Filter_Passed," +
                            "Signals_Before_Volume,Signals_After_Volume," +  // VOLUME TRACKING
                            
                            // GOAL 2: Risk/Reward Tracking (4 columns)
                            "Entry_Price,Exit_Price,Max_Favorable,Max_Adverse," +
                            
                            // GOAL 3: Revenue Optimization (3 columns) 
                            "Trade_PnL,Revenue_Per_Signal,Cumulative_Revenue," +
                            
                            // KEY TECHNICAL INDICATORS (5 columns - minimal set)
                            "MACD_Value,ADX_Value,DI_Spread,Price_Above_EMA_Pct,Exit_Reason"
                        );
                    }
                    headerWritten = true;
                }
                
                // CALCULATE OPTIMIZED METRICS
                
                // Signal Strength Score (0-100) - Replaces individual condition tracking
                int signalScore = CalculateSignalStrengthScore(macdValue, macdAvg, adx, diPlus, diMinus, emaValue);
                
                // Revenue tracking
                double tradePnL = 0;
                double revenuePerSignal = 0;
                
                if (longExit || shortExit)
                {
                    tradePnL = CalculateTradePnL();
                    cumulativeRevenue += tradePnL;
                    revenuePerSignal = tradePnL; // For this specific trade
                }
                
                // Failed within 3 bars check (Goal 1 focus)
                bool failedWithin3Bars = (barsInTrend > 0 && barsInTrend <= 3 && (longExit || shortExit));
                
                // Calculate trade duration in minutes
                double tradeDurationMinutes = 0;
                if ((longExit || shortExit) && tradeEntryTime != DateTime.MinValue)
                {
                    TimeSpan duration = barTime - tradeEntryTime;
                    tradeDurationMinutes = duration.TotalMinutes;
                }
                
                // Volume filter tracking
                double volumeSMAValue = volumeSMA[0];
                double currentVolume = Volume[0];
                double volumeRatio = currentVolume / volumeSMAValue;
                bool volumeFilterPassed = volumeRatio >= VolumeFilterMultiplier;
                int signalsBeforeVolume = (longConditionCount == 6 || shortConditionCount == 6) ? 1 : 0;
                int signalsAfterVolume = ((longConditionCount == 6) && volumeFilterPassed || (shortConditionCount == 6) && volumeFilterPassed) ? 1 : 0;
                
                // Exit reason categorization
                string exitReason = "";
                if (longExit || shortExit)
                {
                    if (useOption3ForTimeframe)
                        exitReason = "ConditionFalse";
                    else if (macdMomentumLoss && adxWeakening)
                        exitReason = "MACD+ADX";
                    else if (macdMomentumLoss && diWeakening)
                        exitReason = "MACD+DI";
                    else
                        exitReason = "Other";
                }
                
                double priceAboveEMAPct = ((Close[0] - emaValue) / emaValue) * 100;
                double diSpread = Math.Abs(diPlus - diMinus);
                double atrValue = atr[0];
                
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    string logLine = $"{barTime:yyyy-MM-dd HH:mm:ss},{High[0]:F2},{Low[0]:F2},{Close[0]:F2},{atrValue:F2}," +
                                   $"{signalScore},{currentLongSignal},{currentShortSignal},{longExit},{shortExit}," +
                                   $"{(inLongTrend || inShortTrend)},{barsInTrend},{failedWithin3Bars}," +
                                   $"{entryHour},{tradeDurationMinutes:F1}," +  // TIME DATA
                                   $"{volumeSMAValue:F0},{currentVolume:F0},{volumeRatio:F2},{volumeFilterPassed}," +  // VOLUME DATA
                                   $"{signalsBeforeVolume},{signalsAfterVolume}," +  // VOLUME TRACKING
                                   $"{entryPrice:F2},{(longExit || shortExit ? Close[0] : 0):F2},{maxFavorableMove:F2},{maxAdverseMove:F2}," +
                                   $"{tradePnL:F2},{revenuePerSignal:F2},{cumulativeRevenue:F2}," +
                                   $"{macdValue:F4},{adx:F2},{diSpread:F2},{priceAboveEMAPct:F2},{exitReason}";
                    writer.WriteLine(logLine);
                }
            }
            catch (Exception ex)
            {
                Print($"Optimized data logging error: {ex.Message}");
            }
        }

        // SIGNAL STRENGTH SCORING SYSTEM
        private int CalculateSignalStrengthScore(double macdValue, double macdAvg, double adx, 
                                               double diPlus, double diMinus, double emaValue)
        {
            int score = 0;
            
            // Core conditions (60 points max)
            if (macdValue > macdAvg) score += 20;  // MACD cross
            if (CurrentBar > 0 && macdValue > (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1])) 
                score += 20;  // MACD momentum
            if (Math.Abs(diPlus - diMinus) > 10) score += 20;  // Strong DI spread
            
            // Quality multipliers (40 points max)
            if (adx > 25) score += 15;  // Strong trend
            if (macdValue > 0) score += 10;  // MACD above zero
            if ((Close[0] - emaValue) / emaValue > 0.001) score += 10;  // Good price position
            if (adx > dm.ADXPlot[1]) score += 5;  // ADX rising
            
            return Math.Min(score, 100);
        }

        // TRADE P&L CALCULATION
        private double CalculateTradePnL()
        {
            if (entryPrice == 0) return 0;
            
            double exitPrice = Close[0];
            double pointValue = Instrument.MasterInstrument.PointValue;
            
            if (inLongTrend)
                return (exitPrice - entryPrice) * pointValue;
            else if (inShortTrend)
                return (entryPrice - exitPrice) * pointValue;
            
            return 0;
        }
        
        // ===== OPTIMIZED GOAL-ORIENTED TRACKING MODULE WITH TIME ANALYSIS END =====
        
        private string DetermineChartTypeFolder()
        {
            try
            {
                string chartType = BarsPeriod.BarsPeriodType.ToString();
                int periodValue = BarsPeriod.Value;
                
                if ((int)BarsPeriod.BarsPeriodType == 12345)
                {
                    if (BarsPeriod.Value2 > 0)
                        return $"NR{BarsPeriod.Value}{BarsPeriod.Value2}";
                    else
                        return $"NR{BarsPeriod.Value}";
                }
                
                switch (BarsPeriod.BarsPeriodType)
                {
                    case BarsPeriodType.Minute:
                        return $"M{periodValue}";
                    case BarsPeriodType.Second:
                        return $"S{periodValue}";
                    case BarsPeriodType.Tick:
                        return $"{periodValue}T";
                    case BarsPeriodType.Volume:
                        return $"{periodValue}V";
                    case BarsPeriodType.Range:
                        return $"R{periodValue}";
                    case BarsPeriodType.Renko:
                        return $"R{(int)BarsPeriod.Value}";
                    default:
                        return chartType;
                }
            }
            catch (Exception ex)
            {
                Print($"Chart type detection error: {ex.Message}");
                return "Unknown";
            }
        }

        private Brush CreateBrushWithOpacity(Brush baseBrush, int opacity)
        {
            if (baseBrush is SolidColorBrush solidBrush)
            {
                Color color = solidBrush.Color;
                color.A = (byte)(255 * opacity / 100);
                return new SolidColorBrush(color);
            }
            return baseBrush;
        }

        #region Properties

        #region MACD Settings
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "MACD Fast", Order = 1, GroupName = "MACD Settings")]
        public int MacdFast { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "MACD Slow", Order = 2, GroupName = "MACD Settings")]
        public int MacdSlow { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "MACD Smooth", Order = 3, GroupName = "MACD Settings")]
        public int MacdSmooth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MACD MA Type", Order = 4, GroupName = "MACD Settings")]
        public CustomEnumNamespace.UniversalMovingAverage MacdMAType { get; set; }
        #endregion

        #region DM Settings
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "DM Period", Order = 1, GroupName = "DM Settings")]
        public int DmPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Rising Bars", Order = 2, GroupName = "DM Settings")]
        public int AdxRisingBars { get; set; }
        #endregion

        #region EMA Settings
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA Period", Order = 1, GroupName = "EMA Settings")]
        public int EmaPeriod { get; set; }
        #endregion

        #region Volume Filter Settings
        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name = "Volume Filter Multiplier", Order = 1, GroupName = "Volume Filter Settings")]
        public double VolumeFilterMultiplier { get; set; }
        #endregion

        #region Signal Settings
        [NinjaScriptProperty]
        [Display(Name = "Show Entry Signals", Order = 1, GroupName = "Signal Settings")]
        public bool ShowEntrySignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Exit Signals", Order = 2, GroupName = "Signal Settings")]
        public bool ShowExitSignals { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal Offset", Order = 3, GroupName = "Signal Settings")]
        public double Signal_Offset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", Order = 4, GroupName = "Signal Settings")]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short On", Order = 5, GroupName = "Signal Settings")]
        public string ShortOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Off", Order = 6, GroupName = "Signal Settings")]
        public string LongOff { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short Off", Order = 7, GroupName = "Signal Settings")]
        public string ShortOff { get; set; }
        #endregion

        #region Background Colors
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Partial Signal Color", Order = 1, GroupName = "Background Colors")]
        public Brush PartialSignalColor { get; set; }

        [Browsable(false)]
        public string PartialSignalColorSerializable
        {
            get { return Serialize.BrushToString(PartialSignalColor); }
            set { PartialSignalColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Partial Signal Opacity", Order = 2, GroupName = "Background Colors")]
        public int PartialSignalOpacity { get; set; }
        #endregion

        #region Entry Arrow Colors
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Entry Color", Order = 1, GroupName = "Entry Arrow Colors")]
        public Brush LongEntryColor { get; set; }

        [Browsable(false)]
        public string LongEntryColorSerializable
        {
            get { return Serialize.BrushToString(LongEntryColor); }
            set { LongEntryColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Short Entry Color", Order = 2, GroupName = "Entry Arrow Colors")]
        public Brush ShortEntryColor { get; set; }

        [Browsable(false)]
        public string ShortEntryColorSerializable
        {
            get { return Serialize.BrushToString(ShortEntryColor); }
            set { ShortEntryColor = Serialize.StringToBrush(value); }
        }
        #endregion

        #region Exit Signal Settings
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Exit Color", Order = 1, GroupName = "Exit Signal Settings")]
        public Brush ExitColor { get; set; }

        [Browsable(false)]
        public string ExitColorSerializable
        {
            get { return Serialize.BrushToString(ExitColor); }
            set { ExitColor = Serialize.StringToBrush(value); }
        }
        #endregion

        #region Data Tracking Settings
        [NinjaScriptProperty]
        [Display(Name = "Enable Data Tracking", Order = 1, GroupName = "Data Tracking Settings")]
        public bool EnableDataTracking { get; set; }
        #endregion

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.TrendSpotter[] cacheTrendSpotter;
		public Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Myindicators.TrendSpotter TrendSpotter(ISeries<double> input, int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			if (cacheTrendSpotter != null)
				for (int idx = 0; idx < cacheTrendSpotter.Length; idx++)
					if (cacheTrendSpotter[idx] != null && cacheTrendSpotter[idx].MacdFast == macdFast && cacheTrendSpotter[idx].MacdSlow == macdSlow && cacheTrendSpotter[idx].MacdSmooth == macdSmooth && cacheTrendSpotter[idx].MacdMAType == macdMAType && cacheTrendSpotter[idx].DmPeriod == dmPeriod && cacheTrendSpotter[idx].AdxRisingBars == adxRisingBars && cacheTrendSpotter[idx].EmaPeriod == emaPeriod && cacheTrendSpotter[idx].VolumeFilterMultiplier == volumeFilterMultiplier && cacheTrendSpotter[idx].ShowEntrySignals == showEntrySignals && cacheTrendSpotter[idx].ShowExitSignals == showExitSignals && cacheTrendSpotter[idx].Signal_Offset == signal_Offset && cacheTrendSpotter[idx].LongOn == longOn && cacheTrendSpotter[idx].ShortOn == shortOn && cacheTrendSpotter[idx].LongOff == longOff && cacheTrendSpotter[idx].ShortOff == shortOff && cacheTrendSpotter[idx].PartialSignalColor == partialSignalColor && cacheTrendSpotter[idx].PartialSignalOpacity == partialSignalOpacity && cacheTrendSpotter[idx].LongEntryColor == longEntryColor && cacheTrendSpotter[idx].ShortEntryColor == shortEntryColor && cacheTrendSpotter[idx].ExitColor == exitColor && cacheTrendSpotter[idx].EnableDataTracking == enableDataTracking && cacheTrendSpotter[idx].EqualsInput(input))
						return cacheTrendSpotter[idx];
			return CacheIndicator<Myindicators.TrendSpotter>(new Myindicators.TrendSpotter(){ MacdFast = macdFast, MacdSlow = macdSlow, MacdSmooth = macdSmooth, MacdMAType = macdMAType, DmPeriod = dmPeriod, AdxRisingBars = adxRisingBars, EmaPeriod = emaPeriod, VolumeFilterMultiplier = volumeFilterMultiplier, ShowEntrySignals = showEntrySignals, ShowExitSignals = showExitSignals, Signal_Offset = signal_Offset, LongOn = longOn, ShortOn = shortOn, LongOff = longOff, ShortOff = shortOff, PartialSignalColor = partialSignalColor, PartialSignalOpacity = partialSignalOpacity, LongEntryColor = longEntryColor, ShortEntryColor = shortEntryColor, ExitColor = exitColor, EnableDataTracking = enableDataTracking }, input, ref cacheTrendSpotter);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}
	}
}

#endregion
