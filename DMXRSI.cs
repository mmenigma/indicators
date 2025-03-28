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
    public class DMXRSI : Indicator
    {
        private Series<double> dmPlus;
        private Series<double> dmMinus;
        private Series<double> sumDmPlus;
        private Series<double> sumDmMinus;
        private Series<double> sumTr;
        private Series<double> tr;
        
        // RSI calculation variables
        private Series<double> avgGain;
        private Series<double> avgLoss;
        
        // Signal tracking flags
        private int longCrossoverBar = -1;
        private int shortCrossoverBar = -1;
        private bool hasGeneratedLongSignal = false;
        private bool hasGeneratedShortSignal = false;
		
         protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"DMX Indicator with DI Divergence and RSI Confirmation";
                Name = "DMXRSI";
                Calculate = Calculate.OnBarClose;
                IsSuspendedWhileInactive = true;
                Period = 14;
                TradeThresholdDefault = 5;
                
                // RSI settings
                RsiPeriod = 13;
                RsiSignalPeriod = 7;

                AddPlot(new Stroke(Brushes.Yellow, 2), PlotStyle.Line, "ADX");
                AddPlot(Brushes.Green, "+DI");
                AddPlot(Brushes.Crimson, "-DI");
                AddPlot(Brushes.BlueViolet, "No Trade Threshold");
                AddPlot(Brushes.DodgerBlue, "RSI");
                AddPlot(Brushes.Orange, "RSI Signal");

                LongFilterOn = "LongOn";
                ShortFilterOn = "ShortOn";
                Signal_Offset = 5;
                
                DIDivergencePoints = 1.0;
            }
            else if (State == State.Configure)
            {
                // Add any custom indicators
                AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.Line, "RSI Plot");
                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "Signal Plot");
            }
            else if (State == State.DataLoaded)
            {
                dmPlus = new Series<double>(this);
                dmMinus = new Series<double>(this);
                sumDmPlus = new Series<double>(this);
                sumDmMinus = new Series<double>(this);
                sumTr = new Series<double>(this);
                tr = new Series<double>(this);
                
                // Initialize series for RSI calculation
                avgGain = new Series<double>(this);
                avgLoss = new Series<double>(this);			
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
        Values[3][0] = TradeThresholdDefault;
        
        // Initialize RSI calculation values
        avgGain[0] = 0;
        avgLoss[0] = 0;
        Values[4][0] = 50; // RSI plot
        Values[5][0] = 50; // RSI Signal plot
        
        longCrossoverBar = -1;
        shortCrossoverBar = -1;
        hasGeneratedLongSignal = false;
        hasGeneratedShortSignal = false;
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
            sumTr[0] = sumTr1 - sumTr1 / Period + tr[0];
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

        Values[3][0] = TradeThresholdDefault;
        
        // Calculate RSI - use a purely manual implementation
        // First calculate the price change
        double change = 0;
        
        if (CurrentBar > 0)
            change = Close[0] - Close[1];
        
        // Calculate gain and loss
        double gain = Math.Max(0, change);
        double loss = Math.Max(0, -change);
        
        // Calculate average gain and loss
        if (CurrentBar <= RsiPeriod)
        {
            // Initialize or update simple averages until we have enough bars
            avgGain[0] = ((avgGain[1] * (CurrentBar - 1)) + gain) / CurrentBar;
            avgLoss[0] = ((avgLoss[1] * (CurrentBar - 1)) + loss) / CurrentBar;
        }
        else
        {
            // Use Wilder's smoothing method
            avgGain[0] = ((avgGain[1] * (RsiPeriod - 1)) + gain) / RsiPeriod;
            avgLoss[0] = ((avgLoss[1] * (RsiPeriod - 1)) + loss) / RsiPeriod;
        }
        
        // Calculate RSI
        double rsi = 50; // Default middle value
        
        if (CurrentBar >= RsiPeriod)
        {
            if (avgLoss[0] == 0)
                rsi = 100;
            else
            {
                double rs = avgGain[0] / avgLoss[0];
                rsi = 100 - (100 / (1 + rs));
            }
        }
        
        // Store current RSI
        Values[4][0] = rsi;
        
        // Calculate RSI Signal Line (simple moving average of RSI)
        double signalSum = 0;
        int validBars = 0;
        
        for (int i = 0; i < RsiSignalPeriod && i <= CurrentBar; i++)
        {
            signalSum += Values[4][i];
            validBars++;
        }
        
        double signalValue = validBars > 0 ? signalSum / validBars : rsi;
        Values[5][0] = signalValue;
        
        // DI Divergence Signal Logic - OPTIMIZED VERSION
        if (ADXPlot[0] >= TradeThresholdDefault)
        {
            // === LONG SIGNAL DETECTION ===
            
            // Check for new crossover (DI+ crosses above DI-)
            if (CrossAbove(DiPlus, DiMinus, 1))
            {
                // Reset tracking for a new potential signal
                longCrossoverBar = CurrentBar;
                hasGeneratedLongSignal = false;
                
                // Generate immediate signal if using 5-bar lookback mode
                if (DIDivergencePoints == 0 && CurrentBar >= 5 && DiPlus[5] < DiMinus[5] && Values[4][0] > Values[5][0])
                {
                    Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                        Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                    hasGeneratedLongSignal = true;
                }
            }
            
            // Signal on divergence after crossover (when DIDivergencePoints > 0)
            if (DIDivergencePoints > 0 && longCrossoverBar > 0 && !hasGeneratedLongSignal && 
                CurrentBar > longCrossoverBar && (CurrentBar - longCrossoverBar) <= 15)
            {
                // Get values at crossover
                double diPlusAtCross = DiPlus[CurrentBar - longCrossoverBar];
                double diMinusAtCross = DiMinus[CurrentBar - longCrossoverBar];
                
                // Check for divergence
                double diPlusDiff = DiPlus[0] - diPlusAtCross;
                double diMinusDiff = diMinusAtCross - DiMinus[0];
                
                // First time both conditions are met after a crossover
                if (diPlusDiff >= DIDivergencePoints && 
                    diMinusDiff >= DIDivergencePoints && 
                    DiPlus[0] > DiMinus[0] &&
                    Values[4][0] > Values[5][0])
                {
                    Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                        Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                    hasGeneratedLongSignal = true;
                }
            }
            
            // === SHORT SIGNAL DETECTION ===
            
            // Check for new crossover (DI- crosses above DI+)
            if (CrossAbove(DiMinus, DiPlus, 1))
            {
                // Reset tracking for a new potential signal
                shortCrossoverBar = CurrentBar;
                hasGeneratedShortSignal = false;
                
                // Generate immediate signal if using 5-bar lookback mode
                if (DIDivergencePoints == 0 && CurrentBar >= 5 && DiMinus[5] < DiPlus[5] && Values[4][0] < Values[5][0])
                {
                    Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                        High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
                    hasGeneratedShortSignal = true;
                }
            }
            
            // Signal on divergence after crossover (when DIDivergencePoints > 0)
            if (DIDivergencePoints > 0 && shortCrossoverBar > 0 && !hasGeneratedShortSignal && 
                CurrentBar > shortCrossoverBar && (CurrentBar - shortCrossoverBar) <= 15)
            {
                // Get values at crossover
                double diPlusAtCross = DiPlus[CurrentBar - shortCrossoverBar];
                double diMinusAtCross = DiMinus[CurrentBar - shortCrossoverBar];
                
                // Check for divergence
                double diMinusDiff = DiMinus[0] - diMinusAtCross;
                double diPlusDiff = diPlusAtCross - DiPlus[0];
                
                // First time both conditions are met after a crossover
                if (diMinusDiff >= DIDivergencePoints && 
                    diPlusDiff >= DIDivergencePoints && 
                    DiMinus[0] > DiPlus[0] &&
                    Values[4][0] < Values[5][0])
                {
                    Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                        High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
                    hasGeneratedShortSignal = true;
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
        public Series<double> TradeThreshold
        {
            get { return Values[3]; }
        }
        
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> RSILine
        {
            get { return Values[4]; }
        }
        
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> RSISignalLine
        {
            get { return Values[5]; }
        }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Period", GroupName = "NinjaScriptParameters", Order = 0)]
        public int Period { get; set; }

        [Range(0, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "No Trade Threshold", GroupName = "NinjaScriptParameters", Order = 2)]
        public int TradeThresholdDefault { get; set; }
		
		[NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "DI Divergence", Description = "Each DI line must move at least this many points to generate a signal", GroupName = "DI Divergence Setting", Order = 1)]
        public double DIDivergencePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "LongFilterOn", GroupName = "Signal Settings", Order = 2)]
        public string LongFilterOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ShortFilterOn", GroupName = "Signal Settings", Order = 3)]
        public string ShortFilterOn { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal_Offset", GroupName = "Signal Settings", Order = 4)]
        public double Signal_Offset { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "RSI Period", Description = "Period for RSI calculation", GroupName = "RSI Parameters", Order = 1)]
        public int RsiPeriod { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "RSI Signal Period", Description = "Period for RSI signal line", GroupName = "RSI Parameters", Order = 2)]
        public int RsiSignalPeriod { get; set; }  
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.DMXRSI[] cacheDMXRSI;
		public Myindicators.DMXRSI DMXRSI(int period, int tradeThresholdDefault, double dIDivergencePoints, string longFilterOn, string shortFilterOn, double signal_Offset, int rsiPeriod, int rsiSignalPeriod)
		{
			return DMXRSI(Input, period, tradeThresholdDefault, dIDivergencePoints, longFilterOn, shortFilterOn, signal_Offset, rsiPeriod, rsiSignalPeriod);
		}

		public Myindicators.DMXRSI DMXRSI(ISeries<double> input, int period, int tradeThresholdDefault, double dIDivergencePoints, string longFilterOn, string shortFilterOn, double signal_Offset, int rsiPeriod, int rsiSignalPeriod)
		{
			if (cacheDMXRSI != null)
				for (int idx = 0; idx < cacheDMXRSI.Length; idx++)
					if (cacheDMXRSI[idx] != null && cacheDMXRSI[idx].Period == period && cacheDMXRSI[idx].TradeThresholdDefault == tradeThresholdDefault && cacheDMXRSI[idx].DIDivergencePoints == dIDivergencePoints && cacheDMXRSI[idx].LongFilterOn == longFilterOn && cacheDMXRSI[idx].ShortFilterOn == shortFilterOn && cacheDMXRSI[idx].Signal_Offset == signal_Offset && cacheDMXRSI[idx].RsiPeriod == rsiPeriod && cacheDMXRSI[idx].RsiSignalPeriod == rsiSignalPeriod && cacheDMXRSI[idx].EqualsInput(input))
						return cacheDMXRSI[idx];
			return CacheIndicator<Myindicators.DMXRSI>(new Myindicators.DMXRSI(){ Period = period, TradeThresholdDefault = tradeThresholdDefault, DIDivergencePoints = dIDivergencePoints, LongFilterOn = longFilterOn, ShortFilterOn = shortFilterOn, Signal_Offset = signal_Offset, RsiPeriod = rsiPeriod, RsiSignalPeriod = rsiSignalPeriod }, input, ref cacheDMXRSI);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.DMXRSI DMXRSI(int period, int tradeThresholdDefault, double dIDivergencePoints, string longFilterOn, string shortFilterOn, double signal_Offset, int rsiPeriod, int rsiSignalPeriod)
		{
			return indicator.DMXRSI(Input, period, tradeThresholdDefault, dIDivergencePoints, longFilterOn, shortFilterOn, signal_Offset, rsiPeriod, rsiSignalPeriod);
		}

		public Indicators.Myindicators.DMXRSI DMXRSI(ISeries<double> input , int period, int tradeThresholdDefault, double dIDivergencePoints, string longFilterOn, string shortFilterOn, double signal_Offset, int rsiPeriod, int rsiSignalPeriod)
		{
			return indicator.DMXRSI(input, period, tradeThresholdDefault, dIDivergencePoints, longFilterOn, shortFilterOn, signal_Offset, rsiPeriod, rsiSignalPeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.DMXRSI DMXRSI(int period, int tradeThresholdDefault, double dIDivergencePoints, string longFilterOn, string shortFilterOn, double signal_Offset, int rsiPeriod, int rsiSignalPeriod)
		{
			return indicator.DMXRSI(Input, period, tradeThresholdDefault, dIDivergencePoints, longFilterOn, shortFilterOn, signal_Offset, rsiPeriod, rsiSignalPeriod);
		}

		public Indicators.Myindicators.DMXRSI DMXRSI(ISeries<double> input , int period, int tradeThresholdDefault, double dIDivergencePoints, string longFilterOn, string shortFilterOn, double signal_Offset, int rsiPeriod, int rsiSignalPeriod)
		{
			return indicator.DMXRSI(input, period, tradeThresholdDefault, dIDivergencePoints, longFilterOn, shortFilterOn, signal_Offset, rsiPeriod, rsiSignalPeriod);
		}
	}
}

#endregion
