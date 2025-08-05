#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using System.Xml.Serialization;
#endregion

using EnumType = NinjaTrader.NinjaScript.Indicators.TOP_Market_Energy_BuyingSelling.EnumType;

namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    [Gui.CategoryOrder("Entry Signal Settings", 1)]
    [Gui.CategoryOrder("Signal Filter", 2)]
    [Gui.CategoryOrder("Timeframe Settings", 3)]
    public class MTFEnergySignals : Indicator
    {
        private TOP_Market_Energy_BuyingSelling renkoIndicator;
        private TOP_Market_Energy_BuyingSelling m5Indicator;
        
        // SignalFilter reference
        private SignalFilter _signalFilter;
        
        private double lastRenkoSignal = 0;
        private double lastM5Signal = 0;
        private double lastConfluenceSignal = 0;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "MTFEnergySignals";
                Description = "Shows signals when Renko 40/10 and M5 energy align";
                IsOverlay = true;
                Calculate = Calculate.OnBarClose;
                
                // Default signal settings
                ShowEntrySignals = true;
                LongOn = "LongEntry";
                LongEntryColor = Brushes.Lime;
                ShortOn = "ShortEntry";
                ShortEntryColor = Brushes.Red;
                SignalOffset = 5;
                
                // Signal Filter
                UseSignalFilter = false;
				
				//Chart Settings
				RenkoBrickSize = 40;
                MinuteTimeframe = 5;
            }
            else if (State == State.Configure)
            {
                // Add Renko and minute data series
                AddRenko(Instrument.FullName, RenkoBrickSize, Data.MarketDataType.Last);
				AddDataSeries(Data.BarsPeriodType.Minute, MinuteTimeframe);
            }
            else if (State == State.DataLoaded)
            {
                renkoIndicator = TOP_Market_Energy_BuyingSelling(BarsArray[1], EnumType.One, 5, 100);
                m5Indicator = TOP_Market_Energy_BuyingSelling(BarsArray[2], EnumType.One, 5, 100);
                
                // Initialize SignalFilter if enabled
                if (UseSignalFilter)
                    _signalFilter = SignalFilter(1.25, 20, 35.0, true);
            }
        }

        public override string DisplayName
        {
            get
            {
                string baseDisplay = Name + "(" + RenkoBrickSize + "/" + MinuteTimeframe + ")";
                if (UseSignalFilter)
                    baseDisplay += " [Filtered]";
                return baseDisplay;
            }
        }

        private bool IsSignalAllowed(bool isLongSignal)
        {
            if (!UseSignalFilter || _signalFilter == null)
                return true;
                
            try
            {
                double allowValue = isLongSignal ? _signalFilter.AllowLong[0] : _signalFilter.AllowShort[0];
                return allowValue == 1;
            }
            catch
            {
                return true; // Allow signal on any error
            }
        }

        protected override void OnBarUpdate()
        {
            // Ensure we have enough bars for all timeframes
            if (CurrentBars[0] < 1 || CurrentBars[1] < 101 || CurrentBars[2] < 101)
                return;

            // Update signals when each timeframe updates
            if (BarsInProgress == 1) // Renko update
            {
                lastRenkoSignal = GetEnergySignal(renkoIndicator, lastRenkoSignal);
            }
            else if (BarsInProgress == 2) // M5 update
            {
                lastM5Signal = GetEnergySignal(m5Indicator, lastM5Signal);
            }
            else if (BarsInProgress == 0) // Primary chart update
            {
                // Calculate current confluence state
                double currentConfluence = 0;
                if (lastRenkoSignal == 1 && lastM5Signal == 1)
                    currentConfluence = 1; // Bullish confluence
                else if (lastRenkoSignal == -1 && lastM5Signal == -1)
                    currentConfluence = -1; // Bearish confluence

                // Only process when confluence state changes
                if (currentConfluence != lastConfluenceSignal)
                {
                    // Update confluence state first (always track this)
                    lastConfluenceSignal = currentConfluence;
                    
                    // Draw signals if enabled and conditions are met
                    if (ShowEntrySignals && currentConfluence != 0)
                    {
                        bool isBullBar = Close[0] > Open[0];
                        
                        // Draw bullish signal on bull bars (with filter check)
                        if (currentConfluence == 1 && isBullBar && IsSignalAllowed(true))
                        {
                            Draw.ArrowUp(this, LongOn + CurrentBar.ToString(), true, 0, Low[0] - SignalOffset * TickSize, LongEntryColor);
                        }
                        // Draw bearish signal on bear bars (with filter check)
                        else if (currentConfluence == -1 && !isBullBar && IsSignalAllowed(false))
                        {
                            Draw.ArrowDown(this, ShortOn + CurrentBar.ToString(), true, 0, High[0] + SignalOffset * TickSize, ShortEntryColor);
                        }
                    }
                }
            }
        }

        private double GetEnergySignal(TOP_Market_Energy_BuyingSelling indicator, double lastSignal)
        {
            try
            {
                double upValue = indicator.Up[0];
                double dnValue = indicator.Dn[0];
                double upValuePrev = indicator.Up[1];
                double dnValuePrev = indicator.Dn[1];
                
                bool upCrossedAbove = (upValuePrev <= dnValuePrev) && (upValue > dnValue);
                bool dnCrossedAbove = (dnValuePrev <= upValuePrev) && (dnValue > upValue);
                
                if (upCrossedAbove)
                    return 1;
                else if (dnCrossedAbove)
                    return -1;
                
                return lastSignal;
            }
            catch
            {
                return lastSignal;
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Show Entry Signals", Order = 1, GroupName = "Entry Signal Settings")]
        public bool ShowEntrySignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", GroupName = "Entry Signal Settings", Order = 2)]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Entry Color", Description = "Color for the long entry arrow", Order = 3, GroupName = "Entry Signal Settings")]
        public Brush LongEntryColor { get; set; }

        [Browsable(false)]
        public string LongEntryColorSerializable
        {
            get { return Serialize.BrushToString(LongEntryColor); }
            set { LongEntryColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Short On", GroupName = "Entry Signal Settings", Order = 4)]
        public string ShortOn { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Short Entry Color", Description = "Color for the short entry arrow", Order = 5, GroupName = "Entry Signal Settings")]
        public Brush ShortEntryColor { get; set; }

        [Browsable(false)]
        public string ShortEntryColorSerializable
        {
            get { return Serialize.BrushToString(ShortEntryColor); }
            set { ShortEntryColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal Offset", GroupName = "Entry Signal Settings", Order = 6)]
        public double SignalOffset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Signal Filter", Description = "Enable Keltner Channel-based signal filtering. Requires SignalFilter indicator on chart.", Order = 1, GroupName = "Signal Filter")]
        public bool UseSignalFilter { get; set; }
		
		[Range(1, 100)]
		[Display(Name = "Renko Brick Size", Order = 1, GroupName = "Timeframe Settings")]
		public int RenkoBrickSize { get; set; }
		
		[Range(1, 60)]
		[Display(Name = "Minute Timeframe", Order = 2, GroupName = "Timeframe Settings")]
		public int MinuteTimeframe { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.MTFEnergySignals[] cacheMTFEnergySignals;
		public Myindicators.MTFEnergySignals MTFEnergySignals(bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signalOffset, bool useSignalFilter)
		{
			return MTFEnergySignals(Input, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, signalOffset, useSignalFilter);
		}

		public Myindicators.MTFEnergySignals MTFEnergySignals(ISeries<double> input, bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signalOffset, bool useSignalFilter)
		{
			if (cacheMTFEnergySignals != null)
				for (int idx = 0; idx < cacheMTFEnergySignals.Length; idx++)
					if (cacheMTFEnergySignals[idx] != null && cacheMTFEnergySignals[idx].ShowEntrySignals == showEntrySignals && cacheMTFEnergySignals[idx].LongOn == longOn && cacheMTFEnergySignals[idx].LongEntryColor == longEntryColor && cacheMTFEnergySignals[idx].ShortOn == shortOn && cacheMTFEnergySignals[idx].ShortEntryColor == shortEntryColor && cacheMTFEnergySignals[idx].SignalOffset == signalOffset && cacheMTFEnergySignals[idx].UseSignalFilter == useSignalFilter && cacheMTFEnergySignals[idx].EqualsInput(input))
						return cacheMTFEnergySignals[idx];
			return CacheIndicator<Myindicators.MTFEnergySignals>(new Myindicators.MTFEnergySignals(){ ShowEntrySignals = showEntrySignals, LongOn = longOn, LongEntryColor = longEntryColor, ShortOn = shortOn, ShortEntryColor = shortEntryColor, SignalOffset = signalOffset, UseSignalFilter = useSignalFilter }, input, ref cacheMTFEnergySignals);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MTFEnergySignals MTFEnergySignals(bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signalOffset, bool useSignalFilter)
		{
			return indicator.MTFEnergySignals(Input, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, signalOffset, useSignalFilter);
		}

		public Indicators.Myindicators.MTFEnergySignals MTFEnergySignals(ISeries<double> input , bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signalOffset, bool useSignalFilter)
		{
			return indicator.MTFEnergySignals(input, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, signalOffset, useSignalFilter);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MTFEnergySignals MTFEnergySignals(bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signalOffset, bool useSignalFilter)
		{
			return indicator.MTFEnergySignals(Input, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, signalOffset, useSignalFilter);
		}

		public Indicators.Myindicators.MTFEnergySignals MTFEnergySignals(ISeries<double> input , bool showEntrySignals, string longOn, Brush longEntryColor, string shortOn, Brush shortEntryColor, double signalOffset, bool useSignalFilter)
		{
			return indicator.MTFEnergySignals(input, showEntrySignals, longOn, longEntryColor, shortOn, shortEntryColor, signalOffset, useSignalFilter);
		}
	}
}

#endregion
