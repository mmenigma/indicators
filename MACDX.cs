///  ZIP File Name:  MACD Custom Directional Color.zip

///  This script has Market Analyzer functionality, but it is remarked out and not used.
///  The remaining functionality plots MACD, MACD Average, and Differential, and paints MACD line, MACD Average line, and Differential histogram per Up/Down conditions.
///  Plot order of lines plots Differential Histogram on bottom, then MACD Average, then MACD on top.

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
	public class MACDX : Indicator
	{

		private Brush UpColor;
		private Brush DownColor;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "MACDX";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;  
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				IsAutoScale									= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;

				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;

				Fast					= 12;
				Slow					= 26;
				Smooth					= 9;
				ShowMACD				= false;  //True will display MACD and MACD Average lines with Up/Down directional color; False will display lines of one color.
				ShowHistogram			= true;  //True will display Differential Histogram with Up/Down directional color; False will display histogram of one color.
				ShowBackColor			= false;  //True will display chart background with Up/Down directional color; False will not change default chart background color.

				HistUpRising			= Brushes.SeaGreen;
				HistUpFalling			= Brushes.DarkSeaGreen;
				HistDownFalling			= Brushes.DarkRed;
				HistDownRising			= Brushes.IndianRed;
				SignalColorRising		= Brushes.Cyan;
				SignalColorFalling		= Brushes.DarkCyan;
				MACDRising				= Brushes.Red;
				MACDFalling				= Brushes.DarkRed;

				/// Standard NinjaTrader chart background color is 27,27,27
				/// Initiate new solid color backbrush with custom color:  Uptrend Background Color
				UpColor = new SolidColorBrush(Color.FromRgb(40, 45, 40));  //Original:  40,40,40 (gray, with no green emphasis)
				UpColor.Freeze();
				/// Initiate new solid color backbrush with custom color:  Downtrend Background Color
				DownColor = new SolidColorBrush(Color.FromRgb(24, 14, 14));  //Original:  14,14,14 (gray, with no red emphasis)
				DownColor.Freeze();
				
				AddPlot(Brushes.DarkGoldenrod, "Hist");  //0 - Plot order, under MACD Average and MACD
				AddPlot(Brushes.Cyan, "Avg");  //1 - Plot order, over Histogram
				AddPlot(Brushes.Red, "MacdValue");  //2 - Plot order, over MACD Average
                AddPlot(Brushes.Goldenrod, "Upper Limit");
                AddPlot(Brushes.Goldenrod, "Lower Limit");
				
///Remove remark to restore MarketAnalyzer function.
//				AddPlot(Brushes.Transparent, "MarketAnalyzer");  //3

				AddLine(new Stroke(Brushes.Gainsboro, DashStyleHelper.Dash, 1), 0, "ZeroLine");
				
				//Hist - MACD Differential Histogram.  Plot order - on bottom.
	            Plots[0].AutoWidth = true; 
	            Plots[0].PlotStyle = PlotStyle.Bar; 
	            Plots[0].DashStyleHelper = DashStyleHelper.Solid;
				//Avg - MACD Average (Signal).  Plot order - on top of Differential Histogram.
	            Plots[1].Width = 2; 
	            Plots[1].PlotStyle = PlotStyle.Line; 
	            Plots[1].DashStyleHelper = DashStyleHelper.Solid;
				//MacdValue - MACD.  Plot order - on top of MACD Average (Signal).
				Plots[2].Width = 2; 
	            Plots[2].PlotStyle = PlotStyle.Line; 
	            Plots[2].DashStyleHelper = DashStyleHelper.Solid;
				
				LongOn = true;
                ShortOn = true;
				ShowCrossSignal = true;
				
				LongOnString = "LongOn";
                ShortOnString = "ShortOn";

                UpperLimit = 12;
                LowerLimit = -12;
				Signal_Offset = 5;

			}
			else if (State == State.Configure)
			{
			}
			else if( State == State.DataLoaded )
			{
			}
		}

		protected override void OnBarUpdate()
		{
		    if (CurrentBar == 0)
		        return;
		
		    // Calculate MACD values
		    double Macd = MACD(Fast, Slow, Smooth)[0];
		    double Signal = MACD(Fast, Slow, Smooth).Avg[0];
		    double Histog = Macd - Signal;
		
		    // Assign MACD values to plots
		    MacdValue[0] = Macd;
		    Avg[0] = Signal;
		    Hist[0] = Histog;
		
		    double priorMacd = MacdValue[1];
		    double priorSignal = Avg[1];
		    double priorHistog = Hist[1];
		
		    // Plot the Upper and Lower Limits
		    Values[3][0] = UpperLimit;
		    Values[4][0] = LowerLimit;

    // Check if ShowCrossSignal is enabled
		   if (ShowCrossSignal)
		{
			
		   // Check for Long Signal (MACD crosses above Avg and both are below LowerLimit)
		if (CrossAbove(MacdValue, Avg, 1)&& LongOn && MacdValue[0] < LowerLimit && Avg[0] < LowerLimit)
		{
		 
		    Draw.ArrowUp(this, LongOnString + CurrentBar, true, 0, Low[0] - Signal_Offset * TickSize, Brushes.Lime);
		}
		
		// Check for Short Signal (MACD crosses below Avg and both are above UpperLimit)
		if (CrossBelow(MacdValue, Avg, 1)&& ShortOn && MacdValue[0] > UpperLimit && Avg[0] > UpperLimit)
		{
		    Draw.ArrowDown(this, ShortOnString + CurrentBar, true, 0, High[0] + Signal_Offset * TickSize, Brushes.Lime);
		}
	}
    // MACD and MACD Average (Signal) Up/Down Color
    if (ShowMACD)
    {
        if (Macd >= priorMacd)
            PlotBrushes[2][0] = MACDRising;
        else
            PlotBrushes[2][0] = MACDFalling;

        if (Signal >= priorSignal)
            PlotBrushes[1][0] = SignalColorRising;
        else
            PlotBrushes[1][0] = SignalColorFalling;
    }
    else
    {
        PlotBrushes[2][0] = Brushes.Red;
        PlotBrushes[1][0] = Brushes.Cyan;
    }

    // Differential Histogram Up/Down Color
    if (ShowHistogram)
    {
        if (Histog >= 0 && Histog >= priorHistog)
            PlotBrushes[0][0] = HistUpRising;
        else if (Histog >= 0 && Histog < priorHistog)
            PlotBrushes[0][0] = HistUpFalling;
        else if (Histog < 0 && Histog >= priorHistog)
            PlotBrushes[0][0] = HistDownRising;
        else
            PlotBrushes[0][0] = HistDownFalling;
    }
    else
    {
        PlotBrushes[0][0] = Brushes.DarkGoldenrod;
    }

    // Chart Background Up/Down Color
    if (ShowBackColor)
    {
        if (Macd > Signal && Macd > 0)
        {
            BackBrush = UpColor;
        }
        else if (Macd < Signal && Macd < 0)
        {
            BackBrush = DownColor;
        }
	}
}
		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Fast period", Description="Number of bars for fast EMA", Order=1, GroupName="Parameters")]
		public int Fast
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Slow period", Description="Number of bars for slow EMA", Order=2, GroupName="Parameters")]
		public int Slow
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Smoothing", Description="Number of bars for smoothing.", Order=3, GroupName="Parameters")]
		public int Smooth
		{ get; set; }

		[Display(Name="MACD Up/Down Color", Description="Show the MACD and Signal Line with directional color?", Order=1, GroupName="Options")]
		public bool ShowMACD
		{ get; set; }

		[Display(Name="Histogram Up/Down Color", Description="Show the MACD Histogram with directional color?", Order=2, GroupName="Options")]
		public bool ShowHistogram
		{ get; set; }

		[Display(Name="Background Up/Down Color", Description="Show the chart background with directional color?", Order=3, GroupName="Options")]
		public bool ShowBackColor
		{ get; set; }
		
// Signals Alerts
		[NinjaScriptProperty]
		[Display(Name = "Show Cross Signal?", GroupName = "Signal & Alert", Order = 0)]
        public bool ShowCrossSignal { get; set; }

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

		[XmlIgnore]
		[Display(Name="Histogram rising above zero", Description="Macd / Hist > 0 (Rising)", Order=1, GroupName="Plot colors")]
		public Brush HistUpRising
		{ get; set; }

		[Browsable(false)]
		public string HistUpRisingSerializable
		{
			get { return Serialize.BrushToString(HistUpRising); }
			set { HistUpRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram falling above zero", Description="Macd / Hist > 0 (Falling)", Order=2, GroupName="Plot colors")]
		public Brush HistUpFalling
		{ get; set; }

		[Browsable(false)]
		public string HistUpFallingSerializable
		{
			get { return Serialize.BrushToString(HistUpFalling); }
			set { HistUpFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram falling below zero", Description="Macd / Hist < 0 (Falling)", Order=3, GroupName="Plot colors")]
		public Brush HistDownFalling
		{ get; set; }

		[Browsable(false)]
		public string HistDownFallingSerializable
		{
			get { return Serialize.BrushToString(HistDownFalling); }
			set { HistDownFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram rising below zero", Description="Macd / Hist < 0 (Rising)", Order=4, GroupName="Plot colors")]
		public Brush HistDownRising
		{ get; set; }

		[Browsable(false)]
		public string HistDownRisingSerializable
		{
			get { return Serialize.BrushToString(HistDownRising); }
			set { HistDownRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Signal color rising", Description="Signal Line Rising", Order=5, GroupName="Plot colors")]
		public Brush SignalColorRising
		{ get; set; }

		[Browsable(false)]
		public string SignalColorRisingSerializable
		{
			get { return Serialize.BrushToString(SignalColorRising); }
			set { SignalColorRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Signal color falling", Description="Signal Line Falling", Order=6, GroupName="Plot colors")]
		public Brush SignalColorFalling
		{ get; set; }

		[Browsable(false)]
		public string SignalColorFallingSerializable
		{
			get { return Serialize.BrushToString(SignalColorFalling); }
			set { SignalColorFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="MACD rising color", Description="MACD Rising", Order=7, GroupName="Plot colors")]
		public Brush MACDRising
		{ get; set; }

		[Browsable(false)]
		public string MACDRisingSerializable
		{
			get { return Serialize.BrushToString(MACDRising); }
			set { MACDRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="MACD falling color", Description="MACD Falling", Order=8, GroupName="Plot colors")]
		public Brush MACDFalling
		{ get; set; }

		[Browsable(false)]
		public string MACDFallingSerializable
		{
			get { return Serialize.BrushToString(MACDFalling); }
			set { MACDFalling = Serialize.StringToBrush(value); }
		}			

			[Browsable(false)]
		[XmlIgnore]
		public Series<double> Hist
		{
			get { return Values[0]; }
		}
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> Avg
		{
			get { return Values[1]; }
		}
		
			[Browsable(false)]
		[XmlIgnore]
		public Series<double> MacdValue
		{
			get { return Values[2]; }
		}

///Remove remark to restore MarketAnalyzer function.
//		[Browsable(false)]
//		[XmlIgnore]
//		public Series<double> MarketAnalyzer
//		{
//			get { return Values[3]; }
//		}

		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.MACDX[] cacheMACDX;
		public Myindicators.MACDX MACDX(int fast, int slow, int smooth, bool showCrossSignal, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return MACDX(Input, fast, slow, smooth, showCrossSignal, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}

		public Myindicators.MACDX MACDX(ISeries<double> input, int fast, int slow, int smooth, bool showCrossSignal, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			if (cacheMACDX != null)
				for (int idx = 0; idx < cacheMACDX.Length; idx++)
					if (cacheMACDX[idx] != null && cacheMACDX[idx].Fast == fast && cacheMACDX[idx].Slow == slow && cacheMACDX[idx].Smooth == smooth && cacheMACDX[idx].ShowCrossSignal == showCrossSignal && cacheMACDX[idx].LongOn == longOn && cacheMACDX[idx].ShortOn == shortOn && cacheMACDX[idx].LongOnString == longOnString && cacheMACDX[idx].ShortOnString == shortOnString && cacheMACDX[idx].Signal_Offset == signal_Offset && cacheMACDX[idx].UpperLimit == upperLimit && cacheMACDX[idx].LowerLimit == lowerLimit && cacheMACDX[idx].EqualsInput(input))
						return cacheMACDX[idx];
			return CacheIndicator<Myindicators.MACDX>(new Myindicators.MACDX(){ Fast = fast, Slow = slow, Smooth = smooth, ShowCrossSignal = showCrossSignal, LongOn = longOn, ShortOn = shortOn, LongOnString = longOnString, ShortOnString = shortOnString, Signal_Offset = signal_Offset, UpperLimit = upperLimit, LowerLimit = lowerLimit }, input, ref cacheMACDX);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MACDX MACDX(int fast, int slow, int smooth, bool showCrossSignal, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.MACDX(Input, fast, slow, smooth, showCrossSignal, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}

		public Indicators.Myindicators.MACDX MACDX(ISeries<double> input , int fast, int slow, int smooth, bool showCrossSignal, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.MACDX(input, fast, slow, smooth, showCrossSignal, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MACDX MACDX(int fast, int slow, int smooth, bool showCrossSignal, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.MACDX(Input, fast, slow, smooth, showCrossSignal, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}

		public Indicators.Myindicators.MACDX MACDX(ISeries<double> input , int fast, int slow, int smooth, bool showCrossSignal, bool longOn, bool shortOn, string longOnString, string shortOnString, int signal_Offset, double upperLimit, double lowerLimit)
		{
			return indicator.MACDX(input, fast, slow, smooth, showCrossSignal, longOn, shortOn, longOnString, shortOnString, signal_Offset, upperLimit, lowerLimit);
		}
	}
}

#endregion
