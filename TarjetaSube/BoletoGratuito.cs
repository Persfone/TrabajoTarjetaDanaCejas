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

            double montoAPagar = aplicaFranquicia ? 0 : monto;

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
    }
}