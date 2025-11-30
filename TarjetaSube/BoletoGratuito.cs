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

        public override double ObtenerMontoAPagar(double tarifa) => tarifa; // Siempre devuelve tarifa completa

        public override string ObtenerTipo() => "Boleto Gratuito";

        // CAMBIO: Se agrega tarifaBase
        public override bool Pagar(double monto, double tarifaBase = 0)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            if (monto == 0)
            {
                return base.Pagar(0);
            }

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesGratisHoy = 0;
            }

            // VERIFICACIÓN CORRECTA DEL HORARIO
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesGratisHoy < 2;

            // ANTES: double montoAPagar = aplicaFranquicia ? 0 : monto;
            // AHORA: Si aplica, se paga 0. Si NO aplica, se cobra la 'tarifaBase' completa.
            double montoAPagar = aplicaFranquicia ? 0 : tarifaBase;

            // Intentar pagar
            bool pagado = base.Pagar(montoAPagar);
            if (!pagado) return false;

            // Actualizar estado
            _ultimoViajeFecha = ahora;

            if (aplicaFranquicia)
            {
                _viajesGratisHoy++;
            }

            return true;
        }

        protected new bool EsHoraValidaParaFranquicia(DateTime ahora)
        {
            // 1. Días: Lunes (1) a Viernes (5)
            // NOTA: Se niega la franquicia explícitamente en fin de semana para mayor seguridad.
            if (ahora.DayOfWeek == DayOfWeek.Saturday || ahora.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            // 2. Horario: de 6:00:00 (incluido) a 22:00:00 (excluido)
            // Se usa TimeSpan para una verificación de tiempo precisa (incluyendo minutos y segundos).
            TimeSpan tiempoActual = ahora.TimeOfDay;
            TimeSpan horaApertura = new TimeSpan(6, 0, 0); // 06:00:00 (inclusive)
            TimeSpan horaCierre = new TimeSpan(22, 0, 0); // 22:00:00 (exclusive, por el operador <)

            return tiempoActual >= horaApertura && tiempoActual < horaCierre;
        }
    }
}