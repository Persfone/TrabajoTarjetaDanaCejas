using System;

namespace TarjetaSube
{
     public class Boleto
    {
        public string Linea { get; }
        public double SaldoRestante { get; }

        public Boleto(string linea, double saldoRestante)
        {
            Linea = linea;
            SaldoRestante = saldoRestante;
        }

        public void Imprimir()
        {
            Console.WriteLine($"Boleto emitido para la línea: {Linea}");
            Console.WriteLine($"Saldo restante en la tarjeta: {SaldoRestante}");
        }
    }
}