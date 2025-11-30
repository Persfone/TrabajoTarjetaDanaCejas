using System;

namespace TarjetaSube
{
    public class BoletoGratuito : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesGratisHoy = 0;

        public BoletoGratuito(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override double ObtenerMontoAPagar(double tarifa) => tarifa; // siempre devuelve tarifa completa

        public override string ObtenerTipo() => "Boleto Gratuito";

        public override bool Pagar(double monto, double tarifaBase = 0)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            if (monto == 0)
            {
                return base.Pagar(0);
            }

            // reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesGratisHoy = 0;
            }

           
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesGratisHoy < 2;

            
            double montoAPagar = aplicaFranquicia ? 0 : tarifaBase;

            // intentar pagar
            bool pagado = base.Pagar(montoAPagar);
            if (!pagado) return false;

            // actualizar estado
            _ultimoViajeFecha = ahora;

            if (aplicaFranquicia)
            {
                _viajesGratisHoy++;
            }

            return true;
        }

        protected new bool EsHoraValidaParaFranquicia(DateTime ahora)
        {
            
            if (ahora.DayOfWeek == DayOfWeek.Saturday || ahora.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            
            TimeSpan tiempoActual = ahora.TimeOfDay;
            TimeSpan horaApertura = new TimeSpan(6, 0, 0); // 6
            TimeSpan horaCierre = new TimeSpan(22, 0, 0); // 22 

            return tiempoActual >= horaApertura && tiempoActual < horaCierre;
        }
    }
}