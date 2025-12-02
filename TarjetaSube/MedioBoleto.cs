using System;

namespace TarjetaSube
{
    public class MedioBoleto : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;

        public override double ObtenerMontoAPagar(double tarifa) => tarifa / 2.0;
        public override string ObtenerTipo() => "Medio Boleto";

        public MedioBoleto(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override bool Pagar(double monto, double tarifaBase = 0)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            if (monto == 0)
            {
                return base.Pagar(0); //por si es trasbordo tiene mayor prioridad
            }

            // reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesHoy < 2;

            double montoADescontar = aplicaFranquicia ? monto : tarifaBase;

            //cada 5 mins
            if (_ultimoViajeFecha.HasValue)
            {
                TimeSpan diferencia = ahora - _ultimoViajeFecha.Value;
                if (diferencia < TimeSpan.FromMinutes(5))
                {
                    return false;
                }
            }

            bool resultado = base.Pagar(montoADescontar);


            if (resultado && aplicaFranquicia)
            {
                _viajesHoy++;
            }

            if (resultado)
            {
                _ultimoViajeFecha = ahora;
            }

            return resultado;
        }

        protected override bool EsHoraValidaParaFranquicia(DateTime ahora)
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