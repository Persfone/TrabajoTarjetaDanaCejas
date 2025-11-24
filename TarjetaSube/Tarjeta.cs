using System;
using System.Linq;

namespace TarjetaSube
{
    public class Tarjeta
    {
        protected double saldo = 0;
        public Guid Id { get; } = Guid.NewGuid();
        public double Saldo => saldo;

        private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };
        private const double LIMITE_SALDO = 40000;
        protected const double SALDO_NEGATIVO_MAX = -1200;

        public bool Cargar(double monto)
        {
            if (!montosValidos.Contains(monto)) return false;
            if (saldo + monto > LIMITE_SALDO) return false;
            saldo += monto;
            return true;
        }

        // NUEVOS MÉTODOS VIRTUALES
        public virtual double ObtenerMontoAPagar(double tarifa) => tarifa;
        public virtual string ObtenerTipo() => "Tarjeta Normal";

        public virtual bool Pagar(double monto)
        {
            if (saldo - monto < SALDO_NEGATIVO_MAX) return false;
            saldo -= monto;
            return true;
        }
    }

    public class MedioBoleto : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => tarifa / 2.0;
        public override string ObtenerTipo() => "Medio Boleto";
    }

    public class BoletoGratuito : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        public override string ObtenerTipo() => "Boleto Gratuito";
    }

    public class FranquiciaCompleta : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        public override bool Pagar(double monto) => true; // nunca descuenta
        public override string ObtenerTipo() => "Franquicia Completa";
    }

    // IClock y SystemClock
    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}