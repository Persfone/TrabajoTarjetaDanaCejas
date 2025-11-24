using System;

namespace TarjetaSube
{
    public class Boleto
    {
        public Guid IdTarjeta { get; }
        public string Linea { get; }
        public DateTime FechaHora { get; }
        public string TipoTarjeta { get; }
        public double MontoDescontado { get; }
        public double SaldoRestante { get; }
        public double MontoTotalAbonado { get; }

        public Boleto(Guid idTarjeta, string linea, DateTime fechaHora,
                      string tipoTarjeta, double montoDescontado, double saldoRestante)
        {
            IdTarjeta = idTarjeta;
            Linea = linea;
            FechaHora = fechaHora;
            TipoTarjeta = tipoTarjeta;
            MontoDescontado = montoDescontado;
            SaldoRestante = saldoRestante;

            if (saldoRestante <= -1200)
            {
                MontoTotalAbonado = 1580 + (montoDescontado - 1580); // recupera deuda
            }
            else
            {
                MontoTotalAbonado = montoDescontado;
            }
        }

        public void Imprimir()
        {
            Console.WriteLine("══════════════════════════════════");
            Console.WriteLine("           BOLETO SUBE");
            Console.WriteLine("══════════════════════════════════");
            Console.WriteLine($"Línea:           {Linea}");
            Console.WriteLine($"Fecha:           {FechaHora:dd/MM/yyyy HH:mm}");
            Console.WriteLine($"Tipo:            {TipoTarjeta}");
            Console.WriteLine($"ID:              {IdTarjeta}");
            Console.WriteLine($"Descontado:      ${MontoDescontado:F2}");
            Console.WriteLine($"Saldo final:     ${SaldoRestante:F2}");
            Console.WriteLine($"TOTAL ABONADO:   ${MontoTotalAbonado:F2}");
            if (MontoTotalAbonado > 1580) Console.WriteLine("  (incluye deuda recuperada)");
            Console.WriteLine("══════════════════════════════════\n");
        }
    }
}