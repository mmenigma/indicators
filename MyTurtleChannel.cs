///6-21 Added tracking system
///6-23 Optimized the code

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

        // SAFE OPTIMIZATION: Reduce lookback limit for better performance
        private const int MAX_LOOKBACK = 200; // Reasonable limit instead of CurrentBar

        // Volume filter variables
        private SMA volumeSMA;
        private bool volumeConfirmed;
        
        // Data logging variables
        private string currentVolumeFilterLogFile;
        private string currentTradePerformanceLogFile;
        private bool volumeFilterHeaderWritten;
        private bool tradePerformanceHeaderWritten;
        
        // Trade tracking variables
        private double entryPrice;
        private double maxFavorableMove;
        private double maxAdverseMove;
        private int barsInTrend;
        private DateTime tradeEntryTime;
        private bool inLongTrend, inShortTrend;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Turtle Trade Channels Indicator - Safe Optimization with Tracking";
                Name = "MyTurtleChannel";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                IsAutoScale = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                
                // Default parameter values
                EntryLength = 12;
                ExitLength = 5;
                ShowEntrySignals = true;
                ShowExitSignals = true;
                ShowEntryText = true;
                ShowExitText = true;
                
                // Default colors
                UpperColor = Brushes.Transparent;
                LowerColor = Brushes.Transparent;
                TrendLineColor = Brushes.Red;      // Used for Short trend
                LongTrendLineColor = Brushes.Green;    // New property for Long trend
                ExitLineColor = Brushes.DimGray;
                LongEntryColor = Brushes.Green;
                ShortEntryColor = Brushes.Red;
                ExitColor = Brushes.CornflowerBlue;

                // Volume Filter Settings
                VolumeFilterEnabled = true;
                VolumeFilterMultiplier = 1.5;
                VolumeSMAPeriod = 20;
                
                // Data Tracking
                EnableDataTracking = true;
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
                // Initialize series (KEEP ORIGINAL LOGIC)
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
                
                // Initialize volume filter
                if (VolumeFilterEnabled)
                {
                    volumeSMA = SMA(Volume, VolumeSMAPeriod);
                }
                
                // Initialize logging variables
                currentVolumeFilterLogFile = "";
                currentTradePerformanceLogFile = "";
                volumeFilterHeaderWritten = false;
                tradePerformanceHeaderWritten = false;
                
                // Initialize trade tracking variables
                entryPrice = 0;
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 0;
                tradeEntryTime = DateTime.MinValue;
                inLongTrend = false;
                inShortTrend = false;
                
                // SAFE OPTIMIZATION: Reduce the problematic initialization loop
                // Only initialize a small number instead of 100
                int safeInitCount = Math.Min(10, BarsArray[0].Count);
                for (int i = 0; i < safeInitCount; i++)
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
            // SAFE OPTIMIZATION: Calculate minimum required bars including volume filter
            int minRequiredBars = Math.Max(Math.Max(EntryLength, ExitLength), VolumeFilterEnabled ? VolumeSMAPeriod : 0);
            
            // SAFE OPTIMIZATION: Early return for insufficient data
            if (CurrentBar < minRequiredBars)
            {
                // Basic values for early bars (KEEP ORIGINAL LOGIC)
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
                
            // SAFE OPTIMIZATION: Combine high/low calculations in one loop
            double highestHigh = double.MinValue;
            double lowestLow = double.MaxValue;
            double highestExit = double.MinValue;
            double lowestExit = double.MaxValue;
            
            int maxLength = Math.Max(EntryLength, ExitLength);
            
            for (int i = 0; i < maxLength && CurrentBar >= i; i++)
            {
                double currentHigh = High[i];
                double currentLow = Low[i];
                
                // Calculate entry channel values
                if (i < EntryLength)
                {
                    if (currentHigh > highestHigh)
                        highestHigh = currentHigh;
                    if (currentLow < lowestLow)
                        lowestLow = currentLow;
                }
                
                // Calculate exit channel values
                if (i < ExitLength)
                {
                    if (currentHigh > highestExit)
                        highestExit = currentHigh;
                    if (currentLow < lowestExit)
                        lowestExit = currentLow;
                }
            }
            
            upperSeries[0] = highestHigh;
            lowerSeries[0] = lowestLow;
            supSeries[0] = highestExit;
            sdownSeries[0] = lowestExit;
            
            // KEEP ORIGINAL SIGNAL DETECTION LOGIC
            buySignalSeries[0] = High[0] >= upperSeries[1] || (High[0] > upperSeries[1] && High[1] <= upperSeries[1]);
            sellSignalSeries[0] = Low[0] <= lowerSeries[1] || (Low[0] < lowerSeries[1] && Low[1] >= lowerSeries[1]);
            
            // Detect signals - exit (KEEP ORIGINAL)
            buyExitSeries[0] = Low[0] <= sdownSeries[1] || (Low[0] < sdownSeries[1] && Low[1] >= sdownSeries[1]);
            sellExitSeries[0] = High[0] >= supSeries[1] || (High[0] > supSeries[1] && High[1] <= supSeries[1]);
            
            // Volume filter confirmation (ADDED)
            if (VolumeFilterEnabled && volumeSMA != null)
            {
                volumeConfirmed = Volume[0] > volumeSMA[0] * VolumeFilterMultiplier;
                
                // Apply volume filter to entry signals only
                bool originalBuySignal = buySignalSeries[0];
                bool originalSellSignal = sellSignalSeries[0];
                
                buySignalSeries[0] = buySignalSeries[0] && volumeConfirmed;
                sellSignalSeries[0] = sellSignalSeries[0] && volumeConfirmed;
            }
            else
            {
                volumeConfirmed = true; // If volume filter disabled, always confirmed
            }
            
            // KEEP ORIGINAL SIGNAL COUNTING LOGIC
            barsSinceBuySignalSeries[0] = buySignalSeries[0] ? 0 : barsSinceBuySignalSeries[1] + 1;
            barsSinceSellSignalSeries[0] = sellSignalSeries[0] ? 0 : barsSinceSellSignalSeries[1] + 1;
            barsSinceBuyExitSeries[0] = buyExitSeries[0] ? 0 : barsSinceBuyExitSeries[1] + 1;
            barsSinceSellExitSeries[0] = sellExitSeries[0] ? 0 : barsSinceSellExitSeries[1] + 1;
            
            // SAFE OPTIMIZATION: Limit trend determination lookback
            int barsSinceHigh = int.MaxValue;
            int barsSinceLow = int.MaxValue;
            int lookbackLimit = Math.Min(CurrentBar, MAX_LOOKBACK);
            
            for (int i = 0; i < lookbackLimit; i++)
            {
                if (barsSinceHigh == int.MaxValue && High[i] >= upperSeries[0])
                {
                    barsSinceHigh = i;
                }
                
                if (barsSinceLow == int.MaxValue && Low[i] <= lowerSeries[0])
                {
                    barsSinceLow = i;
                }
                
                // SAFE OPTIMIZATION: Early exit when both found
                if (barsSinceHigh != int.MaxValue && barsSinceLow != int.MaxValue)
                    break;
            }
            
            // KEEP ORIGINAL TREND LINE LOGIC
            trendLineSeries[0] = barsSinceHigh <= barsSinceLow ? lowerSeries[0] : upperSeries[0];
            exitLineSeries[0] = barsSinceHigh <= barsSinceLow ? sdownSeries[0] : supSeries[0];
            
            // Determine if we're in a long trend (KEEP ORIGINAL)
            bool isLongTrend = barsSinceHigh <= barsSinceLow;
            isLongTrendSeries[0] = isLongTrend;
            
            // Check for trend transition (KEEP ORIGINAL)
            bool isTrendTransition = false;
            if (CurrentBar > 0)
            {
                isTrendTransition = isLongTrendSeries[0] != isLongTrendSeries[1];
            }
            
            // Set plot values (KEEP ORIGINAL)
            Values[0][0] = upperSeries[0];     // Upper
            Values[1][0] = lowerSeries[0];     // Lower
            Values[2][0] = trendLineSeries[0]; // TrendLine
            Values[3][0] = exitLineSeries[0];  // ExitLine
            
            // Set dynamic colors for trend line and exit line (KEEP ORIGINAL)
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
            
            // Trade tracking logic (ADDED)
            bool longEntry = buySignalSeries[0] && !inLongTrend;
            bool shortEntry = sellSignalSeries[0] && !inShortTrend;
            bool longExit = inLongTrend && buyExitSeries[0] && barsInTrend > 1;
            bool shortExit = inShortTrend && sellExitSeries[0] && barsInTrend > 1;
            
            // Update trade state (ADDED)
            if (longEntry)
            {
                inLongTrend = true;
                inShortTrend = false;
                entryPrice = Close[0];
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 1;
                tradeEntryTime = Time[0];
            }
            else if (shortEntry)
            {
                inShortTrend = true;
                inLongTrend = false;
                entryPrice = Close[0];
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 1;
                tradeEntryTime = Time[0];
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
            
            // Track performance during trends (ADDED)
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
            
            // KEEP ORIGINAL SIGNAL DRAWING LOGIC
            if (ShowEntrySignals || ShowExitSignals)
            {
                if (ShowEntrySignals)
                {
                    // Long entry signal (KEEP ORIGINAL)
                    if (buySignalSeries[0] && barsSinceBuyExitSeries[0] < barsSinceBuySignalSeries[1])
                    {
                        Draw.Diamond(this, "LongEntry" + CurrentBar, false, 0, lowerSeries[0], LongEntryColor);
                        
                        // Only draw text if ShowEntryText is enabled
                        if (ShowEntryText)
                        {
                            Draw.Text(this, "LongEntryText" + CurrentBar, "Long Entry", 0, lowerSeries[0] - TickSize * 5, LongEntryColor);
                        }
                    }
                    
                    // Short entry signal (KEEP ORIGINAL)
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
                    // Long exit signal (KEEP ORIGINAL)
                    if (buyExitSeries[0] && barsSinceBuySignalSeries[0] < barsSinceBuyExitSeries[1])
                    {
                        Draw.Diamond(this, "LongExit" + CurrentBar, false, 0, upperSeries[0], ExitColor);
                        
                        // Only draw text if ShowExitText is enabled
                        if (ShowExitText)
                        {
                            Draw.Text(this, "LongExitText" + CurrentBar, "Exit Long", 0, upperSeries[0] + TickSize * 20, ExitColor);
                        }
                    }
                    
                    // Short exit signal (KEEP ORIGINAL)
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
            
            // Data Logging (ADDED)
            if (EnableDataTracking && (longEntry || shortEntry || longExit || shortExit))
            {
                LogVolumeFilterData(longEntry, shortEntry, longExit, shortExit);
                
                // Only log trade performance on trade completion
                if (longExit || shortExit)
                {
                    LogTradePerformanceData(longExit, shortExit);
                }
            }
        }
        
        // ===== DUAL LOGGING SYSTEM START =====
        
        private void LogVolumeFilterData(bool longEntry, bool shortEntry, bool longExit, bool shortExit)
        {
            try
            {
                DateTime barTime = Time[0];
                TimeSpan startTime = new TimeSpan(9, 30, 0);
                TimeSpan endTime = new TimeSpan(16, 0, 0);
                
                if (barTime.TimeOfDay < startTime || barTime.TimeOfDay >= endTime)
                    return;
                
                // Check for raw turtle signals
                bool rawTurtleSignal = (buySignalSeries[0] && !volumeConfirmed) || (sellSignalSeries[0] && !volumeConfirmed) || 
                                      buySignalSeries[0] || sellSignalSeries[0];
                
                if (!rawTurtleSignal)
                    return;
                
                string instrumentName = Instrument.MasterInstrument.Name.Replace(" ", "_").Replace("/", "_");
                string chartTypeFolder = DetermineChartTypeFolder();
                string basePath = @"G:\My Drive\Trading\Data\TurtleData";
                string folderPath = Path.Combine(basePath, $"TC {instrumentName} {chartTypeFolder}");
                
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                
                string dateString = barTime.ToString("yyyy-MM-dd");
                string fileName = $"TC_VolumeFilter_{dateString}.csv";
                string filePath = Path.Combine(folderPath, fileName);
                
                if (currentVolumeFilterLogFile != filePath)
                {
                    currentVolumeFilterLogFile = filePath;
                    volumeFilterHeaderWritten = false;
                }
                
                if (!volumeFilterHeaderWritten)
                {
                    using (StreamWriter writer = new StreamWriter(filePath, false))
                    {
                        writer.WriteLine("DateTime,Volume_Current,Volume_SMA,Volume_Ratio,Signal_Passed_Filter,Trade_PnL,Trade_Duration_Bars,Hour_Of_Day");
                    }
                    volumeFilterHeaderWritten = true;
                }
                
                // Calculate metrics
                double volumeSMAValue = VolumeFilterEnabled && volumeSMA != null ? volumeSMA[0] : Volume[0];
                double currentVolume = Volume[0];
                double volumeRatio = volumeSMAValue > 0 ? currentVolume / volumeSMAValue : 1.0;
                bool signalPassedFilter = VolumeFilterEnabled ? volumeConfirmed : true;
                
                // Calculate trade P&L for completed trades
                double tradePnL = 0;
                if (longExit || shortExit)
                {
                    tradePnL = CalculateTradePnL();
                }
                
                int hourOfDay = barTime.Hour;
                
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    string logLine = $"{barTime:yyyy-MM-dd HH:mm:ss},{currentVolume:F0},{volumeSMAValue:F0},{volumeRatio:F2}," +
                                   $"{signalPassedFilter},{tradePnL:F2},{barsInTrend},{hourOfDay}";
                    writer.WriteLine(logLine);
                }
            }
            catch (Exception ex)
            {
                Print($"Volume filter data logging error: {ex.Message}");
            }
        }
        
        private void LogTradePerformanceData(bool longExit, bool shortExit)
        {
            try
            {
                DateTime barTime = Time[0];
                string instrumentName = Instrument.MasterInstrument.Name.Replace(" ", "_").Replace("/", "_");
                string chartTypeFolder = DetermineChartTypeFolder();
                string basePath = @"G:\My Drive\Trading\Data\TurtleData";
                string folderPath = Path.Combine(basePath, $"TC {instrumentName} {chartTypeFolder}");
                
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                
                string dateString = barTime.ToString("yyyy-MM-dd");
                string fileName = $"TC_TradePerformance_{dateString}.csv";
                string filePath = Path.Combine(folderPath, fileName);
                
                if (currentTradePerformanceLogFile != filePath)
                {
                    currentTradePerformanceLogFile = filePath;
                    tradePerformanceHeaderWritten = false;
                }
                
                if (!tradePerformanceHeaderWritten)
                {
                    using (StreamWriter writer = new StreamWriter(filePath, false))
                    {
                        writer.WriteLine("Entry_Price,Exit_Price,Trade_Type,Exit_DateTime,Max_Favorable_Excursion,Max_Adverse_Excursion");
                    }
                    tradePerformanceHeaderWritten = true;
                }
                
                // Calculate trade metrics
                double exitPrice = Close[0];
                string tradeType = longExit ? "Long" : "Short";
                DateTime exitDateTime = barTime;
                
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    string logLine = $"{entryPrice:F2},{exitPrice:F2},{tradeType},{exitDateTime:yyyy-MM-dd HH:mm:ss}," +
                                   $"{maxFavorableMove:F2},{maxAdverseMove:F2}";
                    writer.WriteLine(logLine);
                }
            }
            catch (Exception ex)
            {
                Print($"Trade performance data logging error: {ex.Message}");
            }
        }
        
        private double CalculateTradePnL()
        {
            if (entryPrice == 0) return 0;
            
            double exitPrice = Close[0];
            
            if (inLongTrend)
                return (exitPrice - entryPrice);  // Raw point difference for long
            else if (inShortTrend)
                return (entryPrice - exitPrice);  // Raw point difference for short
            
            return 0;
        }
        
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
        
        // ===== DUAL LOGGING SYSTEM END =====
        
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
        
        #endregion
        
        #region Volume Filter Settings
        [NinjaScriptProperty]
        [Display(Name="Enable Volume Filter", Order=1, GroupName="Volume Filter Settings")]
        public bool VolumeFilterEnabled { get; set; }
        
        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name="Volume Filter Multiplier", Order=2, GroupName="Volume Filter Settings")]
        public double VolumeFilterMultiplier { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Volume SMA Period", Order=3, GroupName="Volume Filter Settings")]
        public int VolumeSMAPeriod { get; set; }
        #endregion
        
        #region Data Tracking Settings
        [NinjaScriptProperty]
        [Display(Name="Enable Data Tracking", Order=1, GroupName="Data Tracking Settings")]
        public bool EnableDataTracking { get; set; }
        #endregion
        
        #region Colors
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
		public Myindicators.MyTurtleChannel MyTurtleChannel(int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, bool volumeFilterEnabled, double volumeFilterMultiplier, int volumeSMAPeriod, bool enableDataTracking, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return MyTurtleChannel(Input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, volumeFilterEnabled, volumeFilterMultiplier, volumeSMAPeriod, enableDataTracking, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}

		public Myindicators.MyTurtleChannel MyTurtleChannel(ISeries<double> input, int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, bool volumeFilterEnabled, double volumeFilterMultiplier, int volumeSMAPeriod, bool enableDataTracking, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			if (cacheMyTurtleChannel != null)
				for (int idx = 0; idx < cacheMyTurtleChannel.Length; idx++)
					if (cacheMyTurtleChannel[idx] != null && cacheMyTurtleChannel[idx].EntryLength == entryLength && cacheMyTurtleChannel[idx].ExitLength == exitLength && cacheMyTurtleChannel[idx].ShowEntrySignals == showEntrySignals && cacheMyTurtleChannel[idx].ShowExitSignals == showExitSignals && cacheMyTurtleChannel[idx].ShowEntryText == showEntryText && cacheMyTurtleChannel[idx].ShowExitText == showExitText && cacheMyTurtleChannel[idx].VolumeFilterEnabled == volumeFilterEnabled && cacheMyTurtleChannel[idx].VolumeFilterMultiplier == volumeFilterMultiplier && cacheMyTurtleChannel[idx].VolumeSMAPeriod == volumeSMAPeriod && cacheMyTurtleChannel[idx].EnableDataTracking == enableDataTracking && cacheMyTurtleChannel[idx].UpperColor == upperColor && cacheMyTurtleChannel[idx].LowerColor == lowerColor && cacheMyTurtleChannel[idx].TrendLineColor == trendLineColor && cacheMyTurtleChannel[idx].LongTrendLineColor == longTrendLineColor && cacheMyTurtleChannel[idx].ExitLineColor == exitLineColor && cacheMyTurtleChannel[idx].LongEntryColor == longEntryColor && cacheMyTurtleChannel[idx].ShortEntryColor == shortEntryColor && cacheMyTurtleChannel[idx].ExitColor == exitColor && cacheMyTurtleChannel[idx].EqualsInput(input))
						return cacheMyTurtleChannel[idx];
			return CacheIndicator<Myindicators.MyTurtleChannel>(new Myindicators.MyTurtleChannel(){ EntryLength = entryLength, ExitLength = exitLength, ShowEntrySignals = showEntrySignals, ShowExitSignals = showExitSignals, ShowEntryText = showEntryText, ShowExitText = showExitText, VolumeFilterEnabled = volumeFilterEnabled, VolumeFilterMultiplier = volumeFilterMultiplier, VolumeSMAPeriod = volumeSMAPeriod, EnableDataTracking = enableDataTracking, UpperColor = upperColor, LowerColor = lowerColor, TrendLineColor = trendLineColor, LongTrendLineColor = longTrendLineColor, ExitLineColor = exitLineColor, LongEntryColor = longEntryColor, ShortEntryColor = shortEntryColor, ExitColor = exitColor }, input, ref cacheMyTurtleChannel);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, bool volumeFilterEnabled, double volumeFilterMultiplier, int volumeSMAPeriod, bool enableDataTracking, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(Input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, volumeFilterEnabled, volumeFilterMultiplier, volumeSMAPeriod, enableDataTracking, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}

		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(ISeries<double> input , int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, bool volumeFilterEnabled, double volumeFilterMultiplier, int volumeSMAPeriod, bool enableDataTracking, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, volumeFilterEnabled, volumeFilterMultiplier, volumeSMAPeriod, enableDataTracking, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, bool volumeFilterEnabled, double volumeFilterMultiplier, int volumeSMAPeriod, bool enableDataTracking, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(Input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, volumeFilterEnabled, volumeFilterMultiplier, volumeSMAPeriod, enableDataTracking, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}

		public Indicators.Myindicators.MyTurtleChannel MyTurtleChannel(ISeries<double> input , int entryLength, int exitLength, bool showEntrySignals, bool showExitSignals, bool showEntryText, bool showExitText, bool volumeFilterEnabled, double volumeFilterMultiplier, int volumeSMAPeriod, bool enableDataTracking, Brush upperColor, Brush lowerColor, Brush trendLineColor, Brush longTrendLineColor, Brush exitLineColor, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.MyTurtleChannel(input, entryLength, exitLength, showEntrySignals, showExitSignals, showEntryText, showExitText, volumeFilterEnabled, volumeFilterMultiplier, volumeSMAPeriod, enableDataTracking, upperColor, lowerColor, trendLineColor, longTrendLineColor, exitLineColor, longEntryColor, shortEntryColor, exitColor);
		}
	}
}

#endregion
