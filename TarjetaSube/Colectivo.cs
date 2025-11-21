using System;

namespace TarjetaSube
{
     public class Colectivo
    {
        public const double TARIFA_BASICA = 1580;
        private string linea;

        public Colectivo(string linea)
        {
            this.linea = linea;
        }

        public Boleto PagarCon(Tarjeta tarjeta)
        {
            if (tarjeta.Pagar(TARIFA_BASICA))
            {
                return new Boleto(linea, tarjeta.Saldo);
            }
            return null;
        }

        public string ObtenerLinea() => linea;
    }
}