using System;
using System.Linq;

namespace TarjetaSube
{
    public class Tarjeta
    {
    private double saldo = 0;
    private double _saldoPendiente = 0;

    private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };

    private const double LIMITE_SALDO = 56000;

    const double SALDO_NEGATIVO_MAX = -1200;

    public double Saldo => saldo;
    public double SaldoPendiente => _saldoPendiente;

    public bool Cargar(double monto)
    {
        
        if (!montosValidos.Contains(monto))
                return false;
        // Si el saldo actual + la carga supera el límite (56000)
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

    public virtual bool Pagar(double monto)
    {
        if (saldo - monto < SALDO_NEGATIVO_MAX)
            return false;

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

    public MedioBoleto(IClock clock = null)
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
        // Nota: El primer viaje de la tarjeta siempre pasa, ya que _ultimoViajeFecha es null.
        if (_ultimoViajeFecha.HasValue)
        {
            TimeSpan diferencia = ahora - _ultimoViajeFecha.Value;
            if (diferencia < TimeSpan.FromMinutes(5))
            {
                return false; // ¡No puede viajar tan rápido!
            }
        }

        // 1. Determinar el monto real a debitar (790 o 1580)
        double montoADescontar = _viajesHoy < 2 ? monto / 2.0 : monto;

        // 2. Intentar pagar el monto ya ajustado. 
        bool resultado = base.Pagar(montoADescontar); // base.Pagar(790) o base.Pagar(1580)

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
