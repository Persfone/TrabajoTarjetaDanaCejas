using System;
using System.Linq;

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

    // Método virtual que todas las tarjetas especiales van a sobrescribir
    public virtual bool Pagar(double monto)
    {
        // Si con el descuento sigue quedando por encima del negativo permitido
        if (saldo - monto < SALDO_NEGATIVO_MAX)
            return false;

        saldo -= monto;
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
// Boleto gratuito (discapacidad, etc.)
public class BoletoGratuito : Tarjeta
{
    public override bool Pagar(double monto)
    {
        return base.Pagar(0); // siempre paga 0
    }
}

// Franquicia completa (policía, bomberos, etc.)
public class FranquiciaCompleta : Tarjeta
{
    public override bool Pagar(double monto)
    {
        // Nunca descuenta saldo y siempre permite el viaje
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