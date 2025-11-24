using System;

namespace TarjetaSube
{
    public class Colectivo
    {
        public const double TARIFA_BASICA = 1580;
        private readonly string linea;
        private readonly IClock clock;

        public Boleto? UltimoBoleto { get; private set; }

        public Colectivo(string linea, IClock? clock = null)
        {
            this.linea = linea;
            this.clock = clock ?? new SystemClock();
        }

        public string ObtenerLinea() => linea;

        public bool PagarCon(Tarjeta tarjeta)
        {
            // 1. La Tarjeta calcula el monto real a debitar (790, 1580, 0, etc.)
            // **Este valor es crucial para el Boleto.**
            double montoADebitar = tarjeta.ObtenerMontoAPagar(TARIFA_BASICA);

            // 2. Intentamos pagar. Pagar usará la TARIFA_BASICA (1580) 
            // para que las clases derivadas (MedioBoleto/Gratuito) apliquen su lógica interna.
            // Nota: MedioBoleto/BoletoGratuito están diseñados para recibir 1580.
            bool pagado = tarjeta.Pagar(TARIFA_BASICA);

            if (!pagado)
            {
                UltimoBoleto = null;
                return false;
            }

            // 3. Creamos y guardamos el boleto.
            UltimoBoleto = new Boleto(
                idTarjeta: tarjeta.Id,
                linea: linea,
                fechaHora: clock.Now,
                tipoTarjeta: tarjeta.ObtenerTipo(),

                // Usamos el monto que DEBIÓ descontarse, calculado por ObtenerMontoAPagar().
                // Esto permite que el Boleto de la Franquicia Completa sea 0.
                montoDescontado: montoADebitar,

                // Se pasa la tarifa normal para que Boleto pueda calcular la deuda
                tarifaNormal: TARIFA_BASICA,

                // Saldo restante DESPUÉS del pago
                saldoRestante: tarjeta.Saldo
            );

            return true;
        }
    }
}