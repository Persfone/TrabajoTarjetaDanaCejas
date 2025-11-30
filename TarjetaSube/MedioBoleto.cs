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

            double montoADescontar = aplicaFranquicia ? monto : monto * 2;

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
    }
}