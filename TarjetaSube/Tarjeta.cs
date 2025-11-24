using System;
using System.Linq;

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
// Tarjeta con medio boleto (estudiante, jubilado, etc.)
public class MedioBoleto : Tarjeta
{
    public override bool Pagar(double monto)
    {
        double medio = monto / 2.0;
        return base.Pagar(medio); // paga el 50%
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