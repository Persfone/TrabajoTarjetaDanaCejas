using System;

namespace TarjetaSube
{
    public class Colectivo
    {
        public const double TARIFA_BASICA = 1580;
        public const double TARIFA_INTERURBANA = 3000; // NUEVO: agregar tarifa interurbana

        private readonly string linea;
        private readonly IClock clock;

        public Boleto? UltimoBoleto { get; private set; }

        public Colectivo(string linea, IClock? clock = null)
        {
            this.linea = linea;
            this.clock = clock ?? new SystemClock();
        }

        public string ObtenerLinea() => linea;

        public bool PagarCon(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            // DETECCIÓN DE TARIFA: determinar si es interurbana o no
            bool esInterurbana = tarjeta.ObtenerTipo().Contains("(Interurbana)");
            double tarifaBase = esInterurbana ? TARIFA_INTERURBANA : TARIFA_BASICA;

            double montoBase = tarjeta.ObtenerMontoAPagar(tarifaBase);

            // ... (el resto del código de trasbordo se mantiene igual)
            bool esTrasbordo = false;

            var tipoTarjeta = tarjeta.GetType();
            var campoFecha = tipoTarjeta.GetField("ultimoViajeTrasbordo",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var campoLinea = tipoTarjeta.GetField("ultimaLineaTrasbordo",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (campoFecha != null && campoLinea != null)
            {
                var ultimoViaje = (DateTime?)campoFecha.GetValue(tarjeta);
                var ultimaLinea = (string?)campoLinea.GetValue(tarjeta);

                if (ultimoViaje.HasValue)
                {
                    var diferencia = ahora - ultimoViaje.Value;
                    bool dentroDeHora = diferencia <= TimeSpan.FromHours(1);
                    bool lineaDistinta = !string.Equals(ultimaLinea, this.linea, StringComparison.OrdinalIgnoreCase);
                    bool diaValido = ahora.DayOfWeek >= DayOfWeek.Monday && ahora.DayOfWeek <= DayOfWeek.Saturday;
                    bool horarioValido = ahora.Hour >= 7 && ahora.Hour < 22;

                    //esTrasbordo = dentroDeHora && lineaDistinta && diaValido && horarioValido;
                }
            }

            double montoFinal = esTrasbordo ? 0 : montoBase;

            bool pagado = tarjeta.Pagar(montoFinal);
            if (!pagado)
            {
                UltimoBoleto = null;
                return false;
            }

            // Actualizar campos de trasbordo
            if (campoFecha != null && campoLinea != null)
            {
                campoFecha.SetValue(tarjeta, ahora);
                campoLinea.SetValue(tarjeta, this.linea);
            }

            UltimoBoleto = new Boleto(
                idTarjeta: tarjeta.Id,
                linea: linea,
                fechaHora: ahora,
                tipoTarjeta: tarjeta.ObtenerTipo(),
                montoDescontado: montoFinal,
                tarifaNormal: tarifaBase, // CORRECCIÓN: usar la tarifa base correcta
                saldoRestante: tarjeta.Saldo
            );

            return true;
        }
    }
}