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
        private long currentZoneId;

        // Estas son series que almacenan los valores de los picos y valles detectados por el indicador ZigZag en las barras de alto y bajo, respectivamente. Sirven para registrar los puntos de cambio en la tendencia.
        private Series<double> zigZagHighZigZags;
        private Series<double> zigZagLowZigZags;

        // Almacenan los valores 
        private Series<double> zigZagHighSeries;
        private Series<double> zigZagLowSeries;

        // Estas variables almacenan los valores actuales de los picos y valles más recientes del ZigZag. Se utilizan para seguir el último alto y bajo significativo.
        private double currentZigZagHigh;
        private double currentZigZagLow; 

        private double currentMaxHighPrice = double.MaxValue;
        private double currentClosingHighPrice;
        private double lastClosingHighPrice;
        private double lastMaxHighPrice;

        private double currentMinLowPrice = double.MinValue;
        private double currentClosingLowPrice;
        private double lastClosingLowPrice;
        private double lastMinLowPrice;

        private double resistenceZoneBreakoutPrice;
        private double supportZoneBreakoutPrice;

        private int highBreakBar;
        private int maxHighBreakBar;
        private int highBrokenAccumulated;
        private int highBrokenAccumulatedIntermediate;
        private int maxHighBrokenAccumulated;
        private int lowBrokenAccumulatedIntermediate;
        
        private int lowBreakBar;
        private int minLowBreakBar;
        private int lowBrokenAccumulated;
        private int minLowBrokenAccumulated;

        private DateTime highCandleStartTime;
        private DateTime lowCandleStartTime;



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
        private bool redrawHighZoneIsRequired = false;
        private bool redrawLowZoneIsRequired = false;
        private bool isBullishBreakoutConfirmed = false;
        private bool isBearishBreakoutConfirmed = false;
        private bool isChartLoaded = false;


        //private bool resistenceIsPreviouslyBroken = false;
        //private bool supportIsPreviouslyBroken = false;
        //private bool breakHighBarIsUpdatable = true;
        //private bool breakLowBarIsUpdatable = true;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicador para dibujar líneas en regresiones de velas.";
                Name = "CustomIndicatorTest2";
                Calculate = Calculate.OnBarClose;
                DeviationType = DeviationType.Points;
                BarsRequiredToPlot = 5;
                isChartLoaded = false;  // Inicializamos para saber cuándo se ha cargado el gráfico
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
                highBrokenAccumulatedIntermediate = 0;

                currentClosingLowPrice = double.MaxValue;
                lastMinLowPrice = double.MaxValue;
                lastClosingLowPrice = double.MaxValue;
                minLowBrokenAccumulated = 0;
                lowBrokenAccumulated = 0;
                lowBrokenAccumulatedIntermediate = 0;


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
                priceListsZones = new List<Zone>();
                resistenceZoneBreakoutPrice = 0;
                supportZoneBreakoutPrice = double.MaxValue;
                highBreakBar = 0;
                maxHighBreakBar = 0;
                currentZoneId = -1;
                currentZone = null;
                // Aquí capturamos el índice de la última barra cargada
                // Marcamos que el script ha sido reiniciado

            }

            if(State == State.Realtime)
            {
                Print($"State == State.Realtime : {true}");
                // Aquí estamos entrando en tiempo real, después de cargar las barras históricas
                DrawStartVerticalLine();
                currentMaxHighPrice = double.MinValue;
                currentMinLowPrice = double.MaxValue;
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

        private bool IsZoneWithinBarHighRange(Zone zone, double barHighPrice)
        {
            // Validar que tanto MaxOrMinPrice como ClosePrice de la zona estén dentro del rango de la barra
            bool isMaxOrMinWithinRange = barHighPrice >= zone.MaxOrMinPrice;
            Print($"Barra intermedia es mayor a la zona = {isMaxOrMinWithinRange}");

            // Si ambas condiciones se cumplen, la zona está dentro del rango de la barra
            return isMaxOrMinWithinRange ;
        }

        private bool IsZoneWithinBarLowRange(Zone zone, double barLow)
        {
            // Validar que tanto MaxOrMinPrice como ClosePrice de la zona estén dentro del rango de la barra
            bool isMaxOrMinWithinRange = barLow <= zone.MaxOrMinPrice;
            Print($"Barra intermedia es menor a la zona = {isMaxOrMinWithinRange}");

            // Si ambas condiciones se cumplen, la zona está dentro del rango de la barra
            return isMaxOrMinWithinRange;
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

                Print($"{CurrentBar - 5} - {lowSeries[6]}$ <= {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[6].ApproxCompare(lowSeries[breakoutBar]) <= 0}");

                Print($"{CurrentBar - 4} - {lowSeries[5]}$ <= {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[5].ApproxCompare(lowSeries[breakoutBar]) <= 0}");

                Print($"{CurrentBar - 3} - {lowSeries[4]}$ <= {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[4].ApproxCompare(lowSeries[breakoutBar]) <= 0}");

                Print($"{CurrentBar - 2} - {lowSeries[3]}$ <= {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[3].ApproxCompare(lowSeries[breakoutBar]) <= 0}");

                Print($"{CurrentBar - 1} - {lowSeries[2]}$ <= {minLowBreakBar} - {lowSeries[breakoutBar]}$ = {lowSeries[2].ApproxCompare(lowSeries[breakoutBar]) <= 0}");

                // TODO ajustar para que compare exactamente 5 barras excluyendo la actual
                bearishLowBreakoutHasConfirmation =
                lowSeries[6].ApproxCompare(lowSeries[breakoutBar]) <= 0 ||
                lowSeries[5].ApproxCompare(lowSeries[breakoutBar]) <= 0 ||
                lowSeries[4].ApproxCompare(lowSeries[breakoutBar]) <= 0 ||
                lowSeries[3].ApproxCompare(lowSeries[breakoutBar]) <= 0 ||
                lowSeries[2].ApproxCompare(lowSeries[breakoutBar]) <= 0;
            }

            return bearishLowBreakoutHasConfirmation;
        }

        private bool BullishHighBreakoutHasConfirmation(
            ISeries<double> highSeries, int maxHighBreakBar
        ){
            Print($"maxHighBreakBar = {maxHighBreakBar}");
            Print($"Bars.Count = {Bars.Count}");
            Print($"CurrentBar = {CurrentBar}");
            bool bullishHighBreakoutHasConfirmation = false;
            if (maxHighBreakBar > 0)
            {
                int breakoutBar = (CurrentBar - maxHighBreakBar) + 1;
                Print($"breakoutBar = {breakoutBar}");

                Print($"{CurrentBar - 5} - {highSeries[6]}$ >= {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[6].ApproxCompare(highSeries[breakoutBar]) >= 0}");

                Print($"{CurrentBar - 4} - {highSeries[5]}$ >= {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[5].ApproxCompare(highSeries[breakoutBar]) >= 0}");

                Print($"{CurrentBar - 3} - {highSeries[4]}$ >= {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[4].ApproxCompare(highSeries[breakoutBar]) >= 0}");

                Print($"{CurrentBar - 2} - {highSeries[3]}$ >= {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[3].ApproxCompare(highSeries[breakoutBar]) >= 0}");

                Print($"{CurrentBar - 1} - {highSeries[2]}$ >= {maxHighBreakBar} - {highSeries[breakoutBar]}$ = {highSeries[2].ApproxCompare(highSeries[breakoutBar]) >= 0}");

                // TODO ajustar para que compare exactamente 5 barras excluyendo la actual
                bullishHighBreakoutHasConfirmation =
                    highSeries[6].ApproxCompare(highSeries[breakoutBar]) >= 0 ||
                    highSeries[5].ApproxCompare(highSeries[breakoutBar]) >= 0 ||
                    highSeries[4].ApproxCompare(highSeries[breakoutBar]) >= 0 ||
                    highSeries[3].ApproxCompare(highSeries[breakoutBar]) >= 0 ||
                    highSeries[2].ApproxCompare(highSeries[breakoutBar]) >= 0;
            }

            return bullishHighBreakoutHasConfirmation;
        }

        // Método para obtener el número de la barra con un valor High cercano
        private int GetBarNumberForHigh(double targetHigh, int maxBarsLookBack = 5, double tolerance = 0.0001)
        {
            // Limita la búsqueda a las últimas 'maxBarsLookBack' barras o hasta la barra más antigua
            int lookBackBars = Math.Min(CurrentBar, maxBarsLookBack);

            // Itera sobre las barras comenzando desde la más reciente hacia la más antigua
            for (int i = 0; i <= CurrentBar; i++)
            {
                // Usa ApproxCompare para evitar problemas de precisión
                if (targetHigh.ApproxCompare(High[i]) == 0)
                {
                    return i; // Retorna el número de la barra si hay coincidencia
                }
            }

            // Si no encuentra coincidencia, retorna -1
            return -1;
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

        public void RemoveOverlappingZones(Zone newZone)
        {
            // Buscar zonas solapadas en priceListZones
            var overlappingZones = priceListsZones.Where(zone =>
                (newZone.MaxOrMinPrice >= zone.ClosePrice && newZone.ClosePrice <= zone.MaxOrMinPrice) ||
                (newZone.MaxOrMinPrice <= zone.ClosePrice && newZone.ClosePrice >= zone.MaxOrMinPrice))
                .ToList();

            // Eliminar zonas solapadas
            /*foreach (var zone in overlappingZones)
            {
                priceListsZones.Remove(zone);
                if (zone.IsResistenceZone())
                {
                    RemoveDrawObject("RegionHighLightY" + zone.Id);
                }
                else
                {
                    RemoveDrawObject("RegionLowLightY" + zone.Id);
                }
                if (currentZone != null)
                {
                    CalculateCurrentMinOrMaxPrice();
                }
            }
            */

            // Agregar la nueva zona después de eliminar las solapadas
            //priceListsZones.Add(newZone);

            //Print("Zonas solapadas eliminadas");
        }

        protected override void OnBarUpdate()
        {
            
            if (CurrentBar < 1 || CurrentBar < 10) // Need at least 3 bars to calculate Low/High
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
            
            // Establecer un valor minimo alcanzado en base a las últimas 20 barras aprocimadas cuando el valor minimo sea 0
            if (CurrentBar >= ChartBars.FromIndex && currentMinLowPrice == double.MaxValue)
            {
                Print($"guardando el precio mínimo: {lowSeries[0]} en currentMinLowPrice");
                currentMinLowPrice = lowSeries[1];
            }

            try
            {
                // Comprueba si la barra del medio (highSeries[1]) es un pico, es decir, su valor es mayor o igual que las barras adyacentes.
                bool isSwingHigh = highSeries[1].ApproxCompare(highSeries[0]) >= 0
                && highSeries[1].ApproxCompare(highSeries[2]) >= 0;

                bool lastFiveHighBarsHavePriceSuccession =
                highSeries[3].ApproxCompare(highSeries[4]) >= 0 ||
                highSeries[2].ApproxCompare(highSeries[4]) >= 0 ||
                highSeries[1].ApproxCompare(highSeries[4]) >= 0 ||
                highSeries[0].ApproxCompare(highSeries[4]) >= 0;

                // Comprueba si la barra del medio (lowSeries[1]) es un valle, es decir, su valor es menor o igual que las barras adyacentes.
                bool isSwingLow = lowSeries[1].ApproxCompare(lowSeries[0]) <= 0
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

                double saveValue = 0.0;
                bool addHigh = false;
                bool addLow = false;
                bool updateHigh = false;
                bool updateLow = false;

                if (
                    priceListsZones.Any() &&
                    !double.IsNaN(highSeries[5]) &&
                    !double.IsNaN(highSeries[5])
                )
                {
                    List<Zone> updatedZonesExtending = priceListsZones.Select(zone =>
                    {
                        var zoneWithHighestPrice = priceListsZones
                        .OrderByDescending(zone => zone.MaxOrMinPrice)
                        .FirstOrDefault();

                        Print($"zoneWithHighestPrice = {zoneWithHighestPrice.MaxOrMinPrice}$");

                        if (
                            zone.IsResistenceZone() &&
                            IsPriceGreaterThanCurrentMaxHighPrice(
                            highSeries[5], zone.MaxOrMinPrice)
                        )
                        {
                            if (
                                CurrentBar >= ChartBars.FromIndex &&
                                zone.MaxOrMinPrice < zoneWithHighestPrice.MaxOrMinPrice    
                            )
                            {
                                bool isZoneWithinBarRange = IsZoneWithinBarHighRange(
                                    zone, highSeries[5]
                                );

                                Print($"precio del highSeries 5 = {highSeries[5]}$");
                                //Print($"precio máximo actual = {currentMaxHighPrice}$");

                                Print($"precio de la zona alcista superada = {zone.MaxOrMinPrice}$");

                                if (
                                    isZoneWithinBarRange && 
                                    !BullishHighBreakoutHasConfirmation(
                                        highSeries, CurrentBar - 6
                                    )
                                )
                                {
                                    Print(
                                    $"editando dimensiones de la zona intermedia al alza =" +
                                    $"{zone.Id} maxOrMinPrice = {zone.MaxOrMinPrice} type " +
                                    $"={zone.Type}"
                                    );

                                    highBreakBar = CurrentBar;
                                    double maxValue = highSeries[5];
                                    zone.MaxOrMinPrice = maxValue;
                                    zone.RedrawHighZoneIsRequired = true;

                                    highBrokenAccumulated += 1;
                                    Print($"highBrokenAccumulated = {highBrokenAccumulated}");
                                }
                                else if (
                                    isZoneWithinBarRange
                                )
                                {
                                    Print($"Rompiendo resistencia = {zone.Id}");
                                    double lastPriceClose = zone.ClosePrice;
                                    double lastMaxOrMinPrice = zone.MaxOrMinPrice;

                                    zone.MaxOrMinPrice = lastPriceClose;
                                    zone.ClosePrice = lastMaxOrMinPrice;

                                    zone.IsResistenceBreakout = true;
                                    zone.Type = Zone.ZoneType.Support;
                                    zone.HighlightBrush = Brushes.Red.Clone();
                                    zone.HighlightBrush.Opacity = 0.3;

                                    RemoveDrawObject("RegionHighLightY" + zone.Id);

                                    Draw.RegionHighlightY(
                                        this,                   // Contexto del indicador o estrategia
                                        "RegionLowLightY" + zone.Id,  // Nombre único para la región
                                        zone.ClosePrice,              // Nivel de precio superior
                                        zone.MaxOrMinPrice,           // Nivel de precio inferior
                                        zone.HighlightBrush        // Pincel para el color de la región
                                    );
                                }
                            }
                        }
                        else if (
                            !zone.IsResistenceZone() &&
                            IsPriceLessThanCurrentMinLowPrice(lowSeries[5], zone.MaxOrMinPrice)
                        )
                        {
                            var zoneWithLowestPrice = priceListsZones
                            .OrderBy(zone => zone.MaxOrMinPrice)
                            .FirstOrDefault();

                            Print($"zoneWithLowestPrice = {zoneWithLowestPrice.MaxOrMinPrice}$");
                            
                            if (
                                CurrentBar >= ChartBars.FromIndex &&
                                zone.MaxOrMinPrice > zoneWithLowestPrice.MaxOrMinPrice
                            )
                            {
                                bool isZoneWithinBarRange = IsZoneWithinBarLowRange(
                                    zone, lowSeries[5]
                                );


                                Print($"precio del lowSeries 5 = {lowSeries[5]}");
                                //Print($"precio mínimo actual = {currentMinLowPrice}");

                                Print($"precio de la zona bajista superada = {zone.MaxOrMinPrice}");

                                if (
                                    isZoneWithinBarRange && 
                                    !BearishLowBreakoutHasConfirmation(
                                        lowSeries, CurrentBar - 6
                                    )
                                )
                                {
                                    Print(
                                    $"editando dimensiones de la zona intermedia a la baja =" +
                                    $" {zone.Id} maxOrMinPrice = {zone.MaxOrMinPrice}"
                                    );

                                    lowBreakBar = CurrentBar;
                                    double minValue = lowSeries[5];
                                    zone.MaxOrMinPrice = minValue;
                                    zone.RedrawLowZoneIsRequired = true;

                                    lowBrokenAccumulated += 1;
                                    Print($"lowBrokenAccumulated = {lowBrokenAccumulated}");
                                }
                                else if (
                                    isZoneWithinBarRange
                                )
                                {
                                    Print($"Rompiendo soporte = {zone.Id}");
                                    double lastMaxOrMinPrice = zone.MaxOrMinPrice;
                                    double lastPriceClose = zone.ClosePrice;

                                    zone.ClosePrice = lastMaxOrMinPrice;
                                    zone.MaxOrMinPrice = lastPriceClose;

                                    zone.IsSupportBreakout = true;
                                    zone.Type = Zone.ZoneType.Resistance;
                                    zone.HighlightBrush = Brushes.Green.Clone();
                                    zone.HighlightBrush.Opacity = 0.3;

                                    RemoveDrawObject("RegionLowLightY" + zone.Id);

                                    Draw.RegionHighlightY(
                                        this,                    // Contexto del indicador o estrategia
                                        "RegionHighLightY" + zone.Id, // Nombre único para la región
                                        zone.ClosePrice,              // Nivel de precio inferior
                                        zone.MaxOrMinPrice,           // Nivel de precio superior
                                        zone.HighlightBrush       // Pincel para el color de la región
                                    );
                                }
                            }
                        }

                        return zone;
                    })
                    .ToList();
                    priceListsZones = updatedZonesExtending;
                }

                if (IsPriceGreaterThanCurrentMaxHighPrice(highSeries[1]))
                {
                    Print("El precio máximo acaba de ser roto");
                    if (
                        CurrentBar >= ChartBars.FromIndex
                    )
                    {
                        currentMaxHighPrice = highSeries[1];
                        //highCandleStartTime = Time[0];
                        Print("se ha generado un rompimiento alcista");
                        maxHighBreakBar = CurrentBar;
                        //highBreakBarList.Add(CurrentBar);

                        resistenceZoneBreakoutPrice = highSeries[1];
                        currentClosingHighPrice = Open[1] >= Close[1] ? Open[1] : Close[1];
                        //Print($"currentClosingHighPrice = {currentClosingHighPrice}");
                        Draw.Text(
                            this,              // La referencia al indicador o estrategia actual
                            "maxPriceBarText", // Un identificador único para el texto
                            $"Bar: {maxHighBreakBar} MaxPrice: {resistenceZoneBreakoutPrice}$",
                            // El texto a dibujar
                            1, // El índice de la barra donde se dibuja (0 es la barra actual)
                            highSeries[1] + TickSize,  // (encima del máximo de la barra actual)
                            Brushes.Green  // El color del texto
                        );

                        maxHighBrokenAccumulated += 1;
                        Print($"maxHighBrokenAccumulated = {maxHighBrokenAccumulated}");

                    }

                }
                else if (IsPriceLessThanCurrentMinLowPrice(lowSeries[1]))
                {
                    Print("El precio mínimo acaba de ser roto");

                    if (
                        CurrentBar >= ChartBars.FromIndex
                    )
                    {
                        currentMinLowPrice = lowSeries[1];
                        //lowCandleStartTime = Time[0];
                        Print("se ha generado un rompimiento bajista");
                        minLowBreakBar = CurrentBar;
                        // lowBreakBarList.Add(CurrentBar);

                        supportZoneBreakoutPrice = lowSeries[1];
                        currentClosingLowPrice = Close[1] <= Open[1] ? Close[1] : Open[1];
                        //Print($"currentClosingLowPrice = {currentClosingLowPrice}");

                        Draw.Text(
                            this,              // La referencia al indicador o estrategia actual
                            "minPriceBarText", // Un identificador único para el texto
                            $"Bar: {minLowBreakBar} MinPrice: {supportZoneBreakoutPrice}$", // El texto a dibujar
                            1,                 // El índice de la barra donde se dibuja (0 es la barra actual)
                            lowSeries[1] + TickSize,  // (encima del máximo de la barra actual)
                            Brushes.DarkRed  // El color del texto
                        );

                        minLowBrokenAccumulated += 1;
                        Print($"minLowBrokenAccumulated = {minLowBrokenAccumulated}");
                    }

                }

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

                        //highBrokenAccumulated = highSeries[1] <= resistenceZoneBreakoutPrice ? 0 : highBrokenAccumulated;

                    }
                    else if (addLow || updateLow)
                    {
                        zigZagLowZigZags[1] = saveValue;
                        currentZigZagLow = saveValue;
                        zigZagLowSeries[1] = currentZigZagLow;
                        Value[1] = currentZigZagLow;

                        //lowBrokenAccumulated = lowSeries[1] >= supportZoneBreakoutPrice ? 0 : lowBrokenAccumulated;
                    }

                    lastSwingIdx = CurrentBar - 1;
                    lastSwingPrice = Input[1];

                    Print($"currentMaxPrice = {currentMaxHighPrice}");
                    Print($"currentMinPrice = {currentMinLowPrice}");

                    //Print($"currentZigZagHigh = {currentZigZagHigh}");
                    //Print($"currentZigZagLow = {currentZigZagLow}");
                }

                //TimeSpan highTimeElapsed = currentTime - highCandleStartTime;
                bool readrawHighStablished = priceListsZones.Any(
                    zone => zone.RedrawHighZoneIsRequired == true
                );

                if (
                    (
                    maxHighBrokenAccumulated >= 2 || 
                    CurrentBar > maxHighBreakBar + 5) &&
                    lowSeries[0] < lowSeries[1]
                )
                {
                    Print("se ha confirmado un rompimiento alcista");
                    int resistenceCount = priceListsZones
                    .Where(zone => zone.Type == Zone.ZoneType.Resistance)
                    .Count();

                    Print($"procesando superación de precio alcista máxima {resistenceZoneBreakoutPrice}");
                    bool breakoutHasConfirmation = 
                    BullishHighBreakoutHasConfirmation(highSeries, maxHighBreakBar);

                    Print($"breakoutHasConfirmation: {breakoutHasConfirmation}");
                    Print($"maxHighBrokenAccumulated >= 2: {maxHighBrokenAccumulated >= 2}");
                    Print($"resistenceCount == 0: {resistenceCount == 0}");

                    if (
                        breakoutHasConfirmation ||
                        maxHighBrokenAccumulated >= 2 ||
                        resistenceCount == 0
                    )
                    {
                        Print("breaking high zone...");
                        isBullishBreakoutConfirmed = true;
                    }
                    else
                    {
                        Print("extending resistence zone...");
                        isHighZoneExtended = true;
                    }

                    maxHighBrokenAccumulated = 0;
                }
                else if (
                    !BullishHighBreakoutHasConfirmation(highSeries, CurrentBar - 6) &&
                    (
                    highBrokenAccumulated >= 2 || 
                    CurrentBar - 5 > highBreakBar) &&
                    lowSeries[0] < lowSeries[1] &&
                    readrawHighStablished
                )
                {
                    Print("se ha confirmado una redimensión de zona alcista intermedia");
                    redrawHighZoneIsRequired = true;
                    highBreakBar = int.MaxValue - 6;
                    highBrokenAccumulated = 0;
                }

                //TimeSpan lowTimeElapsed = currentTime - lowCandleStartTime;
                bool readrawLowStablished = priceListsZones.Any(
                    zone => zone.RedrawLowZoneIsRequired == true
                );

                if (
                    (
                    minLowBrokenAccumulated >= 2 ||
                    CurrentBar > minLowBreakBar + 5) &&
                    highSeries[0] > highSeries[1]
                )
                {
                    Print("se ha confirmado un rompimiento bajista");
                    int supportCount = priceListsZones
                    .Where(zone => zone.Type == Zone.ZoneType.Support)
                    .Count();

                    if (
                        BearishLowBreakoutHasConfirmation(lowSeries, minLowBreakBar) ||
                        minLowBrokenAccumulated >= 2 ||
                        supportCount == 0
                    )
                    {
                        Print("breaking low zone...");
                        isBearishBreakoutConfirmed = true;
                    }
                    else
                    {
                        Print("extending support zone...");
                        isLowZoneExtended = true;
                    }

                    minLowBrokenAccumulated = 0;
                }
                else if (
                   !BearishLowBreakoutHasConfirmation(lowSeries, CurrentBar - 6) &&
                   (
                   lowBrokenAccumulated >= 2 || 
                   CurrentBar - 5 > lowBreakBar) &&
                   highSeries[0] > highSeries[1] &&
                   readrawLowStablished
               )
                {
                    Print("se ha confirmado una redimensión de zona bajista intermedia");
                    redrawLowZoneIsRequired = true;
                    lowBreakBar = int.MaxValue - 6;
                    lowBrokenAccumulated = 0;
                }

                zigZagHighSeries[0] = currentZigZagHigh;
                zigZagLowSeries[0] = currentZigZagLow;

                // Dibuja la línea vertical en la barra actual cuando el script se reinicia
                if (BarsInProgress == 0)
                {
                    if (!isChartLoaded)
                    {
                        // Dibujar la línea en la barra actual cuando el gráfico se ha cargado
                        // por completo
                        DrawStartVerticalLine();

                        // Cambiar el estado para evitar dibujar la línea más de una vez
                        isChartLoaded = true;
                    }
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

        private bool IsPriceGreaterThanCurrentMaxHighPrice(double lastPrice, double currentMaxHighPrice = double.NaN)
        {
            // Si el parámetro currentMaxHighPrice es NaN, utiliza el atributo de clase currentMaxHighPrice
            if (double.IsNaN(currentMaxHighPrice))
            {
                currentMaxHighPrice = this.currentMaxHighPrice; // Atributo de clase
            }

            // Compara lastPrice con el currentMaxHighPrice adecuado
            bool lastPriceIsGreaterThanMaxHighPrice = lastPrice
            .ApproxCompare(currentMaxHighPrice) > 0;

            Print($"{lastPrice} > {currentMaxHighPrice} = {lastPriceIsGreaterThanMaxHighPrice}");

            return lastPriceIsGreaterThanMaxHighPrice;
        }

        private bool IsPriceLessThanCurrentMinLowPrice(double lastPrice, double currentMinLowPrice = double.NaN)
        {
            if (double.IsNaN(currentMinLowPrice))
            {
                currentMinLowPrice = this.currentMinLowPrice;
            }

            bool lastPriceIsLessThanMinLowPrice = lastPrice
            .ApproxCompare(currentMinLowPrice) < 0;

            Print($"{lastPrice} < {currentMinLowPrice} = {lastPriceIsLessThanMinLowPrice}");
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

                    //Validar si es una movimiento alcista y si el valor de la vela es mayor o igual al último precio                    extendiendo región de la resistencia
                    if (isHigh && isHighZoneExtended && priceListsZones.Any())
                    {

                        //Print($"priceListsZones.count = {priceListsZones.Count()}");
                        currentZone = priceListsZones.LastOrDefault(
                            zone => zone.Type == Zone.ZoneType.Resistance
                        );

                        if (currentZone != null)
                        {
                            currentZone.MaxOrMinPrice = resistenceZoneBreakoutPrice;

                            Print("extendiendo región de la resistencia");

                            Print(
                                $"MaxOrMinPrice = {currentZone.MaxOrMinPrice}, ClosePrice = {currentZone.ClosePrice}, " +
                                $"lastMaxHighPrice = {lastMaxHighPrice}, lastClosingHighPrice = {lastClosingHighPrice}"
                            );


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

                            //: Actualizar máximo preció alcnazado
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
                        else
                        {
                            //Print($"isBullishBreakoutConfirmed = {isBullishBreakoutConfirmed}");
                            Print("Current high zone is null");
                        }

                        maxHighBreakBar = int.MaxValue - 6;
                        isHighZoneExtended = false;
                    }
                    else if (isHigh && isBullishBreakoutConfirmed)
                    {
                        Print("Generando resistencia");

                        if (
                            PriceIsNotBetweenGeneratedPrice(
                            resistenceZoneBreakoutPrice, currentClosingHighPrice,
                            lastMaxHighPrice, lastClosingHighPrice
                            )
                        )
                        {

                            currentZone = new Zone(
                                Zone.ZoneType.Resistance,
                                currentClosingHighPrice,
                                resistenceZoneBreakoutPrice
                            );

                            priceListsZones.Add(currentZone);

                            Print($"resistencia: Y = {currentZone.ClosePrice} maxPrice ={currentZone.MaxOrMinPrice}");
                            Print($"lastClosingHighPrice = {lastClosingHighPrice} lastMaxHighPrice = {lastMaxHighPrice}");


                            List<Zone> updatePriceListZones = priceListsZones.Select(zone =>
                            {
                                bool priceIsNotsbetween = PriceIsNotBetweenGeneratedPrice(
                                    zone.MaxOrMinPrice, zone.ClosePrice,
                                    lastMaxHighPrice, lastClosingHighPrice
                                );

                                //Print($"priceIsNotBetween = {priceIsNotsbetween}");
                                Print("before breakout: ");
                                Print($"Type = {zone.Type} ID = {zone.Id} " +
                                $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                $"supportBreakout = {zone.IsSupportBreakout}");

                                if (zone.IsResistenceZone())
                                {
                                    Print("after breakout: ");
                                    Print($"Type = {zone.Type} ID = {zone.Id} " +
                                    $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                    $"supportBreakout = {zone.IsSupportBreakout} " +
                                    $"maxOrMinPrice = {zone.MaxOrMinPrice}");

                                    if (
                                        zone.MaxOrMinPrice == resistenceZoneBreakoutPrice
                                        && priceIsNotsbetween
                                    )
                                    {

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
                                            $"con el precio mínimo de: {zone.MaxOrMinPrice} y el " +
                                            $"precio de cierre: {zone.ClosePrice}"
                                        );

                                        double lastPriceClose = zone.ClosePrice;
                                        double lastMaxOrMinPrice = zone.MaxOrMinPrice;

                                        zone.MaxOrMinPrice = lastPriceClose;
                                        zone.ClosePrice = lastMaxOrMinPrice;

                                        zone.IsResistenceBreakout = true;
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

                        } 
                        else 
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
                        }

                        lastMaxHighPrice = currentMaxHighPrice;
                        lastClosingHighPrice = currentClosingHighPrice;
                        CalculateCurrentMinOrMaxPrice();
                        maxHighBreakBar = int.MaxValue - 6;
                        isBullishBreakoutConfirmed = false;
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

                        //Print($"zigZagLowSeries[4]: {supportZoneBreakoutPrice}");

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

                            //: Actualizar máximo preció alcnazado
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
                        else
                        {
                            // Print($"isBearishBreakoutConfirmed = {isBearishBreakoutConfirmed}");
                            // Print("Current low zone is null");
                        }

                        minLowBreakBar = int.MaxValue - 6;
                        isLowZoneExtended = false;
                    }
                    else if (isLow && isBearishBreakoutConfirmed)
                    {
                        Print("Generando soporte");
                        
                        Print($"lastMinLowPrice = {lastMinLowPrice} lastClosingLowPrice = {lastClosingLowPrice} supportZoneBreakoutPrice = {supportZoneBreakoutPrice} currentClosingLowPrice = {currentClosingLowPrice}");
                        
                        if (
                            PriceIsNotBetweenGeneratedPrice(
                            lastMinLowPrice, lastClosingLowPrice,
                            supportZoneBreakoutPrice, currentClosingLowPrice
                            )
                        )
                        {
                            currentZone = new Zone(
                                Zone.ZoneType.Support,
                                currentClosingLowPrice,
                                supportZoneBreakoutPrice
                            );

                            priceListsZones.Add(currentZone);

                            Print($"soporte: ClosePrice = {currentZone.ClosePrice} " +
                            $"minPrice = {currentZone.MaxOrMinPrice}");
                            Print($"currentClosingLowPrice = {currentClosingLowPrice} currentMinLowPrice = {currentMinLowPrice}");


                            List<Zone> updatePriceListZones = priceListsZones.Select(zone =>
                            {
                                bool priceIsNotsbetween = PriceIsNotBetweenGeneratedPrice(
                                    lastMinLowPrice, lastClosingLowPrice,
                                    zone.MaxOrMinPrice, zone.ClosePrice
                                );

                                Print("before breakout: ");
                                Print($"Type = {zone.Type} ID = {zone.Id} " +
                                $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                $"supportBreakout = {zone.IsSupportBreakout}");

                                if (!zone.IsResistenceZone())
                                {
                                    Print("after breakout: ");
                                    Print($"Type = {zone.Type} ID = {zone.Id} " +
                                    $"resistenceBreakout = {zone.IsResistenceBreakout} " +
                                    $"supportBreakout = {zone.IsSupportBreakout}");

                                    if (
                                        zone.MaxOrMinPrice == supportZoneBreakoutPrice
                                        && priceIsNotsbetween
                                    )
                                    {

                                        /*Draw.Text(
                                                this,               // La referencia al indicador o estrategia actual
                                                "closingMinPriceText" + zone.Id, // Un identificador único para el texto
                                                $"{zone.ClosePrice}",   // Texto
                                                minLowBreakBar,         // El índice de la barra donde se dibuja 
                                                zone.ClosePrice + TickSize, // La posición vertical 
                                                Brushes.DarkRed          // El color del texto
                                            );
                                            */

                                        // Dibujar zonas de soporte 
                                        Draw.RegionHighlightY(
                                            this,                 // Contexto del indicador o estrategia
                                            "RegionLowLightY" + zone.Id,  // Nombre único para la región
                                            zone.MaxOrMinPrice,           // Nivel de precio inferior
                                            zone.ClosePrice,              // Nivel de precio superior
                                            zone.HighlightBrush     // Pincel para el color de la región
                                        );

                                        /*Draw.Text(
                                                this,       // La referencia al indicador o estrategia actual
                                                "lowMinPriceText" + zone.Id,// Un identificador único para el texto
                                                $"{zone.MaxOrMinPrice}", // Texto
                                                minLowBreakBar,           // El índice de la barra donde se dibuja 
                                                zone.MaxOrMinPrice + TickSize, // La posición vertical 
                                                Brushes.DarkRed          // El color del texto
                                            );
                                            */

                                        //CalculateCurrentMinPrice();
                                    }
                                    else if (
                                        zone.MaxOrMinPrice > supportZoneBreakoutPrice
                                    )
                                    {
                                        Print(
                                        $"La zona {zone.Id} está siendo convertida a resistencia " +
                                        $"con el precio máximo de: {zone.MaxOrMinPrice} y el " +
                                        $"precio de cierre: {zone.ClosePrice}"
                                        );

                                        double lastMaxOrMinPrice = zone.MaxOrMinPrice;
                                        double lastPriceClose = zone.ClosePrice;

                                        zone.ClosePrice = lastMaxOrMinPrice;
                                        zone.MaxOrMinPrice = lastPriceClose;

                                        zone.IsSupportBreakout = true;
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

                        }

                        lastMinLowPrice = currentMinLowPrice;
                        Print($"lastMinLowPrice after update = {lastMinLowPrice}");
                        lastClosingLowPrice = currentClosingLowPrice;
                        CalculateCurrentMinOrMaxPrice();
                        minLowBreakBar = int.MaxValue - 6;
                        isBearishBreakoutConfirmed = false;
                    }

                    if (redrawHighZoneIsRequired)
                    {
                        Print("redibujando dimensiones de la zona intermedia al alza");
                        List<Zone> updatePriceListsZones = priceListsZones.Select(zone =>
                        {
                            if (zone.IsResistenceZone() && zone.RedrawHighZoneIsRequired)
                            {
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
                                    Draw.RegionHighlightY(
                                        this,                   // Contexto del indicador o estrategia
                                        "RegionLowLightY" + zone.Id, // Nombre único para la región
                                        zone.ClosePrice,        // Nivel de precio inferior
                                        zone.MaxOrMinPrice,     // Nivel de precio superior
                                        zone.HighlightBrush     // Pincel para el color de la región
                                    );

                                }
                                // Dibujar zonas de soporte y resistencia
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
                        // Atualizar zonas rotas
                        List<Zone> breakoutsZonesUpdated = priceListsZones.Select(zone => {
                            if (zone.IsResistenceZone())
                            {
                                bool breakoutCondition = currentZigZagLow > zone.MaxOrMinPrice;
                                
                                if (
                                    breakoutCondition
                                )
                                {
                                    Print($"Rompiendo zona de la resistencia = {zone.Id}");
                                    zone.IsResistenceBreakout = true;
                                }
                            }
                            else
                            {
                                bool breakoutCondition = currentZigZagHigh < zone.MaxOrMinPrice;

                                if (
                                    breakoutCondition
                                )
                                {
                                    Print($"currentZigZagHigh = {currentZigZagHigh} zone.MaxOrMinPrice " +
                                    $"= {zone.MaxOrMinPrice}");
                                    Print($"currentZigZagHigh < zone.MaxOrMinPrice {zone.Id} = " +
                                    $"{breakoutCondition}");

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
                                //RemoveDrawObject("RegionLowLightY" + zone.Id);
                                RemoveDrawObject("RegionHighLightY" + zone.Id);
                                //Print("currentMaxHighPrice =" + currentMaxHighPrice);
                                return true; // Eliminar zona
                            }
                            else if (
                                !zone.IsResistenceZone() &&
                                (zone.IsResistenceBreakout && zone.IsSupportBreakout) 
                            )
                            {
                                Print($"eliminando soporte = {zone.Id}");
                                RemoveDrawObject("RegionLowLightY" + zone.Id);
                                //RemoveDrawObject("RegionHighLightY" + zone.Id);
                                //Print("currentMinLowPrice = " + currentMinLowPrice);
                                return true; // Eliminar zona
                            }

                            return false; // No eliminar zona
                        });

                        CalculateCurrentMaxPrice();
                        CalculateCurrentMinPrice();
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
        public Brush HighlightBrush
        {
            get; set;
        } // Color de la zona
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
        public bool LowZoneHadConsequence
        {
            get; set;
        }
        public bool HighZoneHadConsequence
        {
            get; set;
        }
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

            IsResistenceBreakout = false;
            IsSupportBreakout = false;
            HighZoneHadConsequence = false;
            LowZoneHadConsequence = false;

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
