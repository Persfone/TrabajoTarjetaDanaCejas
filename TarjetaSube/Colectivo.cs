using System;
using System.Reflection; // Necesario para acceder a campos protegidos

namespace TarjetaSube
{
    public class Colectivo
    {
        public const double TARIFA_BASICA = 1580;

        protected readonly string linea; // Cambiado a protected para acceso en Interurbano
        protected readonly IClock clock; // Cambiado a protected para acceso en Interurbano

        public Boleto? UltimoBoleto { get; private set; }

        public Colectivo(string linea, IClock? clock = null)
        {
            this.linea = linea;
            this.clock = clock ?? new SystemClock();
        }

        public string ObtenerLinea() => linea;

        // NUEVO MÉTODO: Propiedad para que las tarjetas obtengan la tarifa base
        public virtual double ObtenerTarifaBase() => TARIFA_BASICA;

        public bool PagarCon(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            // DETECCIÓN DE TARIFA: Usa el método virtual
            double tarifaBase = ObtenerTarifaBase();

            double montoBase = tarjeta.ObtenerMontoAPagar(tarifaBase);

            // LÓGICA DE TRASBORDO
            bool esTrasbordo = false;

            // Accedemos a los campos protegidos de trasbordo (están en Tarjeta base)
            // Esto es necesario porque Tarjeta.Pagar no acepta una TarifaBase como argumento
            var tipoTarjeta = tarjeta.GetType();

            // Usamos Reflection para acceder a los campos protegidos de la clase base Tarjeta
            var campoFecha = typeof(Tarjeta).GetField("ultimoViajeTrasbordo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var campoLinea = typeof(Tarjeta).GetField("ultimaLineaTrasbordo",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (campoFecha != null && campoLinea != null)
            {
                var ultimoViaje = (DateTime?)campoFecha.GetValue(tarjeta);
                var ultimaLinea = (string?)campoLinea.GetValue(tarjeta);

                // REUTILIZAR EL MÉTODO DE VERIFICACIÓN DE TRASBORDO DE LA TARJETA
                // Para no duplicar la lógica
                if (tarjeta.EsTrasbordoValido(ahora, this.linea))
                {
                    esTrasbordo = true;
                }
            }

            double montoFinal = esTrasbordo ? 0 : montoBase;

            // Intentar pagar, pasando la TarifaBase para que la franquicia decida cuánto descontar
            bool pagado = tarjeta.Pagar(montoFinal, tarifaBase);
            if (!pagado)
            {
                UltimoBoleto = null;
                return false;
            }

            // ACTUALIZAMOS SOLO los campos de trasbordo (NO TOCAMOS los de MedioBoleto ni Gratuito)
            // Usamos el método de la tarjeta para actualizar el estado de trasbordo
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

    // NUEVA CLASE INTERURBANO
    public class Interurbano : Colectivo
    {
        public const double TARIFA_INTERURBANA = 3000;

        public Interurbano(string linea, IClock? clock = null) : base(linea, clock)
        {
        }

        public override double ObtenerTarifaBase() => TARIFA_INTERURBANA;
    }
}