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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// The ZigZagRegressions indicator shows trend lines filtering out changes below a defined level.
    /// </summary>
    public class ZigZagRegressions : Indicator
    {
        // Color de la línea zigzag
        private static SolidColorBrush lineColorIndicator = new SolidColorBrush(Colors.WhiteSmoke);

        // Definir el valor máximo y mínimo de precio
        private List<Zone> priceListsZones;
        private Zone currentZone;

        // Estas son series que almacenan los valores de los picos y valles detectados por el indicador ZigZag en las barras de alto y bajo, respectivamente. Sirven para registrar los puntos de cambio en la tendencia.
        private Series<double> zigZagHighZigZags;
        private Series<double> zigZagLowZigZags;

        // Almacenan los valores 
        private Series<double> zigZagHighSeries;
        private Series<double> zigZagLowSeries;

        // Estas variables almacenan los valores actuales de los picos y valles más recientes del ZigZag. Se utilizan para seguir el último alto y bajo significativo.
        private double currentZigZagHigh;
        private double currentZigZagLow;

        private double currentMaxHighPrice;
        private double currentClosingHighPrice;
        private double lastClosingHighPrice;
        private double lastMaxHighPrice;

        private double currentMinLowPrice;
        private double currentClosingLowPrice;
        private double lastClosingLowPrice;
        private double lastMinLowPrice;

        private double resistenceZoneBrekoutPrice;
        private double supportZoneBreakoutPrice;

        private int highBreakBar;
        private int lowBreakBar;



        // Precio de la última oscilación (swing) detectada.
        private double lastSwingPrice;

        // Índice de la última barra donde se detectó un cambio de swing (cambio significativo en la tendencia). 
        private int lastSwingIdx;

        // índice de inicio que podría usarse para marcar el punto de inicio del cálculo o para almacenar una barra inicial de interés.
        private int startIndex;

        // Indica la dirección de la tendencia: 1 para tendencia alcista, -1 para tendencia bajista, y 0 para inicialización o sin tendencia.
        private int trendDir;

        // Identifica si existe un rompimiento o consecución alcista y bajista
        private bool isHighZoneExtended = false;
        private bool isLowZoneExtended = false;
        private bool isBullishBreakoutConfirmed = false;
        private bool isBearishBreakout = false;
        private bool isBearishBreakoutConfirmed = false;
        private bool resistenceIsBroken = false;
        private bool supportIsBroken = false;
        private bool breakHighBarIsUpdatable = true;
        private bool breakLowBarIsUpdatable = true;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicador para dibujar líneas en regresiones de velas.";
                Name = "CustomIndicatorTest2";
                Calculate = Calculate.OnBarClose;
                DeviationType = DeviationType.Points;
                BarsRequiredToPlot = 5;
                DeviationValue = 0.5;
                DisplayInDataBox = false;
                DrawOnPricePanel = false;
                IsSuspendedWhileInactive = true;
                IsOverlay = true;
                PaintPriceMarkers = false;
                UseHighLow = false;

                AddPlot(lineColorIndicator, NinjaTrader.Custom.Resource.NinjaScriptIndicatorNameZigZag);

                DisplayInDataBox = false;
                PaintPriceMarkers = false;
            }
            else if (State == State.Configure)
            {
                currentZigZagHigh = 0;
                currentZigZagLow = 0;
                currentMaxHighPrice = 0;
                currentClosingHighPrice = 0;
                lastMaxHighPrice = 0;
                lastClosingHighPrice = 0;
                currentMinLowPrice = 0;
                currentClosingLowPrice = 0;
                lastMinLowPrice = 0;
                lastClosingLowPrice = 0;
                lastSwingIdx = -1;
                lastSwingPrice = 0.0;
                trendDir = 0; // 1 = trend up, -1 = trend down, init = 0
                startIndex = int.MinValue;
                //highlightBrush.Opacity = 0.3; // Ajusta la opacidad del margne del area
                //lowlightBrush.Opacity = 0.3;
            }
            else if (State == State.DataLoaded)
            {
                zigZagHighZigZags = new Series<double>(this, MaximumBarsLookBack.Infinite);
                zigZagLowZigZags = new Series<double>(this, MaximumBarsLookBack.Infinite);
                zigZagHighSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                zigZagLowSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                currentZone = null;
                priceListsZones = new List<Zone>();
                resistenceZoneBrekoutPrice = 0;
                supportZoneBreakoutPrice = 0;
                highBreakBar = 0;

            }
        }

        public void CalculateCurrentMinOrMaxPrice()
        {
            if (priceListsZones.Any())
            {
                if (currentZone.IsResistenceZone())
                {
                    currentMaxHighPrice = priceListsZones.Max(zone => {
                        if (zone.IsResistenceZone())
                        {
                            return zone.MaxOrMinPrice;
                        }
                        else
                        {
                            return currentMaxHighPrice;
                        }
                    });
                    Print("currentMaxHighPrice =" + currentMaxHighPrice);
                }
                else
                {
                    currentMinLowPrice = priceListsZones.Min(zone => {
                        if (!zone.IsResistenceZone())
                        {
                            return zone.MaxOrMinPrice;
                        }
                        else
                        {
                            return currentMinLowPrice;
                        }
                    });
                    Print("currentMinLowPrice = " + currentMinLowPrice);
                }   
            }
        }

        public bool PriceIsNotBetweenGeneratedPrice(
        double maxPrice, double closePrice, double minPrice, double secondClosePrice)
        {
            // Comprueba si maxPrice está entre minPrice y secondClosePrice
            bool isMaxPriceBetween = maxPrice >= minPrice && maxPrice >= secondClosePrice;

            // Comprueba si closePrice está entre minPrice y secondClosePrice
            bool isClosePriceBetween = closePrice >= minPrice && closePrice >= secondClosePrice;

            // Devuelve verdadero si ambos están entre minPrice y secondClosePrice, de lo contrario, devuelve falso
            return isMaxPriceBetween && isClosePriceBetween;
        }

        // Returns the number of bars ago a zig zag low occurred. Returns a value of -1 if a zig zag low is not found within the look back period.
        // NOTE: el método no se llama
        public int LowBar(int barsAgo, int instance, int lookBackPeriod)
        {
            // Print("inside on LowBar");
            if (instance < 1)
                throw new Exception(string.Format(NinjaTrader.Custom.Resource.ZigZagLowBarInstanceGreaterEqual, GetType().Name, instance));
            if (barsAgo < 0)
                throw new Exception(string.Format(NinjaTrader.Custom.Resource.ZigZigLowBarBarsAgoGreaterEqual, GetType().Name, barsAgo));
            if (barsAgo >= Count)
                throw new Exception(string.Format(NinjaTrader.Custom.Resource.ZigZagLowBarBarsAgoOutOfRange, GetType().Name, (Count - 1), barsAgo));

            Update();
            for (int idx = CurrentBar - barsAgo - 1; idx >= CurrentBar - barsAgo - 1 - lookBackPeriod; idx--)
            {
                if (idx < 0)
                    return -1;
                if (idx >= zigZagLowZigZags.Count)
                    continue;

                if (!zigZagLowZigZags.IsValidDataPointAt(idx))
                    continue;

                if (instance == 1) // 1-based, < to be save
                    return CurrentBar - idx;

                instance--;
            }

            return -1;
        }

        // Returns the number of bars ago a zig zag high occurred. Returns a value of -1 if a zig zag high is not found within the look back period.
        // NOTE: el método no se llama
        public int HighBar(int barsAgo, int instance, int lookBackPeriod)
        {
            // Print("inside on HighBar");
            if (instance < 1)
                throw new Exception(string.Format(NinjaTrader.Custom.Resource.ZigZagHighBarInstanceGreaterEqual, GetType().Name, instance));
            if (barsAgo < 0)
                throw new Exception(string.Format(NinjaTrader.Custom.Resource.ZigZigHighBarBarsAgoGreaterEqual, GetType().Name, barsAgo));
            if (barsAgo >= Count)
                throw new Exception(string.Format(NinjaTrader.Custom.Resource.ZigZagHighBarBarsAgoOutOfRange, GetType().Name, (Count - 1), barsAgo));

            Update();
            for (int idx = CurrentBar - barsAgo - 1; idx >= CurrentBar - barsAgo - 1 - lookBackPeriod; idx--)
            {
                if (idx < 0)
                    return -1;
                if (idx >= zigZagHighZigZags.Count)
                    continue;

                if (!zigZagHighZigZags.IsValidDataPointAt(idx))
                    continue;

                if (instance <= 1) // 1-based, < to be save
                    return CurrentBar - idx;

                instance--;
            }

            return -1;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 10) // Need at least 3 bars to calculate Low/High
            {
                zigZagHighSeries[0] = 0;
                zigZagLowSeries[0] = 0;
                //currentMinLowPrice = Input[0];
                return;
            }

            // Initialization
            if (lastSwingPrice == 0.0)
            {
                // Establecer el valor del precio actual
                lastSwingPrice = Input[0];
            }
                

            ISeries<double> highSeries = High;
            ISeries<double> lowSeries = Low;

            try
            {

                // Comprueba si la barra del medio (highSeries[1]) es un pico, es decir, su valor es mayor o igual que las barras adyacentes.
                bool isSwingHigh = highSeries[1].ApproxCompare(highSeries[0]) >= 0
                && highSeries[1].ApproxCompare(highSeries[2]) >= 0;

                bool lastFiveBarsIsSwingHigh = 
                highSeries[0].ApproxCompare(highSeries[4]) >= 0 &&
                highSeries[3].ApproxCompare(highSeries[4]) >= 0;

                // Comprueba si la barra del medio (lowSeries[1]) es un valle, es decir, su valor es menor o igual que las barras adyacentes.
                bool isSwingLow = lowSeries[1].ApproxCompare(lowSeries[0]) <= 0
                && lowSeries[1].ApproxCompare(lowSeries[2]) <= 0;

                bool lastFiveBarsIsSwingLow =
                lowSeries[0].ApproxCompare(highSeries[4]) <= 0 &&
                lowSeries[3].ApproxCompare(highSeries[4]) <= 0;

                // Verifica si el valor de alto actual está por encima del último precio de swing más una desviación definida (DeviationValue), calculada en puntos o en porcentaje.
                bool isOverHighDeviation = (DeviationType == DeviationType.Percent && IsPriceGreater(highSeries[1], lastSwingPrice * (1.0 + DeviationValue))) || (DeviationType == DeviationType.Points && IsPriceGreater(highSeries[1], lastSwingPrice + DeviationValue));

                // Verifica si el valor de bajo actual está por debajo del último precio de swing menos una desviación definida.
                bool isOverLowDeviation = (DeviationType == DeviationType.Percent && IsPriceGreater(lastSwingPrice * (1.0 - DeviationValue), lowSeries[1])) || (DeviationType == DeviationType.Points && IsPriceGreater(lastSwingPrice - DeviationValue, lowSeries[1]));

                double saveValue = 0.0;
                bool addHigh = false;
                bool addLow = false;
                bool updateHigh = false;
                bool updateLow = false;

                // Comprobar si la oscilacion tiene un valor de 0
                if (!isSwingHigh && !isSwingLow)
                {
                    zigZagHighSeries[0] = currentZigZagHigh;
                    zigZagLowSeries[0] = currentZigZagLow;
                    return;
                }
                // Establece valores para dibujar nuevo movimiento al alza
                if (trendDir <= 0 && isSwingHigh && isOverHighDeviation)
                {
                    saveValue = highSeries[1];
                    addHigh = true;
                    trendDir = 1;
                }
                // Establece valores para dibujar nuevo movimiento a la baja
                else if (trendDir >= 0 && isSwingLow && isOverLowDeviation)
                {
                    saveValue = lowSeries[1];
                    addLow = true;
                    trendDir = -1;
                }
                // Establece valores para actualizar dibujo del movimiento al alza 
                else if (trendDir == 1 && isSwingHigh && IsPriceGreater(highSeries[1], lastSwingPrice))
                {
                    saveValue = highSeries[1];
                    updateHigh = true;
                }
                // Establece valores para actualizar dibujo del movimiento a la baja 
                else if (trendDir == -1 && isSwingLow && IsPriceGreater(lastSwingPrice, lowSeries[1]))
                {
                    saveValue = lowSeries[1];
                    updateLow = true;
                }

                if (addHigh || addLow || updateHigh || updateLow)
                {
                    if (updateHigh && lastSwingIdx >= 0)
                    {
                        zigZagHighZigZags.Reset(CurrentBar - lastSwingIdx);
                        Value.Reset(CurrentBar - lastSwingIdx);
                    }
                    else if (updateLow && lastSwingIdx >= 0)
                    {
                        zigZagLowZigZags.Reset(CurrentBar - lastSwingIdx);
                        Value.Reset(CurrentBar - lastSwingIdx);
                    }

                    if (addHigh || updateHigh)
                    {
                        zigZagHighZigZags[1] = saveValue;
                        currentZigZagHigh = saveValue;
                        zigZagHighSeries[1] = currentZigZagHigh;
                        Value[1] = currentZigZagHigh;

                    }
                    else if (addLow || updateLow)
                    {
                        zigZagLowZigZags[1] = saveValue;
                        currentZigZagLow = saveValue;
                        zigZagLowSeries[1] = currentZigZagLow;
                        Value[1] = currentZigZagLow;
                    }

                    if (IsPriceGreaterThanCurrentMaxHighPrice(highSeries[1]))
                    {
                        //Print($"addHigh = {addHigh}");
                        //Print($"updateHigh = {updateHigh}");
                        //Print($"CurrentBar >= ChartBars.FromIndex = {CurrentBar >= ChartBars.FromIndex}");
                        //Print($"breakHighBarIsUpdatable = {breakHighBarIsUpdatable}");

                        if ((addHigh || updateHigh) && CurrentBar >= ChartBars.FromIndex)
                        {
                            if (breakHighBarIsUpdatable)
                            {
                                highBreakBar = CurrentBar;
                                resistenceZoneBrekoutPrice = highSeries[1];
                            }

                            resistenceIsBroken = true;
                            breakHighBarIsUpdatable = false;
                        }
                    }
                    else if (IsPriceLessThanCurrentMinLowPrice(lowSeries[1]))
                    {
                        Print($"addLow = {addLow}");
                        Print($"updateLow = {updateLow}");
                        Print($"CurrentBar >= ChartBars.FromIndex = {CurrentBar >= ChartBars.FromIndex}");
                        Print($"breakHighBarIsUpdatable = {breakHighBarIsUpdatable}");

                        if ((addLow || updateLow) && CurrentBar >= ChartBars.FromIndex)
                        { 
                            if (breakLowBarIsUpdatable)
                            {
                                lowBreakBar = CurrentBar;
                                supportZoneBreakoutPrice = lowSeries[1];
                            }
                            supportIsBroken = true;
                            breakLowBarIsUpdatable = false;
                        }
                    }

                    if (priceListsZones.Any())
                    {
                        foreach (var zone in priceListsZones)
                        {
                            Print($"Zone: ID = {zone.Id}, Type = {zone.Type}");
                        }
                    }

                    Print("maxPrice = " + currentMaxHighPrice);
                    Print("minPrice = " + currentMinLowPrice);
                    lastSwingIdx = CurrentBar - 1;
                    lastSwingPrice = saveValue;

                    Print($"priceListZones length = {priceListsZones.Count()}");
                }

                /*Print($"resistenceIsBroken = {resistenceIsBroken}");
                Print($"CurrentBar = {CurrentBar}");
                Print($"highBreakBar = {highBreakBar}");
                Print($"resistenceZoneBrekoutPrice = {resistenceZoneBrekoutPrice}");
                Print($"currentZigZagHigh = {currentZigZagHigh}");
                */
                if (CurrentBar >= highBreakBar + 5 && resistenceIsBroken)
                {
                    currentClosingHighPrice = Open[1] >= Close[1] ? Open[1] : Close[1];
                    // Print("CurrentBar == highBreakBar + 5 = true");
                    if (currentZigZagHigh < resistenceZoneBrekoutPrice)
                    {
                        //Print("extending resistence zone...");
                        isHighZoneExtended = true;
                    }
                    else if (currentZigZagHigh >= resistenceZoneBrekoutPrice)
                    {
                        //Print("breaking high zone");
                        isBullishBreakoutConfirmed = true;

                    }

                    resistenceIsBroken = false;
                    breakHighBarIsUpdatable = true;
                }

                Print($"supportIsBroken = {supportIsBroken}");
                Print($"CurrentBar = {CurrentBar}");
                Print($"lowBreakBar = {lowBreakBar}");
                Print($"supportZoneBreakoutPrice = {supportZoneBreakoutPrice}");
                Print($"currentZigZagLow = {currentZigZagLow}");
                if (CurrentBar >= lowBreakBar + 5 && supportIsBroken)
                {
                    currentClosingLowPrice = Close[1] <= Open[1] ? Close[1] : Open[1];
                    Print("CurrentBar >= lowBreakBar + 5 = true");
                    if (currentZigZagLow > supportZoneBreakoutPrice)
                    {
                        Print("extending support zone...");
                        isLowZoneExtended = true;
                    }
                    else if (currentZigZagLow <= supportZoneBreakoutPrice)
                    {
                        Print("breaking low zone...");
                        isBearishBreakoutConfirmed = true;
                    }

                    supportIsBroken = false;
                    breakLowBarIsUpdatable = true;
                }

                zigZagHighSeries[0] = currentZigZagHigh;
                zigZagLowSeries[0] = currentZigZagLow;

                // Establecer un valor minimo alcanzado en base a las últimas 20 barras aprocimadas cuando el valor minimo sea 0
                if (CurrentBar >= ChartBars.FromIndex && currentMaxHighPrice > 0 && currentMinLowPrice == 0)
                {
                    currentMinLowPrice = lowSeries[1];
                }

                //maxHighPriceList.Add(currentZigZagHigh);
                //minLowPriceList.Add(currentZigZagLow);

                if (startIndex == int.MinValue && (zigZagHighZigZags.IsValidDataPoint(1) && zigZagHighZigZags[1] != zigZagHighZigZags[2] || zigZagLowZigZags.IsValidDataPoint(1) && zigZagLowZigZags[1] != zigZagLowZigZags[2]))
                    startIndex = CurrentBar - (Calculate == Calculate.OnBarClose ? 2 : 1);

            }
            catch (Exception ex)
            {
                Print("Error accessing series: " + ex.Message); // --> Error accessing series: 'barsAgo' needed to be between 0 and 6657 but was 1
            }
        }

        #region Properties
        /// <summary>
        /// Gets the ZigZag high points.
        /// </summary>
        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "DeviationType", GroupName = "NinjaScriptParameters", Order = 0)]
        public DeviationType DeviationType
        {
            get; set;
        }

        [Range(0, int.MaxValue), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "DeviationValue", GroupName = "NinjaScriptParameters", Order = 1)]
        public double DeviationValue
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "UseHighLow", GroupName = "NinjaScriptParameters", Order = 2)]
        public bool UseHighLow
        {
            get; set;
        }

        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> ZigZagHigh
        {
            get
            {
                Update();
                return zigZagHighSeries;
            }
        }

        /// <summary>
        /// Gets the ZigZag low points.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> ZigZagLow
        {
            get
            {
                Update();
                return zigZagLowSeries;
            }
        }
        #endregion

        #region Miscellaneous
        private bool IsPriceGreater(double a, double b)
        {
            return a.ApproxCompare(b) > 0;
        }

        private bool IsPriceGreaterThanCurrentMaxHighPrice(double lastPrice)
        {

            bool lastPriceIsGreaterThanMaxHighPrice = lastPrice
            .ApproxCompare(currentMaxHighPrice) > 0;

            Print($"is price greater than current max high price = {lastPriceIsGreaterThanMaxHighPrice}");
            return lastPriceIsGreaterThanMaxHighPrice;
        }

        private bool IsPriceLessThanCurrentMinLowPrice(double lastPrice)
        {
            bool lastPriceIsLessThanMinLowPrice = 
            lastPrice.ApproxCompare(currentMinLowPrice) <= 0;

            //Print($"is price less than current min low price = {lastPriceIsLessThanMinLowPrice}");
            return lastPriceIsLessThanMinLowPrice;
        }

        public override void OnCalculateMinMax()
        {
            //Print("inside OnCalculateMinMax");
            MinValue = double.MaxValue;
            MaxValue = double.MinValue;

            if (BarsArray[0] == null || ChartBars == null || startIndex == int.MinValue)
                return;

            for (int seriesCount = 0; seriesCount < Values.Length; seriesCount++)
            {
                for (int idx = ChartBars.FromIndex - Displacement; idx <= ChartBars.ToIndex + Displacement; idx++)
                {
                    if (idx < 0 || idx > Bars.Count - 1 - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0))
                        continue;

                    if (zigZagHighZigZags.IsValidDataPointAt(idx))
                        MaxValue = Math.Max(MaxValue, zigZagHighZigZags.GetValueAt(idx));

                    if (zigZagLowZigZags.IsValidDataPointAt(idx))
                        MinValue = Math.Min(MinValue, zigZagLowZigZags.GetValueAt(idx));
                }
            }
        }
        // NOTE: el método no se llama
        protected override Point[] OnGetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            if (!IsSelected || Count == 0 || Plots[0].Brush.IsTransparent() || startIndex == int.MinValue)
                return new System.Windows.Point[0];

            List<System.Windows.Point> points = new List<System.Windows.Point>();

            int lastIndex = Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? ChartBars.ToIndex - 1 : ChartBars.ToIndex - 2;

            for (int i = Math.Max(0, ChartBars.FromIndex - Displacement); i <= Math.Max(lastIndex, Math.Min(Bars.Count - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 2 : 1), lastIndex - Displacement)); i++)
            {
                int x = (chartControl.BarSpacingType == BarSpacingType.TimeBased || chartControl.BarSpacingType == BarSpacingType.EquidistantMulti && i + Displacement >= ChartBars.Count
                    ? chartControl.GetXByTime(ChartBars.GetTimeByBarIdx(chartControl, i + Displacement))
                    : chartControl.GetXByBarIndex(ChartBars, i + Displacement));

                if (Value.IsValidDataPointAt(i))
                    points.Add(new System.Windows.Point(x, chartScale.GetYByValue(Value.GetValueAt(i))));
            }
            return points.ToArray();
        }

        protected override void OnRender(Gui.Chart.ChartControl chartControl, Gui.Chart.ChartScale chartScale)
        {
            if (Bars == null || chartControl == null || startIndex == int.MinValue || CurrentBar < 5)
                return;

            if (zigZagHighSeries.Count < 10 || ChartBars.FromIndex < 10 || ChartBars.ToIndex < 10)
                return; // Salir si no hay suficientes valores en la serie o si el valor es inválido
            

            IsValidDataPointAt(
                Bars.Count - 1 - 
                (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0)
            ); // Make sure indicator is calculated until last (existing) bar

            int preDiff = 1;
            for (int i = ChartBars.FromIndex - 1; i >= 0; i--)
            {
                if (i - Displacement < startIndex || i - Displacement > Bars.Count - 1 - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0))
                    break;

                bool isHigh = zigZagHighZigZags.IsValidDataPointAt(i - Displacement);
                bool isLow = zigZagLowZigZags.IsValidDataPointAt(i - Displacement);

                if (isHigh || isLow)
                    break;

                preDiff++;
            }
            preDiff -= (Displacement < 0 ? Displacement : 0 - Displacement);

            int postDiff = 0;
            for (int i = Bars.Count; i <= zigZagHighZigZags.Count; i++)
            {
                if (i - Displacement < startIndex || i - Displacement > Bars.Count - 1 - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0))
                    break;

                bool isHigh = zigZagHighZigZags.IsValidDataPointAt(i - Displacement);
                bool isLow = zigZagLowZigZags.IsValidDataPointAt(i - Displacement);

                if (isHigh || isLow)
                    break;

                postDiff++;
            }
            postDiff += (Displacement < 0 ? 0 - Displacement : Displacement);

            int lastIdx = -1;
            double lastValue = -1;
            SharpDX.Direct2D1.PathGeometry g = null;
            SharpDX.Direct2D1.GeometrySink sink = null;

            int previusDiffIndex = ChartBars.FromIndex - preDiff;
            int posteriorDiffIndex = Bars.Count + postDiff;

            //Print("previusDiffIndex = " + previusDiffIndex);
            //Print("posteriorDiffIndex = " + posteriorDiffIndex);

            for (int idx = previusDiffIndex; idx <= posteriorDiffIndex; idx++)
            {
                if (idx < startIndex || idx > Bars.Count - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 2 : 1) || idx < Math.Max(BarsRequiredToPlot - Displacement, Displacement))
                    continue;

                bool isHigh = zigZagHighZigZags.IsValidDataPointAt(idx);
                bool isLow = zigZagLowZigZags.IsValidDataPointAt(idx);

                if (!isHigh && !isLow)
                    continue;
                
                double candlestickBodyValue = isHigh ? zigZagHighZigZags.GetValueAt(idx) : zigZagLowZigZags.GetValueAt(idx);
                //Print("candlestickBodyValue =");
                //Print(candlestickBodyValue);

                // double value = isHigh ? High[idx - 1] : Low[idx - 1];
                // Print("y1 value = " + value);

                if (lastIdx >= startIndex)
                {
                   //Print("Displacement = ");
                   //Print(Displacement);

                    // Establecer cordenadas de la línea zig zag. 
                    float x1 = (chartControl.BarSpacingType == BarSpacingType.TimeBased || chartControl.BarSpacingType == BarSpacingType.EquidistantMulti && idx + Displacement >= ChartBars.Count
                        ? chartControl.GetXByTime(ChartBars.GetTimeByBarIdx(chartControl, idx + Displacement))
                        : chartControl.GetXByBarIndex(ChartBars, idx + Displacement));
                    float y1 = chartScale.GetYByValue(candlestickBodyValue);

                    //Validar si es una movimiento alcista y si el valor de la vela es mayor o igual al último precio                    
                    if (isHigh && isHighZoneExtended && priceListsZones.Any())
                    {
                        Print("Extensión de resistencia:");

                        if (priceListsZones.Any(zone => zone == null))
                        {
                            Print("Hay una zona nula en la lista.");
                        }
                        else
                        {
                            Print("No hay zonas nulas en la lista.");
                        }

                        //Print($"priceListsZones.count = {priceListsZones.Count()}");
                        currentZone = priceListsZones.LastOrDefault(
                            zone => zone.Type == Zone.ZoneType.Resistance
                        );

                        Print($"zigZagHighSeries[4]: {resistenceZoneBrekoutPrice}");

                        if (currentZone != null)
                        {
                            currentZone.MaxOrMinPrice = resistenceZoneBrekoutPrice;

                            //Print("extendiendo región de la resistencia...");
                            //Print($"resistencia: Y = {currentZone.ClosePrice} maxPrice = {currentZone.MaxOrMinPrice}");

                         /*Print(
                               $"MaxOrMinPrice = {currentZone.MaxOrMinPrice}, ClosePrice = {currentZone.ClosePrice}, " +
                               $"lastMaxHighPrice = {lastMaxHighPrice}, lastClosingHighPrice = {lastClosingHighPrice}"
                               );
                            */

                            bool priceIsNotbetween = PriceIsNotBetweenGeneratedPrice(
                               currentZone.MaxOrMinPrice, currentZone.ClosePrice,
                               lastMaxHighPrice, lastClosingHighPrice
                            );

                            //Print($"priceIsNotsbetween = {priceIsNotsbetween}");

                            if (priceIsNotbetween)
                            {
                                //Print("HighlightBrush is "+ currentZone.HighlightBrush);
                                //Print("MaxOrMinPrice is "+ currentZone.MaxOrMinPrice);
                                
                                // Dibujar zonas de soporte y resistencia
                                Draw.RegionHighlightY(
                                    this,                         // Contexto del indicador o estrategia
                                    "RegionHighLightY" + currentZone.Id, // Nombre único para la región
                                    currentZone.ClosePrice,        // Nivel de precio inferior
                                    currentZone.MaxOrMinPrice,     // Nivel de precio superior
                                    currentZone.HighlightBrush     // Pincel para el color de la región
                                );

                                //: Actualizar zona actual 
                                List<Zone> updateMaxOrMinPriceZone =
                                priceListsZones.Select(zone =>
                                {
                                    if (zone.Id == currentZone.Id)
                                    {
                                        zone.MaxOrMinPrice = currentZone.MaxOrMinPrice;
                                    }
                                    return zone;
                                }).ToList();

                                priceListsZones = updateMaxOrMinPriceZone;
                            }
                        }
                        else
                        {
                            Print($"isBullishBreakoutConfirmed = {isBullishBreakoutConfirmed}");
                            Print("Current high zone is null");
                        }

                        isHighZoneExtended = false;
                    }
                    else if (isHigh && isBullishBreakoutConfirmed)
                    {
                        //Print("Rompimiento de resistencia y generación de soporte");

                        currentZone = new Zone(
                            Zone.ZoneType.Resistance, currentClosingHighPrice, currentZigZagHigh
                        );

                        priceListsZones.Add(currentZone);

                        CalculateCurrentMinOrMaxPrice();

                        //Print($"resistencia: Y = {currentZone.ClosePrice} maxPrice = {currentZone.MaxOrMinPrice}");
                        //Print($"lastClosingHighPrice = {lastClosingHighPrice} lastMaxHighPrice = {lastMaxHighPrice}");
                        

                        foreach (Zone zone in priceListsZones)
                        {
                            bool priceIsNotsbetween = PriceIsNotBetweenGeneratedPrice(
                                zone.MaxOrMinPrice, zone.ClosePrice,
                                lastMaxHighPrice, lastClosingHighPrice
                            );

                            if (
                                !zone.IsBreakoutZoneConfirmed(currentZigZagHigh) &&
                                priceIsNotsbetween
                            )
                            {
                                if (zone.IsResistenceZone())
                                {
                                    //Print($"dibujando resistencia: {zone.Id}");

                                    // Dibujar zonas de soporte 
                                    Draw.RegionHighlightY(
                                        this,                         // Contexto del indicador o estrategia
                                        "RegionHighLightY" + zone.Id, // Nombre único para la región
                                        zone.ClosePrice,              // Nivel de precio inferior
                                        zone.MaxOrMinPrice,           // Nivel de precio superior
                                        zone.HighlightBrush           // Pincel para el color de la región
                                    );
                                }
                                else
                                {
                                    //Print($"resistencia rota: {zone.Id}");
                                    RemoveDrawObject("RegionHighLightY" + zone.Id);
                                    // Dibujar zonas de soporte 
                                    Draw.RegionHighlightY(
                                        this,                         // Contexto del indicador o estrategia
                                        "RegionLowLightY" + zone.Id,  // Nombre único para la región
                                        zone.ClosePrice,              // Nivel de precio inferior
                                        zone.MaxOrMinPrice,           // Nivel de precio superior
                                        zone.HighlightBrush           // Pincel para el color de la región
                                    );
                                }
                            }
                        }
                        lastMaxHighPrice = currentMaxHighPrice;
                        lastClosingHighPrice = currentClosingHighPrice;

                        isBullishBreakoutConfirmed = false;
                    }

                    else if (
                        isHigh && 
                        priceListsZones.Any()
                    )
                    {
                        priceListsZones.RemoveAll(zone =>
                        {
                            if (zone.IsResistenceBreakout)
                            {
                                RemoveDrawObject("RegionHighLightY" + zone.Id);
                                Print("eliminando resistencia");
                                return true; // Eliminar zona
                            }
                            return false; // No eliminar zona
                        });
                    }
                    
                    
                    if (isLow && isLowZoneExtended && priceListsZones.Any())
                    {
                        if (priceListsZones.Any(zone => zone == null))
                        {
                            Print("Hay una zona nula en la lista.");
                        }
                        else
                        {
                            Print("No hay zonas nulas en la lista.");
                        }

                        //Print($"priceListsZones.count = {priceListsZones.Count()}");
                        currentZone = priceListsZones.LastOrDefault(
                            zone => zone.Type == Zone.ZoneType.Support
                        );

                        Print($"zigZagHighSeries[4]: {supportZoneBreakoutPrice}");

                        if (currentZone != null)
                        {
                            currentZone.MaxOrMinPrice = supportZoneBreakoutPrice;

                            Print("extendiendo región del soporte...");
                            Print($"soporte: Y = {currentZone.ClosePrice} minPrice = {currentZone.MaxOrMinPrice}");

                            Print(
                           $"MaxOrMinPrice = {currentZone.MaxOrMinPrice}, ClosePrice = {currentZone.ClosePrice}, " +
                           $"lastMinLowPrice = {lastMinLowPrice}, lastClosingLowPrice = {lastClosingLowPrice}"
                           );

                            bool priceIsNotbetween = PriceIsNotBetweenGeneratedPrice(
                               lastMinLowPrice, lastClosingLowPrice, 
                               currentZone.MaxOrMinPrice, currentZone.ClosePrice
                            );

                            Print($"priceIsNotbetween = {priceIsNotbetween}");

                            if (priceIsNotbetween)
                            {
                                Print("lowLightBrush is " + currentZone.HighlightBrush);
                                Print("MaxOrMinPrice is " + currentZone.MaxOrMinPrice);

                                // Dibujar zonas de soporte y resistencia
                                Draw.RegionHighlightY(
                                    this, // Contexto del indicador o estrategia
                                    "RegionLowLightY" + currentZone.Id, // Nombre único para la región
                                    currentZone.MaxOrMinPrice,     // Nivel de precio inferior
                                    currentZone.ClosePrice,        // Nivel de precio superior
                                    currentZone.HighlightBrush     // Pincel para el color de la región
                                );

                                //: Actualizar zona actual 
                                List<Zone> updateMaxOrMinPriceZone =
                                priceListsZones.Select(zone =>
                                {
                                    if (zone.Id == currentZone.Id)
                                    {
                                        zone.MaxOrMinPrice = currentZone.MaxOrMinPrice;
                                    }
                                    return zone;
                                }).ToList();

                                priceListsZones = updateMaxOrMinPriceZone;
                            }
                        }
                        else
                        {
                            Print($"isBearishBreakoutConfirmed = {isBearishBreakoutConfirmed}");
                            Print("Current low zone is null");
                        }

                        isLowZoneExtended = false;
                    }
                    else if (isLow && isBearishBreakoutConfirmed)
                    {
                        Print("Rompimiento de soporte y generación de resistencia");

                        currentZone = new Zone(
                            Zone.ZoneType.Support, currentClosingLowPrice, currentZigZagLow
                        );

                        priceListsZones.Add(currentZone);

                        CalculateCurrentMinOrMaxPrice();

                        Print($"soporte: Y = {currentZone.ClosePrice} minPrice = {currentZone.MaxOrMinPrice}");
                        Print($"currentClosingHighPrice = {currentClosingLowPrice} minPrice = {currentMinLowPrice}");
                        

                        foreach (Zone zone in priceListsZones)
                        {
                            bool priceIsNotsbetween = PriceIsNotBetweenGeneratedPrice(
                                lastMinLowPrice, lastClosingLowPrice,
                                zone.MaxOrMinPrice, zone.ClosePrice
                            );

                            if (
                                !zone.IsBreakoutZoneConfirmed(currentZigZagHigh) &&
                                priceIsNotsbetween
                            )
                            {
                                if (!zone.IsResistenceZone())
                                {
                                    Print($"dibujando soporte: {zone.Id}");
                                    // Dibujar zonas de soporte 
                                    Draw.RegionHighlightY(
                                        this,                         // Contexto del indicador o estrategia
                                        "RegionLowLightY" + zone.Id,  // Nombre único para la región
                                        zone.MaxOrMinPrice,           // Nivel de precio inferior
                                        zone.ClosePrice,              // Nivel de precio superior
                                        zone.HighlightBrush           // Pincel para el color de la región
                                    );
                                }
                                else
                                {
                                    Print($"soporte roto: {zone.Id}");
                                    RemoveDrawObject("RegionLowLightY" + zone.Id);
                                    // Dibujar zonas de soporte 
                                    Draw.RegionHighlightY(
                                        this,                         // Contexto del indicador o estrategia
                                        "RegionHighLightY" + zone.Id, // Nombre único para la región
                                        zone.MaxOrMinPrice,           // Nivel de precio inferior
                                        zone.ClosePrice,              // Nivel de precio superior
                                        zone.HighlightBrush           // Pincel para el color de la región
                                    );
                                }
                            }
                        }
                        lastMinLowPrice = currentMinLowPrice;
                        lastClosingLowPrice = currentClosingLowPrice;

                        isBearishBreakoutConfirmed = false;
                    }

                    if (
                        isLow && 
                        priceListsZones.Any()
                    )
                    {
                        priceListsZones.RemoveAll(zone =>
                        {
                            if (zone.IsSupportBreakout)
                            {
                                RemoveDrawObject("RegionLowLightY" + zone.Id);
                                Print("eliminando soporte");
                                return true; // Eliminar zona
                            }
                            return false; // No eliminar zona
                        });
                    }

                    if (sink == null)
                    {
                        float x0 = (chartControl.BarSpacingType == BarSpacingType.TimeBased || chartControl.BarSpacingType == BarSpacingType.EquidistantMulti && lastIdx + Displacement >= ChartBars.Count
                        ? chartControl.GetXByTime(ChartBars.GetTimeByBarIdx(chartControl, lastIdx + Displacement))
                        : chartControl.GetXByBarIndex(ChartBars, lastIdx + Displacement));
                        float y0 = chartScale.GetYByValue(lastValue);
                        g = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
                        sink = g.Open();
                        sink.BeginFigure(new SharpDX.Vector2(x0, y0), SharpDX.Direct2D1.FigureBegin.Hollow);
                    }
                    sink.AddLine(new SharpDX.Vector2(x1, y1));

                    // Save as previous point
                }
                lastIdx = idx;
                lastValue = candlestickBodyValue;
            }

            if (sink != null)
            {
                sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Open);
                sink.Close();
            }

            if (g != null)
            {
                var oldAntiAliasMode = RenderTarget.AntialiasMode;
                RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
                RenderTarget.DrawGeometry(g, Plots[0].BrushDX, 3, Plots[0].StrokeStyle);
                RenderTarget.AntialiasMode = oldAntiAliasMode;
                g.Dispose();
                RemoveDrawObject("NinjaScriptInfo");
            }

            else
                Draw.TextFixed(this, "NinjaScriptInfo", NinjaTrader.Custom.Resource.ZigZagDeviationValueError, TextPosition.BottomRight);
        }
        #endregion
    }

    public class Zone
    {
        // Enum para los tipos de Zona: Soporte o Resistencia
        public enum ZoneType
        {
            Support,
            Resistance
        }

        // Propiedades
        public ZoneType Type
        {
            get; private set;
        }

        public double ClosePrice
        {
            get; set;
        }    // Precio de cierre
        public double MaxOrMinPrice
        {
            get; set;
        } // Precio máximo para Resistencia o mínimo para Soporte
        public Brush HighlightBrush
        {
            get; private set;
        } // Color de la zona
        public bool IsSupportBreakout
        {
            get; set;
        }       // Indica si el soporte ha sido roto
        public bool IsResistenceBreakout
        {
            get; set;
        }    // Indica si la resistencia ha sido rota

        public long Id
        {
            get; private set;
        } 

        // Constructor para inicializar la Zona
        public Zone(ZoneType type, double closePrice, double maxOrMinPrice)
        {
            Type = type;
            ClosePrice = closePrice;
            MaxOrMinPrice = maxOrMinPrice;

            // Configurar el color según el tipo de Zona
            if (Type == ZoneType.Support)
            {
                HighlightBrush = Brushes.Red.Clone();
                HighlightBrush.Opacity = 0.3;
            }
            else if (Type == ZoneType.Resistance)
            {
                HighlightBrush = Brushes.Green.Clone();
                HighlightBrush.Opacity = 0.3;
            }

            // Inicializamos los estados de breakout
            IsSupportBreakout = false;
            IsResistenceBreakout = false;
            Id = DateTime.Now.Ticks;
        }

        // Método para dibujar la Zona utilizando RegionHighlightY
        public void DrawZone(NinjaTrader.NinjaScript.StrategyBase strategy, string tag)
        {
            // Dibujar la zona usando RegionHighlightY, definiendo el precio máximo o mínimo
            
        }

        public bool IsBreakoutZone(double currentPrice)
        {
            if (Type == ZoneType.Resistance)
            {
                if (currentPrice > this.MaxOrMinPrice)
                {
                    return true;
                }
            }

            else if (Type == ZoneType.Support)
            {
                if (currentPrice < this.MaxOrMinPrice)
                {
                    return true;
                }
            }

            return false;
        }

        // Método para verificar si la zona ha sido rota
        public bool IsBreakoutZoneConfirmed(double currentPrice)
        {
            // Comprobación de ruptura de resistencia
            if (Type == ZoneType.Resistance)
            {
                // Si el precio actual es mayor que el precio máximo, se considera rota como resistencia
                /* for (int currentHighSerie = 5; currentHighSerie <= 0; currentHighSerie--)
                {*/
                            if (currentPrice > MaxOrMinPrice)
                    {
                        // Cambiamos el tipo a soporte después de romper la resistencia
                        ChangeToSupport();
                    }
                /*}
                  */
            }
            // Comprobación de ruptura de soporte
            else if (Type == ZoneType.Support)
            {
                // Si el precio actual es menor que el precio mínimo, se considera rota como soporte
                if (currentPrice < MaxOrMinPrice)
                {
                    // Cambiamos el tipo a resistencia después de romper el soporte
                    ChangeToResistance();
                }
            }

            // Retorna true solo si ambos tipos de ruptura han ocurrido
            return IsSupportBreakout && IsResistenceBreakout;
        }

        // Método para convertir una resistencia en soporte
        private void ChangeToSupport()
        {
            IsResistenceBreakout = true;
            Type = ZoneType.Support;
            HighlightBrush = Brushes.Red.Clone();
            HighlightBrush.Opacity = 0.3;
            //IsSupportBreakout = false; // Reiniciamos el estado de ruptura de soporte
        }

        // Método para convertir un soporte en resistencia
        private void ChangeToResistance()
        {
            IsSupportBreakout = true;
            Type = ZoneType.Resistance;
            HighlightBrush = Brushes.Green.Clone();
            HighlightBrush.Opacity = 0.3;
            //IsResistenceBreakout = false; // Reiniciamos el estado de ruptura de resistencia
        }

        public bool IsResistenceZone()
        {
            if (this.Type.Equals(ZoneType.Resistance))
            {
                return true;
            }
            return false;
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ZigZagRegressions[] cacheZigZagRegressions;
		public ZigZagRegressions ZigZagRegressions(DeviationType deviationType, double deviationValue, bool useHighLow)
		{
			return ZigZagRegressions(Input, deviationType, deviationValue, useHighLow);
		}

		public ZigZagRegressions ZigZagRegressions(ISeries<double> input, DeviationType deviationType, double deviationValue, bool useHighLow)
		{
			if (cacheZigZagRegressions != null)
				for (int idx = 0; idx < cacheZigZagRegressions.Length; idx++)
					if (cacheZigZagRegressions[idx] != null && cacheZigZagRegressions[idx].DeviationType == deviationType && cacheZigZagRegressions[idx].DeviationValue == deviationValue && cacheZigZagRegressions[idx].UseHighLow == useHighLow && cacheZigZagRegressions[idx].EqualsInput(input))
						return cacheZigZagRegressions[idx];
			return CacheIndicator<ZigZagRegressions>(new ZigZagRegressions(){ DeviationType = deviationType, DeviationValue = deviationValue, UseHighLow = useHighLow }, input, ref cacheZigZagRegressions);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ZigZagRegressions ZigZagRegressions(DeviationType deviationType, double deviationValue, bool useHighLow)
		{
			return indicator.ZigZagRegressions(Input, deviationType, deviationValue, useHighLow);
		}

		public Indicators.ZigZagRegressions ZigZagRegressions(ISeries<double> input , DeviationType deviationType, double deviationValue, bool useHighLow)
		{
			return indicator.ZigZagRegressions(input, deviationType, deviationValue, useHighLow);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ZigZagRegressions ZigZagRegressions(DeviationType deviationType, double deviationValue, bool useHighLow)
		{
			return indicator.ZigZagRegressions(Input, deviationType, deviationValue, useHighLow);
		}

		public Indicators.ZigZagRegressions ZigZagRegressions(ISeries<double> input , DeviationType deviationType, double deviationValue, bool useHighLow)
		{
			return indicator.ZigZagRegressions(input, deviationType, deviationValue, useHighLow);
		}
	}
}

#endregion
