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

namespace NinjaTrader.NinjaScript.Indicators.FutureScan
{
    public class FutureScanv2 : Indicator
    {
        // Multi-instrument data storage
        private Dictionary<int, InstrumentData> instrumentData;
        private string[] futuresSymbols;
        private string[] displayNames = { "ES", "NQ", "RTY", "YM", "GC", "CL" };
        
        // Volume confirmation
        private Dictionary<int, Series<double>> volumeSeries;
        private Dictionary<int, SMA> volumeMA;
        
        // Display variables
        private int currentDaySignals = 0;
        private DateTime lastSignalReset = DateTime.MinValue;
        
        // Performance optimization
        private int displayUpdateCounter = 0;
        private const int DISPLAY_UPDATE_FREQUENCY = 5;
        private DateTime lastDisplayUpdate = DateTime.MinValue;

        public class InstrumentData
        {
            public string Symbol { get; set; }
            public string DisplayName { get; set; }
            public double SessionHigh { get; set; } = double.MinValue;
            public double SessionLow { get; set; } = double.MaxValue;
            public bool IsInRange { get; set; } = false;
            public string SetupType { get; set; } = "";
            public int SetupStatus { get; set; } = 0;
            public double CurrentPrice { get; set; } = 0;
            public double RiskReward { get; set; } = 0;
            public bool VolumeConfirmed { get; set; } = false;
            public DateTime LastSignalTime { get; set; } = DateTime.MinValue;
            public double TickSize { get; set; } = 0.25;
            public int TicksToTarget { get; set; } = 0;
            public bool IsLongSetup { get; set; } = false;
            public bool IsBullishBar { get; set; } = false;
            
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
                SetupType = "";
                SetupStatus = 0;
                VolumeConfirmed = false;
                RiskReward = 0;
                TicksToTarget = 0;
                IsLongSetup = false;
                IsBullishBar = false;
            }
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "ES Contract", Order = 1, GroupName = "Contracts")]
        public string ESContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "NQ Contract", Order = 2, GroupName = "Contracts")]
        public string NQContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RTY Contract", Order = 3, GroupName = "Contracts")]
        public string RTYContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "YM Contract", Order = 4, GroupName = "Contracts")]
        public string YMContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "GC Contract", Order = 5, GroupName = "Contracts")]
        public string GCContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "CL Contract", Order = 6, GroupName = "Contracts")]
        public string CLContract { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ORB Start Time", Order = 7, GroupName = "Settings")]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        public DateTime ORBStartTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ORB End Time", Order = 8, GroupName = "Settings")]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        public DateTime ORBEndTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volume Confirmation", Order = 9, GroupName = "Settings")]
        public bool RequireVolumeConfirmation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volume Threshold", Order = 10, GroupName = "Settings")]
        public double VolumeThreshold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Risk/Reward", Order = 11, GroupName = "Settings")]
        public double MinRiskReward { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Daily Signals", Description = "Maximum signals per day across all futures", Order = 12, GroupName = "Settings")]
        public int MaxDailySignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Scanner Display", Description = "Show the futures scanner overlay", Order = 13, GroupName = "Settings")]
        public bool ShowDisplay { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Dummy Setups", Description = "Show placeholder setups for testing display", Order = 14, GroupName = "Settings")]
        public bool EnableDummySetups { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Debug Active Setups", Description = "Show debug info for setups displayed in dashboard", Order = 15, GroupName = "Settings")]
        public bool DebugActiveSetups { get; set; }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Professional Futures Scanner - Multi-Column Display";
                Name = "FutureScanv2";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                
                // Default settings
                ESContract = "ES 06-25";
                NQContract = "NQ 06-25";
                RTYContract = "RTY 06-25";
                YMContract = "YM 06-25";
                GCContract = "GC 08-25";
                CLContract = "CL 07-25";
                
                ORBStartTime = new DateTime(2000, 1, 1, 9, 30, 0);
                ORBEndTime = new DateTime(2000, 1, 1, 9, 45, 0);
                RequireVolumeConfirmation = true;
                VolumeThreshold = 1.3;
                MinRiskReward = 1.5;
                EnableDummySetups = true;
                
                MaxDailySignals = 8;
                ShowDisplay = true;
                DebugActiveSetups = false;
            }
            else if (State == State.Configure)
            {
                futuresSymbols = new string[] { ESContract, NQContract, RTYContract, YMContract, GCContract, CLContract };
                
                // Add data series
                for (int i = 1; i < futuresSymbols.Length; i++)
                {
                    AddDataSeries(futuresSymbols[i], Data.BarsPeriodType.Minute, 1);
                }
                
                // Initialize data structures
                instrumentData = new Dictionary<int, InstrumentData>();
                volumeSeries = new Dictionary<int, Series<double>>();
                volumeMA = new Dictionary<int, SMA>();
                
                double[] tickSizes = { 0.25, 0.25, 0.1, 1.0, 0.1, 0.01 };
                
                for (int i = 0; i < futuresSymbols.Length; i++)
                {
                    instrumentData[i] = new InstrumentData(futuresSymbols[i], displayNames[i], tickSizes[i]);
                    volumeSeries[i] = new Series<double>(this, MaximumBarsLookBack.Infinite);
                }
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
                if (CurrentBars[BarsInProgress] < 21) return;
                
                // Initialize volume MA
                if (!volumeMA.ContainsKey(BarsInProgress) || volumeMA[BarsInProgress] == null)
                {
                    volumeMA[BarsInProgress] = SMA(Volumes[BarsInProgress], 20);
                }
                
                CheckDailyReset();
                
                if (!instrumentData.ContainsKey(BarsInProgress)) return;
                    
                var instrument = instrumentData[BarsInProgress];
                
                UpdateInstrumentData(instrument, BarsInProgress);
                ProcessORBLogic(instrument, BarsInProgress);
                
                if (!instrument.IsInRange && currentDaySignals < MaxDailySignals)
                {
                    DetectSetups(instrument, BarsInProgress);
                }
                
                // Add dummy setups for testing
                if (EnableDummySetups && BarsInProgress == 0)
                {
                    GenerateDummySetups();
                }
                
                // Update display
                if (BarsInProgress == 0 && ShowDisplay)
                {
                    displayUpdateCounter++;
                    if (displayUpdateCounter >= DISPLAY_UPDATE_FREQUENCY || 
                        DateTime.Now.Subtract(lastDisplayUpdate).TotalSeconds > 3)
                    {
                        UpdateDisplay();
                        displayUpdateCounter = 0;
                        lastDisplayUpdate = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error: {ex.Message}");
            }
        }
        
        private void CheckDailyReset()
        {
            DateTime currentDate = Times[BarsInProgress][0].Date;
            
            if (lastSignalReset != currentDate)
            {
                currentDaySignals = 0;
                lastSignalReset = currentDate;
                
                if (BarsInProgress == 0)
                {
                    foreach (var data in instrumentData.Values)
                    {
                        data.ResetDaily();
                    }
                    RemoveAllDisplayObjects();
                }
            }
        }
        
        private void UpdateInstrumentData(InstrumentData instrument, int barsInProgress)
        {
            instrument.CurrentPrice = Closes[barsInProgress][0];
            
            // Determine if current bar is bullish or bearish using proper Close vs Open
            instrument.IsBullishBar = Closes[barsInProgress][0] > Opens[barsInProgress][0];
            
            // Store the correct tick size for this specific instrument
            instrument.TickSize = TickSize; // This gives us the tick size for the current BarsInProgress instrument
            
            if (volumeSeries.ContainsKey(barsInProgress))
            {
                volumeSeries[barsInProgress][0] = Volumes[barsInProgress][0];
            }
            
            if (RequireVolumeConfirmation && volumeMA.ContainsKey(barsInProgress) && volumeMA[barsInProgress] != null)
            {
                double currentVolume = Volumes[barsInProgress][0];
                double avgVolume = volumeMA[barsInProgress][0];
                instrument.VolumeConfirmed = currentVolume >= (avgVolume * VolumeThreshold);
            }
            else
            {
                instrument.VolumeConfirmed = true;
            }
        }
        
        private void ProcessORBLogic(InstrumentData instrument, int barsInProgress)
        {
            DateTime currentTime = Times[barsInProgress][0];
            DateTime todayORBStart = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 
                                                 ORBStartTime.Hour, ORBStartTime.Minute, ORBStartTime.Second);
            DateTime todayORBEnd = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 
                                               ORBEndTime.Hour, ORBEndTime.Minute, ORBEndTime.Second);
            
            if (currentTime >= todayORBStart && currentTime <= todayORBEnd)
            {
                instrument.IsInRange = true;
                instrument.SessionHigh = Math.Max(instrument.SessionHigh, Highs[barsInProgress][0]);
                instrument.SessionLow = Math.Min(instrument.SessionLow, Lows[barsInProgress][0]);
                instrument.SetupType = "";
                instrument.SetupStatus = 0;
            }
            else if (currentTime > todayORBEnd && instrument.IsInRange)
            {
                instrument.IsInRange = false;
            }
        }
        
        private void DetectSetups(InstrumentData instrument, int barsInProgress)
        {
            if (instrument.SessionHigh == double.MinValue || instrument.SessionLow == double.MaxValue)
                return;
                
            double currentPrice = instrument.CurrentPrice;
            double upperLevel = instrument.SessionHigh;
            double lowerLevel = instrument.SessionLow;
            double range = upperLevel - lowerLevel;
            
            double minRangeTicks = GetMinimumRange(instrument.DisplayName);
            if (range < (minRangeTicks * instrument.TickSize))
            {
                return;
            }
            
            // Reset setup status first
            instrument.SetupType = "";
            instrument.SetupStatus = 0;
            instrument.IsLongSetup = false;
            
            // Calculate actual distances to ORB levels in ticks
            double distanceToUpperTicks = Math.Abs(currentPrice - upperLevel) / instrument.TickSize;
            double distanceToLowerTicks = Math.Abs(currentPrice - lowerLevel) / instrument.TickSize;
            
            // Determine which ORB level we're closest to
            bool nearUpperLevel = distanceToUpperTicks <= distanceToLowerTicks;
            double nearestDistance = nearUpperLevel ? distanceToUpperTicks : distanceToLowerTicks;
            
            // 30/10 Logic: Show signal when within 30 ticks, hide when 10+ ticks past the line
            bool showSignal = false;
            
            if (nearUpperLevel)
            {
                // Near upper ORB level
                if (currentPrice <= upperLevel)
                {
                    // Approaching from below - show signal if within 30 ticks
                    showSignal = distanceToUpperTicks <= 30;
                }
                else
                {
                    // Past the upper level - only show signal if within 10 ticks past
                    showSignal = distanceToUpperTicks <= 10;
                }
            }
            else
            {
                // Near lower ORB level  
                if (currentPrice >= lowerLevel)
                {
                    // Approaching from above - show signal if within 30 ticks
                    showSignal = distanceToLowerTicks <= 30;
                }
                else
                {
                    // Past the lower level - only show signal if within 10 ticks past
                    showSignal = distanceToLowerTicks <= 10;
                }
            }
            
            if (showSignal)
            {
                double riskReward = CalculateRiskReward(currentPrice, 
                    nearUpperLevel ? upperLevel : lowerLevel, range, nearUpperLevel);
                
                if (riskReward >= MinRiskReward)
                {
                    instrument.SetupType = "ORB";
                    instrument.RiskReward = riskReward;
                    instrument.IsLongSetup = nearUpperLevel; // Keep this for compatibility
                    
                    if (instrument.VolumeConfirmed)
                    {
                        instrument.SetupStatus = 2; // Ready
                        CreateSignal(instrument, barsInProgress, nearUpperLevel ? "Long" : "Short");
                    }
                    else
                    {
                        instrument.SetupStatus = 1; // Forming
                    }
                }
            }
            
            // DEBUG - Only print for active setups (shown in dashboard)
            if (DebugActiveSetups && instrument.SetupStatus > 0)
            {
                Print($"{instrument.DisplayName}: Price={currentPrice:F2}, ORB High={upperLevel:F2}, ORB Low={lowerLevel:F2}");
                Print($"{instrument.DisplayName}: DistToUpper={distanceToUpperTicks:F1}t, DistToLower={distanceToLowerTicks:F1}t");
                Print($"{instrument.DisplayName}: NearUpper={nearUpperLevel}, IsBullish={instrument.IsBullishBar}, Volume={instrument.VolumeConfirmed}, Status={instrument.SetupStatus}, R/R={instrument.RiskReward:F1}");
            }
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
        
        private void GenerateDummySetups()
        {
            Random rand = new Random();
            
            if (instrumentData.ContainsKey(1)) // NQ
            {
                instrumentData[1].SetupType = "TrendSpotter";
                instrumentData[1].SetupStatus = rand.Next(1, 3);
                instrumentData[1].IsBullishBar = rand.Next(0, 2) == 1;
            }
            
            if (instrumentData.ContainsKey(2)) // RTY
            {
                instrumentData[2].SetupType = "Breakout";
                instrumentData[2].SetupStatus = rand.Next(1, 3);
                instrumentData[2].IsBullishBar = rand.Next(0, 2) == 1;
            }
        }
        
        private void UpdateDisplay()
        {
            RemoveAllDisplayObjects();
            
            // Organize setups by type
            var setupsByType = new Dictionary<string, List<InstrumentData>>();
            setupsByType["ORB"] = new List<InstrumentData>();
            setupsByType["TrendSpotter"] = new List<InstrumentData>();
            setupsByType["Breakout"] = new List<InstrumentData>();
            
            foreach (var instrument in instrumentData.Values)
            {
                if (!string.IsNullOrEmpty(instrument.SetupType) && instrument.SetupStatus > 0)
                {
                    if (setupsByType.ContainsKey(instrument.SetupType))
                    {
                        setupsByType[instrument.SetupType].Add(instrument);
                    }
                }
            }
            
            // Draw header with signal count
            DrawMainHeader();
            
            // Draw individual colored items
            DrawColoredSetups(setupsByType);
        }
        
        private void DrawMainHeader()
        {
            var display = new StringBuilder();
            display.AppendLine($"🎯 FutureScan               Signals: {currentDaySignals}/{MaxDailySignals}");
            display.AppendLine("══════════════════════════════════════════════");
            display.AppendLine("");
            display.AppendLine("ORB Trade       TrendSpotter       Breakout");
            display.AppendLine("─────────       ────────────       ────────");
            
            Draw.TextFixed(this, "MainHeader", display.ToString(), 
                          TextPosition.TopLeft, Brushes.White, 
                          new Gui.Tools.SimpleFont("Consolas", 11), Brushes.Black, 
                          Brushes.Black, 100);
        }
        
        private void DrawColoredSetups(Dictionary<string, List<InstrumentData>> setupsByType)
        {
            // Draw ORB Trade setups (Column 1)
            for (int i = 0; i < setupsByType["ORB"].Count; i++)
            {
                var setup = setupsByType["ORB"][i];
                string contractMonth = ExtractContractMonth(setup.Symbol);
                string text = $"█ {setup.DisplayName} {contractMonth}";
                
                Brush color = GetSetupColor(setup);
                
                string positionedText = "\n\n\n\n\n" + text;
                for (int j = 0; j < i; j++) positionedText = "\n" + positionedText;
                
                Draw.TextFixed(this, $"ORB_{i}", positionedText, 
                              TextPosition.TopLeft, color, 
                              new Gui.Tools.SimpleFont("Consolas", 12), 
                              Brushes.Transparent, Brushes.Transparent, 0);
            }
            
            // Draw TrendSpotter setups (Column 2) 
            for (int i = 0; i < setupsByType["TrendSpotter"].Count; i++)
            {
                var setup = setupsByType["TrendSpotter"][i];
                string contractMonth = ExtractContractMonth(setup.Symbol);
                string text = "                   █ " + $"{setup.DisplayName} {contractMonth}";
                
                Brush color = GetSetupColor(setup);
                
                string positionedText = "\n\n\n\n\n" + text;
                for (int j = 0; j < i; j++) positionedText = "\n" + positionedText;
                
                Draw.TextFixed(this, $"Trend_{i}", positionedText, 
                              TextPosition.TopLeft, color, 
                              new Gui.Tools.SimpleFont("Consolas", 12), 
                              Brushes.Transparent, Brushes.Transparent, 0);
            }
            
            // Draw Breakout setups (Column 3)
            for (int i = 0; i < setupsByType["Breakout"].Count; i++)
            {
                var setup = setupsByType["Breakout"][i];
                string contractMonth = ExtractContractMonth(setup.Symbol);
                string text = "                                     █ " + $"{setup.DisplayName} {contractMonth}";
                
                Brush color = GetSetupColor(setup);
                
                string positionedText = "\n\n\n\n\n" + text;
                for (int j = 0; j < i; j++) positionedText = "\n" + positionedText;
                
                Draw.TextFixed(this, $"Breakout_{i}", positionedText, 
                              TextPosition.TopLeft, color, 
                              new Gui.Tools.SimpleFont("Consolas", 12), 
                              Brushes.Transparent, Brushes.Transparent, 0);
            }
        }
        
        private string ExtractContractMonth(string fullSymbol)
        {
            var parts = fullSymbol.Split(' ');
            return parts.Length > 1 ? parts[1] : "?";
        }
        
        private Brush GetSetupColor(InstrumentData setup)
        {
            // Use bullish/bearish bar color instead of long/short setup direction
            if (setup.IsBullishBar)
            {
                return Brushes.LimeGreen; // Green for bullish bars (Close > Open)
            }
            else
            {
                return Brushes.Red; // Red for bearish bars (Close < Open)
            }
        }
        
        private void RemoveAllDisplayObjects()
        {
            RemoveDrawObject("MainHeader");
            
            for (int i = 0; i < 10; i++)
            {
                RemoveDrawObject($"ORB_{i}");
                RemoveDrawObject($"Trend_{i}");
                RemoveDrawObject($"Breakout_{i}");
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
        
        private int GetMinimumRange(string symbol)
        {
            switch (symbol)
            {
                case "ES": return 8;
                case "NQ": return 12;
                case "RTY": return 20;
                case "YM": return 20;
                case "GC": return 30;
                case "CL": return 50;
                default: return 10;
            }
        }

        public override string DisplayName
        {
            get { return "FutureScan v2"; }
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private FutureScan.FutureScanv2[] cacheFutureScanv2;
		public FutureScan.FutureScanv2 FutureScanv2(string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, bool enableDummySetups, bool debugActiveSetups)
		{
			return FutureScanv2(Input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, enableDummySetups, debugActiveSetups);
		}

		public FutureScan.FutureScanv2 FutureScanv2(ISeries<double> input, string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, bool enableDummySetups, bool debugActiveSetups)
		{
			if (cacheFutureScanv2 != null)
				for (int idx = 0; idx < cacheFutureScanv2.Length; idx++)
					if (cacheFutureScanv2[idx] != null && cacheFutureScanv2[idx].ESContract == eSContract && cacheFutureScanv2[idx].NQContract == nQContract && cacheFutureScanv2[idx].RTYContract == rTYContract && cacheFutureScanv2[idx].YMContract == yMContract && cacheFutureScanv2[idx].GCContract == gCContract && cacheFutureScanv2[idx].CLContract == cLContract && cacheFutureScanv2[idx].ORBStartTime == oRBStartTime && cacheFutureScanv2[idx].ORBEndTime == oRBEndTime && cacheFutureScanv2[idx].RequireVolumeConfirmation == requireVolumeConfirmation && cacheFutureScanv2[idx].VolumeThreshold == volumeThreshold && cacheFutureScanv2[idx].MinRiskReward == minRiskReward && cacheFutureScanv2[idx].MaxDailySignals == maxDailySignals && cacheFutureScanv2[idx].ShowDisplay == showDisplay && cacheFutureScanv2[idx].EnableDummySetups == enableDummySetups && cacheFutureScanv2[idx].DebugActiveSetups == debugActiveSetups && cacheFutureScanv2[idx].EqualsInput(input))
						return cacheFutureScanv2[idx];
			return CacheIndicator<FutureScan.FutureScanv2>(new FutureScan.FutureScanv2(){ ESContract = eSContract, NQContract = nQContract, RTYContract = rTYContract, YMContract = yMContract, GCContract = gCContract, CLContract = cLContract, ORBStartTime = oRBStartTime, ORBEndTime = oRBEndTime, RequireVolumeConfirmation = requireVolumeConfirmation, VolumeThreshold = volumeThreshold, MinRiskReward = minRiskReward, MaxDailySignals = maxDailySignals, ShowDisplay = showDisplay, EnableDummySetups = enableDummySetups, DebugActiveSetups = debugActiveSetups }, input, ref cacheFutureScanv2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.FutureScan.FutureScanv2 FutureScanv2(string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, bool enableDummySetups, bool debugActiveSetups)
		{
			return indicator.FutureScanv2(Input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, enableDummySetups, debugActiveSetups);
		}

		public Indicators.FutureScan.FutureScanv2 FutureScanv2(ISeries<double> input , string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, bool enableDummySetups, bool debugActiveSetups)
		{
			return indicator.FutureScanv2(input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, enableDummySetups, debugActiveSetups);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.FutureScan.FutureScanv2 FutureScanv2(string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, bool enableDummySetups, bool debugActiveSetups)
		{
			return indicator.FutureScanv2(Input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, enableDummySetups, debugActiveSetups);
		}

		public Indicators.FutureScan.FutureScanv2 FutureScanv2(ISeries<double> input , string eSContract, string nQContract, string rTYContract, string yMContract, string gCContract, string cLContract, DateTime oRBStartTime, DateTime oRBEndTime, bool requireVolumeConfirmation, double volumeThreshold, double minRiskReward, int maxDailySignals, bool showDisplay, bool enableDummySetups, bool debugActiveSetups)
		{
			return indicator.FutureScanv2(input, eSContract, nQContract, rTYContract, yMContract, gCContract, cLContract, oRBStartTime, oRBEndTime, requireVolumeConfirmation, volumeThreshold, minRiskReward, maxDailySignals, showDisplay, enableDummySetups, debugActiveSetups);
		}
	}
}

#endregion
