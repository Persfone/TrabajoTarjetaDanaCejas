using System;
using System.Linq;

namespace TarjetaSube
{
    public class Tarjeta
    {
        private double saldo = 0;
        private double _saldoPendiente = 0;

        // NUEVO: Propiedad ID
        public Guid Id { get; }

        private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };

        private const double LIMITE_SALDO = 56000;
        const double SALDO_NEGATIVO_MAX = -1200;

        public double Saldo => saldo;
        public double SaldoPendiente => _saldoPendiente;

        // NUEVO: Constructor para inicializar el ID
        public Tarjeta()
        {
            this.Id = Guid.NewGuid();
        }

        public bool Cargar(double monto)
        {
            if (!montosValidos.Contains(monto)) return false;
            if (saldo + monto > LIMITE_SALDO)
            {
                // Calculamos cuánto se puede acreditar y cuánto queda pendiente
                double montoAcreditable = LIMITE_SALDO - saldo;
                double montoPendiente = monto - montoAcreditable;

                saldo += montoAcreditable;
                _saldoPendiente += montoPendiente;
            }
            else
            {
                // Carga normal si no supera el límite
                saldo += monto;
            }
            return true;
        }


        public void AcreditarCarga()
        {
            double disponibleParaAcreditar = LIMITE_SALDO - saldo;

            if (_saldoPendiente > 0 && disponibleParaAcreditar > 0)
            {
                // Acredita el menor entre lo pendiente y lo que falta para el límite
                double montoAAcreditar = Math.Min(_saldoPendiente, disponibleParaAcreditar);

                saldo += montoAAcreditar;
                _saldoPendiente -= montoAAcreditar;
            }
        }

        public virtual double ObtenerMontoAPagar(double tarifa) => tarifa;
        public virtual string ObtenerTipo() => "Tarjeta Normal";

        public virtual bool Pagar(double monto)
        {
            if (saldo - monto < SALDO_NEGATIVO_MAX) return false;
            saldo -= monto;

            AcreditarCarga();
            return true;
        }
    }


    public class MedioBoleto : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;
        // La tarifa a obtener ES la mitad, pero la lógica de cobro está en Pagar()
        public override double ObtenerMontoAPagar(double tarifa) => tarifa / 2.0;
        public override string ObtenerTipo() => "Medio Boleto";

        public MedioBoleto(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override bool Pagar(double monto) // monto SIEMPRE es TARIFA_BASICA (1580)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            // Verificar intervalo de 5 minutos
            if (_ultimoViajeFecha.HasValue)
            {
                TimeSpan diferencia = ahora - _ultimoViajeFecha.Value;
                if (diferencia < TimeSpan.FromMinutes(5))
                {
                    return false; // ¡No puede viajar tan rápido!
                }
            }

            // 1. Determinar el monto real a debitar (790 o 1580)
            // Usamos 'monto' porque 'monto' aquí es 1580 (TARIFA_BASICA)
            double montoADescontar = _viajesHoy < 2 ? monto / 2.0 : monto;

            // 2. Intentar pagar el monto ya ajustado. 
            bool resultado = base.Pagar(montoADescontar);

            if (resultado)
            {
                _viajesHoy++;
                _ultimoViajeFecha = ahora;
            }

            return resultado;
        }
    }

    public class BoletoGratuito : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;

        // La tarifa a obtener ES 0, pero la lógica de cobro está en Pagar()
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        public override string ObtenerTipo() => "Boleto Gratuito";

        public BoletoGratuito(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override bool Pagar(double monto) // monto SIEMPRE es TARIFA_BASICA (1580)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // 1. Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            bool resultado;
            double montoAPagar = (_viajesHoy < 2) ? 0 : monto;

            // 2. Intentar pagar (0 o 1580)
            resultado = base.Pagar(montoAPagar);

            // 3. Actualizar el contador y la fecha SOLO si el pago fue exitoso
            if (resultado)
            {
                _viajesHoy++;
                _ultimoViajeFecha = ahora;
            }

            return resultado;
        }
    }

    public class FranquiciaCompleta : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        // Pagar siempre es true porque su saldo negativo máximo es mucho más bajo.
        // Asumo que el requisito era que siempre pueden viajar.
        public override bool Pagar(double monto) => true;
        public override string ObtenerTipo() => "Franquicia Completa";
    }


    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}