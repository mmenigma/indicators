///6-19 Ultra-Optimized TrendSpotter with Split Performance/Optimization Tracking
///6-28 Performance CSV: Trade-only logging (minimal overhead)
///6-28 Optimization CSV: Optional detailed analysis (can be disabled)
///6-28 added partial signal toggle
///7-3 DUPLICATE PREVENTION FIX ADDED
///7-11 SIGNAL QUALITY FILTER IMPLEMENTATION ADDED
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
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
        private int exitHour;

        // Volume filter variables
        private bool volumeConfirmed;

        // Optimization tracking variables (NEW)
        private int signalDurationBars;
        private int recentWinStreak;
        private string lastMarketRegime;
		private TradeEvent currentTradeEntry;

        // Signal Quality Filter Variables
        private bool enableSignalQualityFilter = true;  // Will be controlled by property
        private int signalQualityThreshold = 45;        // Will be controlled by property

        // Ultra-Lightweight Logging Variables
        private Queue<TradeEvent> performanceBuffer = new Queue<TradeEvent>(5);
        private Queue<OptimizationEvent> optimizationBuffer = new Queue<OptimizationEvent>(20);
        private string performanceLogFile;
        private string optimizationLogFile;
        private bool performanceHeaderWritten;
        private bool optimizationHeaderWritten;

        // DUPLICATE PREVENTION VARIABLES
        private bool isLoggingInitialized = false;
        
        // Cached instrument name (compute once)
        private string cachedInstrumentName;
        private string cachedChartType;

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
                Description = @"TrendSpotter Trading Signal Indicator - Ultra-Optimized with Signal Quality Filter";
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
				MinAdxThreshold = 18;

                // EMA Settings
                EmaPeriod = 20;

                // Signal Settings
                ShowEntrySignals = true;
                ShowExitSignals = true;
                ShowPartialSignals = true;  // Default ON, can be disabled for performance
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

                // Split Data Tracking (Both Optional)
                EnablePerformanceTracking = true;
                EnableOptimizationTracking = false;  // Default OFF for live trading
				WriteCSVHeaders = false;  // Default OFF to prevent header issues

                // Signal Quality Filter Settings
                EnableSignalQualityFilter = true;   // Default ON for optimization
                SignalQualityThreshold = 45;        // Optimal threshold from analysis
				
				// ADD TRADING HOURS DEFAULTS HERE:
				TradingStartTime = new TimeSpan(0, 0, 0);     // Shows as "12:00 AM"
				TradingEndTime = new TimeSpan(23, 59, 0);     // Shows as "11:59 PM"

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
                
                // Cache expensive computations
                InitializeCachedValues();
            }
            else if (State == State.Terminated)
            {
                try
                {
                    FlushAllBuffers();
                }
                catch (Exception ex)
                {
                    Print($"Error flushing buffers on termination: {ex.Message}");
                }
            }
        }

        private void InitializeTrackingVariables()
        {
            performanceLogFile = string.Empty;
            optimizationLogFile = string.Empty;
            performanceHeaderWritten = false;
            optimizationHeaderWritten = false;
            entryPrice = 0;
            maxFavorableMove = 0;
            maxAdverseMove = 0;
            barsInTrend = 0;
            useOption3ForTimeframe = BarsPeriod.Value >= 5;
            tradeEntryTime = DateTime.MinValue;
            entryHour = 0;
            exitHour = 0;
            conditionsCalculated = false;
            
            // Initialize new optimization tracking variables
            signalDurationBars = 0;
            recentWinStreak = 0;
            lastMarketRegime = "Unknown";
			currentTradeEntry = null;
        }

        private void InitializeCachedValues()
        {
            cachedInstrumentName = Instrument.MasterInstrument.Name.Replace(" ", "_").Replace("/", "_");
            cachedChartType = DetermineChartTypeFolder();
        }

        // DUPLICATE PREVENTION INITIALIZATION
        private void InitializeLogging()
        {
            if (isLoggingInitialized)
                return;

            isLoggingInitialized = true;
        }

        private bool IsTradeAlreadyInFile(DateTime entryTime)
        {
            try
            {
                if (!File.Exists(performanceLogFile))
                    return false;

                string entryTimeStr = entryTime.ToString("yyyy-MM-dd HH:mm:ss");
                string[] lines = File.ReadAllLines(performanceLogFile);
                
                foreach (string line in lines)
                {
                    if (line.Contains(entryTimeStr))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnBarUpdate()
        {
            // DUPLICATE PREVENTION - Initialize logging once
            if (!isLoggingInitialized)
                InitializeLogging();

            if (CurrentBar < Math.Max(Math.Max(MacdSlow, DmPeriod), EmaPeriod))
                return;

            // Cache all indicator values once per bar for performance
            CacheIndicatorValues();

            // Calculate all entry conditions once
            CalculateEntryConditions();

            // Volume filter confirmation (using cached values)
            volumeConfirmed = cachedCurrentVolume > cachedVolSMAVal * VolumeFilterMultiplier;

            // Calculate 6-condition signals
            bool rawLongSignal = (longConditionCount == 6) && volumeConfirmed;
            bool rawShortSignal = (shortConditionCount == 6) && volumeConfirmed;

            // SIGNAL QUALITY FILTER APPLICATION
            bool signalPassedFilter = true;
            string rejectionReason = "Accepted";
            int signalQuality = CalculateSignalStrengthScore();
            
            if (EnableSignalQualityFilter && (rawLongSignal || rawShortSignal))
            {
                if (signalQuality < SignalQualityThreshold)
                {
                    signalPassedFilter = false;
                    rejectionReason = $"Quality {signalQuality} < {SignalQualityThreshold}";
                }
            }

            // Apply filter to signals
            bool currentLongSignal = rawLongSignal && signalPassedFilter;
            bool currentShortSignal = rawShortSignal && signalPassedFilter;

            // Calculate exit conditions and determine exit signals
            bool longExit, shortExit;
            CalculateExitSignals(out longExit, out shortExit);

            // Update performance tracking during trends
            UpdatePerformanceTracking();

            // ENHANCED LOGGING: Include filter information
            LogTradeEventsWithFilter(currentLongSignal, currentShortSignal, longExit, shortExit, 
                                   rawLongSignal, rawShortSignal, signalQuality, signalPassedFilter, rejectionReason);

            // Update trend status with filtered signals
            UpdateTrendStatus(currentLongSignal, currentShortSignal, longExit, shortExit);

            // Draw signals with filtered logic
            DrawSignalsOptimized(currentLongSignal, currentShortSignal, longExit, shortExit);

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
            // Modified ADX logic with consecutive rising and threshold
			bool adxRising = true;
			bool adxAboveThreshold = cachedADX >= MinAdxThreshold;
			
			if (CurrentBar >= AdxRisingBars)
			{
			    // Check for consecutive rising bars
			    for (int i = 0; i < AdxRisingBars; i++)
			    {
			        if (dm.ADXPlot[i] <= dm.ADXPlot[i + 1])
			        {
			            adxRising = false;
			            break;
			        }
			    }
			}
			
			// Update conditions to include both checks
			bool adxConditionMet = adxRising && adxAboveThreshold;

            // 6-Condition Entry System using cached values
            longCondition1 = cachedMacdValue > 0;
            longCondition2 = cachedMacdValue > cachedMacdAvg;
            longCondition3 = CurrentBar > 0 ? cachedMacdValue > GetPreviousMACDValue() : true;
            longCondition4 = adxConditionMet;
            longCondition5 = cachedDIPlus > cachedDIMinus;
            longCondition6 = Close[0] > cachedEMAValue;

            shortCondition1 = cachedMacdValue < 0;
            shortCondition2 = cachedMacdValue < cachedMacdAvg;
            shortCondition3 = CurrentBar > 0 ? cachedMacdValue < GetPreviousMACDValue() : true;
            shortCondition4 = adxConditionMet;
            shortCondition5 = cachedDIMinus > cachedDIPlus;
            shortCondition6 = Close[0] < cachedEMAValue;

            // Optimized condition counting
            longConditionCount = CountConditions(longCondition1, longCondition2, longCondition3, 
                                               longCondition4, longCondition5, longCondition6);
            shortConditionCount = CountConditions(shortCondition1, shortCondition2, shortCondition3, 
                                                shortCondition4, shortCondition5, shortCondition6);

            conditionsCalculated = true;
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
                exitHour = Time[0].Hour;  // Capture exit hour
                inLongTrend = false;
                barsInTrend = 0;
            }
            else if (shortExit)
            {
                exitHour = Time[0].Hour;  // Capture exit hour
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

        #region ENHANCED LOGGING WITH SIGNAL QUALITY FILTER

        // Updated logging function with filter tracking
        private void LogTradeEventsWithFilter(bool currentLongSignal, bool currentShortSignal, 
                                            bool longExit, bool shortExit,
                                            bool rawLongSignal, bool rawShortSignal,
                                            int signalQuality, bool signalAccepted, string rejectionReason)
        {
            // Performance tracking: Only log actual trade events (filtered signals)
            if (EnablePerformanceTracking)
            {
                if (currentLongSignal && !wasLongSignal)
                    LogPerformanceEntry("Long");
                else if (currentShortSignal && !wasShortSignal)
                    LogPerformanceEntry("Short");
                else if (longExit || shortExit)
                    LogPerformanceExit();
            }

            // Optimization tracking: Log all signal events (including rejected)
            if (EnableOptimizationTracking && (rawLongSignal || rawShortSignal || longExit || shortExit))
            {
                LogOptimizationDataWithFilter(currentLongSignal, currentShortSignal, longExit, shortExit,
                                            signalQuality, signalAccepted, rejectionReason);
            }
        }

				private void LogPerformanceEntry(string tradeType)
			{
			    try
			    {
			        if (!IsWithinTradingHours(Time[0])) return;
			
			        EnsurePerformanceLogFile();
			
			        // Store entry data instead of immediately logging
			        currentTradeEntry = new TradeEvent
			        {
			            Instrument = cachedInstrumentName,
			            ChartType = cachedChartType,
			            ChartPeriod = BarsPeriod.Value,
			            EntryDateTime = Time[0],
			            EntryPrice = Close[0],
			            TradeType = tradeType,
			            EntryHour = Time[0].Hour,
			            SignalStrengthScore = CalculateSignalStrengthScore(),
			            IsEntry = true
			        };
			
			        // Don't log yet - wait for exit
			    }
			    catch (Exception ex)
			    {
			        Print($"Performance entry logging error: {ex.Message}");
			    }
			}

				private void LogPerformanceExit()
				{
				    try
				    {
				        if (!IsWithinTradingHours(Time[0]) || currentTradeEntry == null) return;

				        EnsurePerformanceLogFile();

				        // DUPLICATE PREVENTION - Check if this trade already exists
				        if (IsTradeAlreadyInFile(currentTradeEntry.EntryDateTime))
				        {
				            currentTradeEntry = null;
				            return;
				        }
				
				        double tradePnL = CalculateTradePnL();
				        cumulativeRevenue += tradePnL;
				
				        // Create complete trade row combining entry and exit data
				        var completeTrade = new TradeEvent
				        {
				            // Entry data from stored entry
				            Instrument = currentTradeEntry.Instrument,
				            ChartType = currentTradeEntry.ChartType,
				            ChartPeriod = currentTradeEntry.ChartPeriod,
				            EntryDateTime = currentTradeEntry.EntryDateTime,
				            EntryPrice = currentTradeEntry.EntryPrice,
				            TradeType = currentTradeEntry.TradeType,
				            EntryHour = currentTradeEntry.EntryHour,
				            SignalStrengthScore = currentTradeEntry.SignalStrengthScore,
				            
				            // Exit data from current bar
				            ExitDateTime = Time[0],
				            ExitPrice = Close[0],
				            TradePnL = tradePnL,
				            MaxFavorableExcursion = maxFavorableMove,
				            MaxAdverseExcursion = maxAdverseMove,
				            TradeDurationMinutes = CalculateTradeDuration(),
				            ExitHour = Time[0].Hour,
				            ExitReason = DetermineExitReason(),
				            IsEntry = false // Flag as complete trade
				        };
				
				        performanceBuffer.Enqueue(completeTrade);
				        FlushPerformanceBuffer();
				
				        // Clear stored entry data
				        currentTradeEntry = null;
				    }
				    catch (Exception ex)
				    {
				        Print($"Performance exit logging error: {ex.Message}");
				    }
				}

        // Enhanced optimization logging with streamlined fields and filter data
        private void LogOptimizationDataWithFilter(bool longEntry, bool shortEntry, bool longExit, bool shortExit,
                                                 int signalQuality, bool signalAccepted, string rejectionReason)
        {
            try
            {
                if (!IsWithinTradingHours(Time[0])) return;

                EnsureOptimizationLogFile();

                // Determine trade event
                string tradeEvent = "None";
                if (longEntry || shortEntry) tradeEvent = "Entry";
                else if (longExit || shortExit) tradeEvent = "Exit";

                // Determine market regime
                string marketRegime = DetermineMarketRegime();

                var opt = new OptimizationEvent
                {
                    // Core Technical Indicators (6 fields)
                    MacdValue = cachedMacdValue,
                    MacdSignal = cachedMacdAvg,
                    MacdRising = CurrentBar > 0 ? cachedMacdValue > GetPreviousMACDValue() : false,
                    AdxValue = cachedADX,
                    DiSpread = currentDISpread,  // More useful than individual DI+ and DI-
                    VolumeRatio = cachedCurrentVolume / cachedVolSMAVal,
                    
                    // Condition Tracking (6 fields)
                    Condition1 = (longEntry || inLongTrend) ? longCondition1 : shortCondition1,
                    Condition2 = (longEntry || inLongTrend) ? longCondition2 : shortCondition2,
                    Condition3 = (longEntry || inLongTrend) ? longCondition3 : shortCondition3,
                    Condition4 = (longEntry || inLongTrend) ? longCondition4 : shortCondition4,
                    Condition5 = (longEntry || inLongTrend) ? longCondition5 : shortCondition5,
                    Condition6 = (longEntry || inLongTrend) ? longCondition6 : shortCondition6,
                    
                    // Performance & Classification (4 fields)
                    SignalQualityScore = signalQuality,
                    FailedWithin3Bars = (barsInTrend > 0 && barsInTrend <= 3 && (longExit || shortExit)),
                    TradeEvent = tradeEvent,
                    MarketRegime = marketRegime,
                    
                    // NEW: Signal Quality Filter Fields (3 fields)
                    Signal_Accepted = signalAccepted,
                    Filter_Threshold = EnableSignalQualityFilter ? SignalQualityThreshold : 0,
                    Quality_Score_Bucket = GetQualityBucket(signalQuality)
                };

                optimizationBuffer.Enqueue(opt);
                if (optimizationBuffer.Count >= 10)
                    FlushOptimizationBuffer();
            }
            catch (Exception ex)
            {
                Print($"Enhanced optimization logging error: {ex.Message}");
            }
        }

        // Helper function for quality score bucketing
        private string GetQualityBucket(int score)
        {
            if (score >= 81) return "81-100";
            if (score >= 61) return "61-80";
            if (score >= 41) return "41-60";
            if (score >= 21) return "21-40";
            return "0-20";
        }

        private void EnsurePerformanceLogFile()
        {
            DateTime today = Time[0].Date;
            string newPath = GetPerformanceLogPath(today);
            
            if (performanceLogFile != newPath)
            {
                FlushPerformanceBuffer();
                performanceLogFile = newPath;
                performanceHeaderWritten = false;
                WritePerformanceHeader();
            }
        }

        private void EnsureOptimizationLogFile()
        {
            DateTime today = Time[0].Date;
            string newPath = GetOptimizationLogPath(today);
            
            if (optimizationLogFile != newPath)
            {
                FlushOptimizationBuffer();
                optimizationLogFile = newPath;
                optimizationHeaderWritten = false;
                WriteOptimizationHeader();
            }
        }

        private string GetPerformanceLogPath(DateTime date)
        {
            string basePath = @"G:\My Drive\Trading\Data\TrendSpotterData";
            string folderPath = Path.Combine(basePath, $"TS {cachedInstrumentName} {cachedChartType}");
            
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
                
            return Path.Combine(folderPath, $"TS_Performance_{date:yyyy-MM-dd}.csv");
        }

        private string GetOptimizationLogPath(DateTime date)
        {
            string basePath = @"G:\My Drive\Trading\Data\TrendSpotterData";
            string folderPath = Path.Combine(basePath, $"TS {cachedInstrumentName} {cachedChartType}");
            
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
                
            return Path.Combine(folderPath, $"TS_Optimization_{date:yyyy-MM-dd}.csv");
        }

		       private void WritePerformanceHeader()
		{
		    if (performanceHeaderWritten || string.IsNullOrEmpty(performanceLogFile)) return;
		    
		    // Only write header if enabled
		    if (!WriteCSVHeaders) 
		    {
		        performanceHeaderWritten = true; // Mark as written to prevent future attempts
		        return;
		    }
		
		    try
		    {
		        using (StreamWriter writer = new StreamWriter(performanceLogFile, false))
		        {
		            writer.WriteLine(
		                "Instrument,Chart_Type,Chart_Period,Entry_DateTime,Exit_DateTime,Entry_Price,Exit_Price," +
		                "Trade_Type,Trade_PnL,Max_Favorable_Excursion,Max_Adverse_Excursion," +
		                "Trade_Duration_Minutes,Entry_Hour,Exit_Hour,Signal_Strength_Score,Exit_Reason"
		            );
		        }
		        performanceHeaderWritten = true;
		    }
		    catch (Exception ex)
		    {
		        Print($"Error writing performance header: {ex.Message}");
		    }
		}

        // Updated optimization header for streamlined CSV (19 fields total)
        private void WriteOptimizationHeader()
        {
            if (optimizationHeaderWritten || string.IsNullOrEmpty(optimizationLogFile)) return;

            if (!WriteCSVHeaders) 
            {
                optimizationHeaderWritten = true;
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(optimizationLogFile, false))
                {
                    writer.WriteLine(
                        // Core Technical (6 fields)
                        "MACD_Value,MACD_Signal,MACD_Rising,ADX_Value,DI_Spread,Volume_Ratio," +
                        // Condition Tracking (6 fields)
                        "Condition1,Condition2,Condition3,Condition4,Condition5,Condition6," +
                        // Performance & Classification (4 fields)
                        "Signal_Quality_Score,Failed_Within_3_Bars,Trade_Event,Market_Regime," +
                        // NEW Filter Fields (3 fields)
                        "Signal_Accepted,Filter_Threshold,Quality_Score_Bucket"
                    );
                }
                optimizationHeaderWritten = true;
            }
            catch (Exception ex)
            {
                Print($"Error writing enhanced optimization header: {ex.Message}");
            }
        }

        private void FlushPerformanceBuffer()
        {
            if (performanceBuffer.Count == 0 || string.IsNullOrEmpty(performanceLogFile)) return;

            try
            {
                using (StreamWriter writer = new StreamWriter(performanceLogFile, true))
                {
                    while (performanceBuffer.Count > 0)
                    {
                        var trade = performanceBuffer.Dequeue();
                        writer.WriteLine(FormatPerformanceEvent(trade));
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error flushing performance buffer: {ex.Message}");
            }
        }

        private void FlushOptimizationBuffer()
        {
            if (optimizationBuffer.Count == 0 || string.IsNullOrEmpty(optimizationLogFile)) return;

            try
            {
                using (StreamWriter writer = new StreamWriter(optimizationLogFile, true))
                {
                    while (optimizationBuffer.Count > 0)
                    {
                        var opt = optimizationBuffer.Dequeue();
                        writer.WriteLine(FormatOptimizationEvent(opt));
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error flushing optimization buffer: {ex.Message}");
            }
        }

        private void FlushAllBuffers()
        {
            FlushPerformanceBuffer();
            FlushOptimizationBuffer();
        }

		private string FormatPerformanceEvent(TradeEvent trade)
		{
		    // Now always output complete trade rows
		    return $"{trade.Instrument},{trade.ChartType},{trade.ChartPeriod}," +
		           $"{trade.EntryDateTime:yyyy-MM-dd HH:mm:ss},{trade.ExitDateTime:yyyy-MM-dd HH:mm:ss}," +
		           $"{trade.EntryPrice:F2},{trade.ExitPrice:F2}," +
		           $"{trade.TradeType},{trade.TradePnL:F2},{trade.MaxFavorableExcursion:F2},{trade.MaxAdverseExcursion:F2}," +
		           $"{trade.TradeDurationMinutes:F1},{trade.EntryHour},{trade.ExitHour},{trade.SignalStrengthScore},{trade.ExitReason}";
		}

        // Updated format function for streamlined optimization events (19 fields)
        private string FormatOptimizationEvent(OptimizationEvent opt)
        {
            return 
                // Core Technical (6 fields)
                $"{opt.MacdValue:F4},{opt.MacdSignal:F4},{opt.MacdRising}," +
                $"{opt.AdxValue:F2},{opt.DiSpread:F2},{opt.VolumeRatio:F2}," +
                // Condition Tracking (6 fields)
                $"{opt.Condition1},{opt.Condition2},{opt.Condition3},{opt.Condition4},{opt.Condition5},{opt.Condition6}," +
                // Performance & Classification (4 fields)
                $"{opt.SignalQualityScore},{opt.FailedWithin3Bars},{opt.TradeEvent},{opt.MarketRegime}," +
                // NEW Filter Fields (3 fields)
                $"{opt.Signal_Accepted},{opt.Filter_Threshold},{opt.Quality_Score_Bucket}";
        }

        #endregion

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

                // Optimized background color logic - ONLY when partial signals enabled
                if (ShowPartialSignals)
                {
                    SetBackgroundColor();
                }
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

		private bool IsWithinTradingHours(DateTime barTime)
		{
		    TimeSpan timeOfDay = barTime.TimeOfDay;
		    return timeOfDay >= TradingStartTime && timeOfDay < TradingEndTime;
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

        private double CalculateTradeDuration()
        {
            if (tradeEntryTime == DateTime.MinValue) return 0;
            return (Time[0] - tradeEntryTime).TotalMinutes;
        }

        private string DetermineMarketRegime()
        {
            try
            {
                // Use ADX and ATR to determine market regime
                double atrNormalized = cachedATRValue / Close[0] * 100; // ATR as percentage of price
                
                if (cachedADX > 25 && atrNormalized > 0.5)
                    return "Trending";
                else if (cachedADX < 20 && atrNormalized < 0.3)
                    return "Ranging";
                else if (atrNormalized > 0.8)
                    return "Volatile";
                else
                    return "Transitional";
            }
            catch (Exception ex)
            {
                Print($"Market regime detection error: {ex.Message}");
                return "Unknown";
            }
        }

        private string DetermineExitReason()
        {
            if (useOption3ForTimeframe)
                return "ConditionFalse";
            else if (macdMomentumLoss && adxWeakening)
                return "MACD+ADX";
            else if (macdMomentumLoss && diWeakening)
                return "MACD+DI";
            else
                return "Other";
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

        #region STREAMLINED DATA STRUCTURES

        private class TradeEvent
        {
            public string Instrument;
            public string ChartType;
            public int ChartPeriod;
            public DateTime EntryDateTime;
            public DateTime ExitDateTime;
            public double EntryPrice;
            public double ExitPrice;
            public string TradeType;
            public double TradePnL;
            public double MaxFavorableExcursion;
            public double MaxAdverseExcursion;
            public double TradeDurationMinutes;
            public int EntryHour;
            public int ExitHour;
            public int SignalStrengthScore;
            public string ExitReason;
            public bool IsEntry;
        }

        // STREAMLINED OptimizationEvent class (removed 7 fields, added 3 filter fields)
        private class OptimizationEvent
        {
            // Core Technical Indicators (6 fields) - kept MACD_Value, MACD_Signal, MACD_Rising, ADX_Value, DI_Spread, Volume_Ratio
            public double MacdValue;
            public double MacdSignal;
            public bool MacdRising;
            public double AdxValue;
            public double DiSpread;              // KEPT: More useful than individual DI+ and DI-
            public double VolumeRatio;
            
            // Condition Tracking (6 fields) - ALL KEPT as critical
            public bool Condition1;
            public bool Condition2;
            public bool Condition3;
            public bool Condition4;
            public bool Condition5;
            public bool Condition6;
            
            // Performance & Classification (4 fields) - kept Signal_Quality_Score, Failed_Within_3_Bars, Trade_Event, Market_Regime
            public int SignalQualityScore;
            public bool FailedWithin3Bars;
            public string TradeEvent;
            public string MarketRegime;
            
            // NEW: Signal Quality Filter Fields (3 fields)
            public bool Signal_Accepted;
            public int Filter_Threshold;
            public string Quality_Score_Bucket;
            
            // REMOVED FIELDS (7 total):
            // - ATR_Value (Market_Regime provides better context)
            // - Signal_Duration_Bars (limited optimization value)
            // - Conditions_Met_Count (redundant - can count Condition1-6)
            // - DI_Plus (DI_Spread more useful)
            // - DI_Minus (DI_Spread more useful)  
            // - Option2_Exit_Used (manual trading - irrelevant)
            // - Price_vs_EMA (no EMA optimization planned)
        }

        #endregion

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
		[Range(10, 30)]
		[Display(Name = "Minimum ADX Threshold", Order = 3, GroupName = "DM Settings")]
		public double MinAdxThreshold { get; set; }

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
        [Display(Name = "Show Partial Signals", Order = 3, GroupName = "Signal Settings")]
        public bool ShowPartialSignals { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal Offset", Order = 4, GroupName = "Signal Settings")]
        public double Signal_Offset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", Order = 5, GroupName = "Signal Settings")]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short On", Order = 6, GroupName = "Signal Settings")]
        public string ShortOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long Off", Order = 7, GroupName = "Signal Settings")]
        public string LongOff { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Short Off", Order = 8, GroupName = "Signal Settings")]
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

        // SPLIT TRACKING CONTROLS
        [NinjaScriptProperty]
        [Display(Name = "Enable Performance Tracking", Order = 1, GroupName = "Data Tracking Settings")]
        public bool EnablePerformanceTracking { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Optimization Tracking", Order = 2, GroupName = "Data Tracking Settings")]
        public bool EnableOptimizationTracking { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Write CSV Headers", Order = 3, GroupName = "Data Tracking Settings")]
		public bool WriteCSVHeaders { get; set; }
		
		// TRADING HOURS
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Trading Start Time", Order = 4, GroupName = "Data Tracking Settings")]
		public TimeSpan TradingStartTime { get; set; }
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Trading End Time", Order = 5, GroupName = "Data Tracking Settings")]
		public TimeSpan TradingEndTime { get; set; }

        // Signal Quality Filter Properties
        [NinjaScriptProperty]
        [Display(Name = "Enable Signal Quality Filter", Order = 6, GroupName = "Data Tracking Settings")]
        public bool EnableSignalQualityFilter { get; set; }

        [NinjaScriptProperty]
        [Range(20, 80)]
        [Display(Name = "Signal Quality Threshold", Order = 7, GroupName = "Data Tracking Settings")]
        public int SignalQualityThreshold { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.TrendSpotter[] cacheTrendSpotter;
		public Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, double minAdxThreshold, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, bool showPartialSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enablePerformanceTracking, bool enableOptimizationTracking, bool writeCSVHeaders, TimeSpan tradingStartTime, TimeSpan tradingEndTime, bool enableSignalQualityFilter, int signalQualityThreshold)
		{
			return TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, minAdxThreshold, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, showPartialSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enablePerformanceTracking, enableOptimizationTracking, writeCSVHeaders, tradingStartTime, tradingEndTime, enableSignalQualityFilter, signalQualityThreshold);
		}

		public Myindicators.TrendSpotter TrendSpotter(ISeries<double> input, int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, double minAdxThreshold, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, bool showPartialSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enablePerformanceTracking, bool enableOptimizationTracking, bool writeCSVHeaders, TimeSpan tradingStartTime, TimeSpan tradingEndTime, bool enableSignalQualityFilter, int signalQualityThreshold)
		{
			if (cacheTrendSpotter != null)
				for (int idx = 0; idx < cacheTrendSpotter.Length; idx++)
					if (cacheTrendSpotter[idx] != null && cacheTrendSpotter[idx].MacdFast == macdFast && cacheTrendSpotter[idx].MacdSlow == macdSlow && cacheTrendSpotter[idx].MacdSmooth == macdSmooth && cacheTrendSpotter[idx].MacdMAType == macdMAType && cacheTrendSpotter[idx].DmPeriod == dmPeriod && cacheTrendSpotter[idx].AdxRisingBars == adxRisingBars && cacheTrendSpotter[idx].MinAdxThreshold == minAdxThreshold && cacheTrendSpotter[idx].EmaPeriod == emaPeriod && cacheTrendSpotter[idx].VolumeFilterMultiplier == volumeFilterMultiplier && cacheTrendSpotter[idx].ShowEntrySignals == showEntrySignals && cacheTrendSpotter[idx].ShowExitSignals == showExitSignals && cacheTrendSpotter[idx].ShowPartialSignals == showPartialSignals && cacheTrendSpotter[idx].Signal_Offset == signal_Offset && cacheTrendSpotter[idx].LongOn == longOn && cacheTrendSpotter[idx].ShortOn == shortOn && cacheTrendSpotter[idx].LongOff == longOff && cacheTrendSpotter[idx].ShortOff == shortOff && cacheTrendSpotter[idx].PartialLongSignalColor == partialLongSignalColor && cacheTrendSpotter[idx].PartialShortSignalColor == partialShortSignalColor && cacheTrendSpotter[idx].PartialSignalOpacity == partialSignalOpacity && cacheTrendSpotter[idx].LongEntryColor == longEntryColor && cacheTrendSpotter[idx].ShortEntryColor == shortEntryColor && cacheTrendSpotter[idx].ExitColor == exitColor && cacheTrendSpotter[idx].EnablePerformanceTracking == enablePerformanceTracking && cacheTrendSpotter[idx].EnableOptimizationTracking == enableOptimizationTracking && cacheTrendSpotter[idx].WriteCSVHeaders == writeCSVHeaders && cacheTrendSpotter[idx].TradingStartTime == tradingStartTime && cacheTrendSpotter[idx].TradingEndTime == tradingEndTime && cacheTrendSpotter[idx].EnableSignalQualityFilter == enableSignalQualityFilter && cacheTrendSpotter[idx].SignalQualityThreshold == signalQualityThreshold && cacheTrendSpotter[idx].EqualsInput(input))
						return cacheTrendSpotter[idx];
			return CacheIndicator<Myindicators.TrendSpotter>(new Myindicators.TrendSpotter(){ MacdFast = macdFast, MacdSlow = macdSlow, MacdSmooth = macdSmooth, MacdMAType = macdMAType, DmPeriod = dmPeriod, AdxRisingBars = adxRisingBars, MinAdxThreshold = minAdxThreshold, EmaPeriod = emaPeriod, VolumeFilterMultiplier = volumeFilterMultiplier, ShowEntrySignals = showEntrySignals, ShowExitSignals = showExitSignals, ShowPartialSignals = showPartialSignals, Signal_Offset = signal_Offset, LongOn = longOn, ShortOn = shortOn, LongOff = longOff, ShortOff = shortOff, PartialLongSignalColor = partialLongSignalColor, PartialShortSignalColor = partialShortSignalColor, PartialSignalOpacity = partialSignalOpacity, LongEntryColor = longEntryColor, ShortEntryColor = shortEntryColor, ExitColor = exitColor, EnablePerformanceTracking = enablePerformanceTracking, EnableOptimizationTracking = enableOptimizationTracking, WriteCSVHeaders = writeCSVHeaders, TradingStartTime = tradingStartTime, TradingEndTime = tradingEndTime, EnableSignalQualityFilter = enableSignalQualityFilter, SignalQualityThreshold = signalQualityThreshold }, input, ref cacheTrendSpotter);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, double minAdxThreshold, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, bool showPartialSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enablePerformanceTracking, bool enableOptimizationTracking, bool writeCSVHeaders, TimeSpan tradingStartTime, TimeSpan tradingEndTime, bool enableSignalQualityFilter, int signalQualityThreshold)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, minAdxThreshold, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, showPartialSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enablePerformanceTracking, enableOptimizationTracking, writeCSVHeaders, tradingStartTime, tradingEndTime, enableSignalQualityFilter, signalQualityThreshold);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, double minAdxThreshold, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, bool showPartialSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enablePerformanceTracking, bool enableOptimizationTracking, bool writeCSVHeaders, TimeSpan tradingStartTime, TimeSpan tradingEndTime, bool enableSignalQualityFilter, int signalQualityThreshold)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, minAdxThreshold, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, showPartialSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enablePerformanceTracking, enableOptimizationTracking, writeCSVHeaders, tradingStartTime, tradingEndTime, enableSignalQualityFilter, signalQualityThreshold);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, double minAdxThreshold, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, bool showPartialSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enablePerformanceTracking, bool enableOptimizationTracking, bool writeCSVHeaders, TimeSpan tradingStartTime, TimeSpan tradingEndTime, bool enableSignalQualityFilter, int signalQualityThreshold)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, minAdxThreshold, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, showPartialSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enablePerformanceTracking, enableOptimizationTracking, writeCSVHeaders, tradingStartTime, tradingEndTime, enableSignalQualityFilter, signalQualityThreshold);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, double minAdxThreshold, int emaPeriod, double volumeFilterMultiplier, bool showEntrySignals, bool showExitSignals, bool showPartialSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialLongSignalColor, Brush partialShortSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enablePerformanceTracking, bool enableOptimizationTracking, bool writeCSVHeaders, TimeSpan tradingStartTime, TimeSpan tradingEndTime, bool enableSignalQualityFilter, int signalQualityThreshold)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, minAdxThreshold, emaPeriod, volumeFilterMultiplier, showEntrySignals, showExitSignals, showPartialSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialLongSignalColor, partialShortSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enablePerformanceTracking, enableOptimizationTracking, writeCSVHeaders, tradingStartTime, tradingEndTime, enableSignalQualityFilter, signalQualityThreshold);
		}
	}
}

#endregion
