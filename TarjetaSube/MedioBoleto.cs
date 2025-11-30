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

        // CAMBIO: Se agrega tarifaBase
        public override bool Pagar(double monto, double tarifaBase = 0)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // NOTA: Se mantiene la lógica de "pago de 0" que estaba en el código original,
            // aunque en la práctica, PagarCon le enviará el monto descontado (tarifa/2)
            if (monto == 0)
            {
                return base.Pagar(0);
            }

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            // VERIFICACIÓN CORRECTA DEL HORARIO
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesHoy < 2;

            // ANTES: double montoADescontar = aplicaFranquicia ? monto : monto * 2;
            // AHORA: Si aplica la franquicia, se paga el 'monto' (que es la mitad de la tarifa).
            //        Si NO aplica, se cobra la 'tarifaBase' completa.
            double montoADescontar = aplicaFranquicia ? monto : tarifaBase;

            // Verificar intervalo de 5 minutos
            if (_ultimoViajeFecha.HasValue)
            {
                TimeSpan diferencia = ahora - _ultimoViajeFecha.Value;
                if (diferencia < TimeSpan.FromMinutes(5))
                {
                    return false;
                }
            }

            // Intentar pagar
            bool resultado = base.Pagar(montoADescontar);

            // Actualizar contador SOLO si aplicó franquicia
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