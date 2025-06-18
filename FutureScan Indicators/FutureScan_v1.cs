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
    public class FutureScan : Indicator
    {
        // Multi-instrument data storage
        private Dictionary<int, InstrumentData> instrumentData;
        private Dictionary<string, int> symbolToIndex;
        
        // Contract symbols - will be built from user properties
        private string[] futuresSymbols;
        private string[] displayNames = { "ES", "NQ", "RTY", "YM", "GC", "CL" };
        
        // Volume confirmation
        private Dictionary<int, Series<double>> volumeSeries;
        private Dictionary<int, SMA> volumeMA;
        
        // Display variables
        private int maxSignalsPerDay = 8;
        private int currentDaySignals = 0;
        private DateTime lastSignalReset = DateTime.MinValue;
        
        // Performance optimization
        private int displayUpdateCounter = 0;
        private const int DISPLAY_UPDATE_FREQUENCY = 5;
        private bool displayObjectsCreated = false;
        private DateTime lastDisplayUpdate = DateTime.MinValue;

        public class InstrumentData
        {
            public string Symbol { get; set; }
            public string DisplayName { get; set; }
            public double SessionHigh { get; set; } = double.MinValue;
            public double SessionLow { get; set; } = double.MaxValue;
            public bool IsInRange { get; set; } = false;
            public int SetupStatus { get; set; } = 0; // 0=Waiting, 1=Setup Forming, 2=Long Ready, 3=Short Ready
            public string SetupType { get; set; } = "Waiting";
            public double CurrentPrice { get; set; } = 0;
            public double RiskReward { get; set; } = 0;
            public bool VolumeConfirmed { get; set; } = false;
            public DateTime LastSignalTime { get; set; } = DateTime.MinValue;
            public double TickSize { get; set; } = 0.25;
            public int TicksToTarget { get; set; } = 0;
            
            public InstrumentData(string symbol, string displayName, double tickSize)
            {
                Symbol = symbol;
                DisplayName = displayName;
                TickSize = tickSize;
            }
            
            public void ResetDaily()
            {
                SessionHigh = double.MinValue;
                SessionLow = double.MaxValue;
                IsInRange = false;
                SetupStatus = 0;
                SetupType = "Waiting";
                VolumeConfirmed = false;
                RiskReward = 0;
                TicksToTarget = 0;
            }
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "ES Contract", Description = "ES futures contract (e.g., ES 09-25)", Order = 1, GroupName = "Contract Settings")]
        public string ESContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "NQ Contract", Description = "NQ futures contract (e.g., NQ 09-25)", Order = 2, GroupName = "Contract Settings")]
        public string NQContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RTY Contract", Description = "RTY futures contract (e.g., RTY 09-25)", Order = 3, GroupName = "Contract Settings")]
        public string RTYContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "YM Contract", Description = "YM futures contract (e.g., YM 09-25)", Order = 4, GroupName = "Contract Settings")]
        public string YMContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "GC Contract", Description = "GC futures contract (e.g., GC 08-25)", Order = 5, GroupName = "Contract Settings")]
        public string GCContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "CL Contract", Description = "CL futures contract (e.g., CL 07-25)", Order = 6, GroupName = "Contract Settings")]
        public string CLContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ORB Start Time", Description = "Opening Range start time (ET)", Order = 7, GroupName = "ORB Settings")]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        public DateTime ORBStartTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ORB End Time", Description = "Opening Range end time (ET)", Order = 8, GroupName = "ORB Settings")]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        public DateTime ORBEndTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volume Confirmation", Description = "Require volume confirmation for setups", Order = 9, GroupName = "ORB Settings")]
        public bool RequireVolumeConfirmation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volume Threshold %", Description = "Volume increase required (30% = 1.3x average)", Order = 10, GroupName = "ORB Settings")]
        public double VolumeThreshold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Risk/Reward", Description = "Minimum acceptable risk/reward ratio", Order = 11, GroupName = "ORB Settings")]
        public double MinRiskReward { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Daily Signals", Description = "Maximum signals per day across all futures", Order = 12, GroupName = "Professional Filters")]
        public int MaxDailySignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Scanner Display", Description = "Show the futures scanner overlay", Order = 13, GroupName = "Display")]
        public bool ShowDisplay { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Position", Description = "Scanner display position", Order = 14, GroupName = "Display")]
        public TextPosition DisplayPosition { get; set; }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Professional Futures Scanner - Multi-instrument ORB detection with colored dots";
                Name = "FutureScan";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                
                // Default contract settings
                ESContract = "ES 06-25";
                NQContract = "NQ 06-25";
                RTYContract = "RTY 06-25";
                YMContract = "YM 06-25";
                GCContract = "GC 08-25";
                CLContract = "CL 07-25";
                
                // Professional default settings
                ORBStartTime = new DateTime(2000, 1, 1, 9, 30, 0);  // 9:30 AM ET
                ORBEndTime = new DateTime(2000, 1, 1, 9, 45, 0);    // 9:45 AM ET (15-min ORB)
                RequireVolumeConfirmation = true;
                VolumeThreshold = 1.3; // 30% above average
                MinRiskReward = 1.5;   // Minimum 1:1.5 R/R
                MaxDailySignals = 8;   // Professional limit
                ShowDisplay = true;
                DisplayPosition = TextPosition.TopLeft;
            }
            else if (State == State.Configure)
            {
                // Build futures symbols array from user properties
                futuresSymbols = new string[] { ESContract, NQContract, RTYContract, YMContract, GCContract, CLContract };
                
                // Add data series for each futures contract (skip index 0 - primary series)
                for (int i = 1; i < futuresSymbols.Length; i++)
                {
                    AddDataSeries(futuresSymbols[i], Data.BarsPeriodType.Minute, 1);
                }
                
                // Initialize data structures
                instrumentData = new Dictionary<int, InstrumentData>();
                symbolToIndex = new Dictionary<string, int>();
                volumeSeries = new Dictionary<int, Series<double>>();
                volumeMA = new Dictionary<int, SMA>();
                
                // Setup instrument data with proper tick sizes
                double[] tickSizes = { 0.25, 0.25, 0.1, 1.0, 0.1, 0.01 }; // ES, NQ, RTY, YM, GC, CL
                
                for (int i = 0; i < futuresSymbols.Length; i++)
                {
                    instrumentData[i] = new InstrumentData(futuresSymbols[i], displayNames[i], tickSizes[i]);
                    symbolToIndex[displayNames[i]] = i;
                    volumeSeries[i] = new Series<double>(this, MaximumBarsLookBack.Infinite);
                }
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
                // Ensure we have enough data
                if (CurrentBars[BarsInProgress] < 21) return; // Need 21 bars for volume MA
                
                // Initialize volume MA for this instrument if not already done
                if (!volumeMA.ContainsKey(BarsInProgress) || volumeMA[BarsInProgress] == null)
                {
                    volumeMA[BarsInProgress] = SMA(Volumes[BarsInProgress], 20);
                }
                
                // Reset daily signals if new day
                CheckDailyReset();
                
                // Get current instrument data
                if (!instrumentData.ContainsKey(BarsInProgress))
                    return;
                    
                var instrument = instrumentData[BarsInProgress];
                
                // Update current price and volume
                UpdateInstrumentData(instrument, BarsInProgress);
                
                // Process ORB logic
                ProcessORBLogic(instrument, BarsInProgress);
                
                // Detect setups (only after ORB period and if signals available)
                if (!instrument.IsInRange && currentDaySignals < MaxDailySignals)
                {
                    DetectProfessionalSetups(instrument, BarsInProgress);
                }
                
                // Update display
                if (BarsInProgress == 0 && ShowDisplay)
                {
                    displayUpdateCounter++;
                    if (displayUpdateCounter >= DISPLAY_UPDATE_FREQUENCY || 
                        DateTime.Now.Subtract(lastDisplayUpdate).TotalSeconds > 5)
                    {
                        UpdateProfessionalDisplay();
                        displayUpdateCounter = 0;
                        lastDisplayUpdate = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error in OnBarUpdate: {ex.Message}");
            }
        }
        
        private void CheckDailyReset()
        {
            DateTime currentDate = Times[BarsInProgress][0].Date;
            
            if (lastSignalReset != currentDate)
            {
                // New trading day - reset counters and instrument data
                currentDaySignals = 0;
                lastSignalReset = currentDate;
                displayObjectsCreated = false;
                
                if (BarsInProgress == 0) // Only reset once per day on primary series
                {
                    foreach (var data in instrumentData.Values)
                    {
                        data.ResetDaily();
                    }
                }
            }
        }
        
        private void UpdateInstrumentData(InstrumentData instrument, int barsInProgress)
        {
            instrument.CurrentPrice = Closes[barsInProgress][0];
            
            // Store current volume for analysis
            if (volumeSeries.ContainsKey(barsInProgress))
            {
                volumeSeries[barsInProgress][0] = Volumes[barsInProgress][0];
            }
            
            // Calculate volume confirmation
            if (RequireVolumeConfirmation && volumeMA.ContainsKey(barsInProgress) && volumeMA[barsInProgress] != null)
            {
                double currentVolume = Volumes[barsInProgress][0];
                double avgVolume = volumeMA[barsInProgress][0];
                instrument.VolumeConfirmed = currentVolume >= (avgVolume * VolumeThreshold);
            }
            else
            {
                instrument.VolumeConfirmed = true; // Skip volume check if disabled
            }
        }
        
        private void ProcessORBLogic(InstrumentData instrument, int barsInProgress)
        {
            DateTime currentTime = Times[barsInProgress][0];
            DateTime todayORBStart = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 
                                                 ORBStartTime.Hour, ORBStartTime.Minute, ORBStartTime.Second);
            DateTime todayORBEnd = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 
                                               ORBEndTime.Hour, ORBEndTime.Minute, ORBEndTime.Second);
            
            // ORB Range Detection
            if (currentTime >= todayORBStart && currentTime <= todayORBEnd)
            {
                instrument.IsInRange = true;
                instrument.SessionHigh = Math.Max(instrument.SessionHigh, Highs[barsInProgress][0]);
                instrument.SessionLow = Math.Min(instrument.SessionLow, Lows[barsInProgress][0]);
                instrument.SetupType = "Waiting";
                instrument.SetupStatus = 0;
                instrument.TicksToTarget = 0;
            }
            else if (currentTime > todayORBEnd && instrument.IsInRange)
            {
                instrument.IsInRange = false;
                instrument.SetupType = "Waiting";
                instrument.SetupStatus = 0;
            }
        }
        
        private void DetectProfessionalSetups(InstrumentData instrument, int barsInProgress)
        {
            if (instrument.SessionHigh == double.MinValue || instrument.SessionLow == double.MaxValue)
                return;
                
            double currentPrice = instrument.CurrentPrice;
            double upperLevel = instrument.SessionHigh;
            double lowerLevel = instrument.SessionLow;
            double range = upperLevel - lowerLevel;
            
            // Ensure minimum range size (avoid micro ranges)
            double minRangeTicks = GetMinimumRange(instrument.DisplayName);
            if (range < (minRangeTicks * instrument.TickSize))
            {
                instrument.SetupType = "Waiting";
                instrument.SetupStatus = 0;
                instrument.TicksToTarget = 0;
                return;
            }
            
            // Calculate distances to levels
            double distanceToUpper = Math.Abs(upperLevel - currentPrice);
            double distanceToLower = Math.Abs(lowerLevel - currentPrice);
            double distanceToUpperTicks = distanceToUpper / instrument.TickSize;
            double distanceToLowerTicks = distanceToLower / instrument.TickSize;
            
            // Reset status
            instrument.SetupStatus = 0;
            instrument.SetupType = "Waiting";
            instrument.RiskReward = 0;
            instrument.TicksToTarget = 0;
            
            // Check for setups with new timing: 10 ticks = yellow, 5 ticks = green/red
            if (distanceToUpperTicks <= 10) // Within 10 ticks of upper
            {
                double riskReward = CalculateRiskReward(currentPrice, upperLevel, range, true);
                
                if (riskReward >= MinRiskReward)
                {
                    instrument.RiskReward = riskReward;
                    instrument.SetupType = "ORB Trade";
                    instrument.TicksToTarget = (int)Math.Round(distanceToUpperTicks);
                    
                    if (distanceToUpperTicks <= 5 && instrument.VolumeConfirmed)
                    {
                        instrument.SetupStatus = 2; // Green - Ready to trade long
                        CreateSignal(instrument, barsInProgress, "Long");
                    }
                    else if (distanceToUpperTicks <= 10)
                    {
                        instrument.SetupStatus = 1; // Yellow - Setup forming
                    }
                }
            }
            else if (distanceToLowerTicks <= 10) // Within 10 ticks of lower
            {
                double riskReward = CalculateRiskReward(currentPrice, lowerLevel, range, false);
                
                if (riskReward >= MinRiskReward)
                {
                    instrument.RiskReward = riskReward;
                    instrument.SetupType = "ORB Trade";
                    instrument.TicksToTarget = (int)Math.Round(distanceToLowerTicks);
                    
                    if (distanceToLowerTicks <= 5 && instrument.VolumeConfirmed)
                    {
                        instrument.SetupStatus = 3; // Red - Ready to trade short
                        CreateSignal(instrument, barsInProgress, "Short");
                    }
                    else if (distanceToLowerTicks <= 10)
                    {
                        instrument.SetupStatus = 1; // Yellow - Setup forming
                    }
                }
            }
        }
        
        private double CalculateRiskReward(double currentPrice, double entryLevel, double range, bool isLong)
        {
            double target = isLong ? entryLevel + range : entryLevel - range;
            double stop = isLong ? entryLevel - (range * 0.5) : entryLevel + (range * 0.5);
            
            double reward = Math.Abs(target - entryLevel);
            double risk = Math.Abs(entryLevel - stop);
            
            return risk > 0 ? reward / risk : 0;
        }
        
        private void CreateSignal(InstrumentData instrument, int barsInProgress, string direction)
        {
            // Prevent duplicate signals (1 per instrument per bar)
            DateTime currentTime = Times[barsInProgress][0];
            if (instrument.LastSignalTime == currentTime)
                return;
                
            instrument.LastSignalTime = currentTime;
            currentDaySignals++;
        }
        
        private int GetMinimumRange(string symbol)
        {
            // Minimum range requirements in ticks to avoid noise
            switch (symbol)
            {
                case "ES": return 8;   // 2 points
                case "NQ": return 12;  // 3 points  
                case "RTY": return 20; // 2 points
                case "YM": return 20;  // 20 points
                case "GC": return 30;  // 3 points
                case "CL": return 50;  // 50 cents
                default: return 10;
            }
        }
        
        private void DrawStatusDots()
        {
            // Remove previous dots
            for (int i = 0; i < displayNames.Length; i++)
            {
                RemoveDrawObject($"StatusDot_{displayNames[i]}");
            }
            
            // Draw dots at current bar position with different price levels for visibility
            double basePrice = Close[0];
            double tickSpacing = Instrument.MasterInstrument.TickSize * 5; // 5 tick spacing between dots
            
            int instrumentIndex = 0;
            foreach (var data in instrumentData.Values.OrderBy(x => Array.IndexOf(displayNames, x.DisplayName)))
            {
                Brush dotColor = GetDotColor(data);
                
                // Position dots vertically spaced at different price levels
                double dotPrice = basePrice + (instrumentIndex * tickSpacing);
                
                Draw.Dot(this, $"StatusDot_{data.DisplayName}", false, 0, dotPrice, dotColor);
                
                instrumentIndex++;
            }
        }
        
        private Brush GetDotColor(InstrumentData data)
        {
            switch (data.SetupStatus)
            {
                case 1: return Brushes.Yellow;    // Setup forming (10 ticks)
                case 2: return Brushes.LimeGreen; // Long ready (5 ticks)
                case 3: return Brushes.Red;       // Short ready (5 ticks)
                default: return Brushes.White;    // Waiting
            }
        }
        
        private void UpdateProfessionalDisplay()
        {
            // Only remove the specific object we're updating
            if (displayObjectsCreated)
            {
                RemoveDrawObject("FuturesScanner");
                // Clean up any leftover individual line objects
                for (int i = 0; i < displayNames.Length; i++)
                {
                    RemoveDrawObject($"FuturesScanner_Line_{displayNames[i]}");
                }
                RemoveDrawObject("FuturesScanner_Header");
            }
            
            var display = new StringBuilder();
            display.AppendLine("  🎯 FutureScan  ");
            display.AppendLine("  ═══════════════════  ");
            display.AppendLine($"   Signals: {currentDaySignals}/{MaxDailySignals}      ");
            display.AppendLine("");
            
            // Display each instrument with clear text status indicator
            foreach (var data in instrumentData.Values.OrderBy(x => Array.IndexOf(displayNames, x.DisplayName)))
            {
                string statusIndicator = GetStatusIndicator(data);
                string setupText = data.SetupType;
                string ticksInfo = data.TicksToTarget > 0 ? $" | {data.TicksToTarget}t" : "";
                
                string line = $"  {statusIndicator} {data.DisplayName}: {setupText}{ticksInfo}  ";
                display.AppendLine(line);
            }
            
            Draw.TextFixed(this, "FuturesScanner", display.ToString(), 
                          DisplayPosition, Brushes.White, 
                          new Gui.Tools.SimpleFont("Consolas", 14), Brushes.Black, 
                          Brushes.Black, 100);
                          
            displayObjectsCreated = true;
        }
        
        private string GetStatusIndicator(InstrumentData data)
        {
            switch (data.SetupStatus)
            {
                case 1: return "[Y]"; // Yellow - Setup forming (10 ticks)
                case 2: return "[L]"; // Long ready (5 ticks) - changed from [G] to [L] for Long
                case 3: return "[S]"; // Short ready (5 ticks) - changed from [R] to [S] for Short
                default: return "[ ]"; // Waiting
            }
        }

        public override string DisplayName
        {
            get { return "Futures Scanner Pro"; }
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.FutureScan[] cacheFutureScan;
		public Myindicators.FutureScan FutureScan(string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, TextPosition displayPosition)
		{
			return FutureScan(Input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, displayPosition);
		}

		public Myindicators.FutureScan FutureScan(ISeries<double> input, string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, TextPosition displayPosition)
		{
			if (cacheFutureScan != null)
				for (int idx = 0; idx < cacheFutureScan.Length; idx++)
					if (cacheFutureScan[idx] != null && cacheFutureScan[idx].ESContract == eSContract && cacheFutureScan[idx].NQContract == nQContract && cacheFutureScan[idx].RTYContract == rTYContract && cacheFutureScan[idx].YMContract == yMContract && cacheFutureScan[idx].GCContract == gCContract && cacheFutureScan[idx].CLContract == cLContract && cacheFutureScan[idx].ORBStartTime == oRBStartTime && cacheFutureScan[idx].ORBEndTime == oRBEndTime && cacheFutureScan[idx].RequireVolumeConfirmation == requireVolumeConfirmation && cacheFutureScan[idx].VolumeThreshold == volumeThreshold && cacheFutureScan[idx].MinRiskReward == minRiskReward && cacheFutureScan[idx].MaxDailySignals == maxDailySignals && cacheFutureScan[idx].ShowDisplay == showDisplay && cacheFutureScan[idx].DisplayPosition == displayPosition && cacheFutureScan[idx].EqualsInput(input))
						return cacheFutureScan[idx];
			return CacheIndicator<Myindicators.FutureScan>(new Myindicators.FutureScan(){ ESContract = eSContract, NQContract = nQContract, RTYContract = rTYContract, YMContract = yMContract, GCContract = gCContract, CLContract = cLContract, ORBStartTime = oRBStartTime, ORBEndTime = oRBEndTime, RequireVolumeConfirmation = requireVolumeConfirmation, VolumeThreshold = volumeThreshold, MinRiskReward = minRiskReward, MaxDailySignals = maxDailySignals, ShowDisplay = showDisplay, DisplayPosition = displayPosition }, input, ref cacheFutureScan);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.FutureScan FutureScan(string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, TextPosition displayPosition)
		{
			return indicator.FutureScan(Input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, displayPosition);
		}

		public Indicators.Myindicators.FutureScan FutureScan(ISeries<double> input , string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, TextPosition displayPosition)
		{
			return indicator.FutureScan(input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, displayPosition);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.FutureScan FutureScan(string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, TextPosition displayPosition)
		{
			return indicator.FutureScan(Input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, displayPosition);
		}

		public Indicators.Myindicators.FutureScan FutureScan(ISeries<double> input , string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, TextPosition displayPosition)
		{
			return indicator.FutureScan(input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, displayPosition);
		}
	}
}

#endregion