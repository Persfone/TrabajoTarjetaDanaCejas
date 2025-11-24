using System;

namespace TarjetaSube
{
    public class Colectivo
    {
        public const double TARIFA_BASICA = 1580;
        private readonly string linea;
        private readonly IClock clock;

        // ¡NUEVO! Guarda el último boleto emitido
        public Boleto? UltimoBoleto { get; private set; }

        public Colectivo(string linea, IClock? clock = null)
        {
            this.linea = linea;
            this.clock = clock ?? new SystemClock();
        }

        public string ObtenerLinea() => linea;

        // MANTIENE EL COMPORTAMIENTO ANTIGUO: devuelve bool
        public bool PagarCon(Tarjeta tarjeta)
        {
            double montoAPagar = tarjeta.ObtenerMontoAPagar(TARIFA_BASICA);
            double saldoAntes = tarjeta.Saldo;

            bool pagado = tarjeta.Pagar(montoAPagar);
            if (!pagado)
            {
                UltimoBoleto = null;
                return false;
            }

            // Creamos y guardamos el boleto
            UltimoBoleto = new Boleto(
                idTarjeta: tarjeta.Id,
                linea: linea,
                fechaHora: clock.Now,
                tipoTarjeta: tarjeta.ObtenerTipo(),
                montoDescontado: montoAPagar,
                saldoRestante: tarjeta.Saldo
            );

            return true;
        }
    }
}
