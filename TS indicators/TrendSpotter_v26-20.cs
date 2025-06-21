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
        
        // Data logging variables
        private string currentLogFile;
        private bool headerWritten;
        
        // Tracking variables for analysis
        private double entryPrice;
        private double maxFavorableMove;
        private double maxAdverseMove;
        private int barsInTrend;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"TrendSpotter Trading Signal Indicator - Option 2 Exit Strategy with Full Tracking";
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
            }
            else if (State == State.DataLoaded)
            {
                // Initialize indicators and series
                dm = DM(DmPeriod);
                ema20 = EMA(EmaPeriod);
                atr = ATR(14);
                
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

            // Current signal status
            bool currentLongSignal = longConditionCount == 6;
            bool currentShortSignal = shortConditionCount == 6;

            // Option 2 Exit Strategy: Multi-Condition Exit Logic
            CalculateExitConditions(macdValue);

            // Check for exit conditions using Option 2 strategy
            bool longExit = inLongTrend && (macdMomentumLoss && (adxWeakening || diWeakening));
            bool shortExit = inShortTrend && (macdMomentumLoss && (adxWeakening || diWeakening));

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

            // Update trend status
            if (currentLongSignal && !inLongTrend)
            {
                inLongTrend = true;
                inShortTrend = false;
                entryPrice = Close[0];
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 1;
            }
            else if (currentShortSignal && !inShortTrend)
            {
                inShortTrend = true;
                inLongTrend = false;
                entryPrice = Close[0];
                maxFavorableMove = 0;
                maxAdverseMove = 0;
                barsInTrend = 1;
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

            // Exit Signals using Option 2 strategy
            if (ShowExitSignals && CurrentBar > 1)
            {
                // Long Exit: Option 2 conditions met
                if (longExit)
                {
                    Draw.Text(this, LongOff + CurrentBar, true, "o", 0, 
                             High[0] + Signal_Offset * TickSize, 0, ExitColor, 
                             new SimpleFont("Arial", 12), TextAlignment.Center, 
                             Brushes.Transparent, Brushes.Transparent, 0);
                }

                // Short Exit: Option 2 conditions met
                if (shortExit)
                {
                    Draw.Text(this, ShortOff + CurrentBar, true, "o", 0,
                             Low[0] - Signal_Offset * TickSize, 0, ExitColor,
                             new SimpleFont("Arial", 12), TextAlignment.Center,
                             Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            // Data logging for analysis with Option 2 exit tracking
            if (EnableDataTracking)
            {
                LogDataForAnalysis(macdValue, macdAvg, adx, diPlus, diMinus, emaValue, 
                                 adxRising, adxRisingConsecutive, consecutiveRisingBars,
                                 currentLongSignal, currentShortSignal, longExit, shortExit);
            }

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

        private void LogDataForAnalysis(double macdValue, double macdAvg, double adx, double diPlus, double diMinus, 
                                      double emaValue, bool adxRising, bool adxRisingConsecutive, int consecutiveRisingBars,
                                      bool currentLongSignal, bool currentShortSignal, bool longExit, bool shortExit)
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
                string fileName = $"TS_Analysis_{dateString}.csv";
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
                        writer.WriteLine("DateTime,High,Low,Close,Open,Hour_Of_Day," +
                                       "MACD_Above_Zero,MACD_Above_Signal,MACD_Rising,ADX_Rising,DI_Bullish,Price_Above_EMA," +
                                       "MACD_Signal_Above_Zero,Both_MACD_And_Signal_Above_Zero,ADX_Rising_Consecutive," +
                                       "Long_Condition_Count,Short_Condition_Count,InLongTrend,InShortTrend," +
                                       "LongSignalTriggered,ShortSignalTriggered,LongExit,ShortExit," +
                                       "MACD_Value,MACD_Signal,ADX_Value,DI_Plus,DI_Minus,EMA_Value," +
                                       "MACD_Distance_Above_Zero,MACD_Signal_Distance_Above_Zero,DI_Spread,Price_Distance_Above_EMA," +
                                       "ATR_At_Signal,Consecutive_Rising_Bars,Bars_In_Trend," +
                                       "Entry_Price,Max_Favorable_Move,Max_Adverse_Move,Quality_ADX," +
                                       "MACD_Momentum_Loss,ADX_Weakening,DI_Weakening");
                    }
                    headerWritten = true;
                }
                
                double macdDistanceAboveZero = Math.Max(0, macdValue);
                double macdSignalDistanceAboveZero = Math.Max(0, macdAvg);
                double diSpread = diPlus - diMinus;
                double priceDistanceAboveEMA = ((Close[0] - emaValue) / emaValue) * 100;
                double atrValue = atr[0];
                
                // Quality ADX - simplified to true since ADX Quality Mode was removed
                bool qualityADX = true;
                
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    string logLine = $"{barTime:yyyy-MM-dd HH:mm:ss},{High[0]:F2},{Low[0]:F2},{Close[0]:F2},{Open[0]:F2},{barTime.Hour}," +
                                   $"{longCondition1},{longCondition2},{longCondition3},{longCondition4},{longCondition5},{longCondition6}," +
                                   $"{macdAvg > 0},{(longCondition1 && macdAvg > 0)},{adxRisingConsecutive}," +
                                   $"{longConditionCount},{shortConditionCount},{inLongTrend},{inShortTrend}," +
                                   $"{currentLongSignal},{currentShortSignal},{longExit},{shortExit}," +
                                   $"{macdValue:F4},{macdAvg:F4},{adx:F2},{diPlus:F2},{diMinus:F2},{emaValue:F2}," +
                                   $"{macdDistanceAboveZero:F4},{macdSignalDistanceAboveZero:F4},{diSpread:F2},{priceDistanceAboveEMA:F2}," +
                                   $"{atrValue:F2},{consecutiveRisingBars},{barsInTrend}," +
                                   $"{entryPrice:F2},{maxFavorableMove:F2},{maxAdverseMove:F2},{qualityADX}," +
                                   $"{macdMomentumLoss},{adxWeakening},{diWeakening}";
                    writer.WriteLine(logLine);
                }
            }
            catch (Exception ex)
            {
                Print($"Data logging error: {ex.Message}");
            }
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
		public Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Myindicators.TrendSpotter TrendSpotter(ISeries<double> input, int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			if (cacheTrendSpotter != null)
				for (int idx = 0; idx < cacheTrendSpotter.Length; idx++)
					if (cacheTrendSpotter[idx] != null && cacheTrendSpotter[idx].MacdFast == macdFast && cacheTrendSpotter[idx].MacdSlow == macdSlow && cacheTrendSpotter[idx].MacdSmooth == macdSmooth && cacheTrendSpotter[idx].MacdMAType == macdMAType && cacheTrendSpotter[idx].DmPeriod == dmPeriod && cacheTrendSpotter[idx].AdxRisingBars == adxRisingBars && cacheTrendSpotter[idx].EmaPeriod == emaPeriod && cacheTrendSpotter[idx].ShowEntrySignals == showEntrySignals && cacheTrendSpotter[idx].ShowExitSignals == showExitSignals && cacheTrendSpotter[idx].Signal_Offset == signal_Offset && cacheTrendSpotter[idx].LongOn == longOn && cacheTrendSpotter[idx].ShortOn == shortOn && cacheTrendSpotter[idx].LongOff == longOff && cacheTrendSpotter[idx].ShortOff == shortOff && cacheTrendSpotter[idx].PartialSignalColor == partialSignalColor && cacheTrendSpotter[idx].PartialSignalOpacity == partialSignalOpacity && cacheTrendSpotter[idx].LongEntryColor == longEntryColor && cacheTrendSpotter[idx].ShortEntryColor == shortEntryColor && cacheTrendSpotter[idx].ExitColor == exitColor && cacheTrendSpotter[idx].EnableDataTracking == enableDataTracking && cacheTrendSpotter[idx].EqualsInput(input))
						return cacheTrendSpotter[idx];
			return CacheIndicator<Myindicators.TrendSpotter>(new Myindicators.TrendSpotter(){ MacdFast = macdFast, MacdSlow = macdSlow, MacdSmooth = macdSmooth, MacdMAType = macdMAType, DmPeriod = dmPeriod, AdxRisingBars = adxRisingBars, EmaPeriod = emaPeriod, ShowEntrySignals = showEntrySignals, ShowExitSignals = showExitSignals, Signal_Offset = signal_Offset, LongOn = longOn, ShortOn = shortOn, LongOff = longOff, ShortOff = shortOff, PartialSignalColor = partialSignalColor, PartialSignalOpacity = partialSignalOpacity, LongEntryColor = longEntryColor, ShortEntryColor = shortEntryColor, ExitColor = exitColor, EnableDataTracking = enableDataTracking }, input, ref cacheTrendSpotter);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.TrendSpotter TrendSpotter(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}

		public Indicators.Myindicators.TrendSpotter TrendSpotter(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor, bool enableDataTracking)
		{
			return indicator.TrendSpotter(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor, enableDataTracking);
		}
	}
}

#endregion
