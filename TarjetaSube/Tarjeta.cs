using System;
using System.Linq;  

public class Tarjeta
{
    private double saldo = 0;


    private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };

    private const double LIMITE_SALDO = 40000;

    public double Saldo
    {
        get { return saldo; }
    }

    public bool Cargar(double monto)
    {
   
        if (!montosValidos.Contains(monto))
            return false;

        if (saldo + monto > LIMITE_SALDO)
            return false;

        saldo += monto;
        return true;
    }


    internal bool Pagar(double monto)
    {
        if (saldo >= monto)
        {
            saldo -= monto;
            return true;
        }
        return false;
    }
}