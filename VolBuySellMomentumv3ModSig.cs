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
namespace NinjaTrader.NinjaScript.Indicators
{
    public class VolBuySellMomentumv3ModSig : Indicator
    {
        private double xROC;
        private Series<double> nRes1, nRes2, nRes3, nResEMA3, PNVI_PEMA_Diff;
        Brush lightUpColor, lightDownColor;
        SimpleFont valueFont;
        SimpleFont valuePriceChartFont;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Enter the description for your new custom Indicator here.";
                Name = "VolBuySellMomentumv3ModSig";
                Calculate = Calculate.OnBarClose;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                ROC_MA = 25;
                Delta_MA = 14;
                Avg_Delta_MA = 8;
                bullBrush = Brushes.Lime;
                bearBrush = Brushes.Red;
                noTrendBrush = Brushes.Gray;
                ColorBars = true;
                AddPlot(new Stroke(Brushes.Gray), PlotStyle.Bar, "PVI PEMA Diff Histogram");
                AddPlot(Brushes.Cyan, "PVI_NVI");
                AddPlot(Brushes.White, "PEMA");
                AddPlot(Brushes.Goldenrod, "Upper Limit");
                AddPlot(Brushes.Goldenrod, "Lower Limit");
                Plots[0].AutoWidth = true;

                ValueFontSize = 9;
                DistanceFromBarToValue = 0.005;
                ValueFontSizePriceChart = 9;
                DistanceFromBarToValuePriceChart = 60;

                ValueColorUp = ValueColorDown = ValueColorUpPriceChart = ValueColorDownPriceChart = Brushes.Gold;

                AlertCrossSignal = false;

                LongOn = true;
                ShortOn = true;

                LongOnString = "LongOn";
                ShortOnString = "ShortOn";

                UpperLimit = 0.05;
                LowerLimit = -0.05;
				Signal_Offset =5;
				
            }
            else if (State == State.Configure)
            {
                valueFont = new SimpleFont("Arial", ValueFontSize) { Size = ValueFontSize, Bold = ValueBold };
                valuePriceChartFont = new SimpleFont("Arial", ValueFontSizePriceChart) { Size = ValueFontSizePriceChart, Bold = ValueBoldPriceChart };
            }
            else if (State == State.DataLoaded)
            {
                nRes1 = new Series<double>(this);
                nRes2 = new Series<double>(this);
                nRes3 = new Series<double>(this);
                nResEMA3 = new Series<double>(this);
                PNVI_PEMA_Diff = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < ROC_MA) return;
            if (lightUpColor == null)
            {
                lightUpColor = bullBrush.Clone();
                lightUpColor.Opacity = 1;
                lightUpColor.Freeze();

                lightDownColor = bearBrush.Clone();
                lightDownColor.Opacity = 1;
                lightDownColor.Freeze();
            }

            xROC = ROC(Close, 1)[0];
            nRes1[0] = (Bars.GetVolume(0) < Bars.GetVolume(1)) ? nRes1[1] + xROC : nRes1[1];
            nRes2[0] = (Bars.GetVolume(0) > Bars.GetVolume(1)) ? nRes2[1] + xROC : nRes2[1];
            nRes3[0] = nRes1[0] + nRes2[0];
            nResEMA3[0] = SMA(nRes1, ROC_MA)[0] + SMA(nRes2, ROC_MA)[0];
            PNVI_PEMA_Diff[0] = nRes3[0] - nResEMA3[0];
            PNVI_PEMA_Diff_Hist[0] = PNVI_PEMA_Diff[0] * 0.5;
            PVI_NVI[0] = EMA(PNVI_PEMA_Diff, Delta_MA)[0];
            PEMA[0] = EMA(PVI_NVI, Avg_Delta_MA)[0];

            if (ShowValue || ShowValuePriceChart)
            {
                double value = (PNVI_PEMA_Diff_Hist[0] * 1000);
                string drawValue = value.ToString("0");

                if (ShowValue && ((UseValueFilter && (value > ValueFilter || value < -ValueFilter)) || (!UseValueFilter)))
                {
                    Brush colorValue = PNVI_PEMA_Diff_Hist[0] > 0 ? ValueColorUp : ValueColorDown;
                    double positionValue = PNVI_PEMA_Diff_Hist[0] + (PNVI_PEMA_Diff_Hist[0] > 0 ? DistanceFromBarToValue : -DistanceFromBarToValue);
                    DrawOnPricePanel = false;
                    Draw.Text(this, "ValueText" + CurrentBar, true, drawValue, 0, positionValue, 0, colorValue, valueFont, TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
                    DrawOnPricePanel = true;
                }

                if (ShowValuePriceChart && ((UseValueFilterPriceChart && (value > ValueFilterPriceChart || value < -ValueFilterPriceChart)) || (!UseValueFilterPriceChart)))
                {
                    Brush colorValue = PNVI_PEMA_Diff_Hist[0] > 0 ? ValueColorUpPriceChart : ValueColorDownPriceChart;
                    double positionValue = Close[0] + (Close[0] > Close[1] ? DistanceFromBarToValuePriceChart : -DistanceFromBarToValuePriceChart);
                    Draw.Text(this, "ValuePriceChartText" + CurrentBar, true, drawValue, 0, positionValue, 0, colorValue, valuePriceChartFont, TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            PlotBrushes[0][0] = PNVI_PEMA_Diff_Hist[0] > 0 && PNVI_PEMA_Diff_Hist[0] > PNVI_PEMA_Diff_Hist[1] ? Brushes.Lime
                                            : PNVI_PEMA_Diff_Hist[0] > 0 ? Brushes.DarkGreen
                                            : PNVI_PEMA_Diff_Hist[0] < 0 && PNVI_PEMA_Diff_Hist[0] < PNVI_PEMA_Diff_Hist[1] ? Brushes.Red
                                            : Brushes.DarkRed;

            if (ShowCrossSingal)
            {
                bool isCross = false;
                if (CrossAbove(PVI_NVI, PEMA, 1) && LongOn && PVI_NVI[0] < LowerLimit)
                {
                    Draw.ArrowUp(this, LongOnString + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, lightUpColor);
                    isCross = true;
                }
                else if (CrossBelow(PVI_NVI, PEMA, 1) && ShortOn && PVI_NVI[0] > UpperLimit)
                {
                    Draw.ArrowDown(this, ShortOnString + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, lightDownColor);
                    isCross = true;
                }

                if (isCross)
                {
                    if (AlertCrossSignal)
                    {
                        if (!string.IsNullOrEmpty(SoundPath))
                        {
                            PlaySound(SoundPath);
                        }
                    }
                }
            }

            if (ColorBars)
            {
                if (PNVI_PEMA_Diff[0] > 0 && PVI_NVI[0] > PEMA[0])
                {
                    BarBrush = bullBrush;
                    CandleOutlineBrush = bullBrush;
                    if (Close[0] < Open[0]) BarBrush = lightUpColor;
                }
                else if (PNVI_PEMA_Diff[0] < 0 && PVI_NVI[0] < PEMA[0])
                {
                    BarBrush = bearBrush;
                    CandleOutlineBrush = bearBrush;
                    if (Close[0] > Open[0]) BarBrush = lightDownColor;
                }
                else
                {
                    BarBrush = noTrendBrush;
                    CandleOutlineBrush = noTrendBrush;
                }
            }

            // Plot the Upper and Lower Limits
            Values[3][0] = UpperLimit;
            Values[4][0] = LowerLimit;
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "ROC MA Length", Order = 1, GroupName = "Parameters")]
        public int ROC_MA { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "Delta MA Length", Order = 2, GroupName = "Parameters")]
        public int Delta_MA { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "Avg Delta MA Length", Order = 3, GroupName = "Parameters")]
        public int Avg_Delta_MA { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ColorBars", GroupName = "Display", Order = 1)]
        public bool ColorBars { get; set; }

        [XmlIgnore()]
        [NinjaScriptProperty]
        [Display(Name = "Candle UpColor", GroupName = "Display", Order = 2)]
        public Brush bullBrush { get; set; }

        [XmlIgnore()]
        [NinjaScriptProperty]
        [Display(Name = "Candle DownColor", GroupName = "Display", Order = 3)]
        public Brush bearBrush { get; set; }

        [XmlIgnore()]
        [NinjaScriptProperty]
        [Display(Name = "No Trend Candle Color", GroupName = "Display", Order = 4)]
        public Brush noTrendBrush { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Value", GroupName = "Indicator Chart", Order = 1)]
        public bool ShowValue { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Filter?", GroupName = "Indicator Chart", Order = 2)]
        public bool UseValueFilter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Filter Value", GroupName = "Indicator Chart", Order = 3)]
        public double ValueFilter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Value Size", GroupName = "Indicator Chart", Order = 4)]
        public double ValueFontSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Distance from the bar to value (Shoud < 0.01)", GroupName = "Indicator Chart", Order = 5)]
        public double DistanceFromBarToValue { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Value Bold?", GroupName = "Indicator Chart", Order = 6)]
        public bool ValueBold { get; set; }

        [XmlIgnore()]
        [Display(Name = "Value Color Up", GroupName = "Indicator Chart", Order = 7)]
        public Brush ValueColorUp { get; set; }

        [Browsable(false)]
        public string ValueColorUpSerialize
        {
            get { return Serialize.BrushToString(ValueColorUp); }
            set { ValueColorUp = Serialize.StringToBrush(value); }
        }

        [XmlIgnore()]
        [Display(Name = "Value Color Down", GroupName = "Indicator Chart", Order = 8)]
        public Brush ValueColorDown { get; set; }

        [Browsable(false)]
        public string ValueColorDownSerialize
        {
            get { return Serialize.BrushToString(ValueColorDown); }
            set { ValueColorDown = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Value on Price Chart", GroupName = "Price Chart", Order = 1)]
        public bool ShowValuePriceChart { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Filter?", GroupName = "Price Chart", Order = 2)]
        public bool UseValueFilterPriceChart { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Filter Value?", GroupName = "Price Chart", Order = 3)]
        public double ValueFilterPriceChart { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Value Size", GroupName = "Price Chart", Order = 4)]
        public double ValueFontSizePriceChart { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Distance from the bar to value", GroupName = "Price Chart", Order = 5)]
        public double DistanceFromBarToValuePriceChart { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Value Bold?", GroupName = "Price Chart", Order = 6)]
        public bool ValueBoldPriceChart { get; set; }

        [XmlIgnore()]
        [Display(Name = "Value Color Up", GroupName = "Price Chart", Order = 7)]
        public Brush ValueColorUpPriceChart { get; set; }

        [Browsable(false)]
        public string ValueColorUpPriceChartSerialize
        {
            get { return Serialize.BrushToString(ValueColorUpPriceChart); }
            set { ValueColorUpPriceChart = Serialize.StringToBrush(value); }
        }

        [XmlIgnore()]
        [Display(Name = "Value Color Down", GroupName = "Price Chart", Order = 8)]
        public Brush ValueColorDownPriceChart { get; set; }

        [Display(Name = "Show Cross Signal?", GroupName = "Signal & Alert", Order = 0)]
        public bool ShowCrossSingal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "LongOn", Order = 1, GroupName = "Signal & Alert")]
        public bool LongOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ShortOn", Order = 2, GroupName = "Signal & Alert")]
        public bool ShortOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "LongOnString", Order = 3, GroupName = "Signal & Alert")]
        public string LongOnString { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ShortOnString", Order = 4, GroupName = "Signal & Alert")]
        public string ShortOnString { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="Signal_Offset", Order=5, GroupName= "Signal & Alert")]
		public int Signal_Offset { get; set;}
		
		[NinjaScriptProperty]
        [Display(Name = "Upper Limit", Order = 6, GroupName = "Signal & Alert")]
        public double UpperLimit { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Lower Limit", Order = 7, GroupName = "Signal & Alert")]
        public double LowerLimit { get; set; }

        [Display(Name = "Alert Cross?", GroupName = "Signal & Alert", Order = 8)]
        public bool AlertCrossSignal { get; set; }

        [Display(Name = "Sound", Order = 9, GroupName = "Signal & Alert")]
        [PropertyEditor("NinjaTrader.Gui.Tools.FilePathPicker", Filter = "WAV Files (*.wav)|*.wav")]
        public string SoundPath { get; set; }

        [Browsable(false)]
        public string ValueColorDownPriceChartSerialize
        {
            get { return Serialize.BrushToString(ValueColorDownPriceChart); }
            set { ValueColorDownPriceChart = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> PNVI_PEMA_Diff_Hist
        {
            get { return Values[0]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> PVI_NVI
        {
            get { return Values[1]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> PEMA
        {
            get { return Values[2]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> UpperLimitPlot
        {
            get { return Values[3]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> LowerLimitPlot
        {
            get { return Values[4]; }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private VolBuySellMomentumv3ModSig[] cacheVolBuySellMomentumv3ModSig;
		public VolBuySellMomentumv3ModSig VolBuySellMomentumv3ModSig(int rOC_MA, bool colorBars, Brush bullBrush, Brush bearBrush, Brush noTrendBrush, bool showValue, bool useValueFilter, double valueFilter, double valueFontSize, double distanceFromBarToValue, bool valueBold, bool showValuePriceChart, bool useValueFilterPriceChart, double valueFilterPriceChart, double valueFontSizePriceChart, double distanceFromBarToValuePriceChart, bool valueBoldPriceChart, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return VolBuySellMomentumv3ModSig(Input, rOC_MA, colorBars, bullBrush, bearBrush, noTrendBrush, showValue, useValueFilter, valueFilter, valueFontSize, distanceFromBarToValue, valueBold, showValuePriceChart, useValueFilterPriceChart, valueFilterPriceChart, valueFontSizePriceChart, distanceFromBarToValuePriceChart, valueBoldPriceChart, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}

		public VolBuySellMomentumv3ModSig VolBuySellMomentumv3ModSig(ISeries<double> input, int rOC_MA, bool colorBars, Brush bullBrush, Brush bearBrush, Brush noTrendBrush, bool showValue, bool useValueFilter, double valueFilter, double valueFontSize, double distanceFromBarToValue, bool valueBold, bool showValuePriceChart, bool useValueFilterPriceChart, double valueFilterPriceChart, double valueFontSizePriceChart, double distanceFromBarToValuePriceChart, bool valueBoldPriceChart, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			if (cacheVolBuySellMomentumv3ModSig != null)
				for (int idx = 0; idx < cacheVolBuySellMomentumv3ModSig.Length; idx++)
					if (cacheVolBuySellMomentumv3ModSig[idx] != null && cacheVolBuySellMomentumv3ModSig[idx].ROC_MA == rOC_MA && cacheVolBuySellMomentumv3ModSig[idx].ColorBars == colorBars && cacheVolBuySellMomentumv3ModSig[idx].bullBrush == bullBrush && cacheVolBuySellMomentumv3ModSig[idx].bearBrush == bearBrush && cacheVolBuySellMomentumv3ModSig[idx].noTrendBrush == noTrendBrush && cacheVolBuySellMomentumv3ModSig[idx].ShowValue == showValue && cacheVolBuySellMomentumv3ModSig[idx].UseValueFilter == useValueFilter && cacheVolBuySellMomentumv3ModSig[idx].ValueFilter == valueFilter && cacheVolBuySellMomentumv3ModSig[idx].ValueFontSize == valueFontSize && cacheVolBuySellMomentumv3ModSig[idx].DistanceFromBarToValue == distanceFromBarToValue && cacheVolBuySellMomentumv3ModSig[idx].ValueBold == valueBold && cacheVolBuySellMomentumv3ModSig[idx].ShowValuePriceChart == showValuePriceChart && cacheVolBuySellMomentumv3ModSig[idx].UseValueFilterPriceChart == useValueFilterPriceChart && cacheVolBuySellMomentumv3ModSig[idx].ValueFilterPriceChart == valueFilterPriceChart && cacheVolBuySellMomentumv3ModSig[idx].ValueFontSizePriceChart == valueFontSizePriceChart && cacheVolBuySellMomentumv3ModSig[idx].DistanceFromBarToValuePriceChart == distanceFromBarToValuePriceChart && cacheVolBuySellMomentumv3ModSig[idx].ValueBoldPriceChart == valueBoldPriceChart && cacheVolBuySellMomentumv3ModSig[idx].LongOn == longOn && cacheVolBuySellMomentumv3ModSig[idx].ShortOn == shortOn && cacheVolBuySellMomentumv3ModSig[idx].LongOnString == longOnString && cacheVolBuySellMomentumv3ModSig[idx].ShortOnString == shortOnString && cacheVolBuySellMomentumv3ModSig[idx].Signal_Offset == signal_Offset && cacheVolBuySellMomentumv3ModSig[idx].UpperLimit == upperLimit && cacheVolBuySellMomentumv3ModSig[idx].LowerLimit == lowerLimit && cacheVolBuySellMomentumv3ModSig[idx].EqualsInput(input))
						return cacheVolBuySellMomentumv3ModSig[idx];
			return CacheIndicator<VolBuySellMomentumv3ModSig>(new VolBuySellMomentumv3ModSig(){ ROC_MA = rOC_MA, ColorBars = colorBars, bullBrush = bullBrush, bearBrush = bearBrush, noTrendBrush = noTrendBrush, ShowValue = showValue, UseValueFilter = useValueFilter, ValueFilter = valueFilter, ValueFontSize = valueFontSize, DistanceFromBarToValue = distanceFromBarToValue, ValueBold = valueBold, ShowValuePriceChart = showValuePriceChart, UseValueFilterPriceChart = useValueFilterPriceChart, ValueFilterPriceChart = valueFilterPriceChart, ValueFontSizePriceChart = valueFontSizePriceChart, DistanceFromBarToValuePriceChart = distanceFromBarToValuePriceChart, ValueBoldPriceChart = valueBoldPriceChart, LongOn = longOn, ShortOn = shortOn, LongOnString = longOnString, ShortOnString = shortOnString, Signal_Offset = signal_Offset, UpperLimit = upperLimit, LowerLimit = lowerLimit }, input, ref cacheVolBuySellMomentumv3ModSig);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.VolBuySellMomentumv3ModSig VolBuySellMomentumv3ModSig(int rOC_MA, bool colorBars, Brush bullBrush, Brush bearBrush, Brush noTrendBrush, bool showValue, bool useValueFilter, double valueFilter, double valueFontSize, double distanceFromBarToValue, bool valueBold, bool showValuePriceChart, bool useValueFilterPriceChart, double valueFilterPriceChart, double valueFontSizePriceChart, double distanceFromBarToValuePriceChart, bool valueBoldPriceChart, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.VolBuySellMomentumv3ModSig(Input, rOC_MA, colorBars, bullBrush, bearBrush, noTrendBrush, showValue, useValueFilter, valueFilter, valueFontSize, distanceFromBarToValue, valueBold, showValuePriceChart, useValueFilterPriceChart, valueFilterPriceChart, valueFontSizePriceChart, distanceFromBarToValuePriceChart, valueBoldPriceChart, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}

		public Indicators.VolBuySellMomentumv3ModSig VolBuySellMomentumv3ModSig(ISeries<double> input , int rOC_MA, bool colorBars, Brush bullBrush, Brush bearBrush, Brush noTrendBrush, bool showValue, bool useValueFilter, double valueFilter, double valueFontSize, double distanceFromBarToValue, bool valueBold, bool showValuePriceChart, bool useValueFilterPriceChart, double valueFilterPriceChart, double valueFontSizePriceChart, double distanceFromBarToValuePriceChart, bool valueBoldPriceChart, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.VolBuySellMomentumv3ModSig(input, rOC_MA, colorBars, bullBrush, bearBrush, noTrendBrush, showValue, useValueFilter, valueFilter, valueFontSize, distanceFromBarToValue, valueBold, showValuePriceChart, useValueFilterPriceChart, valueFilterPriceChart, valueFontSizePriceChart, distanceFromBarToValuePriceChart, valueBoldPriceChart, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.VolBuySellMomentumv3ModSig VolBuySellMomentumv3ModSig(int rOC_MA, bool colorBars, Brush bullBrush, Brush bearBrush, Brush noTrendBrush, bool showValue, bool useValueFilter, double valueFilter, double valueFontSize, double distanceFromBarToValue, bool valueBold, bool showValuePriceChart, bool useValueFilterPriceChart, double valueFilterPriceChart, double valueFontSizePriceChart, double distanceFromBarToValuePriceChart, bool valueBoldPriceChart, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.VolBuySellMomentumv3ModSig(Input, rOC_MA, colorBars, bullBrush, bearBrush, noTrendBrush, showValue, useValueFilter, valueFilter, valueFontSize, distanceFromBarToValue, valueBold, showValuePriceChart, useValueFilterPriceChart, valueFilterPriceChart, valueFontSizePriceChart, distanceFromBarToValuePriceChart, valueBoldPriceChart, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}

		public Indicators.VolBuySellMomentumv3ModSig VolBuySellMomentumv3ModSig(ISeries<double> input , int rOC_MA, bool colorBars, Brush bullBrush, Brush bearBrush, Brush noTrendBrush, bool showValue, bool useValueFilter, double valueFilter, double valueFontSize, double distanceFromBarToValue, bool valueBold, bool showValuePriceChart, bool useValueFilterPriceChart, double valueFilterPriceChart, double valueFontSizePriceChart, double distanceFromBarToValuePriceChart, bool valueBoldPriceChart, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.VolBuySellMomentumv3ModSig(input, rOC_MA, colorBars, bullBrush, bearBrush, noTrendBrush, showValue, useValueFilter, valueFilter, valueFontSize, distanceFromBarToValue, valueBold, showValuePriceChart, useValueFilterPriceChart, valueFilterPriceChart, valueFontSizePriceChart, distanceFromBarToValuePriceChart, valueBoldPriceChart, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}
	}
}

#endregion
