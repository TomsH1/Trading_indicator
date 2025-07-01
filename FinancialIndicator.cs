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
using System.Security.Policy;
using System.Windows.Media.Animation;
using System.Windows.Media.Converters;
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

        private static int startVerticalLineIndex = -1;

        // Zonas alcistas y bajistas generadas en el gráfico
        private List<Zone> priceListsZones;
        //Ultima zona dibujada o editada en el gráfico
        private Zone currentZone;

        // Lista de velas pendientes a ser validadas para confirmación de rompimiento
        private List<BreakoutCandidate> pendingListOfBreakouts = new();
        // Numero de barras necesarias para confirmar un rompimiento
        private readonly int confirmationBars = 5;

        // Estas son series que almacenan los valores de los picos y valles detectados por el indicador ZigZag en las barras de alto y bajo, respectivamente. Sirven para registrar los puntos de cambio en la tendencia.
        private Series<double> zigZagHighZigZags;
        private Series<double> zigZagLowZigZags;

        // Almacenan toda la serie de zig zags del gráfico
        private Series<double> zigZagHighSeries;
        private Series<double> zigZagLowSeries;

        // Estas variables almacenan los valores actuales de los picos y valles más recientes del ZigZag. Se utilizan para seguir el último alto y bajo significativo.
        private double currentZigZagHigh;
        private double currentZigZagLow;

        // Ultimo valor máximo generado durante la sesión
        private double currentMaxHighPrice = double.MaxValue;
        // Valor de cierre de la última vela que rompe al alza 
        private double currentClosingHighPrice;
        // Valor de cierre anterior de la última vela que rompe al alza
        private double lastClosingHighPrice;
        // Antepenultimo valor máximo generado duranta la sesión
        private double lastMaxHighPrice;

        // Ultimo valor mínimo alcanzado durante la sesión
        private double currentMinLowPrice = double.MinValue;
        // Valor de cierre de la última vela que rompe a la baja 
        private double currentClosingLowPrice;
        // Valor de cierre anterior de la última vela que rompe a la baja
        private double lastClosingLowPrice;
        // Antepenultimo valor mínimo alcanzado duranta la sesión
        private double lastMinLowPrice;

        // Ultimo valor máximo alcanzado durante la sesión
        private double resistenceZoneBreakoutPrice;
        // Ultimo valor mínimo alcanzado durante la sesión
        private double supportZoneBreakoutPrice;

        // guarda la barrra en la que se alcanza un rompimiento al alza, se resetea cuando un nuevo tope al alza es alcanzado
        private int maxHighBreakBar;
        // Cantidad de veces consecutivas que se realiza un rompimiento al alza antes de generarse una resistencia
        private int maxHighBrokenAccumulated;
        // Cantidad de veces consecutivas que se realiza un rompimiento al alza antes de extender una resistencia intermedia
        private int highBrokenAccumulated;

        // guarda la barrra en la que se alcanza un rompimiento a la baja, se resetea cuando un nuevo tope a la baja es alcanzado
        private int minLowBreakBar;
        // Cantidad de veces consecutivas que se realiza un rompimiento a la baja antes de extender un soporte intermedio
        private int lowBrokenAccumulated;
        // Cantidad de veces consecutivas que se realiza un rompimiento a la baja antes de generarse una soporte
        private int minLowBrokenAccumulated;

        // Precio de la última oscilación (swing) detectada.
        private double lastSwingPrice;

        // Índice de la última barra donde se detectó un cambio de swing (cambio significativo en la tendencia). 
        private int lastSwingIdx;

        // índice de inicio que podría usarse para marcar el punto de inicio del cálculo o para almacenar una barra inicial de interés.
        private int startIndex;

        // Indica la dirección de la tendencia: 1 para tendencia alcista, -1 para tendencia bajista, y 0 para inicialización o sin tendencia.
        private int trendDir;

        // Identifica si existe un rompimiento o consecución alcista y bajista
        private bool isHighBreakoutPendingToConfirmation = false;
        private bool isLowBreakoutPendingToConfirmation = false;
        private bool isHighZoneExtended = false;
        private bool isLowZoneExtended = false;
        private bool redrawHighZoneIsRequired = false;
        private bool redrawLowZoneIsRequired = false;
        private bool isBullishBreakoutConfirmed = false;
        private bool isBearishBreakoutConfirmed = false;
        private bool isTheFirstSwingLow = false;
        private bool isTheFirstSwingHigh = false;
        private bool isTheFirstBarToAnalize = true;
        private bool isSwingHigh;
        private bool isSwingLow;
        private bool isConfirmationOfZoneBrokenUpwards;
        private bool isConfirmationOfZoneBrokenDownSide;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicador para dibujar líneas en regresiones de velas.";
                Name = "CustomIndicatorTest2";
                Calculate = Calculate.OnBarClose;
                DeviationType = DeviationType.Points;
                BarsRequiredToPlot = 5;
                isTheFirstBarToAnalize = true;  // Inicializamos para saber cuándo se ha cargado el gráfico
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

                currentClosingHighPrice = 0;
                lastMaxHighPrice = 0;
                lastClosingHighPrice = 0;
                maxHighBrokenAccumulated = 0;
                highBrokenAccumulated = 0;

                currentClosingLowPrice = double.MaxValue;
                lastMinLowPrice = double.MaxValue;
                lastClosingLowPrice = double.MaxValue;
                minLowBrokenAccumulated = 0;
                lowBrokenAccumulated = 0;

                lastSwingIdx = -1;
                lastSwingPrice = 0.0;
                trendDir = 0; // 1 = trend up, -1 = trend down, init = 0
                isConfirmationOfZoneBrokenUpwards = false;
                isConfirmationOfZoneBrokenDownSide = false;
                isSwingHigh = false;
                isSwingLow = false;
                startIndex = int.MinValue;
            }
            else if (State == State.DataLoaded)
            {
                zigZagHighZigZags = new Series<double>(this, MaximumBarsLookBack.Infinite);
                zigZagLowZigZags = new Series<double>(this, MaximumBarsLookBack.Infinite);
                zigZagHighSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                zigZagLowSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                priceListsZones = new List<Zone>();
                pendingListOfBreakouts = new List<BreakoutCandidate>();
                resistenceZoneBreakoutPrice = 0;
                supportZoneBreakoutPrice = double.MaxValue;
                maxHighBreakBar = 0;
                currentZone = null;
                // Aquí capturamos el índice de la última barra cargada
                // Marcamos que el script ha sido reiniciado

            }
            else if (State == State.Realtime)
            {
                Print($"State == State.Realtime : {true}");
                // Aquí estamos entrando en tiempo real, después de cargar las barras históricas
                startVerticalLineIndex = CurrentBar;
                DrawStartVerticalLine();
                currentMaxHighPrice = double.MinValue;
                currentMinLowPrice = double.MaxValue;
                isTheFirstSwingHigh = true;
                isTheFirstSwingLow = true;
                Print($"currentMinLowPrice in realtime state = {currentMinLowPrice}");
            }
        }

        protected void DrawStartVerticalLine()
        {
            Draw.VerticalLine(this, "StartVerticalLine",
            0,
            Brushes.Yellow, DashStyleHelper.Dash, 2);
        }

        public void CalculateCurrentMinOrMaxPrice()
        {
            if (priceListsZones.Any())
            {
                if (currentZone.IsResistenceZone())
                {
                    CalculateCurrentMaxPrice();
                }
                else
                {
                    CalculateCurrentMinPrice();
                }
            }
        }

        public void CalculateCurrentMaxPrice()
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
            //Print("currentMaxHighPrice =" + currentMaxHighPrice);
        }

        public void CalculateCurrentMinPrice()
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

            //Print("currentMinLowPrice = " + currentMinLowPrice);
        }

        public double CalculateMaximumBreakoutValue(ISeries<double> Series)
        {
            double maxValue = double.MinValue;

            for (int i = 0; i < 6; i++)
            {
                if (Series[i] > maxValue)
                    maxValue = Series[i];
            }

            return maxValue;
        }

        public double CalculateMinimumBreakoutValue(ISeries<double> Series)
        {
            double maxValue = double.MaxValue;

            for (int i = 0; i < 6; i++)
            {
                if (Series[i] < maxValue)
                    maxValue = Series[i];
            }

            return maxValue;
        }

        // Validar el precio de apertura y cierre no está entre otros precios de apertura y cierre
        public bool PriceIsNotBetweenGeneratedPrice(
        double maxPrice, double closePrice, double minPrice, double secondClosePrice)
        {
            // Comprueba si maxPrice está entre minPrice y secondClosePrice
            bool isMaxPriceBetween = maxPrice > minPrice && maxPrice > secondClosePrice;

            // Comprueba si closePrice está entre minPrice y secondClosePrice
            bool isClosePriceBetween = closePrice > minPrice && closePrice > secondClosePrice;

            // Devuelve verdadero si ambos están entre minPrice y secondClosePrice, de lo contrario, devuelve falso
            return isMaxPriceBetween && isClosePriceBetween;
        }

        // Valida si un valor es mayor que el precio máximo de una zona
        private bool IsZoneWithinBarHighRange(Zone zone, double barHighPrice)
        {
            // Validar que tanto MaxOrMinPrice como ClosePrice de la zona estén dentro del rango de la barra
            bool isMaxOrMinWithinRange = barHighPrice > zone.MaxOrMinPrice;
            Print($"Barra intermedia es mayor a la zona = {isMaxOrMinWithinRange}");

            // Si ambas condiciones se cumplen, la zona está dentro del rango de la barra
            return isMaxOrMinWithinRange;
        }

        // Valida si un valor es mayor que el precio mínimo de una zona
        private bool IsZoneWithinBarLowRange(Zone zone, double barLow)
        {
            // Validar que tanto MaxOrMinPrice como ClosePrice de la zona estén dentro del rango de la barra
            bool isMaxOrMinWithinRange = barLow < zone.MaxOrMinPrice;
            Print($"Barra intermedia es menor a la zona = {isMaxOrMinWithinRange}");

            // Si ambas condiciones se cumplen, la zona está dentro del rango de la barra
            return isMaxOrMinWithinRange;
        }

        private bool BullishHighBreakoutHasConfirmation(
           ISeries<double> highSeries, int maxHighBreakBar, bool isIntermediateZone = false
        )
        {
            Print($"maxHighBreakBar = {maxHighBreakBar}");
            Print($"Bars.Count = {Bars.Count}");
            Print($"CurrentBar = {CurrentBar}");
            bool bullishHighBreakoutHasConfirmation = false;
            if (maxHighBreakBar > 0)
            {
                int breakoutBar = (CurrentBar - maxHighBreakBar) + 1;
                Print($"breakoutBar = {breakoutBar}");

                Print($"{CurrentBar - 5} - {highSeries[6]}$ > {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[6].ApproxCompare(highSeries[breakoutBar]) > 0}");

                Print($"{CurrentBar - 4} - {highSeries[5]}$ > {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[5].ApproxCompare(highSeries[breakoutBar]) > 0}");

                Print($"{CurrentBar - 3} - {highSeries[4]}$ > {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[4].ApproxCompare(highSeries[breakoutBar]) > 0}");

                Print($"{CurrentBar - 2} - {highSeries[3]}$ > {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[3].ApproxCompare(highSeries[breakoutBar]) > 0}");

                Print($"{CurrentBar - 1} - {highSeries[2]}$ > {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[2].ApproxCompare(highSeries[breakoutBar]) > 0}");

                // TODO ajustar para que compare exactamente 5 barras excluyendo la actual
                bullishHighBreakoutHasConfirmation =
                    highSeries[6].ApproxCompare(highSeries[breakoutBar]) > 0 ||
                    highSeries[5].ApproxCompare(highSeries[breakoutBar]) > 0 ||
                    highSeries[4].ApproxCompare(highSeries[breakoutBar]) > 0 ||
                    highSeries[3].ApproxCompare(highSeries[breakoutBar]) > 0 ||
                    highSeries[2].ApproxCompare(highSeries[breakoutBar]) > 0;
            }

            return bullishHighBreakoutHasConfirmation;
        }

        private bool BearishLowBreakoutHasConfirmation(
            ISeries<double> lowSeries, int minLowBreakBar
        )
        {
            Print($"minLowBreakBar = {minLowBreakBar}");
            Print($"Bars.Count = {Bars.Count}");
            Print($"CurrentBar = {CurrentBar}");
            bool bearishLowBreakoutHasConfirmation = false;
            if (minLowBreakBar > 0)
            {
                int breakoutBar = (CurrentBar - minLowBreakBar) + 1;
                Print($"breakoutBar = {breakoutBar}");

                Print($"{CurrentBar - 5} - {lowSeries[6]}$ < {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[6].ApproxCompare(lowSeries[breakoutBar]) < 0}");

                Print($"{CurrentBar - 4} - {lowSeries[5]}$ < {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[5].ApproxCompare(lowSeries[breakoutBar]) < 0}");

                Print($"{CurrentBar - 3} - {lowSeries[4]}$ < {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[4].ApproxCompare(lowSeries[breakoutBar]) < 0}");

                Print($"{CurrentBar - 2} - {lowSeries[3]}$ < {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[3].ApproxCompare(lowSeries[breakoutBar]) < 0}");

                Print($"{CurrentBar - 1} - {lowSeries[2]}$ < {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[2].ApproxCompare(lowSeries[breakoutBar]) < 0}");

                // TODO ajustar para que se calcule de manera exactas con las 5 barras siguientes que realizan el rompimiento
                bearishLowBreakoutHasConfirmation =
                lowSeries[6].ApproxCompare(lowSeries[breakoutBar]) < 0 ||
                lowSeries[5].ApproxCompare(lowSeries[breakoutBar]) < 0 ||
                lowSeries[4].ApproxCompare(lowSeries[breakoutBar]) < 0 ||
                lowSeries[3].ApproxCompare(lowSeries[breakoutBar]) < 0 ||
                lowSeries[2].ApproxCompare(lowSeries[breakoutBar]) < 0;
            }

            return bearishLowBreakoutHasConfirmation;
        }

        private int GetSupportCount()
        {
            // Validar si ya se han generado soportes
            int supportCount =
            priceListsZones.Where(zone => zone.Type ==
                Zone.ZoneType.Support)
            .Count();

            return supportCount;
        }

        private int GetResistenceCount()
        {
            // Validar si ya se han generado resistencias
            int resistenceCount =
            priceListsZones.Where(zone => zone.Type ==
                Zone.ZoneType.Resistance)
            .Count();

            return resistenceCount;
        }

        // Returns the number of bars ago a zig zag low occurred. Returns a value of -1 if a zig zag low is not found within the look back period.
        // NOTE: el método no se llama
        public int LowBar(int barsAgo, int instance, int lookBackPeriod)
        {
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

            if (CurrentBar < 10) // Need at least 10 bars to calculate Low/High
            {
                zigZagHighSeries[0] = 0;
                zigZagLowSeries[0] = 0;
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
                isSwingHigh = highSeries[1].ApproxCompare(highSeries[0]) >= 0
                && highSeries[1].ApproxCompare(highSeries[2]) >= 0;

                bool lastFiveHighBarsHavePriceSuccession =
                highSeries[3].ApproxCompare(highSeries[4]) >= 0 ||
                highSeries[2].ApproxCompare(highSeries[4]) >= 0 ||
                highSeries[1].ApproxCompare(highSeries[4]) >= 0 ||
                highSeries[0].ApproxCompare(highSeries[4]) >= 0;

                // Comprueba si la barra del medio (lowSeries[1]) es un valle, es decir, su valor es menor o igual que las barras adyacentes.
                isSwingLow = lowSeries[1].ApproxCompare(lowSeries[0]) <= 0
                && lowSeries[1].ApproxCompare(lowSeries[2]) <= 0;

                bool lastFiveLowBarsHavePriceSuccession =
                lowSeries[3].ApproxCompare(lowSeries[4]) <= 0 ||
                lowSeries[2].ApproxCompare(lowSeries[4]) <= 0 ||
                lowSeries[1].ApproxCompare(lowSeries[4]) <= 0 ||
                lowSeries[0].ApproxCompare(lowSeries[4]) <= 0;

                // Verifica si el valor de alto actual está por encima del último precio de swing más una desviación definida (DeviationValue), calculada en puntos o en porcentaje.
                bool isOverHighDeviation = (DeviationType == DeviationType.Percent && IsPriceGreater(highSeries[1], lastSwingPrice * (1.0 + DeviationValue))) || (DeviationType == DeviationType.Points && IsPriceGreater(highSeries[1], lastSwingPrice + DeviationValue));

                // Verifica si el valor de bajo actual está por debajo del último precio de swing menos una desviación definida (DeviationValue), calculada en puntos o en porcentaje.
                bool isOverLowDeviation = (DeviationType == DeviationType.Percent && IsPriceGreater(lastSwingPrice * (1.0 - DeviationValue), lowSeries[1])) || (DeviationType == DeviationType.Points && IsPriceGreater(lastSwingPrice - DeviationValue, lowSeries[1]));

                double currentSwingPrice = 0.0;
                bool addHigh = false;
                bool addLow = false;
                bool updateHigh = false;
                bool updateLow = false;

               
                if (IsPriceGreaterThanCurrentMaxHighPrice(highSeries[1]))
                {
                    //Actualiza el precio máximo alcanzado durante la sesión
                    this.currentMaxHighPrice = highSeries[1];
                    Print($"El precio máximo acaba de ser roto con el valor ${currentMaxHighPrice} en la barra #{CurrentBar}");

                    // Si no hay rompimientos al alza pendientes por completar añade un nuevo rompimiento a la cola de confirmación
                    if (!isHighBreakoutPendingToConfirmation && !isTheFirstSwingHigh)
                    {
                        var newBreakout = new BreakoutCandidate
                        {
                            BreakoutPrice = highSeries[1],
                            BreakoutBarIndex = CurrentBar,
                            Type = BreakoutCandidate.BreakoutType.Bullish,
                            IsConfirmed = false
                        };

                        Print($"se ha generado un rompimiento alcista en la barra:{newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.BreakoutPrice}");

                        pendingListOfBreakouts.Add(newBreakout);
                        isHighBreakoutPendingToConfirmation = true;
                    }

                    maxHighBrokenAccumulated += 1;
                    Print($"maxHighBrokenAccumulated = {maxHighBrokenAccumulated}");

                    /*if (
                        CurrentBar >= ChartBars.FromIndex
                        //&& isConfirmationOfZoneBrokenUpwards
                    )
                    {*/
                    maxHighBreakBar = CurrentBar;
                    resistenceZoneBreakoutPrice = currentMaxHighPrice;
                    currentClosingHighPrice = Open[1] >= Close[1] ? Open[1] : Close[1];
                    Print($"currentClosingHighPrice = {currentClosingHighPrice}");

                    Draw.Text(
                        this,              // La referencia al indicador o estrategia actual
                        "maxPriceBarText", // Un identificador único para el texto
                        $"Bar: {CurrentBar} MaxPrice: {resistenceZoneBreakoutPrice}$",
                        // El texto a dibujar
                        1, // El índice de la barra donde se dibuja (0 es la barra actual)
                        highSeries[1] + TickSize,  // (encima del máximo de la barra actual)
                        Brushes.Green  // El color del texto
                    );
                    /*}*/

                    if (isTheFirstSwingHigh)
                        isTheFirstSwingHigh = false;
                        
                }
                if (IsPriceLessThanCurrentMinLowPrice(lowSeries[1]))
                {
                    //Actualiza el precio máximo alcanzado durante la sesión
                    this.currentMinLowPrice = lowSeries[1];
                    Print($"El precio mínimo acaba de ser roto con el valor ${currentMinLowPrice} en la barra #{CurrentBar}");
                    // Si no hay rompimientos a la baja pendientes por completar añade un nuevo rompimiento a la cola de confirmación
                    if (!isLowBreakoutPendingToConfirmation && !isTheFirstSwingLow)
                    {
                        var newBreakout = new BreakoutCandidate
                        {
                            BreakoutPrice = lowSeries[1],
                            BreakoutBarIndex = CurrentBar,
                            Type = BreakoutCandidate.BreakoutType.Bearish,
                            IsConfirmed = false
                        };

                        Print($"se ha generado un rompimiento bajista en la barra: {newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.BreakoutPrice}");

                        pendingListOfBreakouts.Add(newBreakout);
                        isLowBreakoutPendingToConfirmation = true;
                    }
                        
                    minLowBrokenAccumulated += 1;
                    Print($"minLowBrokenAccumulated = {minLowBrokenAccumulated}");

                    /*if (
                        CurrentBar >= ChartBars.FromIndex
                    //&& isConfirmationOfZoneBrokenDownSide 
                    )
                    {*/
                    minLowBreakBar = CurrentBar;
                    supportZoneBreakoutPrice = currentMinLowPrice;
                    currentClosingLowPrice = Close[1] <= Open[1] ? Close[1] : Open[1];
                    Print($"currentClosingLowPrice = {currentClosingLowPrice}");

                    Draw.Text(
                        this,              // La referencia al indicador o estrategia actual
                        "minPriceBarText", // Un identificador único para el texto
                        $"Bar: {CurrentBar} MinPrice: {supportZoneBreakoutPrice}$", // El texto a dibujar
                        1,                 // El índice de la barra donde se dibuja (0 es la barra actual)
                        lowSeries[1] + TickSize,  // (encima del máximo de la barra actual)
                        Brushes.DarkRed  // El color del texto
                    );
                    /*}*/

                    if (isTheFirstSwingLow)
                        isTheFirstSwingLow = false;
                       
                }        

                if (
                  priceListsZones.Any() &&
                  !double.IsNaN(highSeries[1]) &&
                  !double.IsNaN(lowSeries[1])
               )
                {
                    // Actualiza las extensiones de zonas intermedias 
                    List<Zone> updatedZonesExtending = priceListsZones.Select(zone =>
                    {
                        // Si es una resistencia intermedia y el precio de la zona es superado
                        if (
                            zone.IsResistenceZone() &&
                            zone.IsIntermediateZone &&
                            IsPriceGreaterThanCurrentMaxHighPrice(
                            highSeries[1], zone.MaxOrMinPrice)
                        )
                        {
                            // Si no hay una confirmación de rompimiento intermedio al alza pendiente
                            if (!zone.IsBreakoutPendingToConfirmation)
                            {
                                var newBreakout = new BreakoutCandidate
                                {
                                    BreakoutPrice = highSeries[1],
                                    BreakoutBarIndex = CurrentBar,
                                    Type = BreakoutCandidate.BreakoutType.Bullish,
                                    IsIntermediateBreakout = true
                                };

                                Print($"se ha generado un rompimiento de zona alcista intermedia en la barra: {newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.BreakoutPrice}");

                                pendingListOfBreakouts.Add(newBreakout);
                                zone.IsBreakoutPendingToConfirmation = true;
                            }
                        }
                        else if (
                            !zone.IsResistenceZone() &&
                            zone.IsIntermediateZone &&
                            IsPriceLessThanCurrentMinLowPrice(
                            lowSeries[1], zone.MaxOrMinPrice)
                        )
                        {
                            Print($"lowSeries[1] = ${lowSeries[1]}, zone.MaxOrMinPrice = {zone.MaxOrMinPrice}");
                            Print($"ZoneType = {zone.Type}");
                            if (!zone.IsBreakoutPendingToConfirmation)
                            {
                                var newBreakout = new BreakoutCandidate
                                {
                                    BreakoutPrice = lowSeries[1],
                                    BreakoutBarIndex = CurrentBar,
                                    Type = BreakoutCandidate.BreakoutType.Bearish,
                                    IsIntermediateBreakout = true
                                };

                                Print($"se ha generado un rompimiento de zona bajista intermedia en la barra: {newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.BreakoutPrice}");

                                pendingListOfBreakouts.Add(newBreakout);
                                zone.IsBreakoutPendingToConfirmation = true;
                            }
                        }

                        return zone;
                    })
                    .ToList();
                    priceListsZones = updatedZonesExtending;
                }

                foreach (var breakout in pendingListOfBreakouts)
                {
                    // Valida si la barra actual es posterior a la vela en la que se genera el rompimiento y menor o igual al limite de velas de la confirmación
                    if (
                        CurrentBar > breakout.BreakoutBarIndex && 
                        CurrentBar <= breakout.BreakoutBarIndex + confirmationBars
                       )
                    {
                        if (
                            breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bullish) && breakout.IsIntermediateBreakout)
                        {
                            // Almacenar el precio máximo alcanzado de la vela anterior a la actual
                            breakout.NextFiveIntermediateHighBreakoutBarsPrices.Add(highSeries[1]);
                        }
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bullish))
                        {
                            Print($"guardando en NextFiveHighBreakoutBarsPrices: = {highSeries[1]}");
                            breakout.NextFiveHighBreakoutBarsPrices.Add(highSeries[1]);
                        }
                        else if (
                            breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish) && breakout.IsIntermediateBreakout) 
                        {
                            // Almacenar el precio mínimo alcanzado de la vela anterior a la actual
                            breakout.NextFiveIntermediateLowBreakoutBarsPrices.Add(lowSeries[1]);
                        }
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish))
                        {
                            // Almacenar el precio mínimo alcanzado de la vela anterior a la actual
                            breakout.NextFiveLowBreakoutBarsPrices.Add(lowSeries[1]);
                        }
                    }
                }

                // Comprobar si la oscilacion tiene un valor de 0
                if (!isSwingHigh && !isSwingLow)
                {
                    zigZagHighSeries[0] = currentZigZagHigh;
                    zigZagLowSeries[0] = currentZigZagLow;
                    // Evita realizar más acciones cuando no es un retroceso
                    return;
                }

                // Establece valores para dibujar nuevo movimiento al alza
                if (trendDir <= 0 && isSwingHigh && isOverHighDeviation)
                {
                    currentSwingPrice = highSeries[1];
                    addHigh = true;
                    trendDir = 1;
                }
                // Establece valores para dibujar nuevo movimiento a la baja
                else if (trendDir >= 0 && isSwingLow && isOverLowDeviation)
                {
                    currentSwingPrice = lowSeries[1];
                    addLow = true;
                    trendDir = -1;
                }
                // Establece valores para actualizar dibujo del movimiento al alza 
                else if (trendDir == 1 && isSwingHigh && IsPriceGreater(highSeries[1], lastSwingPrice))
                {
                    currentSwingPrice = highSeries[1];
                    updateHigh = true;
                }
                // Establece valores para actualizar dibujo del movimiento a la baja 
                else if (trendDir == -1 && isSwingLow && IsPriceGreater(lastSwingPrice, lowSeries[1]))
                {
                    currentSwingPrice = lowSeries[1];
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
                        zigZagHighZigZags[1] = currentSwingPrice;
                        currentZigZagHigh    = currentSwingPrice;
                        zigZagHighSeries[1]  = currentZigZagHigh;
                        Value[1]             = currentZigZagHigh;

                        //highBrokenAccumulated = highSeries[1] <= resistenceZoneBreakoutPrice ? 0 : highBrokenAccumulated;

                    }
                    else if (addLow || updateLow)
                    {
                        zigZagLowZigZags[1] = currentSwingPrice;
                        currentZigZagLow    = currentSwingPrice;
                        zigZagLowSeries[1]  = currentZigZagLow;
                        Value[1]            = currentZigZagLow;

                        //lowBrokenAccumulated = lowSeries[1] >= supportZoneBreakoutPrice ? 0 : lowBrokenAccumulated;
                    }

                    lastSwingIdx = CurrentBar - 1;
                    lastSwingPrice = currentSwingPrice;

                    Print($"currentMaxPrice = {currentMaxHighPrice}");
                    Print($"currentMinPrice = {currentMinLowPrice}");
                }

                // Validar si existe la necesidad de redibujar resistencias
                bool readrawHighStablished = priceListsZones.Any(
                    zone => zone.RedrawHighZoneIsRequired == true
                );

                // Validar si existe la necesidad de redibujar soportes
                bool readrawLowStablished = priceListsZones.Any(
                    zone => zone.RedrawLowZoneIsRequired == true
                );

                Print($"Pending breakouts candidates before event = ${pendingListOfBreakouts.Count()}");
                // Procesar lista de rompimientos pendientes
                if (pendingListOfBreakouts.Count > 0)
                {
                    int index = 0;
                    // Modificar for para que no espera a las 5 velas en caso de confirmación de rompimiento, únicamente debe dar el plazo de las 5 velas cuando no se haya confirmado el rompimiento y generar el extendimiento del precio
                    foreach (var breakout in pendingListOfBreakouts)
                    {
                        index++;
                        // Si ya fue confirmado saltamos
                        if (breakout.Completed)
                        {
                            continue;
                        }

                        bool maxBullishBreakoutConfirmed = false;
                        bool intermediateBullishBreakoutConfirmed = false;
                        bool minBearishBreakoutConfirmed = false;
                        bool intermediateBearishBreakoutConfirmed = false;
                        
                        // Si el tipo de rompimiento a confirmar es sobre una resisentancia intermedia
                        if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bullish) && breakout.IsIntermediateBreakout)
                        {
                            intermediateBullishBreakoutConfirmed = breakout.NextFiveIntermediateHighBreakoutBarsPrices.Any(highBreakoutPrice => highBreakoutPrice > breakout.BreakoutPrice);

                            int j = 1;
                            double confirmationPrice = breakout.BreakoutPrice;
                            foreach (var highBreakoutPrice in breakout.NextFiveIntermediateHighBreakoutBarsPrices)
                            {
                                Print($"Verificando si el precio ${highBreakoutPrice} de la barra  {breakout.BreakoutBarIndex + j} es mayor al precio ${breakout.BreakoutPrice} de la barra de rompimiento intermedio {breakout.BreakoutBarIndex} = {highBreakoutPrice > breakout.BreakoutPrice}");
                                // Almacenar el valor de rompimiento.
                                if(highBreakoutPrice > breakout.BreakoutPrice)
                                {
                                    confirmationPrice = highBreakoutPrice;
                                }

                                j++;
                            }

                            foreach( var zone in priceListsZones)
                            {
                                // TODO: Debe romper la zona superada en concreto, en lugar de todas las zonas intermedias cuyo precio aún no ha sido superado

                                // Si el rompimiento es confirmado y la zona actual es una resistencia (originalmente siendo un soporte) intermedia la elimina
                                if (intermediateBullishBreakoutConfirmed && zone.IsResistenceZone() && zone.IsIntermediateZone && confirmationPrice > zone.MaxOrMinPrice)
                                {
                                    Print($"Rompiendo resistencia = {zone.Id}");
                                    double lastPriceClose = zone.ClosePrice;
                                    double lastMaxOrMinPrice = zone.MaxOrMinPrice;

                                    zone.MaxOrMinPrice = lastPriceClose;
                                    zone.ClosePrice = lastMaxOrMinPrice;

                                    zone.IsResistenceBreakout = true;
                                    zone.Type = Zone.ZoneType.Support;

                                    RemoveDrawObject("RegionHighLightY" + zone.Id);

                                    breakout.Completed = true;
                                    breakout.IsConfirmed = true;
                                    break;
                                }

                                // Si no se confirma el rompimiento, es una resistencia intermedia y la barra actual es posterior a la cantidad de barras de confirmación extiende el margen de la zona al precio del rompimiento intermedio sin confirmación
                                else if(zone.IsResistenceZone() && zone.IsIntermediateZone && CurrentBar > breakout.BreakoutBarIndex + confirmationBars)
                                {
                                    
                                    Print(
                                    $"editando dimensiones de la zona intermedia al alza =" +
                                    $"{zone.Id} maxOrMinPrice = {zone.MaxOrMinPrice} type " +
                                    $"={zone.Type}"
                                    );

                                    zone.MaxOrMinPrice = breakout.BreakoutPrice;
                                    zone.RedrawHighZoneIsRequired = true;
                                    redrawHighZoneIsRequired = true;

                                    breakout.Completed = true;
                                    breakout.IsConfirmed = false;
                                }

                            }
                        }

                        // Si el tipo de rompimiento a confirmar es sobre un soporte intermedio
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish) && breakout.IsIntermediateBreakout)
                        {
                            intermediateBearishBreakoutConfirmed = breakout.NextFiveIntermediateLowBreakoutBarsPrices.Any(lowBreakoutPrice => lowBreakoutPrice < breakout.BreakoutPrice);

                            int z = 1;
                            double confirmationPrice = breakout.BreakoutPrice;
                            foreach (var lowBreakoutPrice in breakout.NextFiveIntermediateLowBreakoutBarsPrices)
                            {
                                Print($"Verificando si el precio ${lowBreakoutPrice} de la barra  {breakout.BreakoutBarIndex + z} es menor al precio ${breakout.BreakoutPrice} de la barra de rompimiento intermedio {breakout.BreakoutBarIndex} = {lowBreakoutPrice < breakout.BreakoutPrice}");

                                if (lowBreakoutPrice < breakout.BreakoutPrice)
                                {
                                    confirmationPrice = lowBreakoutPrice;
                                }
                                z++;
                            }

                            foreach (var zone in priceListsZones)
                            {
                                // Si el rompimiento es confirmado y la zona actual es un soporte (originalmente siendo una resistencia) intermedio lo elimina
                                if (intermediateBearishBreakoutConfirmed && !zone.IsResistenceZone() && zone.IsIntermediateZone && confirmationPrice < zone.MaxOrMinPrice)
                                {
                                    Print($"Rompiendo soporte = {zone.Id}");
                                    double lastMaxOrMinPrice = zone.MaxOrMinPrice;
                                    double lastPriceClose = zone.ClosePrice;

                                    zone.ClosePrice = lastMaxOrMinPrice;
                                    zone.MaxOrMinPrice = lastPriceClose;

                                    zone.IsSupportBreakout = true;
                                    zone.Type = Zone.ZoneType.Resistance;

                                    RemoveDrawObject("RegionLowLightY" + zone.Id);

                                    breakout.Completed = true;
                                    breakout.IsConfirmed = true;
                                    break;
                                }

                                // Si no se confirma el rompimiento, es un soporte intermedio y la barra actual es posterior a la cantidad de barras de confirmación extiende el margen de la zona al precio de rompimiento intermedio sin confirmación
                                else if (!zone.IsResistenceZone() && zone.IsIntermediateZone && CurrentBar > breakout.BreakoutBarIndex + confirmationBars)
                                {
                                    
                                    Print(
                                    $"editando dimensiones de la zona intermedia al alza =" +
                                    $"{zone.Id} maxOrMinPrice = {zone.MaxOrMinPrice} type " +
                                    $"={zone.Type}"
                                    );

                                    zone.MaxOrMinPrice = breakout.BreakoutPrice;
                                    zone.RedrawLowZoneIsRequired = true;
                                    redrawLowZoneIsRequired = true;

                                    breakout.Completed = true;
                                    breakout.IsConfirmed = false;
                                }

                            }
                        }
                        
                        // Si el tipo de rompimiento a confirmar es sobre la máxima zona alcista
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bullish))
                        {
                            // Revisa si alguna vela dentro de las próximas 5 velas supera al precio de rompimiento y lo confirma
                            maxBullishBreakoutConfirmed = breakout.NextFiveHighBreakoutBarsPrices.Any(highBreakoutPrice => highBreakoutPrice > breakout.BreakoutPrice)
                              //|| GetResistenceCount() == 0
                              ;

                            int j = 1;
                            foreach (var highbreakoutPrice in breakout.NextFiveHighBreakoutBarsPrices)
                            {
                                Print($"Verificando si el precio ${highbreakoutPrice} de la barra {breakout.BreakoutBarIndex + j} es mayor al precio ${breakout.BreakoutPrice} de la barra de rompimiento {breakout.BreakoutBarIndex} = {highbreakoutPrice > breakout.BreakoutPrice}");

                                j++;
                            }

                            // Sí hay confirmación del rompimiento o sí es el primer rompimiento al alza de la sesión
                            if (maxBullishBreakoutConfirmed || (GetResistenceCount() == 0 && !isTheFirstBarToAnalize))
                            {
                                Print("breaking high zone...");
                                Print("se ha confirmado un rompimiento alcista");
                                // Confirma el rompimiento a nivel general
                                isBullishBreakoutConfirmed = true;
                                isHighBreakoutPendingToConfirmation = false;
                                maxHighBrokenAccumulated = 0;
                                breakout.Completed = true;
                                breakout.IsConfirmed = true;
                            }
                            // Si no se supera el precio despues del rompimiento y han pasado 5 velas desde el primer rompimiento
                            else if (CurrentBar > breakout.BreakoutBarIndex + confirmationBars)
                            {
                                Print("extending resistence zone...");
                                // Establece el extendimiento de precio a nivel general
                                isHighZoneExtended = true;
                                isHighBreakoutPendingToConfirmation = false;
                                maxHighBrokenAccumulated = 0;
                                breakout.Completed = true;
                                breakout.IsConfirmed = false;
                            }
                        }

                        // Si el tipo de rompimiento a confirmar es sobre la mínima zona bajista
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish))
                        {
                            // Revisa si alguna vela dentro de las próximas 5 velas supera al precio de rompimiento y lo confirma
                            minBearishBreakoutConfirmed = breakout.NextFiveLowBreakoutBarsPrices.Any(lowBreakoutPrice => lowBreakoutPrice < breakout.BreakoutPrice)
                              // || GetSupportCount() == 0
                              ;

                            int z = 1;
                            foreach (var lowbreakoutPrice in breakout.NextFiveLowBreakoutBarsPrices)
                            {
                                Print($"Verificando si el precio ${lowbreakoutPrice} de la barra {breakout.BreakoutBarIndex + z} es menor al precio ${breakout.BreakoutPrice} de la barra de rompimiento {breakout.BreakoutBarIndex} = {lowbreakoutPrice < breakout.BreakoutPrice}");

                                z++;
                            }

                            // Sí hay confirmación del rompimiento o sí es el primer rompimiento a la baja de la sesión
                            if (minBearishBreakoutConfirmed || (GetSupportCount() == 0 && !isTheFirstBarToAnalize))
                            {
                                Print("breaking low zone...");
                                Print("se ha confirmado un rompimiento bajista");
                                // Confirma el rompimiento a nivel general
                                isBearishBreakoutConfirmed = true;
                                isLowBreakoutPendingToConfirmation = false;
                                minLowBrokenAccumulated = 0;
                                breakout.Completed = true;
                                breakout.IsConfirmed = true;
                            }
                            // Si no se supera el precio despues del rompimiento y han pasado 5 velas desde el primer rompimiento
                            else if (CurrentBar > breakout.BreakoutBarIndex + confirmationBars)
                            {
                                Print("extending support zone...");
                                //Print($"bar index: {breakout.BreakoutBarIndex}");
                                // Establece el extendimiento de precio a nivel general
                                isLowZoneExtended = true;
                                isLowBreakoutPendingToConfirmation = false;
                                minLowBrokenAccumulated = 0;
                                breakout.Completed = true;
                                breakout.IsConfirmed = false;
                            }
                        } 
                    }

                    // Eliminar elementos completados
                    pendingListOfBreakouts.RemoveAll(breakout => breakout.Completed);
                    Print($"Pending breakouts candidates after event = ${pendingListOfBreakouts.Count()}");
                }

                zigZagHighSeries[0] = currentZigZagHigh;
                zigZagLowSeries[0] = currentZigZagLow;

                // Dibuja la línea vertical en la barra actual cuando el script se reinicia
                if (BarsInProgress == 0 && isTheFirstBarToAnalize)
                {
                    // Dibujar la línea en la barra actual cuando el gráfico se ha cargado
                    // por completo
                   //DrawStartVerticalLine();

                    // Cambiar el estado para evitar dibujar la línea más de una vez
                    isTheFirstBarToAnalize = false;   
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

        // Valida si un precio anterior es superior a un precio actual
        private bool IsPriceGreaterThanCurrentMaxHighPrice(double lastPrice, double currentMaxHighPrice = double.NaN)
        {
            // Si el parámetro currentMaxHighPrice es NaN, utiliza el atributo de clase currentMaxHighPrice
            if (double.IsNaN(currentMaxHighPrice))
            {
                currentMaxHighPrice = this.currentMaxHighPrice; // Atributo de clase
            }

            // Compara el precio máximo anterior alcanzado con el nuevo precio alcanzado
            bool lastPriceIsGreaterThanMaxHighPrice = lastPrice
            .ApproxCompare(currentMaxHighPrice) > 0;
            return lastPriceIsGreaterThanMaxHighPrice;
        }

        // Valida si un precio anterior es inferior a uno actual
        private bool IsPriceLessThanCurrentMinLowPrice(double lastPrice, double currentMinLowPrice = double.NaN)
        {
            if (double.IsNaN(currentMinLowPrice))
            {
                Print($"this.currentMinLowPrice = ${this.currentMinLowPrice}");
                currentMinLowPrice = this.currentMinLowPrice;
                Print($"currentMinLowPrice = ${currentMinLowPrice}");
            }

            // Compara el precio mínimo anterior alcanzado con el nuevo precio alcanzado
            bool lastPriceIsLessThanMinLowPrice = lastPrice
            .ApproxCompare(currentMinLowPrice) < 0;
            
            Print($"lastPrice: ${lastPrice} < currentMinLowPrice: ${currentMinLowPrice} = {lastPriceIsLessThanMinLowPrice}");


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

                //Valida si el swing actual va alza o a la baja
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
                   

                    bool isABullishPullback = isSwingLow;
                    bool isABearishPullback = isSwingHigh;
                 
                    // Establecer cordenadas de la línea zig zag. 
                    float x1 = (chartControl.BarSpacingType == BarSpacingType.TimeBased || chartControl.BarSpacingType == BarSpacingType.EquidistantMulti && idx + Displacement >= ChartBars.Count
                        ? chartControl.GetXByTime(ChartBars.GetTimeByBarIdx(chartControl, idx + Displacement))
                        : chartControl.GetXByBarIndex(ChartBars, idx + Displacement));
                    float y1 = chartScale.GetYByValue(candlestickBodyValue);


                    if (startVerticalLineIndex != -1) {
                        
                        //Validar si es una movimiento alcista y si el valor de la vela es mayor o igual al último precio 
                        if (isHigh && isHighZoneExtended && priceListsZones.Any() && isABullishPullback)
                        {

                            //Print($"priceListsZones.count = {priceListsZones.Count()}");
                            currentZone = priceListsZones.LastOrDefault(
                                zone => zone.Type == Zone.ZoneType.Resistance
                            );

                            if (currentZone != null)
                            {
                                currentZone.MaxOrMinPrice = resistenceZoneBreakoutPrice;

                                Print("extendiendo región de la resistencia");

                                Print($"extendiendo resistencia = {currentZone.Id} precio de cierre: ${currentZone.ClosePrice} precio maximo: ${currentZone.MaxOrMinPrice}");

                                /*Print(
                                    $"MaxOrMinPrice = {currentZone.MaxOrMinPrice}, ClosePrice = {currentZone.ClosePrice}, " +
                                    $"lastMaxHighPrice = {lastMaxHighPrice}, lastClosingHighPrice = {lastClosingHighPrice}"
                                      *);


                                    bool priceIsNotbetween = PriceIsNotBetweenGeneratedPrice(
                                       currentZone.MaxOrMinPrice, currentZone.ClosePrice,
                                       lastMaxHighPrice, lastClosingHighPrice
                                    );

                                    //Print($"priceIsNotsbetween = {priceIsNotsbetween}");

                                    //if (priceIsNotbetween){
                                    /*Draw.Text(
                                       this,
                                       "highMaxPriceText",
                                       $"{resistenceZoneBreakoutPrice}",
                                       CurrentBar - maxHighBreakBar,  // El índice de la barra
                                       zigZagHighSeries[CurrentBar - maxHighBreakBar] + TickSize,
                                       Brushes.Green
                                      );
                                      */


                    // Dibujar zonas de soporte y resistencia
                    Draw.RegionHighlightY(
                                    this,                         // Contexto del indicador o estrategia
                                    "RegionHighLightY" + currentZone.Id, // Nombre único para la región
                                    currentZone.ClosePrice,        // Nivel de precio inferior
                                    currentZone.MaxOrMinPrice,     // Nivel de precio superior
                                    currentZone.HighlightBrush     // Pincel para el color de la región
                                );

                                //}
                                //RemoveOverlappingZones(currentZone);

                                lastMaxHighPrice = currentMaxHighPrice;
                                lastClosingHighPrice = currentClosingHighPrice;

                                //: Actualizar máximo preció alcanzado
                                CalculateCurrentMinOrMaxPrice();
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
                            maxHighBreakBar = int.MaxValue;
                            //Print("actualizando maxHighBreakBar");
                            isHighZoneExtended = false;
                            isABullishPullback = false;
                        }
                        else if (isHigh && isBullishBreakoutConfirmed && isABullishPullback)
                        {


                            /*if (
                                PriceIsNotBetweenGeneratedPrice(
                                resistenceZoneBreakoutPrice, currentClosingHighPrice,
                                lastMaxHighPrice, lastClosingHighPrice
                                )
                                )
                                {*/

                            currentZone = new Zone(
                                Zone.ZoneType.Resistance,
                                currentClosingHighPrice,
                                resistenceZoneBreakoutPrice
                            );

                            priceListsZones.Add(currentZone);

                            List<Zone> updatePriceListZones = priceListsZones.Select(zone =>
                            {

                                /* bool priceIsNotsbetween = PriceIsNotBetweenGeneratedPrice(
                                     zone.MaxOrMinPrice, zone.ClosePrice,
                                     lastMaxHighPrice, lastClosingHighPrice
                                 );
                                */

                                //Print($"priceIsNotBetween = {priceIsNotsbetween}");
                                /*
                                    Print("before breakout: ");
                                    Print($"Type = {zone.Type} ID = {zone.Id} " +
                                    $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                    $"supportBreakout = {zone.IsSupportBreakout}");
                                */

                                if (zone.IsResistenceZone())
                                {
                                    /*
                                        Print("after breakout: ");
                                        Print($"Type = {zone.Type} ID = {zone.Id} " +
                                        $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                        $"supportBreakout = {zone.IsSupportBreakout} " +
                                        $"maxOrMinPrice = {zone.MaxOrMinPrice}");
                                        */

                                    if (
                                        zone.MaxOrMinPrice == resistenceZoneBreakoutPrice
                                    //&& priceIsNotsbetween
                                    )
                                    {

                                        Print($"Generando resistencia: {zone.Id} precio de cierre: {zone.ClosePrice} y precio de maximo: {zone.MaxOrMinPrice}");
                                        // Dibujar zonas de soporte 
                                        Draw.RegionHighlightY(
                                            this,                    // Contexto del indicador o estrategia
                                            "RegionHighLightY" + zone.Id, // Nombre único para la región
                                            zone.ClosePrice,              // Nivel de precio inferior
                                            zone.MaxOrMinPrice,           // Nivel de precio superior
                                            zone.HighlightBrush      // Pincel para el color de la región
                                        );
                                    }
                                    else if (
                                        zone.MaxOrMinPrice < resistenceZoneBreakoutPrice
                                    )
                                    {
                                        Print(
                                            $"La zona {zone.Id} está siendo convertida a soporte " +
                                            $"con el precio máximo de: {zone.ClosePrice} y el " +
                                            $"precio de cierre: {zone.MaxOrMinPrice}"
                                        );

                                        double lastPriceClose = zone.ClosePrice;
                                        double lastMaxOrMinPrice = zone.MaxOrMinPrice;

                                        zone.MaxOrMinPrice = lastPriceClose;
                                        zone.ClosePrice = lastMaxOrMinPrice;

                                        zone.IsResistenceBreakout = true;
                                        zone.IsIntermediateZone = true;
                                        zone.Type = Zone.ZoneType.Support;
                                        zone.HighlightBrush = Brushes.Red.Clone();
                                        zone.HighlightBrush.Opacity = 0.3;

                                        //RemoveDrawObject("highMaxPriceText" + zone.Id);
                                        //RemoveDrawObject("closingMaxPriceText" + zone.Id);
                                        RemoveDrawObject("RegionHighLightY" + zone.Id);

                                        /*Draw.Text(
                                            this,     // La referencia al indicador o estrategia actual
                                            "closingMinPriceText" + currentZone.Id, // Un identificador único para el texto
                                            $"{currentZone.ClosePrice}", // Texto
                                            maxHighBreakBar,             // El índice de la barra donde se dibuja 
                                            currentZone.ClosePrice + TickSize, // La posición vertical 
                                            Brushes.DarkRed          // El color del texto
                                            );
                                            */

                                        Draw.RegionHighlightY(
                                            this,                   // Contexto del indicador o estrategia
                                            "RegionLowLightY" + zone.Id,  // Nombre único para la región
                                            zone.ClosePrice,              // Nivel de precio superior
                                            zone.MaxOrMinPrice,           // Nivel de precio inferior
                                            zone.HighlightBrush        // Pincel para el color de la región
                                        );

                                        /*Draw.Text(
                                            this,     // La referencia al indicador o estrategia actual
                                            "lowMinPriceText" + currentZone.Id, // Un identificador único para el texto
                                            $"{currentZone.MaxOrMinPrice}", // Texto
                                            maxHighBreakBar,                      // El índice de la barra donde se dibuja 
                                            currentZone.MaxOrMinPrice + TickSize, // La posición vertical 
                                            Brushes.DarkRed          // El color del texto
                                            );
                                            */
                                    }
                                }

                                return zone;

                            })
                            .ToList();
                            priceListsZones = updatePriceListZones;

                            //}
                            /*else
                                {
                                    var zoneWithHighestPrice = priceListsZones
                                    .OrderByDescending(zone => zone.MaxOrMinPrice)
                                    .FirstOrDefault();

                                    List<Zone> extendedMaxZoneWhenPriceBreakoutIsEqualToLastPrice =
                                    priceListsZones.Select(zone =>
                                    {
                                        if (zone.Id == zoneWithHighestPrice.Id)
                                        {
                                            zone.MaxOrMinPrice = resistenceZoneBreakoutPrice;
                                            zone.RedrawHighZoneIsRequired = true;
                                        }
                                        return zone;
                                    }).ToList();
                                    redrawHighZoneIsRequired = true;
                                    priceListsZones = extendedMaxZoneWhenPriceBreakoutIsEqualToLastPrice;
                                }*/

                            lastMaxHighPrice = currentMaxHighPrice;
                            lastClosingHighPrice = currentClosingHighPrice;
                            CalculateCurrentMinOrMaxPrice();
                            maxHighBreakBar = int.MaxValue;
                            Print("actualizando maxHighBreakBar");
                            isBullishBreakoutConfirmed = false;
                            isABullishPullback = false;
                        }
                        if (isLow && isLowZoneExtended && priceListsZones.Any() && isABearishPullback)
                        {

                            //Print($"priceListsZones.count = {priceListsZones.Count()}");
                            currentZone = priceListsZones.LastOrDefault(
                                zone => zone.Type == Zone.ZoneType.Support
                            );

                            //Print($"zigZagLowSeries[4]: {supportZoneBreakoutPrice}");

                            if (currentZone != null)
                            {
                                currentZone.MaxOrMinPrice = supportZoneBreakoutPrice;

                                Print("extendiendo región del soporte...");

                                Print($"extendiendo soporte = {currentZone.Id} precio de cierre: ${currentZone.ClosePrice} precio minimo: ${currentZone.MaxOrMinPrice}");

                                bool priceIsNotbetween = PriceIsNotBetweenGeneratedPrice(
                                    lastMinLowPrice, lastClosingLowPrice,
                                    currentZone.MaxOrMinPrice, currentZone.ClosePrice
                                );

                                // Print($"priceIsNotbetween = {priceIsNotbetween}");

                                //if (priceIsNotbetween){

                                // Dibujar zonas de soporte y resistencia
                                Draw.RegionHighlightY(
                                    this, // Contexto del indicador o estrategia
                                    "RegionLowLightY" + currentZone.Id, // Nombre único para la región
                                    currentZone.MaxOrMinPrice,     // Nivel de precio inferior
                                    currentZone.ClosePrice,        // Nivel de precio superior
                                    currentZone.HighlightBrush     // Pincel para el color de la región
                                );

                                /*Draw.Text(
                                    this,       // La referencia al indicador o estrategia actual
                                    "lowMinPriceText" + currentZone.Id,// Un identificador único para el texto
                                    $"{currentZone.MaxOrMinPrice}", // Texto
                                    minLowBreakBar,                      // El índice de la barra donde se dibuja 
                                    currentZone.MaxOrMinPrice + TickSize, // La posición vertical 
                                    Brushes.DarkRed          // El color del texto
                                    );
                                    */

                                //}

                                //RemoveOverlappingZones(currentZone);

                                lastMinLowPrice = currentMinLowPrice;
                                lastClosingLowPrice = currentClosingLowPrice;

                                //: Actualizar mínimo preció alcanzado
                                CalculateCurrentMinOrMaxPrice();

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

                            minLowBreakBar = int.MaxValue;
                            //Print("actualizando minLowBreakBar");
                            isLowZoneExtended = false;
                            isABearishPullback = false;
                        }
                        else if (isLow && isBearishBreakoutConfirmed && isABearishPullback)
                        {
                            Print("Generando soporte");

                            /*Print(
                                $"lastMinLowPrice = {lastMinLowPrice} " +
                                $"lastClosingLowPrice = {lastClosingLowPrice} " +
                                $"supportZoneBreakoutPrice = {supportZoneBreakoutPrice} "+
                                $"currentClosingLowPrice = {currentClosingLowPrice}"
                                );
                               */

                            /*if (
                                PriceIsNotBetweenGeneratedPrice(
                                lastMinLowPrice, lastClosingLowPrice,
                                supportZoneBreakoutPrice, currentClosingLowPrice
                                )
                                )
                                {*/

                            currentZone = new Zone(
                                Zone.ZoneType.Support,
                                currentClosingLowPrice,
                                supportZoneBreakoutPrice
                            );

                            priceListsZones.Add(currentZone);

                            /*Print($"soporte: ClosePrice = {currentZone.ClosePrice} " +
                                $"minPrice = {currentZone.MaxOrMinPrice}");
                                Print($"currentClosingLowPrice = {currentClosingLowPrice} currentMinLowPrice = {currentMinLowPrice}");
                            */
                            List<Zone> updatePriceListZones = priceListsZones.Select(zone =>
                            {

                                /*
                                    Print("before breakout: ");
                                    Print($"Type = {zone.Type} ID = {zone.Id} " +
                                    $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                    $"supportBreakout = {zone.IsSupportBreakout}");*/


                                if (!zone.IsResistenceZone())
                                {
                                    /*
                                        Print("after breakout: ");
                                        Print($"Type = {zone.Type} ID = {zone.Id} " +
                                        $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                        $"supportBreakout = {zone.IsSupportBreakout}");
                                        */
                                    if (
                                        zone.MaxOrMinPrice == supportZoneBreakoutPrice
                                    // && priceIsNotsbetween
                                    )
                                    {

                                        Print($"Generando soporte: {zone.Id} precio de cierre: {zone.ClosePrice} y precio de apertura: {zone.MaxOrMinPrice}");

                                        // Dibujar zonas de soporte 
                                        Draw.RegionHighlightY(
                                            this,                 // Contexto del indicador o estrategia
                                            "RegionLowLightY" + zone.Id,  // Nombre único para la región
                                            zone.MaxOrMinPrice,           // Nivel de precio inferior
                                            zone.ClosePrice,              // Nivel de precio superior
                                            zone.HighlightBrush     // Pincel para el color de la región
                                        );
                                    }
                                    else if (
                                        zone.MaxOrMinPrice > supportZoneBreakoutPrice
                                    )
                                    {
                                        Print(
                                        $"La zona {zone.Id} está siendo convertida a resistencia " +
                                        $"con el precio mínimo de: {zone.MaxOrMinPrice} y el " +
                                        $"precio de cierre: {zone.ClosePrice}"
                                        );

                                        double lastMaxOrMinPrice = zone.MaxOrMinPrice;
                                        double lastPriceClose = zone.ClosePrice;

                                        zone.ClosePrice = lastMaxOrMinPrice;
                                        zone.MaxOrMinPrice = lastPriceClose;

                                        zone.IsSupportBreakout = true;
                                        zone.IsIntermediateZone = true;
                                        zone.Type = Zone.ZoneType.Resistance;
                                        zone.HighlightBrush = Brushes.Green.Clone();
                                        zone.HighlightBrush.Opacity = 0.3;

                                        //RemoveDrawObject("closingMinPriceText" + zone.Id);
                                        //RemoveDrawObject("lowMinPriceText" + zone.Id);
                                        RemoveDrawObject("RegionLowLightY" + zone.Id);

                                        /*Draw.Text(
                                                this,  // La referencia al indicador o estrategia actual
                                                "highMaxPriceText" + zone.Id, // Un identificador único para el texto
                                                $"{zone.MaxOrMinPrice}", // Texto
                                                minLowBreakBar,  // El índice de la barra donde se dibuja 
                                                zone.MaxOrMinPrice + TickSize, // La posición vertical 
                                                Brushes.Green    // El color del texto
                                            );
                                            */

                                        Draw.RegionHighlightY(
                                            this,                    // Contexto del indicador o estrategia
                                            "RegionHighLightY" + zone.Id, // Nombre único para la región
                                            zone.ClosePrice,              // Nivel de precio inferior
                                            zone.MaxOrMinPrice,           // Nivel de precio superior
                                            zone.HighlightBrush       // Pincel para el color de la región
                                        );

                                        /*
                                                Draw.Text(
                                                    this,               // La referencia al indicador o estrategia actual
                                                    "closingMaxPriceText" + zone.Id, // Un identificador único para el texto
                                                    $"{zone.ClosePrice}",   // Texto
                                                    minLowBreakBar,                      // El índice de la barra donde se dibuja 
                                                    zone.ClosePrice + TickSize, // La posición vertical 
                                                    Brushes.Green          // El color del texto
                                                );
                                            */
                                    }
                                }

                                return zone;

                            })
                            .ToList();
                            priceListsZones = updatePriceListZones;

                            //}
                            lastMinLowPrice = currentMinLowPrice;
                            Print($"lastMinLowPrice after update = {lastMinLowPrice}");
                            lastClosingLowPrice = currentClosingLowPrice;
                            CalculateCurrentMinOrMaxPrice();
                            minLowBreakBar = int.MaxValue;
                            Print("actualizando minLowBreakBar");
                            isBearishBreakoutConfirmed = false;
                            isABearishPullback = false;

                        }

                        if (redrawHighZoneIsRequired)
                        {
                            Print("redibujando dimensiones de la zona intermedia al alza");
                            List<Zone> updatePriceListsZones = priceListsZones.Select(zone =>
                            {
                                if (zone.IsResistenceZone() && zone.RedrawHighZoneIsRequired)
                                {
                                    Print($"redibujando resistencia = {zone.Id}");
                                    Draw.RegionHighlightY(
                                        this,                   // Contexto del indicador o estrategia
                                        "RegionHighLightY" + zone.Id, // Nombre único para la región
                                        zone.ClosePrice,        // Nivel de precio inferior
                                        zone.MaxOrMinPrice,     // Nivel de precio superior
                                        zone.HighlightBrush     // Pincel para el color de la región
                                    );

                                }

                                zone.RedrawHighZoneIsRequired = false;
                                return zone;
                                // Dibujar zonas de soporte y resistencia
                            })
                            .ToList();
                            priceListsZones = updatePriceListsZones;
                            redrawHighZoneIsRequired = false;
                        }
                        else if (redrawLowZoneIsRequired)
                        {
                            Print("redibujando dimensiones de la zona intermedia a la baja");

                            List<Zone> updatePriceListsZones = priceListsZones.Select(zone =>
                            {
                                if (!zone.IsResistenceZone() && zone.RedrawLowZoneIsRequired)
                                {
                                    Print($"redibujando soporte = {zone.Id}");
                                    Draw.RegionHighlightY(
                                        this,                   // Contexto del indicador o estrategia
                                        "RegionLowLightY" + zone.Id, // Nombre único para la región
                                        zone.ClosePrice,        // Nivel de precio inferior
                                        zone.MaxOrMinPrice,     // Nivel de precio superior
                                        zone.HighlightBrush     // Pincel para el color de la región
                                    );
                                }

                                zone.RedrawLowZoneIsRequired = false;
                                return zone;
                            })
                            .ToList();
                            priceListsZones = updatePriceListsZones;
                            redrawLowZoneIsRequired = false;
                        }

                        if (
                            priceListsZones.Any()
                        )
                        {
                            // Actualizar la lista de zonas rotas
                            List<Zone> breakoutsZonesUpdated = priceListsZones.Select(zone => {
                                if (zone.IsResistenceZone())
                                {
                                    if (
                                        currentZigZagLow > zone.MaxOrMinPrice
                                    )
                                    {
                                        Print($"Rompiendo zona de la resistencia = {zone.Id}");
                                        zone.IsResistenceBreakout = true;
                                    }
                                }
                                else
                                {

                                    if (currentZigZagHigh < zone.MaxOrMinPrice)
                                    {
                                        /*Print($"currentZigZagHigh = {currentZigZagHigh} zone.MaxOrMinPrice " +
                                        $"= {zone.MaxOrMinPrice}");
                                        Print($"currentZigZagHigh < zone.MaxOrMinPrice {zone.Id} = " +
                                        $"{currentZigZagHigh < zone.MaxOrMinPrice}");
                                        */

                                        Print($"Rompiendo zona del soporte = {zone.Id}");
                                        zone.IsSupportBreakout = true;
                                    }
                                }

                                return zone;
                            })
                            .ToList();
                            priceListsZones = breakoutsZonesUpdated;

                            priceListsZones.RemoveAll(zone =>
                            {
                                if (
                                    zone.IsResistenceZone() &&
                                    (zone.IsResistenceBreakout && zone.IsSupportBreakout)

                                )
                                {
                                    Print($"eliminando resistencia = {zone.Id}");
                                    RemoveDrawObject("RegionHighLightY" + zone.Id);
                                    return true; // Eliminar zona
                                }
                                else if (
                                    !zone.IsResistenceZone() &&
                                    (zone.IsResistenceBreakout && zone.IsSupportBreakout)
                                )
                                {
                                    Print($"eliminando soporte = {zone.Id}");
                                    RemoveDrawObject("RegionLowLightY" + zone.Id);
                                    return true; // Eliminar zona
                                }

                                return false; // No eliminar zona
                            });

                            //CalculateCurrentMaxPrice();
                            //CalculateCurrentMinPrice();
                        }
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
            get; set;
        }

        public double ClosePrice
        {
            get; set;
        }    // Precio de cierre
        public double MaxOrMinPrice
        {
            get; set;
        } // Precio máximo para Resistencia o mínimo para Soporte
        
        public bool IsSupportBreakout
        {
            get; set;
        }       // Indica si el soporte ha sido roto
        public bool IsResistenceBreakout
        {
            get; set;
        }    // Indica si la resistencia ha sido rota
        public bool RedrawHighZoneIsRequired
        {
            get; set;
        }
        public bool RedrawLowZoneIsRequired
        {
            get; set;
        }
        public long Id
        {
            get; private set;
        }
        public Brush HighlightBrush
        {
            get; set;
        } // Color de la zona

        public bool IsIntermediateZone { get; set; } // Es una zona intermedia

        public bool IsBreakoutPendingToConfirmation { get; set; } // La zona tiene un rompimiento pendiente por confirmar

        // Constructor para inicializar la Zona
        public Zone(ZoneType type, double closePrice, double maxOrMinPrice)
        {
            Type = type;
            ClosePrice = closePrice;
            MaxOrMinPrice = maxOrMinPrice;

            IsResistenceBreakout = false;
            IsSupportBreakout = false;

            IsIntermediateZone = false;
            IsBreakoutPendingToConfirmation = false;
            

            // Configurar el color según el tipo de Zona
            if (Type == ZoneType.Support)
            {
                HighlightBrush = Brushes.Red.Clone();
                HighlightBrush.Opacity = 0.3;
                //IsSupportBreakout = true;
                RedrawHighZoneIsRequired = false;
            }
            else if (Type == ZoneType.Resistance)
            {
                HighlightBrush = Brushes.Green.Clone();
                HighlightBrush.Opacity = 0.3;
                //IsResistenceBreakout = true;
                RedrawLowZoneIsRequired = false;
            }

            // Inicializamos los estados de breakout

            Id = DateTime.Now.Ticks;
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

    public class BreakoutCandidate
    {
        public enum BreakoutType
        {
            Bullish, // alcista
            Bearish // bajista

        }

        // Tipo de rompimiento alcista o bajista
        public BreakoutType Type
        {
            get; set;
        }

        // Precio donde se origina el rompimiento
        public double BreakoutPrice
        {
            get; set;
        }
        
        // índice donde se genera el rompimiento
        public int BreakoutBarIndex
        {
            get; set;
        }

        // Establece cuando el rompimiento es completado, sea que se confirme o que no tenga consecusión
        public bool Completed
        {
            get; set;
        } = false;

        // Determina si el rompimiento tiene consecusión
        public bool IsConfirmed
        {
            get; set; 
        } = false;

        public bool IsIntermediateBreakout { get; set; } = false;

        public List<double> NextFiveHighBreakoutBarsPrices
        {
            get; set;
        } = new List<double>();

        public List<double> NextFiveIntermediateHighBreakoutBarsPrices
        {
            get; set;
        } = new List<double>();

        public List<double> NextFiveLowBreakoutBarsPrices
        {
            get; set;
        } = new List<double>();

        public List<double> NextFiveIntermediateLowBreakoutBarsPrices
        {
            get; set;
        } = new List<double>();

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
