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
	public class DMXv2 : Indicator
	{
		private Series<double> dmPlus;
        private Series<double> dmMinus;
        private Series<double> sumDmPlus;
        private Series<double> sumDmMinus;
        private Series<double> sumTr;
        private Series<double> tr;
        private bool adxRisingActive = false;
        
        // Volume calculation variables
        private Series<double> buyVolume;
        private Series<double> sellVolume;
        private Series<double> buyVolumeAvg;
        private Series<double> sellVolumeAvg;
        private Series<double> buyVolumeEMA;
        private Series<double> sellVolumeEMA;
        
        // Signal tracking flags
        private int longCrossoverBar = -1;
        private int shortCrossoverBar = -1;
        private int longConvergenceBar = -1;
        private int shortConvergenceBar = -1;
        private bool hasGeneratedLongSignal = false;
        private bool hasGeneratedShortSignal = false;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"DMX Indicator with Enhanced DI Divergence";
				Name										= "DMXv2";
				Calculate									= Calculate.OnBarClose;
				IsSuspendedWhileInactive = true;
                Period = 14;
                ADXCrossover = 25;
                UpperThresholdDefault = 50;
                TradeThresholdDefault = 20;

                AddPlot(new Stroke(Brushes.Yellow, 2), PlotStyle.Line, "ADX");
                AddPlot(Brushes.Green, "+DI");
                AddPlot(Brushes.Crimson, "-DI");

                AddPlot(Brushes.DarkCyan, "ADX Cross");
                AddPlot(Brushes.Goldenrod, "Upper Threshold");
                AddPlot(Brushes.BlueViolet, "No Trade Threshold");

                LongFilterOn = "LongOn";
                ShortFilterOn = "ShortOn";
                Signal_Offset = 5;
                ADXCross = false;
                DIDiv = true;
                ADXRising = false;
                
                BuyVolumeAvgLength = 17;
                SellVolumeAvgLength = 17;
                BSVPEmaPeriod = 9;
                DIDivergencePoints = 1.0;
                DIConvergenceThreshold = 0.5;
            }
            else if (State == State.DataLoaded)
            {
                dmPlus = new Series<double>(this);
                dmMinus = new Series<double>(this);
                sumDmPlus = new Series<double>(this);
                sumDmMinus = new Series<double>(this);
                sumTr = new Series<double>(this);
                tr = new Series<double>(this);
                
                buyVolume = new Series<double>(this);
                sellVolume = new Series<double>(this);
                buyVolumeAvg = new Series<double>(this);
                sellVolumeAvg = new Series<double>(this);
                buyVolumeEMA = new Series<double>(this);
                sellVolumeEMA = new Series<double>(this);
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

                Values[3][0] = ADXCrossover;
                Values[4][0] = UpperThresholdDefault;
                Values[5][0] = TradeThresholdDefault;
                
                buyVolume[0] = 0;
                sellVolume[0] = 0;
                buyVolumeAvg[0] = 0;
                sellVolumeAvg[0] = 0;
                buyVolumeEMA[0] = 0;
                sellVolumeEMA[0] = 0;
                
                longCrossoverBar = -1;
                shortCrossoverBar = -1;
                longConvergenceBar = -1;
                shortConvergenceBar = -1;
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

                Values[3][0] = ADXCrossover;
                Values[4][0] = UpperThresholdDefault;
                Values[5][0] = TradeThresholdDefault;
                
                // Calculate buy/sell volume
                if (Volume[0] > 0 && (High[0] - Low[0]) > 0)
                {
                    double rawBuyVolume = Math.Round(((High[0] - Open[0]) + (Close[0] - Low[0])) / 2 / (High[0] - Low[0]) * Volume[0], 0);
                    double rawSellVolume = Math.Round(((Low[0] - Open[0]) + (Close[0] - High[0])) / 2 / (High[0] - Low[0]) * Volume[0], 0);

                    buyVolume[0] = Math.Max(0, rawBuyVolume);
                    sellVolume[0] = Math.Max(0, Math.Abs(rawSellVolume));
                    
                    buyVolumeAvg[0] = EMA(buyVolume, BuyVolumeAvgLength)[0];
                    sellVolumeAvg[0] = EMA(sellVolume, SellVolumeAvgLength)[0];
                    
                    buyVolumeEMA[0] = EMA(buyVolumeAvg, BSVPEmaPeriod)[0];
                    sellVolumeEMA[0] = EMA(sellVolumeAvg, BSVPEmaPeriod)[0];
                }
                else
                {
                    if (CurrentBar > 0)
                    {
                        buyVolume[0] = buyVolume[1];
                        sellVolume[0] = sellVolume[1];
                        buyVolumeAvg[0] = buyVolumeAvg[1];
                        sellVolumeAvg[0] = sellVolumeAvg[1];
                        buyVolumeEMA[0] = buyVolumeEMA[1];
                        sellVolumeEMA[0] = sellVolumeEMA[1];
                    }
                }

                // ADX Cross Signal Logic
                if (ADXCross && ADXPlot[0] >= TradeThresholdDefault)
                {
                    // Long Signal: ADX crosses above the Lower Threshold and price is increasing
                    if (CrossAbove(ADXPlot, ADXCrossover, 1) && Close[0] > Close[1])
                    {
                        Draw.ArrowUp(this, LongFilterOn + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                    }

                    // Short Signal: ADX crosses above the Lower Threshold and price is decreasing
                    if (CrossAbove(ADXPlot, ADXCrossover, 1) && Close[0] < Close[1])
                    {
                        Draw.ArrowDown(this, ShortFilterOn + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
                    }
                }

               // Inside the OnBarUpdate method, replace the DI Divergence Signal Logic section with this:

// Inside the OnBarUpdate method, replace the DI Divergence Signal Logic section with this:

// DI Divergence Signal Logic
if (DIDiv && ADXPlot[0] >= TradeThresholdDefault)
{
    // === LONG SIGNAL DETECTION AND TRACKING ===
    
    // 1. Check for new crossover (DI+ crosses above DI-)
    if (CrossAbove(DiPlus, DiMinus, 1))
    {
        // Reset tracking for a new potential signal
        longCrossoverBar = CurrentBar;
        hasGeneratedLongSignal = false;
        
        // If DIDivergencePoints is 0, generate signal immediately like the original DMX
        // but with the additional EMA check for 2 consecutive bars
        if (DIDivergencePoints == 0)
        {
            // Check if conditions match DMX indicator with additional EMA rule
            // Ensure buy volume EMA is greater than sell volume EMA for 2 consecutive bars
            bool emaConditionMet = buyVolumeEMA[0] > sellVolumeEMA[0] && 
                                  buyVolumeEMA[1] > sellVolumeEMA[1];
                              
            if (CurrentBar >= 5 && DiPlus[5] < DiMinus[5] && emaConditionMet)
            {
                Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                    Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                hasGeneratedLongSignal = true;
            }
            // Alternative: just generate signal on any crossover with EMA condition
            else if (emaConditionMet)
            {
                Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                    Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                hasGeneratedLongSignal = true;
            }
        }
    }
    
    // 2. Check for convergence pattern (without crossover)
    if (DiPlus[0] > DiMinus[0]) // DI+ must be above DI-
    {
        double currentDiff = DiPlus[0] - DiMinus[0];
        double prevDiff = DiPlus[1] - DiMinus[1];
        
        // Find when lines have converged and now start diverging
        if (currentDiff > prevDiff && prevDiff <= DIConvergenceThreshold)
        {
            // Found a new convergence point where lines start diverging again
            longConvergenceBar = CurrentBar;
            hasGeneratedLongSignal = false;
        }
    }
    
    // 3. Signal on first occurrence of conditions after crossover (when DIDivergencePoints > 0)
    if (DIDivergencePoints > 0 && longCrossoverBar > 0 && !hasGeneratedLongSignal && 
        CurrentBar > longCrossoverBar && (CurrentBar - longCrossoverBar) <= 15)
    {
        // Get values at crossover
        double diPlusAtCross = DiPlus[CurrentBar - longCrossoverBar];
        double diMinusAtCross = DiMinus[CurrentBar - longCrossoverBar];
        
        // Check for divergence and volume condition
        double diPlusDiff = DiPlus[0] - diPlusAtCross;
        double diMinusDiff = diMinusAtCross - DiMinus[0];
        
        // First time both conditions are met after a crossover
        // Added check for EMA condition for 2 consecutive bars
        bool longEmaConditionMet = buyVolumeEMA[0] > sellVolumeEMA[0] && 
                                  buyVolumeEMA[1] > sellVolumeEMA[1];
                                  
        if (diPlusDiff >= DIDivergencePoints && 
            diMinusDiff >= DIDivergencePoints && 
            DiPlus[0] > DiMinus[0] &&
            longEmaConditionMet)
        {
            // Generate first signal after crossover
            Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
            hasGeneratedLongSignal = true;
        }
    }
    
    // 4. Signal on first occurrence of conditions after convergence (when DIDivergencePoints > 0)
    if (DIDivergencePoints > 0 && longConvergenceBar > 0 && !hasGeneratedLongSignal && 
        CurrentBar > longConvergenceBar && (CurrentBar - longConvergenceBar) <= 15)
    {
        // Calculate divergence since convergence point
        double diPlusAtConv = DiPlus[CurrentBar - longConvergenceBar];
        double diMinusAtConv = DiMinus[CurrentBar - longConvergenceBar];
        
        // Check for sufficient divergence after convergence
        double diPlusDiff = DiPlus[0] - diPlusAtConv;
        double diMinusDiff = diMinusAtConv - DiMinus[0];
        bool enoughDivergence = (diPlusDiff + diMinusDiff) >= DIDivergencePoints;
        
        // Signal on first sufficient divergence after convergence
        // Added check for EMA condition for 2 consecutive bars
        bool longConvergenceEmaConditionMet = buyVolumeEMA[0] > sellVolumeEMA[0] && 
                                             buyVolumeEMA[1] > sellVolumeEMA[1];
                                             
        if (enoughDivergence && 
            DiPlus[0] > DiMinus[0] && 
            longConvergenceEmaConditionMet)
        {
            // Generate first signal after convergence
            Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
            hasGeneratedLongSignal = true;
        }
    }
    
    // === SHORT SIGNAL DETECTION AND TRACKING ===
    
    // 1. Check for new crossover (DI- crosses above DI+)
    if (CrossAbove(DiMinus, DiPlus, 1))
    {
        // Reset tracking for a new potential signal
        shortCrossoverBar = CurrentBar;
        hasGeneratedShortSignal = false;
        
        // If DIDivergencePoints is 0, generate signal immediately like the original DMX
        // but with the additional EMA check for 2 consecutive bars
        if (DIDivergencePoints == 0)
        {
            // Check if conditions match DMX indicator with additional EMA rule
            // Ensure sell volume EMA is greater than buy volume EMA for 2 consecutive bars
            bool emaConditionMet = sellVolumeEMA[0] > buyVolumeEMA[0] && 
                                  sellVolumeEMA[1] > buyVolumeEMA[1];
                              
            if (CurrentBar >= 5 && DiMinus[5] < DiPlus[5] && emaConditionMet)
            {
                Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                    High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
                hasGeneratedShortSignal = true;
            }
            // Alternative: just generate signal on any crossover with EMA condition
            else if (emaConditionMet)
            {
                Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                    High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
                hasGeneratedShortSignal = true;
            }
        }
    }
    
    // 2. Check for convergence pattern (without crossover)
    if (DiMinus[0] > DiPlus[0]) // DI- must be above DI+
    {
        double currentDiff = DiMinus[0] - DiPlus[0];
        double prevDiff = DiMinus[1] - DiPlus[1];
        
        // Find when lines have converged and now start diverging
        if (currentDiff > prevDiff && prevDiff <= DIConvergenceThreshold)
        {
            // Found a new convergence point where lines start diverging again
            shortConvergenceBar = CurrentBar;
            hasGeneratedShortSignal = false;
        }
    }
    
    // 3. Signal on first occurrence of conditions after crossover (when DIDivergencePoints > 0)
    if (DIDivergencePoints > 0 && shortCrossoverBar > 0 && !hasGeneratedShortSignal && 
        CurrentBar > shortCrossoverBar && (CurrentBar - shortCrossoverBar) <= 15)
    {
        // Get values at crossover
        double diPlusAtCross = DiPlus[CurrentBar - shortCrossoverBar];
        double diMinusAtCross = DiMinus[CurrentBar - shortCrossoverBar];
        
        // Check for divergence and volume condition
        double diMinusDiff = DiMinus[0] - diMinusAtCross;
        double diPlusDiff = diPlusAtCross - DiPlus[0];
        
        // First time both conditions are met after a crossover
        // Added check for EMA condition for 2 consecutive bars
        bool shortEmaConditionMet = sellVolumeEMA[0] > buyVolumeEMA[0] && 
                                   sellVolumeEMA[1] > buyVolumeEMA[1];
                                   
        if (diMinusDiff >= DIDivergencePoints && 
            diPlusDiff >= DIDivergencePoints && 
            DiMinus[0] > DiPlus[0] &&
            shortEmaConditionMet)
        {
            // Generate first signal after crossover
            Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
            hasGeneratedShortSignal = true;
        }
    }
    
    // 4. Signal on first occurrence of conditions after convergence (when DIDivergencePoints > 0)
    if (DIDivergencePoints > 0 && shortConvergenceBar > 0 && !hasGeneratedShortSignal && 
        CurrentBar > shortConvergenceBar && (CurrentBar - shortConvergenceBar) <= 15)
    {
        // Calculate divergence since convergence point
        double diPlusAtConv = DiPlus[CurrentBar - shortConvergenceBar];
        double diMinusAtConv = DiMinus[CurrentBar - shortConvergenceBar];
        
        // Check for sufficient divergence after convergence
        double diMinusDiff = DiMinus[0] - diMinusAtConv;
        double diPlusDiff = diPlusAtConv - DiPlus[0];
        bool enoughDivergence = (diMinusDiff + diPlusDiff) >= DIDivergencePoints;
        
        // Signal on first sufficient divergence after convergence
        // Added check for EMA condition for 2 consecutive bars
        bool shortConvergenceEmaConditionMet = sellVolumeEMA[0] > buyVolumeEMA[0] && 
                                              sellVolumeEMA[1] > buyVolumeEMA[1];
                                              
        if (enoughDivergence && 
            DiMinus[0] > DiPlus[0] && 
            shortConvergenceEmaConditionMet)
        {
            // Generate first signal after convergence
            Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
            hasGeneratedShortSignal = true;
        }
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
                                Draw.Diamond(this, LongFilterOn + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                            }
                            else if (Close[0] < Open[0]) // Short signal
                            {
                                Draw.Diamond(this, ShortFilterOn + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
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
        [Display(ResourceType = typeof(Custom.Resource), Name = "ADX Cross", GroupName = "NinjaScriptParameters", Order = 1)]
        public int ADXCrossover { get; set; }

        [Range(0, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Upper Threshold", GroupName = "NinjaScriptParameters", Order = 2)]
        public int UpperThresholdDefault { get; set; }

        [Range(0, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "No Trade Threshold", GroupName = "NinjaScriptParameters", Order = 3)]
        public int TradeThresholdDefault { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ADX Cross ↑↓", GroupName = "SignalSettings", Order = 1)]
        public bool ADXCross { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DI Divergence ▲", GroupName = "SignalSettings", Order = 2)]
        public bool DIDiv { get; set; }

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
        
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Required DI Divergence", Description = "Each DI line must move at least this many points to generate a signal", GroupName = "Signal Parameters", Order = 1)]
        public double DIDivergencePoints { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Pullback Gap", Description = "Maximum points between DI lines to detect a pullback without crossover", GroupName = "Signal Parameters", Order = 2)]
        public double DIConvergenceThreshold { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Buy Volume Avg Length", Description = "Length for Buy Volume Average", GroupName = "Volume Settings", Order = 1)]
        public int BuyVolumeAvgLength { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Sell Volume Avg Length", Description = "Length for Sell Volume Average", GroupName = "Volume Settings", Order = 2)]
        public int SellVolumeAvgLength { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "BSVP EMA Period", Description = "EMA period for volume calculation", GroupName = "Volume Settings", Order = 3)]
        public int BSVPEmaPeriod { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.DMXv2[] cacheDMXv2;
		public Myindicators.DMXv2 DMXv2(int period, int aDXCrossover, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dIDiv, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int buyVolumeAvgLength, int sellVolumeAvgLength, int bSVPEmaPeriod)
		{
			return DMXv2(Input, period, aDXCrossover, upperThresholdDefault, tradeThresholdDefault, aDXCross, dIDiv, aDXRising, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, buyVolumeAvgLength, sellVolumeAvgLength, bSVPEmaPeriod);
		}

		public Myindicators.DMXv2 DMXv2(ISeries<double> input, int period, int aDXCrossover, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dIDiv, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int buyVolumeAvgLength, int sellVolumeAvgLength, int bSVPEmaPeriod)
		{
			if (cacheDMXv2 != null)
				for (int idx = 0; idx < cacheDMXv2.Length; idx++)
					if (cacheDMXv2[idx] != null && cacheDMXv2[idx].Period == period && cacheDMXv2[idx].ADXCrossover == aDXCrossover && cacheDMXv2[idx].UpperThresholdDefault == upperThresholdDefault && cacheDMXv2[idx].TradeThresholdDefault == tradeThresholdDefault && cacheDMXv2[idx].ADXCross == aDXCross && cacheDMXv2[idx].DIDiv == dIDiv && cacheDMXv2[idx].ADXRising == aDXRising && cacheDMXv2[idx].LongFilterOn == longFilterOn && cacheDMXv2[idx].ShortFilterOn == shortFilterOn && cacheDMXv2[idx].Signal_Offset == signal_Offset && cacheDMXv2[idx].DIDivergencePoints == dIDivergencePoints && cacheDMXv2[idx].DIConvergenceThreshold == dIConvergenceThreshold && cacheDMXv2[idx].BuyVolumeAvgLength == buyVolumeAvgLength && cacheDMXv2[idx].SellVolumeAvgLength == sellVolumeAvgLength && cacheDMXv2[idx].BSVPEmaPeriod == bSVPEmaPeriod && cacheDMXv2[idx].EqualsInput(input))
						return cacheDMXv2[idx];
			return CacheIndicator<Myindicators.DMXv2>(new Myindicators.DMXv2(){ Period = period, ADXCrossover = aDXCrossover, UpperThresholdDefault = upperThresholdDefault, TradeThresholdDefault = tradeThresholdDefault, ADXCross = aDXCross, DIDiv = dIDiv, ADXRising = aDXRising, LongFilterOn = longFilterOn, ShortFilterOn = shortFilterOn, Signal_Offset = signal_Offset, DIDivergencePoints = dIDivergencePoints, DIConvergenceThreshold = dIConvergenceThreshold, BuyVolumeAvgLength = buyVolumeAvgLength, SellVolumeAvgLength = sellVolumeAvgLength, BSVPEmaPeriod = bSVPEmaPeriod }, input, ref cacheDMXv2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.DMXv2 DMXv2(int period, int aDXCrossover, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dIDiv, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int buyVolumeAvgLength, int sellVolumeAvgLength, int bSVPEmaPeriod)
		{
			return indicator.DMXv2(Input, period, aDXCrossover, upperThresholdDefault, tradeThresholdDefault, aDXCross, dIDiv, aDXRising, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, buyVolumeAvgLength, sellVolumeAvgLength, bSVPEmaPeriod);
		}

		public Indicators.Myindicators.DMXv2 DMXv2(ISeries<double> input , int period, int aDXCrossover, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dIDiv, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int buyVolumeAvgLength, int sellVolumeAvgLength, int bSVPEmaPeriod)
		{
			return indicator.DMXv2(input, period, aDXCrossover, upperThresholdDefault, tradeThresholdDefault, aDXCross, dIDiv, aDXRising, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, buyVolumeAvgLength, sellVolumeAvgLength, bSVPEmaPeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.DMXv2 DMXv2(int period, int aDXCrossover, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dIDiv, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int buyVolumeAvgLength, int sellVolumeAvgLength, int bSVPEmaPeriod)
		{
			return indicator.DMXv2(Input, period, aDXCrossover, upperThresholdDefault, tradeThresholdDefault, aDXCross, dIDiv, aDXRising, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, buyVolumeAvgLength, sellVolumeAvgLength, bSVPEmaPeriod);
		}

		public Indicators.Myindicators.DMXv2 DMXv2(ISeries<double> input , int period, int aDXCrossover, int upperThresholdDefault, int tradeThresholdDefault, bool aDXCross, bool dIDiv, bool aDXRising, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int buyVolumeAvgLength, int sellVolumeAvgLength, int bSVPEmaPeriod)
		{
			return indicator.DMXv2(input, period, aDXCrossover, upperThresholdDefault, tradeThresholdDefault, aDXCross, dIDiv, aDXRising, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, buyVolumeAvgLength, sellVolumeAvgLength, bSVPEmaPeriod);
		}
	}
}

#endregion
