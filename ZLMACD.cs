///  Zero Lag MACD Custom Directional Color
///
///  This indicator implements a Zero Lag version of the MACD (Moving Average Convergence/Divergence) indicator 
///  with directional coloring. Zero Lag processing reduces the lag typically present in traditional MACD indicators,
///  providing earlier signals with less delay.
///
///  The indicator plots Zero Lag MACD, MACD Average, and Differential, and paints them based on their directional movement.
///  Plot order places Differential Histogram on bottom, MACD Average in middle, and MACD on top.

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
	public class ZLMACD : Indicator
	{
///Remove remark to restore PaintBar function.
//		public enum MaCD_PaintBarType2 {maCD, hist}

		private Brush UpColor;
		private Brush DownColor;
		private Series<double> fastEMA;
		private Series<double> slowEMA;
		private Series<double> zlFastEMA;
		private Series<double> zlSlowEMA;
		private Series<double> zeroLagMaCD;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= "Zero Lag MACD (Moving Average Convergence/Divergence) reduces the lag typically present in traditional MACD indicators.";
				Name										= "ZLMACD";
				Calculate									= Calculate.OnPriceChange;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= false;  ///Change to True if PaintBar funcationality enabled.
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
				ZeroLagFactor           = 0.7;
				ShowMACD				= true;  //True will display MACD and MACD Average lines with Up/Down directional color; False will display lines of one color.
				ShowHistogram			= true;  //True will display Differential Histogram with Up/Down directional color; False will display histogram of one color.
				ShowBackColor			= true;  //True will display chart background with Up/Down directional color; False will not change default chart background color.

///Remove remark to restore PaintBar function.
//				PaintBars				= false;

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

///Remove remark to restore PaintBar function.				
//				pbType					= MaCD_PaintBarType2.maCD;
				
				AddPlot(Brushes.DarkGoldenrod, "Hist");  //0 - Plot order, under MACD Average and MACD
				AddPlot(Brushes.Cyan, "Avg");  //1 - Plot order, over Histogram
				AddPlot(Brushes.Red, "MaCDValue");  //2 - Plot order, over MACD Average

///Remove remark to restore MarketAnalyzer function.
//				AddPlot(Brushes.Transparent, "MarketAnalyzer");  //3

				AddLine(new Stroke(Brushes.Yellow, DashStyleHelper.Dash, 1), 0, "ZeroLine");
				
				//Hist - MACD Differential Histogram.  Plot order - on bottom.
	            Plots[0].AutoWidth = true; 
	            Plots[0].PlotStyle = PlotStyle.Bar; 
	            Plots[0].DashStyleHelper = DashStyleHelper.Solid;
				//Avg - MACD Average (Signal).  Plot order - on top of Differential Histogram.
	            Plots[1].Width = 2; 
	            Plots[1].PlotStyle = PlotStyle.Line; 
	            Plots[1].DashStyleHelper = DashStyleHelper.Solid;
				//MaCDValue - MACD.  Plot order - on top of MACD Average (Signal).
				Plots[2].Width = 2; 
	            Plots[2].PlotStyle = PlotStyle.Line; 
	            Plots[2].DashStyleHelper = DashStyleHelper.Solid;

			}
			else if (State == State.Configure)
			{
				// Create the required series
                fastEMA = new Series<double>(this);
                slowEMA = new Series<double>(this);
                zlFastEMA = new Series<double>(this);
                zlSlowEMA = new Series<double>(this);
                zeroLagMaCD = new Series<double>(this);
			}
			else if( State == State.DataLoaded )
			{
			}
		}

protected override void OnBarUpdate()
{
    if (CurrentBar < Math.Max(Fast, Slow) + Smooth)
        return;

    // Calculate standard EMAs
    fastEMA[0] = EMA(Input, Fast)[0];
    slowEMA[0] = EMA(Input, Slow)[0];
    
    // Apply Zero Lag calculation
    // Convert ZeroLagFactor (0-1) to an appropriate lookback period
    // Use Fast period as a scaling factor for the lookback
    int lookbackPeriod = (int)Math.Round(ZeroLagFactor * Fast);
    
    // Ensure we have at least one bar of lookback and don't exceed available bars
    lookbackPeriod = Math.Max(1, Math.Min(lookbackPeriod, CurrentBar));
    
    // Calculate Zero Lag EMAs using the formula: ZL-EMA = 2 * EMA - EMA(lookback periods ago)
    zlFastEMA[0] = 2 * fastEMA[0] - EMA(Input, Fast)[lookbackPeriod];
    zlSlowEMA[0] = 2 * slowEMA[0] - EMA(Input, Slow)[lookbackPeriod];
    
    // Calculate the Zero Lag MACD and store it in the series
    zeroLagMaCD[0] = zlFastEMA[0] - zlSlowEMA[0];
    
    // Calculate Signal line using EMA of the Zero Lag MACD
    double MaCD = zeroLagMaCD[0];
    double Signal = EMA(zeroLagMaCD, Smooth)[0];
    double Histog = MaCD - Signal;

    MaCDValue[0] = MaCD;
    Avg[0] = Signal;
    Hist[0] = Histog;

    double priorMaCD = MaCDValue[1];
    double priorSignal = Avg[1];
    double priorHistog = Hist[1];
    
    // Add crossover detection and plotting
    if (CurrentBar > 0)
    {
        // MACD crosses above Signal line (bullish)
        if (MaCDValue[0] > Avg[0] && MaCDValue[1] <= Avg[1])
            Draw.Dot(this, "CrossUp" + CurrentBar.ToString(), false, 0, MaCDValue[0], Brushes.DarkSeaGreen);
            
        // MACD crosses below Signal line (bearish)
        if (MaCDValue[0] < Avg[0] && MaCDValue[1] >= Avg[1])
            Draw.Dot(this, "CrossDown" + CurrentBar.ToString(), false, 0, MaCDValue[0], Brushes.IndianRed);
    }

    // Original coloring code follows...
    ///Remove remark to restore MarketAnalyzer function.
    //            if (MaCD >= priorMaCD)
    //                MarketAnalyzer[0] = -1;
    //            else
    //                MarketAnalyzer[0] = -2;


    //MACD and MACD Average (Signal) Up/Down Color
    if (ShowMACD)
    {
        if (MaCD >= priorMaCD)
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

    
    //Differential Histogram Up/Down Color
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


    //Chart Background Up/Down Color
    if (ShowBackColor)
    {
       // Up - Background Color	
    if (MaCD > Signal && MaCD > 0)
        {
        BackBrush = UpColor; 
        }
       // Down - Background Color	
    else if (MaCD < Signal && MaCD < 0)
        {
        BackBrush = DownColor; 
        }
    else
        {
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
        
        [NinjaScriptProperty]
		[Range(0, 1)]
		[Display(Name="Zero Lag Factor", Description="Factor to adjust the amount of lag reduction (0-1)", Order=4, GroupName="Parameters")]
		public double ZeroLagFactor
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

///Remove remark to restore PaintBar function.
//		[Display(Name="Price Bars Up/Down Color", Description="Color the price bars according to the MACD or the Histogram?", Order=4, GroupName="Options")]
//		public bool PaintBars
//		{ get; set; }

///Remove remark to restore PaintBar function.
//		[Display(Name="Paint Bars Type", Description="Paint Bars Input", Order=5, GroupName="Options")]
//		public MaCD_PaintBarType2 pbType
//		{ get; set; }	

		
		[XmlIgnore]
		[Display(Name="Histogram rising above zero", Description="MaCD / Hist > 0 (Rising)", Order=1, GroupName="Plot colors")]
		public Brush HistUpRising
		{ get; set; }

		[Browsable(false)]
		public string HistUpRisingSerializable
		{
			get { return Serialize.BrushToString(HistUpRising); }
			set { HistUpRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram falling above zero", Description="MaCD / Hist > 0 (Falling)", Order=2, GroupName="Plot colors")]
		public Brush HistUpFalling
		{ get; set; }

		[Browsable(false)]
		public string HistUpFallingSerializable
		{
			get { return Serialize.BrushToString(HistUpFalling); }
			set { HistUpFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram falling below zero", Description="MaCD / Hist < 0 (Falling)", Order=3, GroupName="Plot colors")]
		public Brush HistDownFalling
		{ get; set; }

		[Browsable(false)]
		public string HistDownFallingSerializable
		{
			get { return Serialize.BrushToString(HistDownFalling); }
			set { HistDownFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram rising below zero", Description="MaCD / Hist < 0 (Rising)", Order=4, GroupName="Plot colors")]
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
		public Series<double> MaCDValue
		{
			get { return Values[2]; }
		}
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> Avg
		{
			get { return Values[1]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> Hist
		{
			get { return Values[0]; }
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
		private Myindicators.ZLMACD[] cacheZLMACD;
		public Myindicators.ZLMACD ZLMACD(int fast, int slow, int smooth, double zeroLagFactor)
		{
			return ZLMACD(Input, fast, slow, smooth, zeroLagFactor);
		}

		public Myindicators.ZLMACD ZLMACD(ISeries<double> input, int fast, int slow, int smooth, double zeroLagFactor)
		{
			if (cacheZLMACD != null)
				for (int idx = 0; idx < cacheZLMACD.Length; idx++)
					if (cacheZLMACD[idx] != null && cacheZLMACD[idx].Fast == fast && cacheZLMACD[idx].Slow == slow && cacheZLMACD[idx].Smooth == smooth && cacheZLMACD[idx].ZeroLagFactor == zeroLagFactor && cacheZLMACD[idx].EqualsInput(input))
						return cacheZLMACD[idx];
			return CacheIndicator<Myindicators.ZLMACD>(new Myindicators.ZLMACD(){ Fast = fast, Slow = slow, Smooth = smooth, ZeroLagFactor = zeroLagFactor }, input, ref cacheZLMACD);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.ZLMACD ZLMACD(int fast, int slow, int smooth, double zeroLagFactor)
		{
			return indicator.ZLMACD(Input, fast, slow, smooth, zeroLagFactor);
		}

		public Indicators.Myindicators.ZLMACD ZLMACD(ISeries<double> input , int fast, int slow, int smooth, double zeroLagFactor)
		{
			return indicator.ZLMACD(input, fast, slow, smooth, zeroLagFactor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.ZLMACD ZLMACD(int fast, int slow, int smooth, double zeroLagFactor)
		{
			return indicator.ZLMACD(Input, fast, slow, smooth, zeroLagFactor);
		}

		public Indicators.Myindicators.ZLMACD ZLMACD(ISeries<double> input , int fast, int slow, int smooth, double zeroLagFactor)
		{
			return indicator.ZLMACD(input, fast, slow, smooth, zeroLagFactor);
		}
	}
}

#endregion
