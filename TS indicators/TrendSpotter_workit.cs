#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
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

        // Logging buffer
        private List<string> logBuffer = new List<string>();
        private const int LogFlushBars = 50;  // Number of bars to buffer before flushing log

        // Log file path tracking
        private string currentLogFile;
        private bool headerWritten;

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"TrendSpotter Trading Signal Indicator - Optimized for Performance with Buffered Logging, Retains Historical Drawings";
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
                currentLogFile = string.Empty;
                headerWritten = false;
                entryPrice = 0;
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 0;
                useOption3ForTimeframe = BarsPeriod.Value >= 5;

                tradeEntryTime = DateTime.MinValue;
                entryHour = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(MacdSlow, DmPeriod), EmaPeriod))
                return;

            // Cached values to reduce repeated indexer access
            double volSMAVal = volumeSMA[0];
            double currVolume = Volume[0];

            // Compute MACD values
            double macdValue, macdAvg;

            if (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA)
            {
                macdValue = macd.Default[0];
                macdAvg = macd.Avg[0];
            }
            else
            {
                fastSMA.Update();
                slowSMA.Update();

                macdLine[0] = fastSMA[0] - slowSMA[0];

                if (CurrentBar >= MacdSmooth - 1)
                {
                    signalSMA.Update();
                    macdAvg = signalSMA[0];
                }
                else
                {
                    macdAvg = macdLine[0];
                }
                macdValue = macdLine[0];
            }

            // Get values for DI, ADX, EMA
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
            for (int i = 1; i <= AdxRisingBars && CurrentBar >= i; i++)
            {
                if (dm.ADXPlot[0] <= dm.ADXPlot[i])
                {
                    adxRising = false;
                    break;
                }
            }

            // 6-Condition Entry System
            longCondition1 = macdValue > 0;
            longCondition2 = macdValue > macdAvg;
            longCondition3 = (CurrentBar > 0) ? macdValue > (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1]) : true;
            longCondition4 = adxRising;
            longCondition5 = diPlus > diMinus;
            longCondition6 = Close[0] > emaValue;

            shortCondition1 = macdValue < 0;
            shortCondition2 = macdValue < macdAvg;
            shortCondition3 = (CurrentBar > 0) ? macdValue < (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1]) : true;
            shortCondition4 = adxRising;
            shortCondition5 = diMinus > diPlus;
            shortCondition6 = Close[0] < emaValue;

            longConditionCount = (longCondition1 ? 1 : 0) + (longCondition2 ? 1 : 0) + (longCondition3 ? 1 : 0) +
                                 (longCondition4 ? 1 : 0) + (longCondition5 ? 1 : 0) + (longCondition6 ? 1 : 0);

            shortConditionCount = (shortCondition1 ? 1 : 0) + (shortCondition2 ? 1 : 0) + (shortCondition3 ? 1 : 0) +
                                  (shortCondition4 ? 1 : 0) + (shortCondition5 ? 1 : 0) + (shortCondition6 ? 1 : 0);

            // Volume filter confirmation (cached volumeSMAVal and currVolume)
            volumeConfirmed = currVolume > volSMAVal * VolumeFilterMultiplier;

            // Current signals including volume filter
            bool currentLongSignal = (longConditionCount == 6) && volumeConfirmed;
            bool currentShortSignal = (shortConditionCount == 6) && volumeConfirmed;

            // Exit logic
            bool longExit, shortExit;

            if (useOption3ForTimeframe)
            {
                bool anyLongFalse = !longCondition1 || !longCondition2 || !longCondition3 || !longCondition4 || !longCondition5 || !longCondition6;
                bool anyShortFalse = !shortCondition1 || !shortCondition2 || !shortCondition3 || !shortCondition4 || !shortCondition5 || !shortCondition6;

                longExit = inLongTrend && anyLongFalse;
                shortExit = inShortTrend && anyShortFalse;
            }
            else
            {
                CalculateExitConditions(macdValue);
                longExit = inLongTrend && (macdMomentumLoss && (adxWeakening || diWeakening));
                shortExit = inShortTrend && (macdMomentumLoss && (adxWeakening || diWeakening));
            }

            // Track performance during trends
            if (inLongTrend || inShortTrend)
            {
                barsInTrend++;

                double currentMove = (inLongTrend) ? Close[0] - entryPrice : entryPrice - Close[0];

                if (currentMove > maxFavorableMove)
                    maxFavorableMove = currentMove;
                if (currentMove < maxAdverseMove)
                    maxAdverseMove = currentMove;
            }

            // Update trend status with time tracking
            if (currentLongSignal && !inLongTrend)
            {
                inLongTrend = true;
                inShortTrend = false;
                SetEntryTracking(Close[0], Time[0]);
            }
            else if (currentShortSignal && !inShortTrend)
            {
                inShortTrend = true;
                inLongTrend = false;
                SetEntryTracking(Close[0], Time[0]);
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

            // Historical drawing of entry and exit signals (retained as per your requirements)
            if (ShowEntrySignals)
            {
                if (currentLongSignal && !wasLongSignal)
                {
                    Draw.ArrowUp(this, LongOn + CurrentBar.ToString(), true, 0, Low[0] - Signal_Offset * TickSize, LongEntryColor);
                }
                else if (currentShortSignal && !wasShortSignal)
                {
                    Draw.ArrowDown(this, ShortOn + CurrentBar.ToString(), true, 0, High[0] + Signal_Offset * TickSize, ShortEntryColor);
                }

                if (!inLongTrend && !inShortTrend && (longConditionCount == 5 || shortConditionCount == 5))
                {
                    BackBrush = CreateBrushWithOpacity(PartialSignalColor, PartialSignalOpacity);
                }
                else
                {
                    BackBrush = Brushes.Transparent;
                }
            }

            if (ShowExitSignals && CurrentBar > 1)
            {
                if (longExit)
                {
                    Draw.Text(this, LongOff + CurrentBar, true, "o", 0, High[0] + Signal_Offset * TickSize, 0, ExitColor,
                              new SimpleFont("Arial", 12), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
                }
                if (shortExit)
                {
                    Draw.Text(this, ShortOff + CurrentBar, true, "o", 0, Low[0] - Signal_Offset * TickSize, 0, ExitColor,
                              new SimpleFont("Arial", 12), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            // Optimized buffered data logging
            if (EnableDataTracking)
            {
                BufferOptimizedDataForAnalysis(macdValue, macdAvg, adx, diPlus, diMinus, emaValue,
                                          currentLongSignal, currentShortSignal, longExit, shortExit);
            }

            // Update previous signal flags conservatively
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

        // Helper method to set entry tracking variables
        private void SetEntryTracking(double price, DateTime time)
        {
            entryPrice = price;
            maxFavorableMove = 0;
            maxAdverseMove = 0;
            barsInTrend = 1;

            tradeEntryTime = time;
            entryHour = time.Hour;
        }

        private void CalculateExitConditions(double macdValue)
        {
            macdMomentumLoss = false;
            if (CurrentBar >= 2)
            {
                double prevMacd1 = MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1];
                double prevMacd2 = MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[2] : macdLine[2];

                macdMomentumLoss = (macdValue <= prevMacd1) && (prevMacd1 <= prevMacd2);
            }

            adxWeakening = CurrentBar >= 1 && dm.ADXPlot[0] < dm.ADXPlot[1];
            diWeakening = CurrentBar >= 1 && currentDISpread < previousDISpread;
        }

        private void BufferOptimizedDataForAnalysis(double macdValue, double macdAvg, double adx, double diPlus, double diMinus,
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
                    Directory.CreateDirectory(folderPath);

                string dateString = barTime.ToString("yyyy-MM-dd");
                string fileName = $"TS_Optimized_{dateString}.csv";
                string filePath = Path.Combine(folderPath, fileName);

                if (currentLogFile != filePath)
                {
                    FlushLogBuffer();
                    currentLogFile = filePath;
                    headerWritten = false;
                }

                if (!headerWritten)
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
                    headerWritten = true;
                }

                int signalScore = CalculateSignalStrengthScore(macdValue, macdAvg, adx, diPlus, diMinus, emaValue);
                double tradePnL = 0;
                double revenuePerSignal = 0;

                if (longExit || shortExit)
                {
                    tradePnL = CalculateTradePnL();
                    cumulativeRevenue += tradePnL;
                    revenuePerSignal = tradePnL;
                }

                bool failedWithin3Bars = (barsInTrend > 0 && barsInTrend <= 3 && (longExit || shortExit));
                double tradeDurationMinutes = 0;
                if ((longExit || shortExit) && tradeEntryTime != DateTime.MinValue)
                {
                    TimeSpan duration = barTime - tradeEntryTime;
                    tradeDurationMinutes = duration.TotalMinutes;
                }

                double volumeRatio = currVolume / volSMAVal;
                bool volumeFilterPassed = volumeRatio >= VolumeFilterMultiplier;
                int signalsBeforeVolume = (longConditionCount == 6 || shortConditionCount == 6) ? 1 : 0;
                int signalsAfterVolume = ((longConditionCount == 6 && volumeFilterPassed) || (shortConditionCount == 6 && volumeFilterPassed)) ? 1 : 0;

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

                var logLine = $"{barTime:yyyy-MM-dd HH:mm:ss},{High[0]:F2},{Low[0]:F2},{Close[0]:F2},{atrValue:F2}," +
                              $"{signalScore},{currentLongSignal},{currentShortSignal},{longExit},{shortExit}," +
                              $"{(inLongTrend || inShortTrend)},{barsInTrend},{failedWithin3Bars}," +
                              $"{entryHour},{tradeDurationMinutes:F1}," +
                              $"{volSMAVal:F0},{currVolume:F0},{volumeRatio:F2},{volumeFilterPassed}," +
                              $"{signalsBeforeVolume},{signalsAfterVolume}," +
                              $"{entryPrice:F2},{(longExit || shortExit ? Close[0] : 0):F2},{maxFavorableMove:F2},{maxAdverseMove:F2}," +
                              $"{tradePnL:F2},{revenuePerSignal:F2},{cumulativeRevenue:F2}," +
                              $"{macdValue:F4},{adx:F2},{diSpread:F2},{priceAboveEMAPct:F2},{exitReason}";

                logBuffer.Add(logLine);

                // Flush buffer periodically
                if (logBuffer.Count >= LogFlushBars)
                    FlushLogBuffer();
            }
            catch (Exception ex)
            {
                Print($"Optimized data logging error: {ex.Message}");
            }
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

        private int CalculateSignalStrengthScore(double macdValue, double macdAvg, double adx,
                                                double diPlus, double diMinus, double emaValue)
        {
            int score = 0;

            if (macdValue > macdAvg) score += 20;
            if (CurrentBar > 0 && macdValue > (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1]))
                score += 20;
            if (Math.Abs(diPlus - diMinus) > 10) score += 20;

            if (adx > 25) score += 15;
            if (macdValue > 0) score += 10;
            if ((Close[0] - emaValue) / emaValue > 0.001) score += 10;
            if (adx > dm.ADXPlot[1]) score += 5;

            return Math.Min(score, 100);
        }

        private double CalculateTradePnL()
        {
            if (entryPrice == 0)
                return 0;

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

                // If custom BarsPeriodType detected (example placeholder)
                if ((int)BarsPeriod.BarsPeriodType == 12345)
                {
                    if (BarsPeriod.Value2 > 0)
                        return $"NR{BarsPeriod.Value}{BarsPeriod.Value2}";
                    else
                        return $"NR{BarsPeriod.Value}";
                }

                switch (BarsPeriod.BarsPeriodType)
                {
                    case BarsPeriodType.Minute: return $"M{periodValue}";
                    case BarsPeriodType.Second: return $"S{periodValue}";
                    case BarsPeriodType.Tick: return $"{periodValue}T";
                    case BarsPeriodType.Volume: return $"{periodValue}V";
                    case BarsPeriodType.Range: return $"R{periodValue}";
                    case BarsPeriodType.Renko: return $"R{(int)BarsPeriod.Value}";
                    default: return chartType;
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

        protected override void OnTermination()
        {
            FlushLogBuffer();
            base.OnTermination();
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