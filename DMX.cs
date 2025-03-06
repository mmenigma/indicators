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
    public class DMX : Indicator
    {
        private Series<double> dmPlus;
        private Series<double> dmMinus;
        private Series<double> sumDmPlus;
        private Series<double> sumDmMinus;
        private Series<double> sumTr;
        private Series<double> tr;
        private bool adxRisingActive = false;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Enter the description for your new custom Indicator here.";
                Name = "DMX";
                IsSuspendedWhileInactive = true;
                Period = 14;
                LowerThresholdDefault = 25;
                UpperThresholdDefault = 50;
                TradeThresholdDefault = 20; // Default value for Trade Threshold

                AddPlot(new Stroke(Brushes.DarkSeaGreen, 2), PlotStyle.Line, NinjaTrader.Custom.Resource.NinjaScriptIndicatorNameADX);
                AddPlot(Brushes.DodgerBlue, NinjaTrader.Custom.Resource.DMPlusDI);
                AddPlot(Brushes.Crimson, NinjaTrader.Custom.Resource.DMMinusDI);

                AddPlot(Brushes.DarkCyan, "Lower Threshold"); // Lower threshold plot
                AddPlot(Brushes.Goldenrod, "Upper Threshold"); // Upper threshold plot
                AddPlot(Brushes.BlueViolet, "Trade Threshold"); // Trade threshold plot

                LongFilterOn = "LongOn";
                ShortFilterOn = "ShortOn";
                Signal_Offset = 5;
                ADXCross = true;
                DICross = true;
                ADXRising = true;
            }
            else if (State == State.DataLoaded)
            {
                dmPlus = new Series<double>(this);
                dmMinus = new Series<double>(this);
                sumDmPlus = new Series<double>(this);
                sumDmMinus = new Series<double>(this);
                sumTr = new Series<double>(this);
                tr = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            double high0 = High[0];
            double low0 = Low[0];
            double trueRange = high0 - low0;

            if (CurrentBar == 0)
            {
                tr[0] = trueRange;
                dmPlus[0] = 0;
                dmMinus[0] = 0;
                sumTr[0] = tr[0];
                sumDmPlus[0] = dmPlus[0];
                sumDmMinus[0] = dmMinus[0];
                ADXPlot[0] = 50;

                Values[3][0] = LowerThresholdDefault; // Lower threshold plot value
                Values[4][0] = UpperThresholdDefault; // Upper threshold plot value
                Values[5][0] = TradeThresholdDefault; // Trade threshold plot value
            }
            else
            {
                double low1 = Low[1];
                double high1 = High[1];
                double close1 = Close[1];

                tr[0] = Math.Max(Math.Abs(low0 - close1), Math.Max(trueRange, Math.Abs(high0 - close1)));
                dmPlus[0] = high0 - high1 > low1 - low0 ? Math.Max(high0 - high1, 0) : 0;
                dmMinus[0] = low1 - low0 > high0 - high1 ? Math.Max(low1 - low0, 0) : 0;

                double sumDmPlus1 = sumDmPlus[1];
                double sumDmMinus1 = sumDmMinus[1];
                double sumTr1 = sumTr[1];

                if (CurrentBar < Period)
                {
                    sumTr[0] = sumTr1 + tr[0];
                    sumDmPlus[0] = sumDmPlus1 + dmPlus[0];
                    sumDmMinus[0] = sumDmMinus1 + dmMinus[0];
                }
                else
                {
                    sumTr[0] = sumTr1 - sumTr[1] / Period + tr[0];
                    sumDmPlus[0] = sumDmPlus1 - sumDmPlus1 / Period + dmPlus[0];
                    sumDmMinus[0] = sumDmMinus1 - sumDmMinus1 / Period + dmMinus[0];
                }

                double diPlus = 100 * (sumTr[0] == 0 ? 0 : sumDmPlus[0] / sumTr[0]);
                double diMinus = 100 * (sumTr[0] == 0 ? 0 : sumDmMinus[0] / sumTr[0]);
                double diff = Math.Abs(diPlus - diMinus);
                double sum = diPlus + diMinus;

                ADXPlot[0] = sum == 0 ? 50 : ((Period - 1) * ADXPlot[1] + 100 * diff / sum) / Period;
                DiPlus[0] = diPlus;
                DiMinus[0] = diMinus;

                Values[3][0] = LowerThresholdDefault; // Lower threshold plot value
                Values[4][0] = UpperThresholdDefault; // Upper threshold plot value
                Values[5][0] = TradeThresholdDefault; // Trade threshold plot value

                // ADX Cross Signal Logic
                if (ADXCross && ADXPlot[0] >= TradeThresholdDefault)
                {
                    // Long Signal: ADX crosses above the Lower Threshold and price is increasing
                    if (CrossAbove(ADXPlot, LowerThresholdDefault, 1) && Close[0] > Close[1])
                    {
                        Draw.ArrowUp(this, LongFilterOn + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, Period < 10 ? Brushes.Goldenrod : Brushes.Goldenrod);
                    }

                    // Short Signal: ADX crosses above the Lower Threshold and price is decreasing
                    if (CrossAbove(ADXPlot, LowerThresholdDefault, 1) && Close[0] < Close[1])
                    {
                        Draw.ArrowDown(this, ShortFilterOn + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, Period < 10 ? Brushes.Goldenrod : Brushes.Goldenrod);
                    }
                }

                // DI Cross Signal Logic
                if (DICross && ADXPlot[0] >= TradeThresholdDefault)
                {
                    // Long Triangle: +DI crosses above -DI AND 5 bars ago +DI was less than -DI
                    if (CrossAbove(DiPlus, DiMinus, 1) && CurrentBar >= 5 && DiPlus[5] < DiMinus[5])
                    {
                        Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, Period > 10 ? Brushes.Goldenrod : Brushes.Goldenrod);
                    }

                    // Short Triangle: -DI crosses above +DI AND 5 bars ago -DI was less than +DI
                    if (CrossAbove(DiMinus, DiPlus, 1) && CurrentBar >= 5 && DiMinus[5] < DiPlus[5])
                    {
                        Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, Period > 10 ? Brushes.Goldenrod : Brushes.Goldenrod);
                    }
                }

                // ADX Rising Signal Logic
                if (ADXRising && ADXPlot[0] >= TradeThresholdDefault)
                {
                    if (CurrentBar >= 3)  // Ensure enough bars for checking ADX rising condition
                    {
                        bool isADXRising = ADXPlot[0] > ADXPlot[1] && ADXPlot[1] > ADXPlot[2] && ADXPlot[2] > ADXPlot[3];
                        bool isTrendReversal = (Close[0] < Open[0] && Close[1] > Open[1]) || (Close[0] > Open[0] && Close[1] < Open[1]);

                        if (isTrendReversal)
                        {
                            adxRisingActive = false;  // Reset the signal after a trend reversal
                        }

                        if (isADXRising && !adxRisingActive)
                        {
                            if (Close[0] > Open[0]) // Long signal
                            {
                                Draw.Diamond(this, LongFilterOn + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, Period > 10 ? Brushes.Goldenrod : Brushes.Goldenrod);
                            }
                            else if (Close[0] < Open[0]) // Short signal
                            {
                                Draw.Diamond(this, ShortFilterOn + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, Period > 10 ? Brushes.Goldenrod : Brushes.Goldenrod);
                            }
                            adxRisingActive = true;  // Activate ADX Rising state
                        }
                    }
                }
            }
        }

        #region Properties
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> ADXPlot
        {
            get { return Values[0]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> DiPlus
        {
            get { return Values[1]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> DiMinus
        {
            get { return Values[2]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> LowerThreshold
        {
            get { return Values[3]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> UpperThreshold
        {
            get { return Values[4]; }
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> TradeThreshold
        {
            get { return Values[5]; }
        }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Period", GroupName = "NinjaScriptParameters", Order = 0)]
        public int Period { get; set; }

        [Range(0, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Lower Threshold", GroupName = "NinjaScriptParameters", Order = 1)]
        public int LowerThresholdDefault { get; set; }

        [Range(0, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Upper Threshold", GroupName = "NinjaScriptParameters", Order = 2)]
        public int UpperThresholdDefault { get; set; }

        [Range(0, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Trade Threshold", GroupName = "NinjaScriptParameters", Order = 3)]
        public int TradeThresholdDefault { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ADX Cross ↑↓", GroupName = "SignalSettings", Order = 1)]
        public bool ADXCross { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DI Cross ▲", GroupName = "SignalSettings", Order = 2)]
        public bool DICross { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ADX Rising ◆", GroupName = "SignalSettings", Order = 3)]
        public bool ADXRising { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "LongFilterOn", GroupName = "SignalSettings", Order = 4)]
        public string LongFilterOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ShortFilterOn", GroupName = "SignalSettings", Order = 5)]
        public string ShortFilterOn { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal_Offset", GroupName = "SignalSettings", Order = 6)]
        public double Signal_Offset { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.DMX[] cacheDMX;
		public Myindicators.DMX DMX(int period, int lowerThresholdDefault, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dICross, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset)
		{
			return DMX(Input, period, lowerThresholdDefault, upperThresholdDefault, tradeThresholdDefault, aDXCross, dICross, aDXRising, longFilterOn, shortFilterOn, signal_Offset);
		}

		public Myindicators.DMX DMX(ISeries<double> input, int period, int lowerThresholdDefault, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dICross, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset)
		{
			if (cacheDMX != null)
				for (int idx = 0; idx < cacheDMX.Length; idx++)
					if (cacheDMX[idx] != null && cacheDMX[idx].Period == period && cacheDMX[idx].LowerThresholdDefault == lowerThresholdDefault && cacheDMX[idx].UpperThresholdDefault == upperThresholdDefault && cacheDMX[idx].TradeThresholdDefault == tradeThresholdDefault && cacheDMX[idx].ADXCross == aDXCross && cacheDMX[idx].DICross == dICross && cacheDMX[idx].ADXRising == aDXRising && cacheDMX[idx].LongFilterOn == longFilterOn && cacheDMX[idx].ShortFilterOn == shortFilterOn && cacheDMX[idx].Signal_Offset == signal_Offset && cacheDMX[idx].EqualsInput(input))
						return cacheDMX[idx];
			return CacheIndicator<Myindicators.DMX>(new Myindicators.DMX(){ Period = period, LowerThresholdDefault = lowerThresholdDefault, UpperThresholdDefault = upperThresholdDefault, TradeThresholdDefault = tradeThresholdDefault, ADXCross = aDXCross, DICross = dICross, ADXRising = aDXRising, LongFilterOn = longFilterOn, ShortFilterOn = shortFilterOn, Signal_Offset = signal_Offset }, input, ref cacheDMX);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.DMX DMX(int period, int lowerThresholdDefault, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dICross, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset)
		{
			return indicator.DMX(Input, period, lowerThresholdDefault, upperThresholdDefault, tradeThresholdDefault, aDXCross, dICross, aDXRising, longFilterOn, shortFilterOn, signal_Offset);
		}

		public Indicators.Myindicators.DMX DMX(ISeries<double> input , int period, int lowerThresholdDefault, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dICross, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset)
		{
			return indicator.DMX(input, period, lowerThresholdDefault, upperThresholdDefault, tradeThresholdDefault, aDXCross, dICross, aDXRising, longFilterOn, shortFilterOn, signal_Offset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.DMX DMX(int period, int lowerThresholdDefault, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dICross, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset)
		{
			return indicator.DMX(Input, period, lowerThresholdDefault, upperThresholdDefault, tradeThresholdDefault, aDXCross, dICross, aDXRising, longFilterOn, shortFilterOn, signal_Offset);
		}

		public Indicators.Myindicators.DMX DMX(ISeries<double> input , int period, int lowerThresholdDefault, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dICross, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset)
		{
			return indicator.DMX(input, period, lowerThresholdDefault, upperThresholdDefault, tradeThresholdDefault, aDXCross, dICross, aDXRising, longFilterOn, shortFilterOn, signal_Offset);
		}
	}
}

#endregion
