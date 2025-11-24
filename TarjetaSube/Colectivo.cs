using System;

namespace TarjetaSube
{
    // Clase Colectivo - limpia y simple
    public class Colectivo
    {
        public const double TARIFA_BASICA = 1580;
        private string linea;

        public Colectivo(string linea)
        {
            this.linea = linea;
        }

        // Este es el único método que se debe usar desde afuera
        public bool PagarCon(Tarjeta tarjeta)
        {
            return tarjeta.Pagar(TARIFA_BASICA);
        }

        public string ObtenerLinea() => linea;
    }
}


