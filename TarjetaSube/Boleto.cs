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
        public double TarifaNormal { get; }
        public bool EsTrasbordo { get; }  

        public Boleto(Guid idTarjeta, string linea, DateTime fechaHora,
                      string tipoTarjeta, double montoDescontado, double tarifaNormal,
                      double saldoRestante, bool esTrasbordo = false)
        {
            IdTarjeta = idTarjeta;
            Linea = linea;
            FechaHora = fechaHora;
            TipoTarjeta = tipoTarjeta;
            MontoDescontado = montoDescontado;
            SaldoRestante = saldoRestante;
            TarifaNormal = tarifaNormal;
            EsTrasbordo = esTrasbordo;

            double saldoAntes = SaldoRestante + MontoDescontado;
            MontoTotalAbonado = saldoAntes < 0 ? TarifaNormal : MontoDescontado;
        }

        public void Imprimir()
        {
            Console.WriteLine("══════════════════════════════════");
            Console.WriteLine(" BOLETO SUBE");
            Console.WriteLine("══════════════════════════════════");
            Console.WriteLine($"Línea: {Linea}");
            Console.WriteLine($"Fecha: {FechaHora:dd/MM/yyyy HH:mm}");
            Console.WriteLine($"Tipo: {TipoTarjeta}");
            Console.WriteLine($"ID: {IdTarjeta}");
            Console.WriteLine($"Descontado: ${MontoDescontado:F2}");
            Console.WriteLine($"Saldo final: ${SaldoRestante:F2}");
            Console.WriteLine($"TOTAL ABONADO: ${MontoTotalAbonado:F2}");
            Console.WriteLine($"Trasbordo: {(EsTrasbordo ? "SÍ" : "No")}");
            if (MontoTotalAbonado > 1580) Console.WriteLine(" (incluye deuda recuperada)");
            Console.WriteLine("══════════════════════════════════\n");
        }
    }
}