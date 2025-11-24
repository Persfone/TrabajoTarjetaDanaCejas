using System;
using System.Linq;

namespace TarjetaSube
{
    public class Tarjeta
    {
        protected double saldo = 0;
        private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };
        private const double LIMITE_SALDO = 40000;
        protected const double SALDO_NEGATIVO_MAX = -1200;
        public double Saldo => saldo;

        public bool Cargar(double monto)
        {
            if (!montosValidos.Contains(monto))
                return false;
            if (saldo + monto > LIMITE_SALDO)
                return false;
            saldo += monto;
            return true;
        }

        public virtual bool Pagar(double monto)
        {
            if (saldo - monto < SALDO_NEGATIVO_MAX)
                return false;
            saldo -= monto;
            return true;
        }
    }

    public class MedioBoleto : Tarjeta
    {
        public override bool Pagar(double monto)
        {
            return base.Pagar(monto / 2.0);
        }
    }

    public class BoletoGratuito : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;

        public BoletoGratuito(IClock clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override bool Pagar(double monto)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            _ultimoViajeFecha = ahora;

            if (_viajesHoy < 2)
            {
                _viajesHoy++;
                return base.Pagar(0);
            }
            else
            {
                return base.Pagar(monto);
            }
        }
    }

    public class FranquiciaCompleta : Tarjeta
    {
        public override bool Pagar(double monto)
        {
            return true;
        }
    }
    // ← AQUÍ VAN IClock y SystemClock
    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}