///6-21 Fully optomized code and extensive tracking added.
///6-23 Updated to include partial setup colorcoding.
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
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
        private SMA volumeSMA;

        // For custom SMA-based MACD calculation
        private SMA fastSMA;
        private SMA slowSMA;
        private SMA signalSMA;
        private Series<double> macdLine;

        // Entry/exit condition flags
        private bool longCondition1, longCondition2, longCondition3, longCondition4, longCondition5, longCondition6;
        private bool shortCondition1, shortCondition2, shortCondition3, shortCondition4, shortCondition5, shortCondition6;
        private int longConditionCount, shortConditionCount;

        private bool wasLongSignal, wasShortSignal;
        private bool inLongTrend, inShortTrend;

        // Exit strategy variables
        private bool macdMomentumLoss;
        private bool adxWeakening;
        private bool diWeakening;
        private double previousDISpread, currentDISpread;
        private bool useOption3ForTimeframe;

        // Tracking variables for analysis
        private double entryPrice;
        private double maxFavorableMove;
        private double maxAdverseMove;
        private int barsInTrend;
        private static double cumulativeRevenue = 0;

        // Time tracking variables
        private DateTime tradeEntryTime;
        private int entryHour;

        // Volume filter variables
        private bool volumeConfirmed;

        // Optimized logging variables
        private List<string> logBuffer = new List<string>();
        private const int LogFlushBars = 50;
        private string currentLogFile;
        private bool headerWritten;

        // Performance optimization - cached values
        private double cachedVolSMAVal;
        private double cachedCurrentVolume;
        private double cachedMacdValue;
        private double cachedMacdAvg;
        private double cachedADX;
        private double cachedDIPlus;
        private double cachedDIMinus;
        private double cachedEMAValue;
        private double cachedATRValue;

        // Condition validation cache
        private bool conditionsCalculated;

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"TrendSpotter Trading Signal Indicator - Fully Optimized with Enhanced Performance & Complete Functionality";
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
                PartialLongSignalColor = Brushes.Green;
                PartialShortSignalColor = Brushes.Red;
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
                // Initialize built-in indicators
                dm = DM(DmPeriod);
                ema20 = EMA(EmaPeriod);
                atr = ATR(14);
                volumeSMA = SMA(Volume, 20);

                // Pre-instantiate SMA indicators if custom MACD (SMA based)
                if (MacdMAType != CustomEnumNamespace.UniversalMovingAverage.EMA)
                {
                    fastSMA = SMA(MacdFast);
                    slowSMA = SMA(MacdSlow);
                    macdLine = new Series<double>(this);
                    signalSMA = SMA(macdLine, MacdSmooth);
                }
                else
                {
                    macd = MACD(MacdFast, MacdSlow, MacdSmooth);
                }

                // Initialize tracking variables
                InitializeTrackingVariables();
            }
            else if (State == State.Terminated)
            {
                try
                {
                    FlushLogBuffer();
                }
                catch (Exception ex)
                {
                    Print($"Error flushing log buffer on termination: {ex.Message}");
                }
            }
        }

        private void InitializeTrackingVariables()
        {
            currentLogFile = string.Empty;
            headerWritten = false;
            entryPrice = 0;
            maxFavorableMove = 0;
            maxAdverseMove = 0;
            barsInTrend = 0;
            useOption3ForTimeframe = BarsPeriod.Value >= 5;
            tradeEntryTime = DateTime.MinValue;
            entryHour = 0;
            conditionsCalculated = false;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(MacdSlow, DmPeriod), EmaPeriod))
                return;

            // Cache all indicator values once per bar for performance
            CacheIndicatorValues();

            // Calculate all entry conditions once
            CalculateEntryConditions();

            // Volume filter confirmation (using cached values)
            volumeConfirmed = cachedCurrentVolume > cachedVolSMAVal * VolumeFilterMultiplier;

            // Current signals including volume filter
            bool currentLongSignal = (longConditionCount == 6) && volumeConfirmed;
            bool currentShortSignal = (shortConditionCount == 6) && volumeConfirmed;

            // Calculate exit conditions and determine exit signals
            bool longExit, shortExit;
            CalculateExitSignals(out longExit, out shortExit);

            // Update performance tracking during trends
            UpdatePerformanceTracking();

            // Update trend status with optimized entry tracking
            UpdateTrendStatus(currentLongSignal, currentShortSignal, longExit, shortExit);

            // Draw signals with optimized drawing logic
            DrawSignalsOptimized(currentLongSignal, currentShortSignal, longExit, shortExit);

            // Optimized buffered data logging
            if (EnableDataTracking)
            {
                BufferOptimizedDataForAnalysis(currentLongSignal, currentShortSignal, longExit, shortExit);
            }

            // Update signal tracking flags efficiently
            UpdateSignalFlags(currentLongSignal, currentShortSignal, longExit, shortExit);
        }

        private void CacheIndicatorValues()
        {
            // Cache volume values to reduce repeated access
            cachedVolSMAVal = volumeSMA[0];
            cachedCurrentVolume = Volume[0];

            // Cache MACD values
            if (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA)
            {
                cachedMacdValue = macd.Default[0];
                cachedMacdAvg = macd.Avg[0];
            }
            else
            {
                fastSMA.Update();
                slowSMA.Update();
                macdLine[0] = fastSMA[0] - slowSMA[0];

                if (CurrentBar >= MacdSmooth - 1)
                {
                    signalSMA.Update();
                    cachedMacdAvg = signalSMA[0];
                }
                else
                {
                    cachedMacdAvg = macdLine[0];
                }
                cachedMacdValue = macdLine[0];
            }

            // Cache DM and other indicator values
            cachedADX = dm.ADXPlot[0];
            cachedDIPlus = dm.DiPlus[0];
            cachedDIMinus = dm.DiMinus[0];
            cachedEMAValue = ema20[0];
            cachedATRValue = atr[0];

            // Calculate DI Spread for exit conditions
            currentDISpread = Math.Abs(cachedDIPlus - cachedDIMinus);
            if (CurrentBar > 0)
                previousDISpread = Math.Abs(dm.DiPlus[1] - dm.DiMinus[1]);
        }

        private void CalculateEntryConditions()
        {
            // Check if ADX is rising over specified bars (optimized)
            bool adxRising = IsADXRising();

            // 6-Condition Entry System using cached values
            longCondition1 = cachedMacdValue > 0;
            longCondition2 = cachedMacdValue > cachedMacdAvg;
            longCondition3 = CurrentBar > 0 ? cachedMacdValue > GetPreviousMACDValue() : true;
            longCondition4 = adxRising;
            longCondition5 = cachedDIPlus > cachedDIMinus;
            longCondition6 = Close[0] > cachedEMAValue;

            shortCondition1 = cachedMacdValue < 0;
            shortCondition2 = cachedMacdValue < cachedMacdAvg;
            shortCondition3 = CurrentBar > 0 ? cachedMacdValue < GetPreviousMACDValue() : true;
            shortCondition4 = adxRising;
            shortCondition5 = cachedDIMinus > cachedDIPlus;
            shortCondition6 = Close[0] < cachedEMAValue;

            // Optimized condition counting
            longConditionCount = CountConditions(longCondition1, longCondition2, longCondition3, 
                                               longCondition4, longCondition5, longCondition6);
            shortConditionCount = CountConditions(shortCondition1, shortCondition2, shortCondition3, 
                                                shortCondition4, shortCondition5, shortCondition6);

            conditionsCalculated = true;
        }

        private bool IsADXRising()
        {
            if (CurrentBar < AdxRisingBars) return true;

            for (int i = 1; i <= AdxRisingBars; i++)
            {
                if (cachedADX <= dm.ADXPlot[i])
                    return false;
            }
            return true;
        }

        private double GetPreviousMACDValue()
        {
            return MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA 
                ? macd.Default[1] 
                : macdLine[1];
        }

        private int CountConditions(bool c1, bool c2, bool c3, bool c4, bool c5, bool c6)
        {
            return (c1 ? 1 : 0) + (c2 ? 1 : 0) + (c3 ? 1 : 0) + (c4 ? 1 : 0) + (c5 ? 1 : 0) + (c6 ? 1 : 0);
        }

        private void CalculateExitSignals(out bool longExit, out bool shortExit)
        {
            if (useOption3ForTimeframe)
            {
                // Option 3: Exit when any of the 6 conditions becomes false
                bool anyLongFalse = !longCondition1 || !longCondition2 || !longCondition3 || 
                                   !longCondition4 || !longCondition5 || !longCondition6;
                bool anyShortFalse = !shortCondition1 || !shortCondition2 || !shortCondition3 || 
                                    !shortCondition4 || !shortCondition5 || !shortCondition6;

                longExit = inLongTrend && anyLongFalse;
                shortExit = inShortTrend && anyShortFalse;
            }
            else
            {
                // Option 2: Multi-Condition Exit Strategy
                CalculateExitConditions();
                longExit = inLongTrend && (macdMomentumLoss && (adxWeakening || diWeakening));
                shortExit = inShortTrend && (macdMomentumLoss && (adxWeakening || diWeakening));
            }
        }

        private void CalculateExitConditions()
        {
            // MACD Momentum Check: Current MACD ≤ Previous MACD for 2 consecutive bars
            macdMomentumLoss = false;
            if (CurrentBar >= 2)
            {
                double prevMacd1 = MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA 
                    ? macd.Default[1] : macdLine[1];
                double prevMacd2 = MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA 
                    ? macd.Default[2] : macdLine[2];

                macdMomentumLoss = (cachedMacdValue <= prevMacd1) && (prevMacd1 <= prevMacd2);
            }

            // ADX and DI weakening checks
            adxWeakening = CurrentBar >= 1 && cachedADX < dm.ADXPlot[1];
            diWeakening = CurrentBar >= 1 && currentDISpread < previousDISpread;
        }

        private void UpdatePerformanceTracking()
        {
            if (inLongTrend || inShortTrend)
            {
                barsInTrend++;

                double currentMove = inLongTrend ? Close[0] - entryPrice : entryPrice - Close[0];

                if (currentMove > maxFavorableMove)
                    maxFavorableMove = currentMove;
                if (currentMove < maxAdverseMove)
                    maxAdverseMove = currentMove;
            }
        }

        private void UpdateTrendStatus(bool currentLongSignal, bool currentShortSignal, bool longExit, bool shortExit)
        {
            if (currentLongSignal && !inLongTrend)
            {
                SetLongTrendEntry();
            }
            else if (currentShortSignal && !inShortTrend)
            {
                SetShortTrendEntry();
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
        }

        private void SetLongTrendEntry()
        {
            inLongTrend = true;
            inShortTrend = false;
            SetEntryTracking(Close[0], Time[0]);
        }

        private void SetShortTrendEntry()
        {
            inShortTrend = true;
            inLongTrend = false;
            SetEntryTracking(Close[0], Time[0]);
        }

        private void SetEntryTracking(double price, DateTime time)
        {
            entryPrice = price;
            maxFavorableMove = 0;
            maxAdverseMove = 0;
            barsInTrend = 1;
            tradeEntryTime = time;
            entryHour = time.Hour;
        }

        private void DrawSignalsOptimized(bool currentLongSignal, bool currentShortSignal, bool longExit, bool shortExit)
        {
            if (ShowEntrySignals)
            {
                // Entry arrows - only on first bar of new signal
                if (currentLongSignal && !wasLongSignal)
                {
                    Draw.ArrowUp(this, LongOn + CurrentBar.ToString(), true, 0, 
                               Low[0] - Signal_Offset * TickSize, LongEntryColor);
                }
                else if (currentShortSignal && !wasShortSignal)
                {
                    Draw.ArrowDown(this, ShortOn + CurrentBar.ToString(), true, 0, 
                                 High[0] + Signal_Offset * TickSize, ShortEntryColor);
                }

                // Optimized background color logic
                SetBackgroundColor();
            }

            if (ShowExitSignals && CurrentBar > 1)
            {
                if (longExit)
                {
                    Draw.Text(this, LongOff + CurrentBar, true, "o", 0, 
                             High[0] + Signal_Offset * TickSize, 0, ExitColor,
                             new SimpleFont("Arial", 12), System.Windows.TextAlignment.Center, 
                             Brushes.Transparent, Brushes.Transparent, 0);
                }
                if (shortExit)
                {
                    Draw.Text(this, ShortOff + CurrentBar, true, "o", 0, 
                             Low[0] - Signal_Offset * TickSize, 0, ExitColor,
                             new SimpleFont("Arial", 12), System.Windows.TextAlignment.Center, 
                             Brushes.Transparent, Brushes.Transparent, 0);
                }
            }
        }

        private void SetBackgroundColor()
        {
            if (!inLongTrend && !inShortTrend)
            {
                if (longConditionCount == 5)
                {
                    BackBrush = CreateBrushWithOpacity(PartialLongSignalColor, PartialSignalOpacity);
                }
                else if (shortConditionCount == 5)
                {
                    BackBrush = CreateBrushWithOpacity(PartialShortSignalColor, PartialSignalOpacity);
                }
                else
                {
                    BackBrush = Brushes.Transparent;
                }
            }
            else
            {
                BackBrush = Brushes.Transparent;
            }
        }

        private void UpdateSignalFlags(bool currentLongSignal, bool currentShortSignal, bool longExit, bool shortExit)
        {
            if (longExit || shortExit)
            {
                wasLongSignal = false;
                wasShortSignal = false;
            }
            else if (inLongTrend || inShortTrend)
            {
                wasLongSignal = inLongTrend ? true : wasLongSignal;
                wasShortSignal = inShortTrend ? true : wasShortSignal;
            }
            else
            {
                wasLongSignal = currentLongSignal;
                wasShortSignal = currentShortSignal;
            }
        }

        private void BufferOptimizedDataForAnalysis(bool currentLongSignal, bool currentShortSignal,
                                                   bool longExit, bool shortExit)
        {
            try
            {
                DateTime barTime = Time[0];
                
                // Quick time filter check
                if (!IsWithinTradingHours(barTime))
                    return;

                // Determine file path efficiently
                string filePath = GetOptimizedLogFilePath(barTime);
                if (string.IsNullOrEmpty(filePath))
                    return;

                // Handle file change and header
                if (currentLogFile != filePath)
                {
                    FlushLogBuffer();
                    currentLogFile = filePath;
                    headerWritten = false;
                }

                // Write header if needed
                if (!headerWritten)
                {
                    WriteOptimizedHeader();
                    headerWritten = true;
                }

                // Create optimized log entry
                string logEntry = CreateOptimizedLogEntry(barTime, currentLongSignal, currentShortSignal, 
                                                        longExit, shortExit);
                
                logBuffer.Add(logEntry);

                // Flush buffer when it reaches capacity
                if (logBuffer.Count >= LogFlushBars)
                    FlushLogBuffer();
            }
            catch (Exception ex)
            {
                Print($"Optimized data logging error: {ex.Message}");
            }
        }

        private bool IsWithinTradingHours(DateTime barTime)
        {
            TimeSpan timeOfDay = barTime.TimeOfDay;
            return timeOfDay >= new TimeSpan(9, 30, 0) && timeOfDay < new TimeSpan(16, 0, 0);
        }

        private string GetOptimizedLogFilePath(DateTime barTime)
        {
            string instrumentName = Instrument.MasterInstrument.Name.Replace(" ", "_").Replace("/", "_");
            string chartTypeFolder = DetermineChartTypeFolder();
                string basePath = @"G:\My Drive\Trading\Data\TrendSpotterData";
                string folderPath = Path.Combine(basePath, $"TS {instrumentName} {chartTypeFolder}");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string dateString = barTime.ToString("yyyy-MM-dd");
            return Path.Combine(folderPath, $"TS_Optimized_{dateString}.csv");
        }

        private void WriteOptimizedHeader()
        {
            using (StreamWriter writer = new StreamWriter(currentLogFile, false))
            {
                writer.WriteLine(
                    "DateTime,High,Low,Close,ATR," +
                    "Signal_Strength_Score,LongEntry,ShortEntry,LongExit,ShortExit," +
                    "InTrend,Bars_In_Trend,Failed_Within_3_Bars," +
                    "Entry_Hour,Trade_Duration_Minutes," +
                    "Volume_SMA,Current_Volume,Volume_Ratio,Volume_Filter_Passed," +
                    "Signals_Before_Volume,Signals_After_Volume," +
                    "Entry_Price,Exit_Price,Max_Favorable,Max_Adverse," +
                    "Trade_PnL,Revenue_Per_Signal,Cumulative_Revenue," +
                    "MACD_Value,ADX_Value,DI_Spread,Price_Above_EMA_Pct,Exit_Reason"
                );
            }
        }

        private string CreateOptimizedLogEntry(DateTime barTime, bool currentLongSignal, bool currentShortSignal,
                                             bool longExit, bool shortExit)
        {
            // Calculate metrics efficiently using cached values
            int signalScore = CalculateSignalStrengthScore();
            double tradePnL = 0;
            double revenuePerSignal = 0;

            if (longExit || shortExit)
            {
                tradePnL = CalculateTradePnL();
                cumulativeRevenue += tradePnL;
                revenuePerSignal = tradePnL;
            }

            bool failedWithin3Bars = (barsInTrend > 0 && barsInTrend <= 3 && (longExit || shortExit));
            double tradeDurationMinutes = CalculateTradeDuration(barTime);
            double volumeRatio = cachedCurrentVolume / cachedVolSMAVal;
            bool volumeFilterPassed = volumeRatio >= VolumeFilterMultiplier;
            
            int signalsBeforeVolume = (longConditionCount == 6 || shortConditionCount == 6) ? 1 : 0;
            int signalsAfterVolume = ((longConditionCount == 6 && volumeFilterPassed) || 
                                     (shortConditionCount == 6 && volumeFilterPassed)) ? 1 : 0;

            string exitReason = DetermineExitReason(longExit, shortExit);
            double priceAboveEMAPct = ((Close[0] - cachedEMAValue) / cachedEMAValue) * 100;

            return $"{barTime:yyyy-MM-dd HH:mm:ss},{High[0]:F2},{Low[0]:F2},{Close[0]:F2},{cachedATRValue:F2}," +
                   $"{signalScore},{currentLongSignal},{currentShortSignal},{longExit},{shortExit}," +
                   $"{(inLongTrend || inShortTrend)},{barsInTrend},{failedWithin3Bars}," +
                   $"{entryHour},{tradeDurationMinutes:F1}," +
                   $"{cachedVolSMAVal:F0},{cachedCurrentVolume:F0},{volumeRatio:F2},{volumeFilterPassed}," +
                   $"{signalsBeforeVolume},{signalsAfterVolume}," +
                   $"{entryPrice:F2},{(longExit || shortExit ? Close[0] : 0):F2},{maxFavorableMove:F2},{maxAdverseMove:F2}," +
                   $"{tradePnL:F2},{revenuePerSignal:F2},{cumulativeRevenue:F2}," +
                   $"{cachedMacdValue:F4},{cachedADX:F2},{currentDISpread:F2},{priceAboveEMAPct:F2},{exitReason}";
        }

        private double CalculateTradeDuration(DateTime barTime)
        {
            if (tradeEntryTime == DateTime.MinValue) return 0;
            return (barTime - tradeEntryTime).TotalMinutes;
        }

        private string DetermineExitReason(bool longExit, bool shortExit)
        {
            if (!longExit && !shortExit) return "";

            if (useOption3ForTimeframe)
                return "ConditionFalse";
            else if (macdMomentumLoss && adxWeakening)
                return "MACD+ADX";
            else if (macdMomentumLoss && diWeakening)
                return "MACD+DI";
            else
                return "Other";
        }

        private void FlushLogBuffer()
        {
            try
            {
                if (logBuffer.Count == 0 || string.IsNullOrEmpty(currentLogFile))
                    return;

                File.AppendAllLines(currentLogFile, logBuffer);
                logBuffer.Clear();
            }
            catch (Exception ex)
            {
                Print($"Error flushing log buffer: {ex.Message}");
            }
        }

        private int CalculateSignalStrengthScore()
        {
            int score = 0;

            // Core conditions (using cached values)
            if (cachedMacdValue > cachedMacdAvg) score += 20;
            if (CurrentBar > 0 && cachedMacdValue > GetPreviousMACDValue()) score += 20;
            if (currentDISpread > 10) score += 20;

            // Quality multipliers
            if (cachedADX > 25) score += 15;
            if (cachedMacdValue > 0) score += 10;
            if ((Close[0] - cachedEMAValue) / cachedEMAValue > 0.001) score += 10;
            if (cachedADX > dm.ADXPlot[1]) score += 5;

            return Math.Min(score, 100);
        }

        private double CalculateTradePnL()
        {
            if (entryPrice == 0) return 0;

            double exitPrice = Close[0];
            double pointValue = Instrument.MasterInstrument.PointValue;

            if (inLongTrend)
                return (exitPrice - entryPrice) * pointValue;
            if (inShortTrend)
                return (entryPrice - exitPrice) * pointValue;

            return 0;
        }

        private string DetermineChartTypeFolder()
        {
            try
            {
                var chartType = BarsPeriod.BarsPeriodType.ToString();
                int periodValue = BarsPeriod.Value;

                // Handle custom chart types
                if ((int)BarsPeriod.BarsPeriodType == 12345)
                {
                    return BarsPeriod.Value2 > 0 ? $"NR{BarsPeriod.Value}{BarsPeriod.Value2}" : $"NR{BarsPeriod.Value}";
                }

                return BarsPeriod.BarsPeriodType switch
                {
                    BarsPeriodType.Minute => $"M{periodValue}",
                    BarsPeriodType.Second => $"S{periodValue}",
                    BarsPeriodType.Tick => $"{periodValue}T",
                    BarsPeriodType.Volume => $"{periodValue}V",
                    BarsPeriodType.Range => $"R{periodValue}",
                    BarsPeriodType.Renko => $"R{(int)BarsPeriod.Value}",
                    _ => chartType
                };
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

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "DM Period", Order = 1, GroupName = "DM Settings")]
        public int DmPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Rising Bars", Order = 2, GroupName = "DM Settings")]
        public int AdxRisingBars { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA Period", Order = 1, GroupName = "EMA Settings")]
        public int EmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name = "Volume Filter Multiplier", Order = 1, GroupName = "Volume Filter Settings")]
        public double VolumeFilterMultiplier { get; set; }

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

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Partial Long Signal Color", Order = 1, GroupName = "Background Colors")]
        public Brush PartialLongSignalColor { get; set; }

        [Browsable(false)]
        public string PartialLongSignalColorSerializable
        {
            get { return Serialize.BrushToString(PartialLongSignalColor); }
            set { PartialLongSignalColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Partial Short Signal Color", Order = 2, GroupName = "Background Colors")]
        public Brush PartialShortSignalColor { get; set; }

        [Browsable(false)]
        public string PartialShortSignalColorSerializable
        {
            get { return Serialize.BrushToString(PartialShortSignalColor); }
            set { PartialShortSignalColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Partial Signal Opacity", Order = 3, GroupName = "Background Colors")]
        public int PartialSignalOpacity { get; set; }

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

        [NinjaScriptProperty]
        [Display(Name = "Enable Data Tracking", Order = 1, GroupName = "Data Tracking Settings")]
        public bool EnableDataTracking { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.TrendSpotter[] cacheTrendSpotter;
		public Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Myindicators.TrendSpotter TrendSpotter(ISeries<double> input, int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			if (cacheTrendSpotter != null)
				for (int idx = 0; idx < cacheTrendSpotter.Length; idx++)
					if (cacheTrendSpotter[idx] != null && cacheTrendSpotter[idx].MacdFast == macdFast && cacheTrendSpotter[idx].MacdSlow == macdSlow && cacheTrendSpotter[idx].MacdSmooth == macdSmooth && cacheTrendSpotter[idx].MacdMAType == macdMAType && cacheTrendSpotter[idx].DmPeriod == dmPeriod && cacheTrendSpotter[idx].AdxRisingBars == adxRisingBars && cacheTrendSpotter[idx].EmaPeriod == emaPeriod && cacheTrendSpotter[idx].VolumeFilterMultiplier == volumeFilterMultiplier && cacheTrendSpotter[idx].ShowEntrySignals == showEntrySignals && cacheTrendSpotter[idx].ShowExitSignals == showExitSignals && cacheTrendSpotter[idx].Signal_Offset == signal_Offset && cacheTrendSpotter[idx].LongOn == longOn && cacheTrendSpotter[idx].ShortOn == shortOn && cacheTrendSpotter[idx].LongOff == longOff && cacheTrendSpotter[idx].ShortOff == shortOff && cacheTrendSpotter[idx].PartialLongSignalColor == partialLongSignalColor && cacheTrendSpotter[idx].PartialShortSignalColor == partialShortSignalColor && cacheTrendSpotter[idx].PartialSignalOpacity == partialSignalOpacity && cacheTrendSpotter[idx].LongEntryColor == longEntryColor && cacheTrendSpotter[idx].ShortEntryColor == shortEntryColor && cacheTrendSpotter[idx].ExitColor == exitColor && cacheTrendSpotter[idx].EnableDataTracking == enableDataTracking && cacheTrendSpotter[idx].EqualsInput(input))
						return cacheTrendSpotter[idx];
			return CacheIndicator<Myindicators.TrendSpotter>(new Myindicators.TrendSpotter(){ MacdFast = macdFast, MacdSlow = macdSlow, MacdSmooth = macdSmooth, MacdMAType = macdMAType, DmPeriod = dmPeriod, AdxRisingBars = adxRisingBars, EmaPeriod = emaPeriod, VolumeFilterMultiplier = volumeFilterMultiplier, ShowEntrySignals = showEntrySignals, ShowExitSignals = showExitSignals, Signal_Offset = signal_Offset, LongOn = longOn, ShortOn = shortOn, LongOff = longOff, ShortOff = shortOff, PartialLongSignalColor = partialLongSignalColor, PartialShortSignalColor = partialShortSignalColor, PartialSignalOpacity = partialSignalOpacity, LongEntryColor = longEntryColor, ShortEntryColor = shortEntryColor, ExitColor = exitColor, EnableDataTracking = enableDataTracking }, input, ref cacheTrendSpotter);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}
	}
}

#endregion
