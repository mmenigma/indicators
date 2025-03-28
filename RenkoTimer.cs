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
    [Description("Calculates the average time NinzaRenko or UniRenko bars remain open")]
    public class RenkoTimer : Indicator
    {
        private List<long> barTimes = new List<long>();
        private DateTime lastBarTime;
        private bool isFirstBar = true;
        private TimeSpan avgTimeOpen = TimeSpan.Zero;
        private string labelName = "AvgTimeLabel";
        private DateTime currentBarStartTime;
 
		#region Properties
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Lookback Period", Description = "Number of bars to use for average calculation", Order = 1, GroupName = "Settings")]
        public int LookbackPeriod { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Time On Price Panel", Description = "Display the average time directly on the price chart panel", Order = 2, GroupName = "Settings")]
        public bool ShowTimeLabel { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Label Position", Description = "Position of the time label on the chart", Order = 3, GroupName = "Settings")]
        public TextPosition LabelPosition { get; set; }
        
        [NinjaScriptProperty]
        [Range(8, 24)]
        [Display(Name = "Font Size", Description = "Size of the timer text", Order = 4, GroupName = "Text Appearance")]
        public int FontSize { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Text Color", Description = "Color of the timer text", Order = 5, GroupName = "Text Appearance")]
		public Brush TextColor { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Background Opacity", Description = "Opacity of the text background (0-255, 0 = transparent)", Order = 6, GroupName = "Text Appearance")]
        [Range(0, 255)]
        public int BackgroundOpacity { get; set; }
        #endregion
        
		protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                 = @"Calculates the average time NinzaRenko or UniRenko bars remain open";
                Name                        = "RenkoTimer";
                Calculate                   = Calculate.OnBarClose;
                IsOverlay                   = true;
                DisplayInDataBox            = true;
                DrawOnPricePanel            = false;
                DrawHorizontalGridLines     = true;
                DrawVerticalGridLines       = true;
                PaintPriceMarkers           = true;
                ScaleJustification          = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive    = false;
                
                // Default parameter values
                LookbackPeriod              = 20;
                ShowTimeLabel               = true;
                LabelPosition               = TextPosition.BottomLeft;
                FontSize                    = 12;
                TextColor                   = Brushes.Silver;
                BackgroundOpacity           = 100;
                
                // Indicator plot properties - setting opacity to 0 to make it invisible
                AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Line, "AvgTimeSeconds");
            }
           else if (State == State.Configure)
            {
                // Check for NinzaRenko, UniRenko and other custom Renko bar types
                // Using case-insensitive comparison for better detection
                if (BarsArray[0].BarsPeriod.BarsPeriodType != BarsPeriodType.HeikenAshi 
                    && BarsArray[0].BarsPeriod.BarsPeriodType != BarsPeriodType.Renko
                    && BarsArray[0].BarsPeriod.BarsPeriodType != BarsPeriodType.Range
                    && !Bars.BarsPeriod.ToString().ToLower().Contains("renko")
                    && !Bars.BarsPeriod.ToString().ToLower().Contains("ninza")
                    && !Bars.BarsPeriod.ToString().ToLower().Contains("uni"))
                {
                    Draw.TextFixed(this, "Error", "This indicator is designed for NinzaRenko or UniRenko bars", TextPosition.BottomRight);
                    return;
                }
        }
	}
        protected override void OnBarUpdate()
        {
            // Skip the first bar as we need two bars to calculate time difference
            if (isFirstBar)
            {
                lastBarTime = Time[0];
                currentBarStartTime = Time[0];
                isFirstBar = false;
                return;
            }
            
            // If we have a new bar
            if (CurrentBar > 0)
            {
                // Calculate time the previous bar was open
                TimeSpan timeDifference = Time[0] - lastBarTime;
                
                // Store the time in seconds
                barTimes.Add((long)timeDifference.TotalSeconds);
                
                // Trim the list to maintain only the specified lookback period
                if (barTimes.Count > LookbackPeriod)
                    barTimes.RemoveAt(0);
                
                // Calculate the average time bars remain open
                double averageSeconds = barTimes.Average();
                avgTimeOpen = TimeSpan.FromSeconds(averageSeconds);
                
                // Update plot with the average in seconds
                Values[0][0] = averageSeconds;
                
                // Store current time for next calculation
                lastBarTime = Time[0];
                
                // Reset current bar start time
                currentBarStartTime = Time[0];
            }
        }
        
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            
            // Display the timer text with both average and current bar times
            if (ShowTimeLabel && !isFirstBar)
            {
                // Calculate current bar running time
                TimeSpan currentBarTime = DateTime.Now - currentBarStartTime;
                
                // Format both times
                string timeText = string.Format("Avg Bar Time: {0:D2}:{1:D2}:{2:D2}  |  Current Bar: {3:D2}:{4:D2}:{5:D2}", 
                                avgTimeOpen.Hours, 
                                avgTimeOpen.Minutes, 
                                avgTimeOpen.Seconds,
                                currentBarTime.Hours,
                                currentBarTime.Minutes,
                                currentBarTime.Seconds);
                
                // Create background brush with user-defined opacity (or transparent if opacity=0)
                Brush backgroundBrush = BackgroundOpacity > 0 
                    ? new SolidColorBrush(Color.FromArgb((byte)BackgroundOpacity, 0, 0, 0)) 
                    : Brushes.Transparent;
                
                // Draw text with user-defined properties
                Draw.TextFixed(this, "TimeTextPricePanel", timeText, LabelPosition, 
                    TextColor, new SimpleFont("Arial", FontSize), 
                    null, backgroundBrush, 100);
            }
        }
        
        public override string ToString()
        {
            return string.Format("Avg Time: {0:D2}:{1:D2}:{2:D2}", 
                                avgTimeOpen.Hours, 
                                avgTimeOpen.Minutes, 
                                avgTimeOpen.Seconds);
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.RenkoTimer[] cacheRenkoTimer;
		public Myindicators.RenkoTimer RenkoTimer(int lookbackPeriod, bool showTimeLabel, TextPosition labelPosition, int fontSize, Brush textColor, int backgroundOpacity)
		{
			return RenkoTimer(Input, lookbackPeriod, showTimeLabel, labelPosition, fontSize, textColor, backgroundOpacity);
		}

		public Myindicators.RenkoTimer RenkoTimer(ISeries<double> input, int lookbackPeriod, bool showTimeLabel, TextPosition labelPosition, int fontSize, Brush textColor, int backgroundOpacity)
		{
			if (cacheRenkoTimer != null)
				for (int idx = 0; idx < cacheRenkoTimer.Length; idx++)
					if (cacheRenkoTimer[idx] != null && cacheRenkoTimer[idx].LookbackPeriod == lookbackPeriod && cacheRenkoTimer[idx].ShowTimeLabel == showTimeLabel && cacheRenkoTimer[idx].LabelPosition == labelPosition && cacheRenkoTimer[idx].FontSize == fontSize && cacheRenkoTimer[idx].TextColor == textColor && cacheRenkoTimer[idx].BackgroundOpacity == backgroundOpacity && cacheRenkoTimer[idx].EqualsInput(input))
						return cacheRenkoTimer[idx];
			return CacheIndicator<Myindicators.RenkoTimer>(new Myindicators.RenkoTimer(){ LookbackPeriod = lookbackPeriod, ShowTimeLabel = showTimeLabel, LabelPosition = labelPosition, FontSize = fontSize, TextColor = textColor, BackgroundOpacity = backgroundOpacity }, input, ref cacheRenkoTimer);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.RenkoTimer RenkoTimer(int lookbackPeriod, bool showTimeLabel, TextPosition labelPosition, int fontSize, Brush textColor, int backgroundOpacity)
		{
			return indicator.RenkoTimer(Input, lookbackPeriod, showTimeLabel, labelPosition, fontSize, textColor, backgroundOpacity);
		}

		public Indicators.Myindicators.RenkoTimer RenkoTimer(ISeries<double> input , int lookbackPeriod, bool showTimeLabel, TextPosition labelPosition, int fontSize, Brush textColor, int backgroundOpacity)
		{
			return indicator.RenkoTimer(input, lookbackPeriod, showTimeLabel, labelPosition, fontSize, textColor, backgroundOpacity);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.RenkoTimer RenkoTimer(int lookbackPeriod, bool showTimeLabel, TextPosition labelPosition, int fontSize, Brush textColor, int backgroundOpacity)
		{
			return indicator.RenkoTimer(Input, lookbackPeriod, showTimeLabel, labelPosition, fontSize, textColor, backgroundOpacity);
		}

		public Indicators.Myindicators.RenkoTimer RenkoTimer(ISeries<double> input , int lookbackPeriod, bool showTimeLabel, TextPosition labelPosition, int fontSize, Brush textColor, int backgroundOpacity)
		{
			return indicator.RenkoTimer(input, lookbackPeriod, showTimeLabel, labelPosition, fontSize, textColor, backgroundOpacity);
		}
	}
}

#endregion
