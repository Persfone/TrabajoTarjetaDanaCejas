using System;
using System.Reflection; // necesario para acceder a campos protegidos

namespace TarjetaSube
{
    public class Colectivo
    {
        public const double TARIFA_BASICA = 1580;

        protected readonly string linea; 
        protected readonly IClock clock; 

        public Boleto? UltimoBoleto { get; private set; }

        public Colectivo(string linea, IClock? clock = null)
        {
            this.linea = linea;
            this.clock = clock ?? new SystemClock();
        }

        public string ObtenerLinea() => linea;

        public virtual double ObtenerTarifaBase() => TARIFA_BASICA;

        public bool PagarCon(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            double tarifaBase = ObtenerTarifaBase();

            double montoBase = tarjeta.ObtenerMontoAPagar(tarifaBase);

            // trasbordo
            bool esTrasbordo = false;

            var tipoTarjeta = tarjeta.GetType();

            // uso Reflection para acceder a los campos protegidos de la clase base Tarjeta
            var campoFecha = typeof(Tarjeta).GetField("ultimoViajeTrasbordo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var campoLinea = typeof(Tarjeta).GetField("ultimaLineaTrasbordo",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (campoFecha != null && campoLinea != null)
            {
                var ultimoViaje = (DateTime?)campoFecha.GetValue(tarjeta);
                var ultimaLinea = (string?)campoLinea.GetValue(tarjeta);

                if (tarjeta.EsTrasbordoValido(ahora, this.linea))
                {
                    esTrasbordo = true;
                }
            }

            double montoFinal = esTrasbordo ? 0 : montoBase;


            bool pagado = tarjeta.Pagar(montoFinal, tarifaBase);
            if (!pagado)
            {
                UltimoBoleto = null;
                return false;
            }

            tarjeta.ActualizarTrasbordo(ahora, this.linea);

            UltimoBoleto = new Boleto(
                idTarjeta: tarjeta.Id,
                linea: linea,
                fechaHora: ahora,
                tipoTarjeta: tarjeta.ObtenerTipo(),
                montoDescontado: montoFinal,
                tarifaNormal: tarifaBase,
                saldoRestante: tarjeta.Saldo,
                esTrasbordo: esTrasbordo
            );

            return true;
        }
    }


    public class Interurbano : Colectivo
    {
        public const double TARIFA_INTERURBANA = 3000;

        public Interurbano(string linea, IClock? clock = null) : base(linea, clock)
        {
        }

        public override double ObtenerTarifaBase() => TARIFA_INTERURBANA;
    }
}