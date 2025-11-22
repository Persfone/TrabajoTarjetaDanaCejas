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

        public bool PagarCon(Tarjeta tarjeta, out Boleto? boleto) //out es una forma en la q una funcion puede devolver mas de un valor
        {
            if (tarjeta.Pagar(Colectivo.TARIFA_BASICA))
            {
                boleto = new Boleto(linea, tarjeta.Saldo);
                return true;
            }

            boleto = null;
            return false;
        }


        public string ObtenerLinea() => linea;
    }
}


