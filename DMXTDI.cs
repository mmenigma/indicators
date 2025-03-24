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
    public class DMXTDI : Indicator
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
        private int longConvergenceBar = -1;
        private int shortConvergenceBar = -1;
        private bool hasGeneratedLongSignal = false;
        private bool hasGeneratedShortSignal = false;
        
        // Track RSI crossovers
        private int rsiLongCrossoverBar = -1;
        private int rsiShortCrossoverBar = -1;
        private int maxBarsAfterDICrossover = 10; // Maximum bars to look for RSI crossover after DI crossover

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"DMX Indicator with DI Divergence and TDI Confirmation";
                Name = "DMXTDI";
                Calculate = Calculate.OnBarClose;
                IsSuspendedWhileInactive = true;
                Period = 14;
                TradeThresholdDefault = 5;
                
                // TDI settings
                RsiPeriod = 13;
                RsiSignalPeriod = 7;
                MaxBarsAfterDICrossover = 10;
                
                // Initialize local variable to match property
                maxBarsAfterDICrossover = MaxBarsAfterDICrossover;

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
                DIConvergenceThreshold = 0.5;
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
                
                // Update local variable with property value
                maxBarsAfterDICrossover = MaxBarsAfterDICrossover;
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
                longConvergenceBar = -1;
                shortConvergenceBar = -1;
                hasGeneratedLongSignal = false;
                hasGeneratedShortSignal = false;
                rsiLongCrossoverBar = -1;
                rsiShortCrossoverBar = -1;
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
                
                // Track RSI crossovers
                if (CurrentBar > 0)
                {
                    // RSI crosses above signal (bullish)
                    if (Values[4][0] > Values[5][0] && Values[4][1] <= Values[5][1])
                    {
                        rsiLongCrossoverBar = CurrentBar;
                        
                        // Always update the local variable with the property value
                        maxBarsAfterDICrossover = MaxBarsAfterDICrossover;
                        
                        // Check if this RSI crossover happened shortly after a DI crossover
                        // AND Make sure DI+ is above DI- (in a bullish trend)
                        // AND Make sure RSI is increasing (positive momentum)
                        if (longCrossoverBar > 0 && 
                            DiPlus[0] > DiMinus[0] && // Ensure trend alignment
                            Values[4][0] > Values[4][1] && // Ensure RSI is increasing
                            (CurrentBar - longCrossoverBar) <= MaxBarsAfterDICrossover && 
                            !hasGeneratedLongSignal)
                        {
                            // Apply DI Divergence check for RSI signal as well
                            if (DIDivergencePoints > 0)
                            {
                                // Get values at crossover
                                double diPlusAtCross = DiPlus[CurrentBar - longCrossoverBar];
                                double diMinusAtCross = DiMinus[CurrentBar - longCrossoverBar];
                                
                                // Check for sufficient divergence since crossover
                                double diPlusDiff = DiPlus[0] - diPlusAtCross;
                                double diMinusDiff = diMinusAtCross - DiMinus[0];
                                
                                // Only generate signal if lines have diverged enough
                                if (diPlusDiff >= DIDivergencePoints && diMinusDiff >= DIDivergencePoints)
                                {
                                    // RSI crossover with sufficient DI divergence - generate signal
                                    Draw.Diamond(this, LongFilterOn + CurrentBar, true, 0, 
                                        Low[0] - Signal_Offset * TickSize, Brushes.LimeGreen);
                                    hasGeneratedLongSignal = true;
                                }
                            }
                            else // If no DI divergence required, generate signal immediately
                            {
                                // RSI crossover shortly after DI crossover - generate signal
                                Draw.Diamond(this, LongFilterOn + CurrentBar, true, 0, 
                                    Low[0] - Signal_Offset * TickSize, Brushes.LimeGreen);
                                hasGeneratedLongSignal = true;
                            }
                        }
                    }
                    
                    // RSI crosses below signal (bearish)
                    if (Values[4][0] < Values[5][0] && Values[4][1] >= Values[5][1])
                    {
                        rsiShortCrossoverBar = CurrentBar;
                        
                        // Always update the local variable with the property value
                        maxBarsAfterDICrossover = MaxBarsAfterDICrossover;
                        
                        // Check if this RSI crossover happened shortly after a DI crossover
                        // AND Make sure DI- is above DI+ (in a bearish trend)
                        // AND Make sure RSI is decreasing (negative momentum)
                        if (shortCrossoverBar > 0 && 
                            DiMinus[0] > DiPlus[0] && // Ensure trend alignment
                            Values[4][0] < Values[4][1] && // Ensure RSI is decreasing
                            (CurrentBar - shortCrossoverBar) <= MaxBarsAfterDICrossover && 
                            !hasGeneratedShortSignal)
                        {
                            // Apply DI Divergence check for RSI signal as well
                            if (DIDivergencePoints > 0)
                            {
                                // Get values at crossover
                                double diPlusAtCross = DiPlus[CurrentBar - shortCrossoverBar];
                                double diMinusAtCross = DiMinus[CurrentBar - shortCrossoverBar];
                                
                                // Check for sufficient divergence since crossover
                                double diMinusDiff = DiMinus[0] - diMinusAtCross;
                                double diPlusDiff = diPlusAtCross - DiPlus[0];
                                
                                // Only generate signal if lines have diverged enough
                                if (diMinusDiff >= DIDivergencePoints && diPlusDiff >= DIDivergencePoints)
                                {
                                    // RSI crossover with sufficient DI divergence - generate signal
                                    Draw.Diamond(this, ShortFilterOn + CurrentBar, true, 0, 
                                        High[0] + Signal_Offset * TickSize, Brushes.Crimson);
                                    hasGeneratedShortSignal = true;
                                }
                            }
                            else // If no DI divergence required, generate signal immediately
                            {
                                // RSI crossover shortly after DI crossover - generate signal
                                Draw.Diamond(this, ShortFilterOn + CurrentBar, true, 0, 
                                    High[0] + Signal_Offset * TickSize, Brushes.Crimson);
                                hasGeneratedShortSignal = true;
                            }
                        }
                    }
                }

                // DI Divergence Signal Logic
                if (ADXPlot[0] >= TradeThresholdDefault)
                {
                    // === LONG SIGNAL DETECTION AND TRACKING ===
                    
                    // 1. Check for new crossover (DI+ crosses above DI-)
                    if (CrossAbove(DiPlus, DiMinus, 1))
                    {
                        // Reset tracking for a new potential signal
                        longCrossoverBar = CurrentBar;
                        hasGeneratedLongSignal = false;
                        
                        // If DIDivergencePoints is 0, generate signal immediately
                        if (DIDivergencePoints == 0)
                        {
                            // Generate signal on crossover
                            if (CurrentBar >= 5 && DiPlus[5] < DiMinus[5] && Values[4][0] > Values[5][0])
                            {
                                Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                                    Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                                hasGeneratedLongSignal = true;
                            }
                            // Alternative: just generate signal on any crossover with TDI confirmation
                            else if (Values[4][0] > Values[5][0])
                            {
                                Draw.TriangleUp(this, LongFilterOn + CurrentBar, true, 0, 
                                    Low[0] - Signal_Offset * TickSize, Brushes.DodgerBlue);
                                hasGeneratedLongSignal = true;
                            }
                            // Check if the RSI is currently below signal but recently crossed (within maxBarsAfterDICrossover)
                            else if (Values[4][0] < Values[5][0] && rsiLongCrossoverBar > 0 && 
                                    (CurrentBar - rsiLongCrossoverBar) <= MaxBarsAfterDICrossover)
                            {
                                // We'll anticipate the RSI crossing above signal soon
                                Draw.Diamond(this, LongFilterOn + CurrentBar, true, 0, 
                                    Low[0] - Signal_Offset * TickSize, Brushes.Goldenrod);
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
                        if (diPlusDiff >= DIDivergencePoints && 
                            diMinusDiff >= DIDivergencePoints && 
                            DiPlus[0] > DiMinus[0] &&
                            Values[4][0] > Values[5][0])
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
                        if (enoughDivergence && 
                            DiPlus[0] > DiMinus[0] &&
                            Values[4][0] > Values[5][0])
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
                        
                        // If DIDivergencePoints is 0, generate signal immediately
                        if (DIDivergencePoints == 0)
                        {
                            // Generate signal on crossover
                            if (CurrentBar >= 5 && DiMinus[5] < DiPlus[5] && Values[4][0] < Values[5][0])
                            {
                                Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                                    High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
                                hasGeneratedShortSignal = true;
                            }
                            // Alternative: just generate signal on any crossover with TDI confirmation
                            else if (Values[4][0] < Values[5][0])
                            {
                                Draw.TriangleDown(this, ShortFilterOn + CurrentBar, true, 0, 
                                    High[0] + Signal_Offset * TickSize, Brushes.DodgerBlue);
                                hasGeneratedShortSignal = true;
                            }
                            // Check if the RSI is currently above signal but recently crossed (within maxBarsAfterDICrossover)
                            else if (Values[4][0] > Values[5][0] && rsiShortCrossoverBar > 0 && 
                                    (CurrentBar - rsiShortCrossoverBar) <= MaxBarsAfterDICrossover)
                            {
                                // We'll anticipate the RSI crossing below signal soon
                                Draw.Diamond(this, ShortFilterOn + CurrentBar, true, 0, 
                                    High[0] + Signal_Offset * TickSize, Brushes.Goldenrod);
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
                        if (diMinusDiff >= DIDivergencePoints && 
                            diPlusDiff >= DIDivergencePoints && 
                            DiMinus[0] > DiPlus[0] &&
                            Values[4][0] < Values[5][0])
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
                        if (enoughDivergence && 
                            DiMinus[0] > DiPlus[0] &&
                            Values[4][0] < Values[5][0])
                        {
                            // Generate first signal after convergence
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
        [Display(ResourceType = typeof(Custom.Resource), Name = "No Trade Threshold", GroupName = "NinjaScriptParameters", Order = 1)]
        public int TradeThresholdDefault { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "LongFilterOn", GroupName = "SignalSettings", Order = 1)]
        public string LongFilterOn { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ShortFilterOn", GroupName = "SignalSettings", Order = 2)]
        public string ShortFilterOn { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal_Offset", GroupName = "SignalSettings", Order = 3)]
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
        [Display(Name = "RSI Period", Description = "Period for RSI calculation", GroupName = "TDI Parameters", Order = 1)]
        public int RsiPeriod { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "RSI Signal Period", Description = "Period for RSI signal line", GroupName = "TDI Parameters", Order = 2)]
        public int RsiSignalPeriod { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Bars After DI Crossover", Description = "Maximum bars to look for RSI crossover after DI crossover", GroupName = "TDI Parameters", Order = 3)]
        public int MaxBarsAfterDICrossover { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.DMXTDI[] cacheDMXTDI;
		public Myindicators.DMXTDI DMXTDI(int period, int tradeThresholdDefault, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int rsiPeriod, int rsiSignalPeriod, int maxBarsAfterDICrossover)
		{
			return DMXTDI(Input, period, tradeThresholdDefault, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, rsiPeriod, rsiSignalPeriod, maxBarsAfterDICrossover);
		}

		public Myindicators.DMXTDI DMXTDI(ISeries<double> input, int period, int tradeThresholdDefault, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int rsiPeriod, int rsiSignalPeriod, int maxBarsAfterDICrossover)
		{
			if (cacheDMXTDI != null)
				for (int idx = 0; idx < cacheDMXTDI.Length; idx++)
					if (cacheDMXTDI[idx] != null && cacheDMXTDI[idx].Period == period && cacheDMXTDI[idx].TradeThresholdDefault == tradeThresholdDefault && cacheDMXTDI[idx].LongFilterOn == longFilterOn && cacheDMXTDI[idx].ShortFilterOn == shortFilterOn && cacheDMXTDI[idx].Signal_Offset == signal_Offset && cacheDMXTDI[idx].DIDivergencePoints == dIDivergencePoints && cacheDMXTDI[idx].DIConvergenceThreshold == dIConvergenceThreshold && cacheDMXTDI[idx].RsiPeriod == rsiPeriod && cacheDMXTDI[idx].RsiSignalPeriod == rsiSignalPeriod && cacheDMXTDI[idx].MaxBarsAfterDICrossover == maxBarsAfterDICrossover && cacheDMXTDI[idx].EqualsInput(input))
						return cacheDMXTDI[idx];
			return CacheIndicator<Myindicators.DMXTDI>(new Myindicators.DMXTDI(){ Period = period, TradeThresholdDefault = tradeThresholdDefault, LongFilterOn = longFilterOn, ShortFilterOn = shortFilterOn, Signal_Offset = signal_Offset, DIDivergencePoints = dIDivergencePoints, DIConvergenceThreshold = dIConvergenceThreshold, RsiPeriod = rsiPeriod, RsiSignalPeriod = rsiSignalPeriod, MaxBarsAfterDICrossover = maxBarsAfterDICrossover }, input, ref cacheDMXTDI);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.DMXTDI DMXTDI(int period, int tradeThresholdDefault, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int rsiPeriod, int rsiSignalPeriod, int maxBarsAfterDICrossover)
		{
			return indicator.DMXTDI(Input, period, tradeThresholdDefault, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, rsiPeriod, rsiSignalPeriod, maxBarsAfterDICrossover);
		}

		public Indicators.Myindicators.DMXTDI DMXTDI(ISeries<double> input , int period, int tradeThresholdDefault, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int rsiPeriod, int rsiSignalPeriod, int maxBarsAfterDICrossover)
		{
			return indicator.DMXTDI(input, period, tradeThresholdDefault, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, rsiPeriod, rsiSignalPeriod, maxBarsAfterDICrossover);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.DMXTDI DMXTDI(int period, int tradeThresholdDefault, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int rsiPeriod, int rsiSignalPeriod, int maxBarsAfterDICrossover)
		{
			return indicator.DMXTDI(Input, period, tradeThresholdDefault, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, rsiPeriod, rsiSignalPeriod, maxBarsAfterDICrossover);
		}

		public Indicators.Myindicators.DMXTDI DMXTDI(ISeries<double> input , int period, int tradeThresholdDefault, string longFilterOn, string shortFilterOn, double signal_Offset, double dIDivergencePoints, double dIConvergenceThreshold, int rsiPeriod, int rsiSignalPeriod, int maxBarsAfterDICrossover)
		{
			return indicator.DMXTDI(input, period, tradeThresholdDefault, longFilterOn, shortFilterOn, signal_Offset, dIDivergencePoints, dIConvergenceThreshold, rsiPeriod, rsiSignalPeriod, maxBarsAfterDICrossover);
		}
	}
}

#endregion
