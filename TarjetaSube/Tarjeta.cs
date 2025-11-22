using System;
using System.Linq;  


public class Tarjeta
{
    private double saldo = 0;


    private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };

    private const double LIMITE_SALDO = 40000;
    const double SALDO_NEGATIVO_MAX = -1200;

    public double Saldo
    {
        get { return saldo; }
    }

    public bool Cargar(double monto) //esta funcion ya hace el manejo de saldo negativo
    {
   
        if (!montosValidos.Contains(monto))
            return false;

        if (saldo + monto > LIMITE_SALDO)
            return false;

        saldo += monto;
        return true;
    }


    public bool Pagar(double monto)
    {

        // Si pagar este monto hace que el saldo sea menor que -1200, no se puede
        if (saldo - monto < SALDO_NEGATIVO_MAX)
            return false;

        saldo -= monto;
        return true;
    }

}