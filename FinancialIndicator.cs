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
        // Variable para controlar si estamos en tiempo real y debemos generar ZigZag
        private bool isRealtimeZigZagActive = false;

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
        // Precio máximo de la barra del primer swing alcista
        //private double firstSwingHighBarPrice;
        // Valor de cierre de la última vela que rompe al alza 
        private double currentClosingHighPrice;
        // Valor de cierre anterior de la última vela que rompe al alza
        private double lastClosingHighPrice;
        // Antepenultimo valor máximo generado duranta la sesión
        private double lastMaxHighPrice;

        // Ultimo valor mínimo alcanzado durante la sesión
        private double currentMinLowPrice = double.MinValue;
        // Precio mínimo de la barra del primer swing alcista
        //private double firstSwingLowBarPrice;
        
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
        // Establece valores para dibujar nuevo movimiento al alza y la baja
        private bool itIsABullishPullback = false;
        private bool itIsABearishPullback = false;
        private bool isHighBreakoutPendingToConfirmation = false;
        private bool isLowBreakoutPendingToConfirmation = false;
        private bool isHighZoneExtended = false;
        private bool isLowZoneExtended = false;
        private bool redrawHighZoneIsRequired = false;
        private bool redrawLowZoneIsRequired = false;
        
        // VARIABLES SEÑUELO: Almacenan el precio máximo/mínimo anterior antes del dibujo de zona
        // para evitar que se use el precio de velas posteriores durante el extendimiento
        private double previousMaxHighPrice = double.MinValue;
        private double previousMinLowPrice = double.MaxValue;
        private bool isBullishBreakoutConfirmed = false;
        private bool isBearishBreakoutConfirmed = false;
        private bool isTheFirstSwingHigh = false;
        private bool isTheFirstSwingLow = false;
        //private bool isTheFirstBarToAnalize = true;
        private bool isSwingHigh;
        private bool isSwingLow;
        
        // Variables para manejar el caso especial de "V" cuando una vela supera por ambos picos
        private bool isCreatingVShape = false;
        private bool vShapeDrawn = false;
        private double vShapeHighPrice = 0;
        private double vShapeLowPrice = 0;
        private bool isConfirmationOfZoneBrokenUpwards;
        private bool isConfirmationOfZoneBrokenDownSide;
        private bool currentCandleIsDoji;

        private double lastHistHigh;
        private double lastHistLow;

        // Al inicio del archivo, agrega una variable de control para evitar dibujar múltiples veces:
        private bool initialZigZagLineDrawn = false;

        // 1. Agregar variables (a nivel de clase):
        private bool pendingFirstZigZagLine = false;
        private double initialY0, initialY1;
        private int initialBarIdx0, initialBarIdx1;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicador para dibujar líneas en regresiones de velas.";
                Name = "CustomIndicatorTest2";
                Calculate = Calculate.OnBarClose;
                DeviationType = DeviationType.Points;
                BarsRequiredToPlot = 5;
                //isTheFirstBarToAnalize = true;  // Inicializamos para saber cuándo se ha cargado el gráfico
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

                currentClosingHighPrice = double.MinValue;
                lastMaxHighPrice = double.MinValue;
                lastClosingHighPrice = double.MinValue;
                previousMaxHighPrice = double.MinValue;
                //firstSwingHighBarPrice = double.MaxValue;
                maxHighBrokenAccumulated = 0;
                highBrokenAccumulated = 0;

                currentClosingLowPrice = double.MaxValue;
                lastMinLowPrice = double.MaxValue;
                lastClosingLowPrice = double.MaxValue;
                previousMinLowPrice = double.MaxValue;
                //firstSwingLowBarPrice = double.MinValue;
                minLowBrokenAccumulated = 0;
                lowBrokenAccumulated = 0;

                lastSwingIdx = -1;
                lastSwingPrice = 0.0;
                trendDir = 0; // 1 = trend up, -1 = trend down, init = 0
                isConfirmationOfZoneBrokenUpwards = false;
                isConfirmationOfZoneBrokenDownSide = false;
                isSwingHigh = false;
                isSwingLow = false;
                
                // Inicializar variables para forma de "V"
                isCreatingVShape = false;
                vShapeDrawn = false;
                vShapeHighPrice = 0;
                vShapeLowPrice = 0;
                
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
                DrawStartVerticalLine();
                
                // ACTIVAR ZIGZAG EN TIEMPO REAL
                isRealtimeZigZagActive = true;
                startVerticalLineIndex = CurrentBar;
                
                // Reiniciar estado del ZigZag para comenzar desde cero
                ResetZigZagState();
                
                Print($"ZigZag activado en tiempo real desde la barra #{CurrentBar}");

                pendingFirstZigZagLine = false;
                if (Bars != null && Bars.Count > 1 && Highs[0].Count > 1 && Lows[0].Count > 1)
                {
                    initialBarIdx0 = CurrentBar;
                    initialBarIdx1 = CurrentBar - 1;
                    if (Open[0] > Close[0])
                    {
                        initialY0 = High[0];
                        initialY1 = High[1];
                    }
                    else if (Close[0] > Open[0])
                    {
                        initialY0 = Low[0];
                        initialY1 = Low[1];
                    }
                    else
                    {
                        initialY0 = Close[0];
                        initialY1 = Close[1];
                    }
                    pendingFirstZigZagLine = true;
                }
            }
        }

        protected void DrawStartVerticalLine()
        {
            Draw.VerticalLine(this, "StartVerticalLine", 0, Brushes.Yellow, DashStyleHelper.Dash, 2);
        }
        private void ResetZigZagState()
        {
            // Reiniciar precios
            currentMaxHighPrice = double.MinValue;
            currentMinLowPrice = double.MaxValue;
            
            // Reiniciar flags de primer swing
            isTheFirstSwingHigh = true;
            isTheFirstSwingLow = true;
            
            // Reiniciar estado del ZigZag
            lastSwingPrice = 0.0;
            lastSwingIdx = -1;
            trendDir = 0;
            
            // Reiniciar confirmaciones
            isConfirmationOfZoneBrokenUpwards = false;
            isConfirmationOfZoneBrokenDownSide = false;
            isSwingHigh = false;
            isSwingLow = false;
            
            // Reiniciar variables para forma de "V"
            isCreatingVShape = false;
            vShapeDrawn = false;
            vShapeHighPrice = 0;
            vShapeLowPrice = 0;
            
            // Reiniciar contadores
            maxHighBrokenAccumulated = 0;
            highBrokenAccumulated = 0;
            minLowBrokenAccumulated = 0;
            lowBrokenAccumulated = 0;
            
            // Reiniciar precios de cierre
            currentClosingHighPrice = double.MinValue;
            lastMaxHighPrice = double.MinValue;
            lastClosingHighPrice = double.MinValue;
            previousMaxHighPrice = double.MinValue;
            
            currentClosingLowPrice = double.MaxValue;
            lastMinLowPrice = double.MaxValue;
            lastClosingLowPrice = double.MaxValue;
            previousMinLowPrice = double.MaxValue;
            
            // Reiniciar precios de rompimiento
            resistenceZoneBreakoutPrice = 0;
            supportZoneBreakoutPrice = double.MaxValue;

            currentCandleIsDoji = false;
            
            // Reiniciar barras de rompimiento
            maxHighBreakBar = 0;
            minLowBreakBar = 0;

            lastHistHigh = 0;
            lastHistLow = 0;
            
            // Limpiar listas
            priceListsZones.Clear();
            pendingListOfBreakouts.Clear();
            
            // Reiniciar series de ZigZag
            if (zigZagHighZigZags != null)
                zigZagHighZigZags.Reset();
            if (zigZagLowZigZags != null)
                zigZagLowZigZags.Reset();
            if (zigZagHighSeries != null)
                zigZagHighSeries.Reset();
            if (zigZagLowSeries != null)
                zigZagLowSeries.Reset();
            
            Print("Estado del ZigZag reiniciado para tiempo real");
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
            // VALIDACIÓN: Verificar que priceListsZones no esté vacío antes de usar .Max()
            if (priceListsZones.Any())
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
        }
        public void CalculateCurrentMinPrice()
        {
            // VALIDACIÓN: Verificar que priceListsZones no esté vacío antes de usar .Min()
            if (priceListsZones.Any())
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
        private int GetResistanceCount()
        {
            // Validar si ya se han generado resistencias
            int resistenceCount =
            priceListsZones.Where(zone => zone.Type ==
                Zone.ZoneType.Resistance)
            .Count();

            return resistenceCount;
        }

        // Método para encontrar el índice de la última vela que no fue doji antes de la barra actual
        int FindLastNonDojiCandleIndex(int startIndex)
        {
            for (int i = startIndex; i < Bars.Count; i++)
            {
                // Una vela no es doji si el cierre y la apertura no son iguales
                if (Close[i] != Open[i])
                    return i;
            }
            return -1; // No se encontró ninguna vela no doji
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
            //bool is_the_latest_bar = false;
            // SOLO PROCESAR ZIGZAG SI ESTAMOS EN TIEMPO REAL Y LA BARRA ES NUEVA
            if (!isRealtimeZigZagActive)
            {
                Print($"startVerticalLineIndex = {startVerticalLineIndex}");
                Print($"ultima barra omitida antes de entrar en tiempo real {CurrentBar}");

                lastHistHigh = High[0];
                lastHistLow = Low[0];
                // Si no estamos en tiempo real, no procesar ZigZag
                return;
            }
            // Initialization - Solo para la primera barra en tiempo real
            if (lastSwingPrice == 0.0 && CurrentBar == startVerticalLineIndex + 1)
            {
                // Establecer el valor del precio actual como punto de partida
                lastSwingPrice = Input[0];
                Print($"Inicializando ZigZag en tiempo real con precio inicial: ${lastSwingPrice} en barra #{CurrentBar}");

            }

            ISeries<double> highSeries = High;
            ISeries<double> lowSeries = Low;

            try
            {
                
                double currentSwingPrice = 0.0;
                bool addHigh = false;
                bool addLow = false;
                bool updateHigh = false;
                bool updateLow = false;

                // Determinar si la vela anterior es verde (alcista) y la actual es roja (bajista)
                // Vela verde: Close[1] > Open[1]
                // Vela roja:  Close[0] < Open[0]
                bool candleBeforePreviousOneIsBullish = false;
                bool previousCandleIsBullish = false;
                bool currentCandleIsBullish = false;

				// ¿Cambia la dirección? (mínimo actual retrocede más que el mínimo de la vela anterior)
				bool changeDir = false;

                // Determinar si el precio de cierre es igual al precio de apertura
                int lastNonDojiIndex = -1;
                // Validar si la última vela que no fue Doji y alcista
                bool lastNonDojiIsBullish = false;
                // Buscar la última vela no doji antes de la actual
                lastNonDojiIndex = FindLastNonDojiCandleIndex(1); // Comenzar desde la anterior a la actual
                
                if (lastNonDojiIndex != -1)
                {
                    //Print($"lastNonDojiIndex = {lastNonDojiIndex}");
                    // Determinar si la última vela no doji fue alcista o bajista
                    lastNonDojiIsBullish = Close[lastNonDojiIndex] > Open[lastNonDojiIndex];
                    //previousCandleMinPrice = lastNonDojiIsBullish ? lowSeries[1] : highSeries[1];
                }
                // Establecer precios iniciales solo para la primera barra en tiempo real
                if (CurrentBar == startVerticalLineIndex + 1)
                {
                    Print($"=== ZIGZAG ACTIVADO EN TIEMPO REAL - Procesando barra #{CurrentBar} ===");

                    currentMaxHighPrice = highSeries[1];
                    currentMinLowPrice = lowSeries[1];
                    Print($"Estableciendo precios iniciales - Max: ${currentMaxHighPrice}, Min: ${currentMinLowPrice} en barra #{CurrentBar}");
                }
                else if(CurrentBar >= startVerticalLineIndex + 2){
                    
                    // valida si la vela actual es Doji
                    currentCandleIsDoji = !(Close[0] < Open[0]) && !(Close[0] > Open[0]);
                    candleBeforePreviousOneIsBullish = Close[2] > Open[2];
                    previousCandleIsBullish = Close[1] > Open[1] || lastNonDojiIsBullish;
                    currentCandleIsBullish = Close[0] > Open[0] || currentCandleIsDoji;
                }

                // Min por color de vela 
				double previousCandleMinPrice   = (Close[1] < Open[1] || !lastNonDojiIsBullish) ? highSeries[1] : lowSeries[1];
                double previousCandleMaxPrice   = (Close[1] < Open[1] || !lastNonDojiIsBullish) ? lowSeries[1]  : highSeries[1];
				//double currentCandleMaxPrice    = (Close[0] < Open[0] || !lastNonDojiIsBullish) ? lowSeries[0]  : highSeries[0];
                
                string typeOfCandle = previousCandleIsBullish ? "Bullish" : "Bearish";
                Print($"vela anterior es: {typeOfCandle}");

                // Comprueba si la barra del medio (highSeries[1]) es un pico, es decir, su valor es mayor o igual que las barras adyacentes.
                isSwingHigh = highSeries[0].ApproxCompare(highSeries[1]) > 0;
                // Comprueba si la barra del medio (lowSeries[1]) es un pico, es decir, su valor es mayor o igual que las barras adyacentes.
                isSwingLow = lowSeries[0].ApproxCompare(lowSeries[1]) < 0;

                itIsABearishPullback = false;
                itIsABullishPullback = false;

                // NUEVA LÓGICA ESPECIAL: Verificar si la vela actual supera por ambos picos a la anterior
                // Condición: vela actual supera tanto el máximo como el mínimo de la vela anterior
                bool currentCandleExceedsBothPeaks = isSwingHigh && isSwingLow;
                // Condición: vela antepenúltima supera tanto el máximo como el mínimo de la vela trasantepenúltima 
                bool lastCandleExceedsBothPeaks = highSeries[1] > highSeries[2] && lowSeries[1] < lowSeries[2];
            

                // Verifica si el valor de alto actual está por encima del último precio de swing más una desviación definida (DeviationValue), calculada en puntos o en porcentaje.
                bool isOverHighDeviation = (DeviationType == DeviationType.Percent && IsPriceGreater(highSeries[0], lastSwingPrice * (1.0 + DeviationValue))) || (DeviationType == DeviationType.Points && IsPriceGreater(highSeries[0], lastSwingPrice + DeviationValue));

                // Verifica si el valor de bajo actual está por debajo del último precio de swing menos una desviación definida (DeviationValue), calculada en puntos o en porcentaje.
                bool isOverLowDeviation = (DeviationType == DeviationType.Percent && IsPriceGreater(lastSwingPrice * (1.0 - DeviationValue), lowSeries[0])) || (DeviationType == DeviationType.Points && IsPriceGreater(lastSwingPrice - DeviationValue, lowSeries[0]));
   
                bool is_a_maximum_in_the_opposite_direction_to_the_upward_movement = candleBeforePreviousOneIsBullish && !previousCandleIsBullish;
                
                bool is_a_maximum_in_the_opposite_direction_to_the_downward_movement = !candleBeforePreviousOneIsBullish && previousCandleIsBullish;
                
                bool max_in_opposite_bearish_direction_is_exceeded = is_a_maximum_in_the_opposite_direction_to_the_downward_movement && highSeries[0] > highSeries[1];

                bool max_in_opposite_bullish_direction_is_exceeded = is_a_maximum_in_the_opposite_direction_to_the_upward_movement && lowSeries[0] < lowSeries[1]; 

                // Establece valores para actualizar dibujo del movimiento al alza y a la baja 
                bool itIsAnUpdatedBullishPullback = trendDir == 1 && isSwingHigh && IsPriceGreater
                (highSeries[0], lastSwingPrice);
                bool itIsAnUpdatedBearishPullback = trendDir == -1 && isSwingLow && !IsPriceGreater(lowSeries[0], lastSwingPrice);

                bool isPriceGreatherThanCurrentMaxHighPrice = 
                IsPriceGreaterThanCurrentMaxHighPrice(highSeries[0]);
                bool isPriceLessThanCurrentMinLowPrice = 
                IsPriceLessThanCurrentMinLowPrice(lowSeries[0]);

                // Actualizar precios máximos y mínimos alcanzados y guardar rompimientos pendientes a confirmar
                if (isPriceGreatherThanCurrentMaxHighPrice)
                {
                    //Actualiza el precio máximo alcanzado durante la sesión
                    currentMaxHighPrice = highSeries[0];
                    resistenceZoneBreakoutPrice = currentMaxHighPrice;   
                    currentClosingHighPrice = Open[0] >= Close[0] ? Open[0] : Close[0];
                    Print($"El precio máximo acaba de ser roto con el valor ${currentMaxHighPrice} en la barra #{CurrentBar}");
                    Print($"isHighBreakoutPendingToConfirmation = {isHighBreakoutPendingToConfirmation} ");

                    // Si no hay rompimientos al alza pendientes por completar añade un nuevo rompimiento a la cola de confirmación
                    if (!isHighBreakoutPendingToConfirmation 
                    //    && !isTheFirstSwingHigh
                    )
                    {
                        var newBreakout = new BreakoutCandidate
                        {
                            MaxBreakoutPrice = highSeries[0],
                            MinBreakoutPrice = lowSeries[0],
                            BreakoutBarIndex = CurrentBar,
                            Type = BreakoutCandidate.BreakoutType.Bullish,
                            BreakoutIsConfirmed = false
                        };

                        Print($"se ha generado un rompimiento alcista en la barra: {newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.MaxBreakoutPrice}");

                        pendingListOfBreakouts.Add(newBreakout);
                        isHighBreakoutPendingToConfirmation = true; 
                    }

                    NinjaTrader.NinjaScript.DrawingTools.Draw.Text(
                        this,              // La referencia al indicador o estrategia actual
                        "maxPriceBarText", // Un identificador único para el texto
                        $"Bar: {CurrentBar} MaxPrice: {currentMaxHighPrice}$",
                        // El texto a dibujar
                        0, // El índice de la barra donde se dibuja (0 es la barra actual)
                        highSeries[0] + TickSize,  // (encima del máximo de la barra actual)
                        Brushes.Green  // El color del texto
                    );

                    if (isTheFirstSwingHigh)
                        isTheFirstSwingHigh = false;

                }
                if (isPriceLessThanCurrentMinLowPrice)
                {
                    //Actualiza el precio máximo alcanzado durante la sesión
                    currentMinLowPrice = lowSeries[0];
                    supportZoneBreakoutPrice = currentMinLowPrice;
                    currentClosingLowPrice = Close[0] <= Open[0] ? Close[0] : Open[0];
                    Print($"El precio mínimo acaba de ser roto con el valor ${currentMinLowPrice} en la barra #{CurrentBar}");

                    // Si no hay rompimientos a la baja pendientes por completar añade un nuevo rompimiento a la cola de confirmación
                    if (!isLowBreakoutPendingToConfirmation)
                    {
                        var newBreakout = new BreakoutCandidate
                        {
                            MaxBreakoutPrice = lowSeries[0],
                            MinBreakoutPrice = highSeries[0],
                            BreakoutBarIndex = CurrentBar,
                            Type = BreakoutCandidate.BreakoutType.Bearish,
                            BreakoutIsConfirmed = false
                        };

                        Print($"se ha generado un rompimiento bajista en la barra: {newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.MaxBreakoutPrice}");

                        pendingListOfBreakouts.Add(newBreakout);
                        isLowBreakoutPendingToConfirmation = true;
                    }
                    
                    NinjaTrader.NinjaScript.DrawingTools.Draw.Text(
                        this,              // La referencia al indicador o estrategia actual
                        "minPriceBarText", // Un identificador único para el texto
                        $"Bar: {CurrentBar} MinPrice: {currentMinLowPrice}$", // El texto a dibujar
                        0,                 // El índice de la barra donde se dibuja (0 es la barra actual)
                        lowSeries[0] + TickSize,  // (encima del máximo de la barra actual)
                        Brushes.DarkRed  // El color del texto
                    );
                    

                    if (isTheFirstSwingLow)
                        isTheFirstSwingLow = false;

                }

                // Validación para evitar análisis de swings cuando no hay suficientes barras después del inicio del indicador
                // Necesitamos al menos 2 barras después del startVerticalLineIndex para analizar swings correctamente
                if (CurrentBar - startVerticalLineIndex < 2)
                {

                    double firstHigh = highSeries[1];
                    double firstLow = lowSeries[1];
                    double secondHigh = highSeries[0];
                    double secondLow = lowSeries[0];
                    /*
                    if(secondHigh > firstHigh){

                        zigZagHighZigZags[0] = secondHigh;
                        currentZigZagHigh    = secondHigh;
                        zigZagHighSeries[0]  = currentZigZagHigh;
                        Value[0]             = currentZigZagHigh;

                        trendDir = 1;
                    }
                    else if(secondLow < firstLow){

                        zigZagLowZigZags[0] = secondLow;
                        currentZigZagLow    = secondLow;
                        zigZagLowSeries[0]  = currentZigZagLow;
                        Value[0]            = currentZigZagLow;

                        trendDir = -1;
                    }                
                    else{
                        // No hay suficientes barras para analizar swings, saltar este análisis
                        zigZagHighSeries[0] = currentZigZagHigh;
                        zigZagLowSeries[0] = currentZigZagLow;
                    }
                    */

                    Print($"Saltando análisis de swings - Barras insuficientes: CurrentBar={CurrentBar}, startVerticalLineIndex={startVerticalLineIndex}, diferencia={CurrentBar - startVerticalLineIndex}");
                    //return;
                }

                // Guardar rompimientos intermedios pendientes a confirmar
                if (priceListsZones.Any())
                {
                    // Actualiza extendimientos de zonas intermedias 
                    List<Zone> updatedZonesExtending = priceListsZones.Select(zone =>
                    {
                        // Si es una resistencia intermedia y el precio de la zona es superado
                        if (
                            zone.IsResistenceZone() &&
                            zone.IsIntermediateZone &&
                            IsPriceGreaterThanCurrentMaxHighPrice(
                            highSeries[0], zone.MaxOrMinPrice)
                        )
                        {
                            // Si no hay una confirmación de rompimiento intermedio al alza pendiente lo agrega
                            if (!zone.IsBreakoutPendingToConfirmation)
                            {
                                var newBreakout = new BreakoutCandidate
                                {
                                    MaxBreakoutPrice = highSeries[0],
                                    MinBreakoutPrice = lowSeries[0],
                                    BreakoutBarIndex = CurrentBar,
                                    Type = BreakoutCandidate.BreakoutType.Bullish,
                                    IsIntermediateBreakout = true
                                };

                                Print($"se ha generado un rompimiento de zona alcista intermedia en la barra: {newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.MaxBreakoutPrice}");

                                pendingListOfBreakouts.Add(newBreakout);
                                zone.IsBreakoutPendingToConfirmation = true;
                            }
                        }
                        else if (
                            !zone.IsResistenceZone() &&
                            zone.IsIntermediateZone &&
                            IsPriceLessThanCurrentMinLowPrice(
                            lowSeries[0], zone.MaxOrMinPrice)
                        )
                        {
                            if (!zone.IsBreakoutPendingToConfirmation)
                            {
                                var newBreakout = new BreakoutCandidate
                                {
                                    MaxBreakoutPrice = lowSeries[0],
                                    MinBreakoutPrice = highSeries[0],
                                    BreakoutBarIndex = CurrentBar,
                                    Type = BreakoutCandidate.BreakoutType.Bearish,
                                    IsIntermediateBreakout = true
                                };

                                Print($"se ha generado un rompimiento de zona bajista intermedia en la barra: {newBreakout.BreakoutBarIndex} - con el precio: ${newBreakout.MaxBreakoutPrice}");

                                pendingListOfBreakouts.Add(newBreakout);
                                zone.IsBreakoutPendingToConfirmation = true;
                            }
                        }

                        return zone;
                    })
                    .ToList();
                    priceListsZones = updatedZonesExtending;
                }

                // Actualizar y guardar todos los precios de las siguientes 5 velas pendientes en las propiedades del rompimiento
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
                            breakout.NextFiveIntermediateHighBreakoutBarsPrices.Add(new BreakoutPriceData(highSeries[0], BreakoutPriceData.PriceType.High, CurrentBar));

                            breakout.NextFiveIntermediateHighBreakoutBarsPrices.Add(new BreakoutPriceData(lowSeries[0], BreakoutPriceData.PriceType.Low, CurrentBar));

                        }
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bullish))
                        {
                            // Almacenar el precio máximo alcanzado de la vela anterior a la actual
                            breakout.NextFiveHighBreakoutBarsPrices.Add(new BreakoutPriceData(highSeries[0], BreakoutPriceData.PriceType.High, CurrentBar));

                            // Almacenar el precio mínimo alcanzado de la vela anterior a la actual
                            breakout.NextFiveHighBreakoutBarsPrices.Add(new BreakoutPriceData(lowSeries[0], BreakoutPriceData.PriceType.Low, CurrentBar));
                        }
                        else if (
                            breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish) && breakout.IsIntermediateBreakout)
                        {
                            // Almacenar el precio mínimo alcanzado de la vela anterior a la actual
                            breakout.NextFiveIntermediateLowBreakoutBarsPrices.Add(new BreakoutPriceData(lowSeries[0], BreakoutPriceData.PriceType.Low, CurrentBar));

                            breakout.NextFiveIntermediateLowBreakoutBarsPrices.Add(new BreakoutPriceData(highSeries[0], BreakoutPriceData.PriceType.High, CurrentBar));
                        }
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish))
                        {
                            // Almacenar el precio máximo alcanzado de la vela anterior a la actual
                            breakout.NextFiveLowBreakoutBarsPrices.Add(new BreakoutPriceData(lowSeries[0], BreakoutPriceData.PriceType.Low, CurrentBar));

                            // Almacenar el precio mínimo alcanzado de la vela anterior a la actual
                            breakout.NextFiveLowBreakoutBarsPrices.Add(new BreakoutPriceData(highSeries[0], BreakoutPriceData.PriceType.High, CurrentBar));
                        }
                    }
                }

                if (is_a_maximum_in_the_opposite_direction_to_the_upward_movement)
                {
                    // Aquí se cumple el criterio: "Si la vela anterior es verde y la actual es roja"
                    // Puedes usar esta condición para lógica adicional de máximos en dirección opuesta
                    //Print($"Cambio de dirección detectado: Vela anterior verde (Close[1]={Close[1]}, Open[1]={Open[1]}), actual roja (Close[0]={Close[0]}, Open[0]={Open[0]})");
                }

                if (is_a_maximum_in_the_opposite_direction_to_the_downward_movement)
                {
                    // Aquí se cumple el criterio: "Si la vela anterior es roja y la actual es verde"
                    // Puedes usar esta condición para lógica adicional de máximos en dirección opuesta
                    //Print($"Cambio de dirección detectado: Vela anterior roja (Close[1]={Close[1]}, Open[1]={Open[1]}), actual verde (Close[0]={Close[0]}, Open[0]={Open[0]})");
                }
                
                if(!previousCandleIsBullish){
                    if(highSeries[0] > previousCandleMinPrice){
                        changeDir = trendDir <= 0;
                        trendDir = 1;
                        itIsABullishPullback = true;
                    }
                    else if(lowSeries[0] < previousCandleMaxPrice || max_in_opposite_bullish_direction_is_exceeded){
                        changeDir = trendDir >= 0;
                        trendDir = -1;
                        itIsABearishPullback = true;
                    }

                    //Print($"max_in_opposite_bullish_direction_is_exceeded = {max_in_opposite_bullish_direction_is_exceeded}");
                    Print($"cambiando a retroceso alcista = {changeDir} trendDir = {trendDir}");       
                }
                else if(previousCandleIsBullish){

                    if(lowSeries[0] < previousCandleMinPrice){
                        changeDir = trendDir >= 0;
                        trendDir = -1;
                        itIsABearishPullback = true;
                    }
                    else if(highSeries[0] > previousCandleMaxPrice || max_in_opposite_bearish_direction_is_exceeded){
                        changeDir = trendDir <= 0;
                        trendDir = 1;
                        itIsABullishPullback = true;
                    }

                    Print($"cambiando a retroceso bajista = {changeDir} trendDir = {trendDir}");       
                }
                else if (currentCandleIsDoji){
                    Print($"La vela actual #{CurrentBar} con el precio al alza ${highSeries[0]} y precio a la baja ${lowSeries[0]} es DOJI");

                    if(!lastNonDojiIsBullish && (highSeries[0] > previousCandleMinPrice || lowSeries[0] < previousCandleMaxPrice)){
                        itIsABullishPullback = true;
                        changeDir = trendDir >= 0;
                        trendDir = -1;
                        Print($"cambiando a retroceso bajista = {changeDir} trendDir = {trendDir}");       
                    }
                    // TODO validar si es mejor usar un else if
                    if(lastNonDojiIsBullish && (lowSeries[0] < previousCandleMinPrice || highSeries[0] > previousCandleMaxPrice)){
                        Print($"highSeries[0] de la vela doji {CurrentBar} ${highSeries[0]} > previousCandleMaxPrice ${previousCandleMaxPrice} = {highSeries[0] > previousCandleMaxPrice}");

                        itIsABearishPullback = true;
                        changeDir = trendDir <= 0;
                        trendDir = 1;       
                        Print($"cambiando a retroceso alcista = {changeDir} trendDir = {trendDir}");       
                    }


                }

                // Sí el mínimo de la vel aactual retrocede más que la anterior
				if (changeDir)
				{
					if (trendDir > 0) // vela actual verde -> nuevo tramo al alza, anclar en el máximo entre [0] y [1]
					{
						addHigh = true;
					}
					else if(trendDir < 0) // vela actual roja -> nuevo tramo a la baja, anclar en el mínimo entre [0] y [1]
					{
						
						addLow = true;
					}
                    else if(currentCandleIsDoji){ // si la vela actual es Doji
                        if(trendDir > 0) {addHigh = true;}
                        else if(trendDir < 0) {addLow = true;}
                    }

                    currentSwingPrice = trendDir >= 0 ? Math.Max(highSeries[0], highSeries[1]) : Math.Min(lowSeries[0], lowSeries[1]);

                    Print($"currentSwingPrice = ${currentSwingPrice}");
				}
				else
				{
                    // LÓGICA NORMAL: Seguir en la misma dirección con las validaciones existentes
                    if (trendDir >= 0)
                    {
                        // Nueva lógica: solo actualizar si el mínimo no retrocede más Y el precio actual es mayor
                        bool currentMinDoesNotRetreatFurtherThanPrevOne = lowSeries[0] >= lowSeries[1];
                        bool currentPriceIsHigh = highSeries[0] > highSeries[1];
                        
                        if (currentMinDoesNotRetreatFurtherThanPrevOne && currentPriceIsHigh && highSeries[0] > currentZigZagHigh)
                        {
                            currentSwingPrice = highSeries[0];
                            updateHigh = true;
                        }
                    }
                    else if (trendDir <= 0)
                    {
                        // Nueva lógica: solo actualizar si el máximo no retrocede más Y el precio actual es menor
                        bool currentMinDoesNotRetreatFurtherThanPrevOne = highSeries[0] <= highSeries[1];
                        bool currentPriceIsLower = lowSeries[0] < lowSeries[1];

                        Print($"Validando condición para actualizar el updateLow = {currentMinDoesNotRetreatFurtherThanPrevOne && currentPriceIsLower && lowSeries[0] < currentZigZagLow}");

                        Print($"Validado si el precio mínimo actual ${lowSeries[0]} es menor al último punto bajista guardado ${currentZigZagLow}: {lowSeries[0] < currentZigZagLow}");

                        if (currentMinDoesNotRetreatFurtherThanPrevOne && currentPriceIsLower && lowSeries[0] < currentZigZagLow)
                        {
                            currentSwingPrice = lowSeries[0];
                            updateLow = true;
                        }
                    }
				}

                if (currentCandleExceedsBothPeaks)
                {
                    Print($"VELA SUPERA POR AMBOS PICOS: High actual {highSeries[0]} > High anterior {highSeries[1]} Y Low actual {lowSeries[0]} < Low anterior {lowSeries[1]}");
                    // CASO ESPECIAL: Crear AMBOS puntos permanentes en OnBarUpdate
                    isCreatingVShape = true;
                    vShapeHighPrice = highSeries[0];
                    vShapeLowPrice = lowSeries[0];
                    
                    // Crear AMBOS puntos para que persistan
                    // Punto 1: Alto
                    currentSwingPrice = highSeries[0];
                    updateHigh = true;
                    
                    // Punto 2: Bajo (se creará después del procesamiento del alto)
                    addLow = true;  // Usar addLow para crear un nuevo punto bajo
                    trendDir = -1; // Actualiza la dirección de la línea como bajista
                    
                    Print($"Creando V: Punto alto {highSeries[0]} y punto bajo {lowSeries[0]} - AMBOS PUNTOS PERMANENTES");
                }
                if(lastCandleExceedsBothPeaks && lowSeries[0] < vShapeLowPrice){
                    // Fuerza a que updateLow se actualice cuando el precio continua en la misma dirección bajista
                    Print("lastCandleExceedsBothPeaks es verdadero, forzando actualizacion bajista");
                    vShapeDrawn = false;
                    updateLow = true;
                }

                Print($"Pending breakouts candidates before event = {pendingListOfBreakouts.Count()}");
                
                // Realizar validación de confirmación de los rompimientos pendientes por tipo de rompimiento
                if (pendingListOfBreakouts.Any())
                {
                    int index = 0;

                    foreach (BreakoutCandidate breakout in pendingListOfBreakouts)
                    {
                        index++;
                        bool current_bar_is_post_confirmation_bars = CurrentBar >= breakout.BreakoutBarIndex + confirmationBars;

                        // Si ya fue confirmado saltamos
                        if (breakout.BreakoutCompleted)
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
                            intermediateBullishBreakoutConfirmed = breakout.NextFiveIntermediateHighBreakoutBarsPrices.Any(highBreakoutPrice => highBreakoutPrice.Price > breakout.MaxBreakoutPrice);
                            double confirmationPrice = breakout.MaxBreakoutPrice;

                            Print($"Iterando zona breakout index: {breakout.BreakoutBarIndex} con precio máximo ${breakout.MaxBreakoutPrice} y precio mínimo ${breakout.MinBreakoutPrice}");
                            
                            foreach (Zone zone in priceListsZones)
                            {
                                // ✅ TODO: Debe romper la zona superada en concreto, en lugar de todas las zonas intermedias cuyo precio aún no ha sido superado

                                if(zone.IsResistenceZone() && zone.IsIntermediateZone)
                                {
                                    // si el precio de la zona es mayor al rompimiento
                                    //TODO verificar si MaxOrMinPrice se está actualizando al generar una zona intermedia
                                    bool zoneIsMayorThanCurrentBreakout = zone.MaxOrMinPrice > breakout.MaxBreakoutPrice;

                                    Print($"verificando si intermediateBullishBreakoutConfirmed: {intermediateBullishBreakoutConfirmed} && zoneIsMayorThanCurrentBreakout: {!zoneIsMayorThanCurrentBreakout} && itIsABearishPullback: {itIsABearishPullback} o max_in_opposite_bullish_direction_is_exceeded: {breakout.MaxInOppositeDirectionHasConsequence} RESULT = {intermediateBullishBreakoutConfirmed && !zoneIsMayorThanCurrentBreakout && (itIsABearishPullback || max_in_opposite_bullish_direction_is_exceeded)}");

                                    // Si el rompimiento es confirmado y la zona actual es una resistencia (originalmente siendo un soporte) intermedia la elimina
                                    if (
                                    intermediateBullishBreakoutConfirmed && !zoneIsMayorThanCurrentBreakout && 
                                    (itIsABearishPullback || max_in_opposite_bullish_direction_is_exceeded)
                                    )
                                    {
                                        Print($"Rompiendo resistencia = {zone.Id}");
                                        zone.IsResistenceBreakout = true;
                                        zone.IsBreakoutPendingToConfirmation = false;

                                        RemoveDrawObject("RegionHighLightY" + zone.Id);

                                        breakout.BreakoutCompleted = true;
                                        breakout.BreakoutIsConfirmed = true;
                                    }

                                    // Si no se confirma el rompimiento, es una resistencia intermedia y la barra actual es posterior a la cantidad de barras de confirmación extiende el margen de la zona al precio del rompimiento intermedio sin confirmación
                                    else if (
                                        !intermediateBullishBreakoutConfirmed &&
                                        !zoneIsMayorThanCurrentBreakout &&
                                        current_bar_is_post_confirmation_bars
                                    )
                                    {

                                        Print(
                                        $"editando dimensiones de la zona intermedia al alza =" +
                                        $"{zone.Id} maxOrMinPrice = {zone.MaxOrMinPrice} type " +
                                        $"={zone.Type}"
                                        );

                                        zone.MaxOrMinPrice = breakout.MaxBreakoutPrice;
                                        zone.RedrawHighZoneIsRequired = true;
                                        zone.IsBreakoutPendingToConfirmation = false;

                                        breakout.BreakoutCompleted = true;
                                        breakout.BreakoutIsConfirmed = false;

                                        redrawHighZoneIsRequired = true;
                                    }
                                }
                            }
                        }

                        // Si el tipo de rompimiento a confirmar es sobre un soporte intermedio
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish) && breakout.IsIntermediateBreakout)
                        {
                            intermediateBearishBreakoutConfirmed = breakout.NextFiveIntermediateLowBreakoutBarsPrices.Any(lowBreakoutPrice => lowBreakoutPrice.Price < breakout.MaxBreakoutPrice);
                            double confirmationPrice = breakout.MaxBreakoutPrice;
                            
                            foreach (Zone zone in priceListsZones)
                            {
                                if(!zone.IsResistenceZone() && zone.IsIntermediateZone)
                                {
                                    // si el precio de la zona es menor al rompimiento
                                    bool zoneIsMinorThanCurrentBreakout = zone.MaxOrMinPrice < breakout.MaxBreakoutPrice;

                                    // Si el rompimiento es confirmado y la zona actual es un soporte (originalmente siendo una resistencia) intermedio lo elimina
                                    if (
                                        intermediateBearishBreakoutConfirmed && !zoneIsMinorThanCurrentBreakout
                                        && (itIsABullishPullback || max_in_opposite_bearish_direction_is_exceeded)
                                    )
                                    {
                                        Print($"Rompiendo soporte = {zone.Id}");
                                        zone.IsSupportBreakout = true;
                                        zone.IsBreakoutPendingToConfirmation = false;

                                        RemoveDrawObject("RegionLowLightY" + zone.Id);

                                        breakout.BreakoutCompleted = true;
                                        breakout.BreakoutIsConfirmed = true;
                                    }

                                    // Si no se confirma el rompimiento, es un soporte intermedio y la barra actual es posterior a la cantidad de barras de confirmación extiende el margen de la zona al precio de rompimiento intermedio sin confirmación
                                    else if (
                                        !intermediateBearishBreakoutConfirmed &&
                                        !zoneIsMinorThanCurrentBreakout && 
                                        current_bar_is_post_confirmation_bars
                                    )
                                    {

                                        Print(
                                        $"editando dimensiones de la zona intermedia al alza =" +
                                        $"{zone.Id} maxOrMinPrice = {zone.MaxOrMinPrice} type " +
                                        $"={zone.Type}"
                                        );

                                        zone.MaxOrMinPrice = breakout.MaxBreakoutPrice;
                                        zone.RedrawLowZoneIsRequired = true;
                                        zone.IsBreakoutPendingToConfirmation = false;

                                        breakout.BreakoutCompleted = true;
                                        breakout.BreakoutIsConfirmed = false;

                                        redrawLowZoneIsRequired = true;
                                    }
                                }
                            }
                        }

                        // Si el tipo de rompimiento a confirmar es sobre la máxima zona alcista 
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bullish))
                        {

                            //Print($"verificando si la barra {breakout.BreakoutBarIndex} con el precio ${breakout.MaxBreakoutPrice} es posterior a las barras de confirmación breakout.BreakoutBarIndex + confirmationBars {breakout.BreakoutBarIndex + confirmationBars} {breakout.BreakoutBarIndex > breakout.BreakoutBarIndex + confirmationBars}");

                            // Revisa si alguna vela dentro de las próximas 5 velas supera al precio de rompimiento y lo confirma
                            maxBullishBreakoutConfirmed = breakout.NextFiveHighBreakoutBarsPrices.Any(highBreakoutPrice => highBreakoutPrice.Price > breakout.MaxBreakoutPrice)
                            ;

                            //int j = 1;
                            //double maxPriceInConfirmationBars = breakout.MaxBreakoutPrice; 
                            /*foreach (var postBreakoutBar in breakout.NextFiveHighBreakoutBarsPrices)
                            {
                                if (postBreakoutBar.Type.Equals(BreakoutPriceData.PriceType.High))
                                {
                                   // Print($"Verificando si el precio ${postBreakoutBar.Price} de la barra {postBreakoutBar.BarIndex} es mayor al precio ${breakout.MaxBreakoutPrice} de la barra de rompimiento {breakout.BreakoutBarIndex} = {postBreakoutBar.Price > breakout.MaxBreakoutPrice}");

                                }

                                // Verificar precios bajista de las velas en la cola de rompimientos pendientes para comprobar si se produce un máximo en dirección opuesta al rompimiento
                                if (postBreakoutBar.Type.Equals(BreakoutPriceData.PriceType.Low))
                                {
                                    //Sí el elemento ya ha sido verificado anteriormente continua 
                                    if(postBreakoutBar.hasAlreadyBeenProcessed){
                                        continue;
                                    }

                                    //Print($"verificando si postBreakoutBar.Price ${postBreakoutBar.Price} es menor a breakout.MinBreakoutPrice ${breakout.MinBreakoutPrice} = {postBreakoutBar.Price < breakout.MinBreakoutPrice}");

                                    // Si tiene un máximo en dirección opuesta y el precio de BreakoutPriceData es menor al máximo en dirección opuesta
                                    if (
                                        breakout.HasAMaxInOppositeDirection &&
                                        postBreakoutBar.Price < breakout.MinBreakoutPrice)
                                    {
                                        breakout.MaxInOppositeDirectionHasConsequence = true;
                                        //Print($"Se ha confirmado una consecución de máximo en dirección opuesta en zona alcista con el precio ${postBreakoutBar.Price}");
                                    }

                                    // Si ocurre un cambio en la dirección del precio 
                                    if (is_a_maximum_in_the_opposite_direction_to_the_upward_movement)
                                    {
                                        Print("HasAMaxInOppositeDirection");
                                        breakout.HasAMaxInOppositeDirection = true;

                                        breakout.MaxInOppositeDirectionPrice = postBreakoutBar.Price;
                                        breakout.MaxInOppositeDirecionBarIndex = postBreakoutBar.BarIndex;

                                       ///Print($"El rompimiento alcista en la barra {breakout.BreakoutBarIndex} con el precio {breakout.MaxBreakoutPrice} ha generado un máximo en dirección opuesta en la barra {breakout.MaxInOppositeDirecionBarIndex} con el valor {breakout.MaxInOppositeDirectionPrice}");

                                        break;
                                    }

                                    // Actualizar el valor mínimo anterior por valor mínimo de la vela actual
                                    breakout.MinBreakoutPrice = postBreakoutBar.Price;
                                    
                                    //marca el elemento como ya procesado
                                    postBreakoutBar.hasAlreadyBeenProcessed = true;

                                }

                                j++;
                            }
                            */
                            
                            // SOLO PROCESAR SI ESTAMOS EN TIEMPO REAL
                            if (!isRealtimeZigZagActive || CurrentBar < startVerticalLineIndex)
                            {
                                continue;
                            }

                            Print($"verificando si maxBullishBreakoutConfirmed: {maxBullishBreakoutConfirmed} || GetResistanceCount() == 0: {GetResistanceCount() == 0} && itIsABearishPullback: {itIsABearishPullback} o max_in_opposite_bullish_direction_is_exceeded: {max_in_opposite_bullish_direction_is_exceeded} RESULT = {(GetResistanceCount() == 0 || maxBullishBreakoutConfirmed) && (itIsABearishPullback || max_in_opposite_bullish_direction_is_exceeded)}");

                            // Sí hay confirmación del rompimiento y ocurre en un retroceso bajista o después que se genere un máximo en dirección opuesta con confirmación
                            if ((GetResistanceCount() == 0 || maxBullishBreakoutConfirmed) && (itIsABearishPullback || max_in_opposite_bullish_direction_is_exceeded))
                            {
                                Print("breaking high zone...");
                                Print("se ha confirmado un rompimiento alcista");
                                //resistenceZoneBreakoutPrice = maxPriceInConfirmationBars;
                                // Confirma el rompimiento a nivel general
                                isBullishBreakoutConfirmed = true;
                                isHighBreakoutPendingToConfirmation = false;
                                breakout.BreakoutCompleted = true;
                                breakout.BreakoutIsConfirmed = true;

                            }
                            // Si no se supera el precio despues del rompimiento y han pasado 5 velas desde el primer rompimiento extiende la zona
                            else if (!maxBullishBreakoutConfirmed && current_bar_is_post_confirmation_bars)
                            {
                                Print("extending resistence zone...");
                                // Establece el extendimiento de precio a nivel general
                                isHighZoneExtended = true;
                                isHighBreakoutPendingToConfirmation = false;
                                breakout.BreakoutCompleted = true;
                                breakout.BreakoutIsConfirmed = false;

                                // SOLUCIÓN SEÑUELO: Guardar el precio máximo actual ANTES de que se dibuje la zona
                                // Esto evita que se use el precio máximo de velas posteriores
                                //previousMaxHighPrice = currentMaxHighPrice;
                                //resistenceZoneBreakoutPrice = previousMaxHighPrice;
                            }
                        }

                        // Si el tipo de rompimiento a confirmar es sobre la mínima zona bajista
                        else if (breakout.Type.Equals(BreakoutCandidate.BreakoutType.Bearish))
                        {
                            // Revisa si alguna vela dentro de las próximas 5 velas supera al precio de rompimiento y lo confirma
                            minBearishBreakoutConfirmed = breakout.NextFiveLowBreakoutBarsPrices.Any(lowBreakoutPrice => lowBreakoutPrice.Price < breakout.MaxBreakoutPrice)
                              // || GetSupportCount() == 0
                              ;

                            //int z = 1;
                            /*foreach (var postBreakoutBar in breakout.NextFiveLowBreakoutBarsPrices)
                            {

                                if (postBreakoutBar.Type.Equals(BreakoutPriceData.PriceType.Low))
                                {
                                    //Print($"Verificando si el precio ${postBreakoutBar.Price} de la barra {postBreakoutBar.BarIndex} es menor al precio ${breakout.MaxBreakoutPrice} de la barra de rompimiento {breakout.BreakoutBarIndex} = {postBreakoutBar.Price < breakout.MaxBreakoutPrice}");
                                }

                                // Verificar precios aclistas de las velas faltantes por rompimientos pendientes para comprobar si se produce un máximo en dirección opuesta al rompimiento
                                if (postBreakoutBar.Type.Equals(BreakoutPriceData.PriceType.High))
                                {
                                    if(postBreakoutBar.hasAlreadyBeenProcessed){
                                        continue;
                                    }
                                    //Print($"verificando si postBreakoutBar.Price ${postBreakoutBar.Price} es mayor a breakout.MinBreakoutPrice ${breakout.MinBreakoutPrice} = {postBreakoutBar.Price > breakout.MinBreakoutPrice}");

                                    // Si tiene un máximo en dirección opuesta y el precio de BreakoutPriceData es menor al máximo en dirección opuesta
                                    if (
                                        breakout.HasAMaxInOppositeDirection &&
                                        postBreakoutBar.Price > breakout.MinBreakoutPrice)
                                    {
                                        breakout.MaxInOppositeDirectionHasConsequence = true;
                                        //Print($"Se ha confirmado una consecución de máximo en dirección opuesta en zona bajista con el precio ${postBreakoutBar.Price}");
                                    }

                                    // Si el precio de BreakoutPriceData es menor al máximo en dirección opuesta
                                    if (is_a_maximum_in_the_opposite_direction_to_the_downward_movement)
                                    {
                                        breakout.HasAMaxInOppositeDirection = true;

                                        breakout.MaxInOppositeDirectionPrice = postBreakoutBar.Price;
                                        breakout.MaxInOppositeDirecionBarIndex = postBreakoutBar.BarIndex;

                                        //Print($"El rompimiento bajista en la barra {breakout.BreakoutBarIndex} con el precio {breakout.MinBreakoutPrice} ha generado un máximo en dirección opuesta en la barra {breakout.MaxInOppositeDirecionBarIndex} con el valor {breakout.MaxInOppositeDirectionPrice}");
                                    }

                                    // Actualizar el valor mínimo anterior por valor mínimo de la vela actual
                                    breakout.MinBreakoutPrice = postBreakoutBar.Price;

                                    //marca el elemento como ya procesado
                                    postBreakoutBar.hasAlreadyBeenProcessed = true;
                                }

                                z++;
                            }*/

                            // SOLO PROCESAR SI ESTAMOS EN TIEMPO REAL
                            if (!isRealtimeZigZagActive || CurrentBar < startVerticalLineIndex)
                            {
                                continue;
                            }

                            Print($"verificando si minBearishBreakoutConfirmed: {minBearishBreakoutConfirmed} || GetSupportCount() == 0: {GetSupportCount() == 0} && itIsABullishPullback: {itIsABullishPullback} o max_in_opposite_bearish_direction_is_exceeded: {max_in_opposite_bearish_direction_is_exceeded} RESULT = {(GetSupportCount() == 0 || minBearishBreakoutConfirmed) && (itIsABullishPullback || max_in_opposite_bearish_direction_is_exceeded)}");

                            // Sí hay confirmación del rompimiento y ocurre en un retroceso alcista o en lugar de un retroceso hay un max en dirección bajista con consecución.
                            if ((GetSupportCount() == 0 || minBearishBreakoutConfirmed) && (itIsABullishPullback || breakout.MaxInOppositeDirectionHasConsequence))
                            {

                                Print("breaking low zone...");
                                Print("se ha confirmado un rompimiento bajista");
                                // Confirma el rompimiento a nivel general
                                isBearishBreakoutConfirmed = true;
                                isLowBreakoutPendingToConfirmation = false;
                                breakout.BreakoutCompleted = true;
                                breakout.BreakoutIsConfirmed = true;
                                
                                // ACTUALIZAR VARIABLE SEÑUELO: Cuando se confirma un rompimiento,
                                // actualizamos el precio señuelo para futuras extensiones
                                //previousMinLowPrice = currentMinLowPrice;
                                //Print($"Actualizando variable señuelo previousMinLowPrice a ${previousMinLowPrice} (rompimiento confirmado)");
                            }
                            // Si no se supera el precio despues del rompimiento y han pasado 5 velas desde el primer rompimiento extiende la zona
                            else if (!minBearishBreakoutConfirmed && current_bar_is_post_confirmation_bars)
                            {
                                Print("extending support zone...");
                                //Print($"bar index: {breakout.BreakoutBarIndex}");
                                // Establece el extendimiento de precio a nivel general
                                isLowZoneExtended = true;
                                isLowBreakoutPendingToConfirmation = false;
                                breakout.BreakoutCompleted = true;
                                breakout.BreakoutIsConfirmed = false;

                                // SOLUCIÓN SEÑUELO: Guardar el precio mínimo actual ANTES de que se dibuje la zona
                                // Esto evita que se use el precio mínimo de velas posteriores
                                //Print($"Guardando precio mínimo actual ${currentMinLowPrice} para extendimiento de zona");
                                //previousMinLowPrice = currentMinLowPrice;
                                //supportZoneBreakoutPrice = previousMinLowPrice;
                            }
                        }
                    }

                    // Eliminar elementos completados   
                    pendingListOfBreakouts.RemoveAll(breakout => breakout.BreakoutCompleted);
                    Print($"Pending breakouts candidates after event = {pendingListOfBreakouts.Count()}");
                }

                // Comprobar si la vela actual no supera a la anterior en su maximo o mínimo
                if (!isSwingHigh && !isSwingLow)
                {
                    zigZagHighSeries[0] = currentZigZagHigh;
                    zigZagLowSeries[0] = currentZigZagLow;
                    Print($"Saltando a la vela {CurrentBar + 1} CurrentBar <= startVerticalLineIndex = {CurrentBar <= startVerticalLineIndex}");

                    // Evita realizar más acciones cuando no es un retroceso
                    return;
                }

                if (addHigh || addLow || updateHigh || updateLow)
                {
                    if (updateHigh && lastSwingIdx >= 0 && !vShapeDrawn)
                    {
                        // Para updates: eliminar completamente el punto anterior y crear línea directa al nuevo punto
                        int barsToReset = CurrentBar - lastSwingIdx;
                        
                        // Limpiar completamente desde el último swing hasta ahora
                        zigZagHighZigZags.Reset(barsToReset);
                        Value.Reset(barsToReset);
                        
                        Print($"Actualizando movimiento alcista de las últimas {barsToReset} velas");
                        // NO crear punto intermedio - la línea se dibujará directamente desde el punto anterior al nuevo
                        // El punto anterior ya existe en lastSwingIdx, solo necesitamos el nuevo punto
                    }
                    if (updateLow && lastSwingIdx >= 0 && !vShapeDrawn)
                    {
                        // Para updates: eliminar completamente el punto anterior y crear línea directa al nuevo punto
                        int barsToReset = CurrentBar - lastSwingIdx;
                        
                        // Limpiar completamente desde el último swing hasta ahora
                        zigZagLowZigZags.Reset(barsToReset);
                        Value.Reset(barsToReset);

                        Print($"Actualizando movimiento bajista de las últimas {barsToReset} velas");
                        
                        // NO crear punto intermedio - la línea se dibujará directamente desde el punto anterior al nuevo
                        // El punto anterior ya existe en lastSwingIdx, solo necesitamos el nuevo punto
                    }

                    if (addHigh || updateHigh)
                    {
                        zigZagHighZigZags[0] = currentSwingPrice;
                        currentZigZagHigh    = currentSwingPrice;
                        zigZagHighSeries[0]  = currentZigZagHigh;
                        Value[0]             = currentZigZagHigh;
                    }
                    if (addLow || updateLow)
                    {
                        // CASO ESPECIAL: Si estamos creando V, usar el precio bajo guardado
                        double priceToUse = isCreatingVShape ? vShapeLowPrice : currentSwingPrice;
                        
                        zigZagLowZigZags[0] = priceToUse;
                        currentZigZagLow    = priceToUse;
                        zigZagLowSeries[0]  = currentZigZagLow;
                        Value[0]            = currentZigZagLow;
                    }

                    // Actualizar índices - si tenemos ambos puntos, usar el bajo como referencia
                    if (isCreatingVShape && (addLow || updateLow))
                    {
                        lastSwingIdx = CurrentBar;
                        lastSwingPrice = vShapeLowPrice;  // Usar el precio bajo como referencia
                    }
                    else
                    {
                        lastSwingIdx = CurrentBar;
                        lastSwingPrice = currentSwingPrice;
                    }

                    // Resetear vShapeDrawn al final del render para la próxima vela
                    if (vShapeDrawn)
                    {
                        vShapeDrawn = false;
                        Print("vShapeDrawn reseteado para la próxima vela");
                    }
                }

                // Mostrar línea en tiempo real en la barra actual, pero solo si no hubo swing confirmado
                /*if (!addHigh && !addLow && !updateHigh && !updateLow)
                {
                    // Solo mostrar la línea proyectada en la barra actual sin confirmar el swing
                    // Pero solo si cumple las condiciones para evitar movimientos horizontales
                    if (trendDir <= 0)
                    {
                        // En tendencia alcista, mostrar el máximo actual solo si no hay retroceso significativo
                        bool minActualNoRetrocede = lowSeries[0] >= lowSeries[1];
                        if (minActualNoRetrocede)
                        {
                            zigZagHighSeries[0] = highSeries[0];
                            zigZagLowSeries[0]  = currentZigZagLow;
                            Value[0]            = highSeries[0];
                        }
                        else
                        {
                            // Mantener el último valor confirmado
                            zigZagHighSeries[0] = currentZigZagHigh;
                            zigZagLowSeries[0]  = currentZigZagLow;
                            Value[0]            = currentZigZagHigh;
                        }
                    }
                    else // trendDir >= 0
                    {
                        // En tendencia bajista, mostrar el mínimo actual solo si no hay retroceso significativo
                        bool maxActualNoRetrocede = highSeries[0] <= highSeries[1];
                        if (maxActualNoRetrocede)
                        {
                            zigZagLowSeries[0]  = lowSeries[0];
                            zigZagHighSeries[0] = currentZigZagHigh;
                            Value[0]            = lowSeries[0];
                        }
                        else
                        {
                            // Mantener el último valor confirmado
                            zigZagLowSeries[0]  = currentZigZagLow;
                            zigZagHighSeries[0] = currentZigZagHigh;
                            Value[0]            = currentZigZagLow;
                        }
                    }
                }
                */

                if (startIndex == int.MinValue && (zigZagHighZigZags.IsValidDataPoint(0) && zigZagHighZigZags[0] != zigZagHighZigZags[1] || zigZagLowZigZags.IsValidDataPoint(0) && zigZagLowZigZags[0] != zigZagLowZigZags[1]))
                    startIndex = CurrentBar - (Calculate == Calculate.OnBarClose ? 2 : 1);
                    
                // Dentro de OnBarUpdate, después de la activación en tiempo real (donde uses startVerticalLineIndex):
                /*if (isRealtimeZigZagActive && !initialZigZagLineDrawn && CurrentBar == startVerticalLineIndex + 1)
                {
                    int barIdx0 = CurrentBar;
                    int barIdx1 = CurrentBar - 1;
                    double y0, y1;

                    if (Open[0] > Close[0]) { y0 = High[0]; y1 = High[1]; }
                    else if (Close[0] > Open[0]) { y0 = Low[0]; y1 = Low[1]; }
                    else { y0 = Close[0]; y1 = Close[1]; }

                    string tag = $"InitialZigZagLine_{CurrentBar}";
                    RemoveDrawObject(tag);
                    Draw.Line(this, tag, false, barIdx1, y1, barIdx0, y0, Brushes.DodgerBlue, DashStyleHelper.Solid, 2);

                    initialZigZagLineDrawn = true;
                    // NO accedas aquí a ChartControl, ni a chartScale, ni a PathGeometry, ni nada visual directo.
                }*/

                Print($"Pasando a vela #{CurrentBar + 1}");
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
        private bool IsPriceGreaterThanCurrentMaxHighPrice(double currentPrice, double currentMaxHighPrice = double.NaN)
        {
            // Si el parámetro currentMaxHighPrice es NaN, utiliza el atributo de clase currentMaxHighPrice
            if (double.IsNaN(currentMaxHighPrice))
            {
                currentMaxHighPrice = this.currentMaxHighPrice; // Atributo de clase
            }

            bool currentPriceIsGreaterThanMaxHighPrice = currentPrice
            .ApproxCompare(currentMaxHighPrice) > 0;

            if(CurrentBar == 4705){
                Print($"verificando si highSeries[0]: ${currentPrice} es mayor a el valor de la zona ${currentMaxHighPrice} = ${currentPriceIsGreaterThanMaxHighPrice}");
            }
            // Compara el precio máximo anterior alcanzado con el nuevo precio alcanzado
            return currentPriceIsGreaterThanMaxHighPrice;
        }

        // Valida si un precio anterior es inferior a uno actual
        private bool IsPriceLessThanCurrentMinLowPrice(double currentPrice, double currentMinLowPrice = double.NaN)
        {
            if (double.IsNaN(currentMinLowPrice))
            {
                //Print($"this.currentMinLowPrice = ${this.currentMinLowPrice}");
                currentMinLowPrice = this.currentMinLowPrice;
                //Print($"currentMinLowPrice = ${currentMinLowPrice}");
            }

            // Compara el precio mínimo anterior alcanzado con el nuevo precio alcanzado
            bool currentPriceIsLessThanMinLowPrice = currentPrice
            .ApproxCompare(currentMinLowPrice) < 0;
            //Print($"lastPrice: ${lastPrice} < currentMinLowPrice: ${currentMinLowPrice} = {lastPriceIsLessThanMinLowPrice}");
            return currentPriceIsLessThanMinLowPrice;
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
            //Print("Inicio del método OnRender");
            // SOLO RENDERIZAR SI ESTAMOS EN TIEMPO REAL
            if (!isRealtimeZigZagActive)
            return;

            //Print("Primera validación del método OnRender pasada");
                
            if (Bars == null || chartControl == null || startIndex == int.MinValue || CurrentBar < 5)
            return;

            //Print("segunda validación del método OnRender pasada");
            //IsValidDataPointAt(Bars.Count - 1 -(Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0)); // Make sure indicator is calculated until last (existing) bar

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
            
            //Print("Fuera de la iteración inicial OnRender");
            for (int idx = previusDiffIndex; idx <= posteriorDiffIndex; idx++)
            {
                //Print("Dentro de la iteración inicial OnRender");
                if (idx < startIndex || idx > Bars.Count - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 2 : 1) || idx < Math.Max(BarsRequiredToPlot - Displacement, Displacement))
                    continue;
                
                // Valida si el swing actual va alza o a la baja (puntos fijos)
                bool isHigh = zigZagHighZigZags.IsValidDataPointAt(idx);
                bool isLow  = zigZagLowZigZags.IsValidDataPointAt(idx);

                // Si estamos en la barra visible más reciente, proyectar la punta aunque no haya punto fijo
                int lastPlotIdx = Bars.Count - 1 - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0);
                bool projectHighTip = (idx == lastPlotIdx) && trendDir <= 0;
                bool projectLowTip  = (idx == lastPlotIdx) && trendDir >= 0;

                //Print("Antes de validación para dibujar zona");

                if (!isHigh && !isLow && !projectHighTip && !projectLowTip)
                    continue;

                //Print("Después de validación para dibujar zona");

                double candlestickBodyValue =
                    isHigh         ? zigZagHighZigZags.GetValueAt(idx) :
                    isLow          ? zigZagLowZigZags.GetValueAt(idx)  :
                    projectHighTip ? currentZigZagHigh                 :
                                    currentZigZagLow;

                // Lógica para crear nuevas zonas y actualizar extendimientos de zonas creadas
                if (isHighZoneExtended && priceListsZones.Any())
                {

                    // Obtiene la zona de resistencia con el mayor valor máximo alcanzado
                    currentZone = priceListsZones
                        .Where(zone => zone.Type == Zone.ZoneType.Resistance)
                        .OrderByDescending(zone => zone.MaxOrMinPrice)
                        .FirstOrDefault();

                    if (currentZone != null)
                    {
                        currentZone.MaxOrMinPrice = resistenceZoneBreakoutPrice;

                        Print("extendiendo región de la resistencia");

                        Print($"extendiendo resistencia = {currentZone.Id} precio de cierre: ${currentZone.ClosePrice} precio maximo: ${currentZone.MaxOrMinPrice}");

                        // Dibujar zonas de soporte y resistencia
                        NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
                            this,                         // Contexto del indicador o estrategia
                            "RegionHighLightY" + currentZone.Id, // Nombre único para la región
                            currentZone.ClosePrice,        // Nivel de precio inferior
                            currentZone.MaxOrMinPrice,     // Nivel de precio superior
                            currentZone.HighlightBrush     // Pincel para el color de la región
                        );

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
                    isHighZoneExtended = false;
                }
                else if (isBullishBreakoutConfirmed)
                {

                    currentZone = new Zone(
                        Zone.ZoneType.Resistance,
                        currentClosingHighPrice,
                        resistenceZoneBreakoutPrice
                    );

                    priceListsZones.Add(currentZone);

                    List<Zone> updatePriceListZones = priceListsZones.Select(zone =>
                    {

                        if (zone.IsResistenceZone())
                        {
                            if (
                                zone.MaxOrMinPrice == resistenceZoneBreakoutPrice
                            )
                            {

                                Print($"Generando resistencia: {zone.Id} precio de cierre: {zone.ClosePrice} y precio de maximo: {zone.MaxOrMinPrice}");
                                // Dibujar zonas de soporte 
                                NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
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

                                RemoveDrawObject("RegionHighLightY" + zone.Id);

                                

                                NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
                                    this,                   // Contexto del indicador o estrategia
                                    "RegionLowLightY" + zone.Id,  // Nombre único para la región
                                    zone.ClosePrice,              // Nivel de precio superior
                                    zone.MaxOrMinPrice,           // Nivel de precio inferior
                                    zone.HighlightBrush        // Pincel para el color de la región
                                );

                            }
                        }

                        return zone;

                    })
                    .ToList();
                    
                    priceListsZones = updatePriceListZones;
                    lastMaxHighPrice = currentMaxHighPrice;
                    lastClosingHighPrice = currentClosingHighPrice;
                    CalculateCurrentMinOrMaxPrice();
                    maxHighBreakBar = int.MaxValue;
                    //Print("actualizando maxHighBreakBar");
                    isBullishBreakoutConfirmed = false;
                    //isABullishPullback = false;
                }
                
                if (isLowZoneExtended && priceListsZones.Any())
                {
                    // Obtiene la zona del soporte con el menor valor máximo alcanzado
                    currentZone = priceListsZones
                        .Where(zone => zone.Type == Zone.ZoneType.Support)
                        .OrderBy(zone => zone.MaxOrMinPrice)
                        .FirstOrDefault();

                    if (currentZone != null)
                    {
                        currentZone.MaxOrMinPrice = supportZoneBreakoutPrice;

                        Print("extendiendo región del soporte...");

                        Print($"extendiendo soporte = {currentZone.Id} precio de cierre: ${currentZone.ClosePrice} precio minimo: ${currentZone.MaxOrMinPrice}");

                        // Dibujar zonas de soporte y resistencia
                        NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
                            this, // Contexto del indicador o estrategia
                            "RegionLowLightY" + currentZone.Id, // Nombre único para la región
                            currentZone.MaxOrMinPrice,     // Nivel de precio inferior
                            currentZone.ClosePrice,        // Nivel de precio superior
                            currentZone.HighlightBrush     // Pincel para el color de la región
                        );

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
                    isLowZoneExtended = false;
                }
                else if (isBearishBreakoutConfirmed)
                {
                    Print("Generando soporte");

                    currentZone = new Zone(
                        Zone.ZoneType.Support,
                        currentClosingLowPrice,
                        supportZoneBreakoutPrice
                    );

                    priceListsZones.Add(currentZone);

                    List<Zone> updatePriceListZones = priceListsZones.Select(zone =>
                    {

                        if (!zone.IsResistenceZone())
                        {
                            
                            if (
                                zone.MaxOrMinPrice == supportZoneBreakoutPrice
                            )
                            {

                                Print($"Generando soporte: {zone.Id} precio de cierre: {zone.ClosePrice} y precio de apertura: {zone.MaxOrMinPrice}");

                                // Dibujar zonas de soporte 
                                NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
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

                                RemoveDrawObject("RegionLowLightY" + zone.Id);

                                NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
                                    this,                    // Contexto del indicador o estrategia
                                    "RegionHighLightY" + zone.Id, // Nombre único para la región
                                    zone.ClosePrice,              // Nivel de precio inferior
                                    zone.MaxOrMinPrice,           // Nivel de precio superior
                                    zone.HighlightBrush       // Pincel para el color de la región
                                );
                            }
                        }

                        return zone;

                    })
                    .ToList();
                    priceListsZones = updatePriceListZones;

                    //}
                    //lastMinLowPrice = currentMinLowPrice;
                    //Print($"lastMinLowPrice after update = {lastMinLowPrice}");
                    lastClosingLowPrice = currentClosingLowPrice;
                    CalculateCurrentMinOrMaxPrice();
                    //Print("actualizando minLowBreakBar");
                    isBearishBreakoutConfirmed = false;
                    // isABearishPullback = false;

                }

                // redibujar precios de extendimientos de zonas intermedias
                if (redrawHighZoneIsRequired)
                {
                    Print("redibujando dimensiones de la zona intermedia al alza");
                    List<Zone> updatePriceListsZones = priceListsZones.Select(zone =>
                    {
                        if (zone.IsResistenceZone() && zone.RedrawHighZoneIsRequired)
                        {
                            Print($"redibujando resistencia = {zone.Id} ClosePrice = {zone.ClosePrice} MaxOrMinPrice = {zone.MaxOrMinPrice}");
                            NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
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
                if (redrawLowZoneIsRequired)
                {
                    Print("redibujando dimensiones de la zona intermedia a la baja");

                    List<Zone> updatePriceListsZones = priceListsZones.Select(zone =>
                    {
                        if (!zone.IsResistenceZone() && zone.RedrawLowZoneIsRequired)
                        {
                            //Print($"redibujando soporte = {zone.Id}");
                            NinjaTrader.NinjaScript.DrawingTools.Draw.RegionHighlightY(
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

                if (priceListsZones.Any())
                {
                    // TODO Crear un contador para el caso en que una zona intermedia sea superada con confirmación eliminar sin necesidad de esperar el retroceso 
                    /*List<Zone> breakoutsZonesUpdated = priceListsZones.Select(zone => {
                        if (zone.IsResistenceZone())
                        {
                            if (currentZigZagLow > zone.MaxOrMinPrice)
                            {zone.IsResistenceBreakout = true;}
                        }
                        else
                        {
                            if (currentZigZagHigh < zone.MaxOrMinPrice)
                            {zone.IsSupportBreakout = true;}
                        }
                        return zone;
                    })
                    .ToList();
                    priceListsZones = breakoutsZonesUpdated;
                    */
                    
                    priceListsZones.RemoveAll(zone =>
                    {
                        if (
                            zone.IsResistenceZone() &&
                            zone.IsResistenceBreakout && 
                            zone.IsSupportBreakout

                        )
                        {
                            Print($"eliminando resistencia = {zone.Id}");
                            RemoveDrawObject("RegionHighLightY" + zone.Id);
                            return true; // Eliminar zona
                        }
                        else if (
                            !zone.IsResistenceZone() &&
                            zone.IsResistenceBreakout && 
                            zone.IsSupportBreakout
                        )
                        {
                            Print($"eliminando soporte = {zone.Id}");
                            RemoveDrawObject("RegionLowLightY" + zone.Id);
                            return true; // Eliminar zona
                        }

                        return false; // No eliminar zona
                    });
                }

                if (lastIdx >= startIndex)
                {
                    //Print("justo antes de calcular para dibujar");
                    // Establecer cordenadas de la línea zig zag. 
                    float x1 = (chartControl.BarSpacingType == BarSpacingType.TimeBased || chartControl.BarSpacingType == BarSpacingType.EquidistantMulti && idx + Displacement >= ChartBars.Count
                        ? chartControl.GetXByTime(ChartBars.GetTimeByBarIdx(chartControl, idx + Displacement))
                        : chartControl.GetXByBarIndex(ChartBars, idx + Displacement));
                    float y1 = chartScale.GetYByValue(candlestickBodyValue);
                    
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
                    
                    // CASO ESPECIAL: Detectar y dibujar forma de "V" 
                    bool hasVShape = false;
                    double vHigh = 0, vLow = 0;
                    
                    // Verificar si esta vela tiene forma de "V" (ambos puntos en la misma vela)
                    if (zigZagHighZigZags.IsValidDataPointAt(idx) && zigZagLowZigZags.IsValidDataPointAt(idx))
                    {
                        // Esta vela tiene tanto punto alto como bajo - es una V
                        vHigh = zigZagHighZigZags.GetValueAt(idx);
                        vLow = zigZagLowZigZags.GetValueAt(idx);
                        hasVShape = true;
                        
                        //Print($"Detectando V histórica en vela {idx}: Alto {vHigh}, Bajo {vLow}");
                    }
                    
                    // Si estamos creando V actualmente o encontramos una V histórica
                    //! PORCIÓN DE CÓDIGO SE MANTIENE EJECUTANDO INFINITAMENTE POR TODAS LAS VELAS EN FORMA DE "V"
                    if ((isCreatingVShape && !vShapeDrawn && idx == CurrentBar) || (hasVShape && idx != CurrentBar))
                    {
                        double highPrice = isCreatingVShape ? vShapeHighPrice : vHigh;
                        double lowPrice = isCreatingVShape ? vShapeLowPrice : vLow;
                        
                        // Dibujar línea al pico alto primero
                        sink.AddLine(new SharpDX.Vector2(x1, y1));
                        
                        // Luego dibujar línea vertical al pico bajo de la misma vela
                        float y1_low = chartScale.GetYByValue(lowPrice);
                        sink.AddLine(new SharpDX.Vector2(x1, y1_low));
                        
                        // Actualizar para la siguiente iteración
                        lastIdx = idx;
                        lastValue = lowPrice;  // El siguiente punto será desde el bajo

                        //! AL DESCOMENTAR EL PRINT SE VALIDA COMO SE EJECUTA DE FORMA INDEFINIDA EN ITERACIONES INNECESARIAS 
                        //Print($"Dibujando V: Línea vertical desde {y1} hasta {y1_low} en vela {idx}");
                        
                        // Solo resetear variables si es la vela actual
                        if (isCreatingVShape && idx == CurrentBar)
                        {
                            isCreatingVShape = false;
                            vShapeDrawn = true; 
                            // NO resetear vShapeDrawn aquí - debe persistir durante toda la vela
                            Print("Variables de V reseteadas después del dibujo");
                        }
                        
                        continue; // Saltar el procesamiento normal para esta vela
                    }
                    
                    sink.AddLine(new SharpDX.Vector2(x1, y1));
                }
                // Save as previous point
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
                
            // 3. Al INICIO de OnRender (como primera instrucción tras las validaciones iniciales):
            /*if (pendingFirstZigZagLine && ChartBars != null && chartControl != null && chartScale != null)
            {
                try
                {
                    float x0 = chartControl.GetXByBarIndex(ChartBars, initialBarIdx1);
                    float y0 = chartScale.GetYByValue(initialY1);
                    float x1 = chartControl.GetXByBarIndex(ChartBars, initialBarIdx0);
                    float y1 = chartScale.GetYByValue(initialY0);
                    using (var path = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory))
                    using (var sink2 = path.Open())
                    {
                        sink2.BeginFigure(new SharpDX.Vector2(x0, y0), SharpDX.Direct2D1.FigureBegin.Hollow);
                        sink2.AddLine(new SharpDX.Vector2(x1, y1));
                        sink2.EndFigure(SharpDX.Direct2D1.FigureEnd.Open);
                        sink2.Close();
                        RenderTarget.DrawGeometry(path, Brushes.DodgerBlue.ToDxBrush(RenderTarget), 3);
                    }
                    pendingFirstZigZagLine = false;
                }
                catch (Exception ex)
                {
                    Print("Error dibujando la primera línea ZigZag: " + ex.ToString());
                }
            }
            */
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
                RedrawHighZoneIsRequired = false;
            }
            else if (Type == ZoneType.Resistance)
            {
                HighlightBrush = Brushes.Green.Clone();
                HighlightBrush.Opacity = 0.3;
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

        // Precio máximo donde se origina el rompimiento (Teniendo en cuenta que el precio máximo es el valor de High en movimientos al alza y Low en movimientos a la baja)
        public double MaxBreakoutPrice
        {
            get; set;
        }

        // Precio mínimo donde se origina el rompimiento (Teniendo en cuenta que el precio mínimo es el valor de Low en movimientos al alza y High en movimientos a la baja)
        public double MinBreakoutPrice {
            get; set; 
        }

        // Precio de la barra en que se genera un máximo en dirección opuesta al rompimiento
        public double MaxInOppositeDirectionPrice {
            get; set;    
        }
        
        // índice donde se genera el rompimiento
        public int BreakoutBarIndex
        {
            get; set;
        }

        // índice donde se genera el máximo en la direccion opuesta al rompimiento
        public int MaxInOppositeDirecionBarIndex
        {
            get; set;
        }

        // Establece cuando el rompimiento es completado, sea que se confirme o que no tenga consecusión
        public bool BreakoutCompleted
        {
            get; set;
        } = false;

        // Determina si el rompimiento tiene consecusión
        public bool BreakoutIsConfirmed
        {
            get; set; 
        } = false;
        
        // Determina si el rompimiento tiene un máximo en dirección opuesta
        public bool HasAMaxInOppositeDirection
        {
            get; set;
        } = false;

        // Determina si el máximo en dirección opuesta tiene consecución. 
        public bool MaxInOppositeDirectionHasConsequence
        {
            get; set;
        } = false;

        public bool IsIntermediateBreakout { 
            get; set; 
        } = false;

        public HashSet<BreakoutPriceData> NextFiveHighBreakoutBarsPrices
        {
            get; set;
        } = new HashSet<BreakoutPriceData>();

        public HashSet<BreakoutPriceData> NextFiveIntermediateHighBreakoutBarsPrices
        {
            get; set;
        } = new HashSet<BreakoutPriceData>();

        public HashSet<BreakoutPriceData> NextFiveLowBreakoutBarsPrices
        {
            get; set;
        } = new HashSet<BreakoutPriceData>();

        public HashSet<BreakoutPriceData> NextFiveIntermediateLowBreakoutBarsPrices
        {
            get; set;
        } = new HashSet<BreakoutPriceData>();

    }

    public class BreakoutPriceData
    {
        public enum PriceType
        {
            High,   // Precio alto de la barra
            Low     // Precio bajo de la barra
        }

        // El precio (highSeries[1] o lowSeries[1])
        public double Price { get; set; }
        
        // El tipo de precio (High o Low)
        public PriceType Type { get; set; }
        
        // El índice de la barra donde se registró este precio
        public int BarIndex { get; set; }

        public bool hasAlreadyBeenProcessed {get; set;} = false;
        
        // Constructor para facilitar la creación
        public BreakoutPriceData(double price, PriceType type, int barIndex)
        {
            Price = price;
            Type = type;
            BarIndex = barIndex;
        }
        
        // Override de Equals para HashSet
        public override bool Equals(object obj)
        {
            if (obj is BreakoutPriceData other)
            {
                return Price == other.Price && Type == other.Type && BarIndex == other.BarIndex;
            }
            return false;
        }
        
        // Override de GetHashCode para HashSet
        /*public override int GetHashCode()
        {
            return HashCode.Combine(Price, Type, BarIndex);
        }
        */
                    
        // Override de ToString para debugging
        public override string ToString()
        {
            return $"{Type}: {Price:F2} (Bar {BarIndex})";
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
