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

        public override bool Pagar(double monto)
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
                _viajesHoy = 0;
            }

            // VERIFICACIÓN CORRECTA DEL HORARIO
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesHoy < 2;

            // CORRECCIÓN: NO dividir por 2 otra vez - el monto YA VIENE con descuento
            // Si NO aplica franquicia, debe pagar tarifa completa (1580)
            double montoADescontar = aplicaFranquicia ? monto : 1580;

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