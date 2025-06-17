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

namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    public class LRHG : Indicator
    {
        #region Variables
        private MACD macd;
        private DM dm;
        private EMA ema20;
        private Series<double> fastMA;
        private Series<double> slowMA;
        private Series<double> macdLine;
        private Series<double> signalLine;
        
        private bool longCondition1, longCondition2, longCondition3, longCondition4, longCondition5;
        private bool shortCondition1, shortCondition2, shortCondition3, shortCondition4, shortCondition5;
        private bool wasLongSignal, wasShortSignal;
        private bool inLongTrend, inShortTrend;
        private int longConditionCount, shortConditionCount;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"LRHG Trading Signal Indicator";
                Name = "LRHG";
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
            }
            else if (State == State.SetDefaults)
            {
            }
            else if (State == State.DataLoaded)
            {
                // Initialize indicators and series
                dm = DM(DmPeriod);
                ema20 = EMA(EmaPeriod);
                
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

            // Long Conditions
            longCondition1 = macdValue > 0 && macdValue > macdAvg;  // MACD above 0 and above Avg
            longCondition2 = adxRising;                             // ADX Rising
            longCondition3 = diPlus > diMinus;                      // DI is bullish (+DI > -DI)
            longCondition4 = Close[0] > emaValue;                   // Bar closes above 20EMA
            longCondition5 = CurrentBar > 0 ? macdValue > (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1]) : true; // MACD rising

            // Short Conditions  
            shortCondition1 = macdValue < 0 && macdValue < macdAvg; // MACD below 0 and below Avg
            shortCondition2 = adxRising;                            // ADX Rising
            shortCondition3 = diMinus > diPlus;                     // DI is bearish (-DI > +DI)
            shortCondition4 = Close[0] < emaValue;                  // Bar closes below 20EMA
            shortCondition5 = CurrentBar > 0 ? macdValue < (MacdMAType == CustomEnumNamespace.UniversalMovingAverage.EMA ? macd.Default[1] : macdLine[1]) : true; // MACD falling

            // Count conditions
            longConditionCount = (longCondition1 ? 1 : 0) + (longCondition2 ? 1 : 0) + 
                               (longCondition3 ? 1 : 0) + (longCondition4 ? 1 : 0) + (longCondition5 ? 1 : 0);
            
            shortConditionCount = (shortCondition1 ? 1 : 0) + (shortCondition2 ? 1 : 0) + 
                                (shortCondition3 ? 1 : 0) + (shortCondition4 ? 1 : 0) + (shortCondition5 ? 1 : 0);

            // Current signal status
            bool currentLongSignal = longConditionCount == 5;
            bool currentShortSignal = shortConditionCount == 5;

            // Check for exit conditions
            bool longExit = inLongTrend && macdValue < macdAvg;
            bool shortExit = inShortTrend && macdValue > macdAvg;

            // Update trend status
            if (currentLongSignal && !inLongTrend)
            {
                inLongTrend = true;
                inShortTrend = false;
            }
            else if (currentShortSignal && !inShortTrend)
            {
                inShortTrend = true;
                inLongTrend = false;
            }
            else if (longExit)
            {
                inLongTrend = false;
            }
            else if (shortExit)
            {
                inShortTrend = false;
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
                if (!inLongTrend && !inShortTrend && (longConditionCount == 4 || shortConditionCount == 4))
                {
                    BackBrush = CreateBrushWithOpacity(PartialSignalColor, PartialSignalOpacity);
                }
                else
                {
                    BackBrush = Brushes.Transparent;
                }
            }

            // Exit Signals
            if (ShowExitSignals && CurrentBar > 0)
            {
                // Long Exit: was in long trend, MACD crosses below avg
                if (longExit)
                {
                    Draw.Text(this, LongOff + CurrentBar, true, "o", 0, 
                             High[0] + Signal_Offset * TickSize, 0, ExitColor, 
                             new SimpleFont("Arial", 12), TextAlignment.Center, 
                             Brushes.Transparent, Brushes.Transparent, 0);
                }

                // Short Exit: was in short trend, MACD crosses above avg  
                if (shortExit)
                {
                    Draw.Text(this, ShortOff + CurrentBar, true, "o", 0,
                             Low[0] - Signal_Offset * TickSize, 0, ExitColor,
                             new SimpleFont("Arial", 12), TextAlignment.Center,
                             Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            // Update previous signal status
            wasLongSignal = currentLongSignal;
            wasShortSignal = currentShortSignal;
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

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.LRHG[] cacheLRHG;
		public Myindicators.LRHG LRHG(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return LRHG(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor);
		}

		public Myindicators.LRHG LRHG(ISeries<double> input, int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			if (cacheLRHG != null)
				for (int idx = 0; idx < cacheLRHG.Length; idx++)
					if (cacheLRHG[idx] != null && cacheLRHG[idx].MacdFast == macdFast && cacheLRHG[idx].MacdSlow == macdSlow && cacheLRHG[idx].MacdSmooth == macdSmooth && cacheLRHG[idx].MacdMAType == macdMAType && cacheLRHG[idx].DmPeriod == dmPeriod && cacheLRHG[idx].AdxRisingBars == adxRisingBars && cacheLRHG[idx].EmaPeriod == emaPeriod && cacheLRHG[idx].ShowEntrySignals == showEntrySignals && cacheLRHG[idx].ShowExitSignals == showExitSignals && cacheLRHG[idx].Signal_Offset == signal_Offset && cacheLRHG[idx].LongOn == longOn && cacheLRHG[idx].ShortOn == shortOn && cacheLRHG[idx].LongOff == longOff && cacheLRHG[idx].ShortOff == shortOff && cacheLRHG[idx].PartialSignalColor == partialSignalColor && cacheLRHG[idx].PartialSignalOpacity == partialSignalOpacity && cacheLRHG[idx].LongEntryColor == longEntryColor && cacheLRHG[idx].ShortEntryColor == shortEntryColor && cacheLRHG[idx].ExitColor == exitColor && cacheLRHG[idx].EqualsInput(input))
						return cacheLRHG[idx];
			return CacheIndicator<Myindicators.LRHG>(new Myindicators.LRHG(){ MacdFast = macdFast, MacdSlow = macdSlow, MacdSmooth = macdSmooth, MacdMAType = macdMAType, DmPeriod = dmPeriod, AdxRisingBars = adxRisingBars, EmaPeriod = emaPeriod, ShowEntrySignals = showEntrySignals, ShowExitSignals = showExitSignals, Signal_Offset = signal_Offset, LongOn = longOn, ShortOn = shortOn, LongOff = longOff, ShortOff = shortOff, PartialSignalColor = partialSignalColor, PartialSignalOpacity = partialSignalOpacity, LongEntryColor = longEntryColor, ShortEntryColor = shortEntryColor, ExitColor = exitColor }, input, ref cacheLRHG);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.LRHG LRHG(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.LRHG(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor);
		}

		public Indicators.Myindicators.LRHG LRHG(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.LRHG(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.LRHG LRHG(int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.LRHG(Input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor);
		}

		public Indicators.Myindicators.LRHG LRHG(ISeries<double> input , int macdFast, int macdSlow, int macdSmooth, CustomEnumNamespace.UniversalMovingAverage macdMAType, int dmPeriod, int adxRisingBars, int emaPeriod, bool showEntrySignals, bool showExitSignals, double signal_Offset, string longOn, string shortOn, string longOff, string shortOff, Brush partialSignalColor, int partialSignalOpacity, Brush longEntryColor, Brush shortEntryColor, Brush exitColor)
		{
			return indicator.LRHG(input, macdFast, macdSlow, macdSmooth, macdMAType, dmPeriod, adxRisingBars, emaPeriod, showEntrySignals, showExitSignals, signal_Offset, longOn, shortOn, longOff, shortOff, partialSignalColor, partialSignalOpacity, longEntryColor, shortEntryColor, exitColor);
		}
	}
}

#endregion
