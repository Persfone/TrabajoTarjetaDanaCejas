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