using System;
using System.Collections.Generic;

namespace TarjetaSube
{
    public class MiBiciTuBici
    {
        public const double TARIFA_DIARIA = 1777.50;
        public const double MULTA_POR_EXCESO = 1000;
        public const double TIEMPO_MAXIMO_USO = 60; // minutos

        private readonly IClock clock;
        private Dictionary<Guid, DateTime> ultimosRetiros = new Dictionary<Guid, DateTime>();
        private Dictionary<Guid, List<DateTime>> historialMultas = new Dictionary<Guid, List<DateTime>>();
        private Dictionary<Guid, int> multasPendientes = new Dictionary<Guid, int>();

        public BoletoBici? UltimoBoleto { get; private set; }

        public MiBiciTuBici(IClock? clock = null)
        {
            this.clock = clock ?? new SystemClock();
        }

        public bool PagarCon(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            // calcula monto a pagar (tarifa + multas pendientes)
            double montoAPagar = CalcularMontoAPagar(tarjeta);

            // intentar realizar el pago
            bool pagado = tarjeta.Pagar(montoAPagar);

            if (!pagado)
            {
                UltimoBoleto = null;
                return false;
            }

            // limpia las multas pendientes si es que fue exitoso 
            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                multasPendientes.Remove(tarjeta.Id);
            }

            // retiro exitoso
            RegistrarRetiro(tarjeta, ahora, montoAPagar);

            return true;
        }

        // necesite este metodo para dejar de calcular las multas por el tiempo
        public void DevolvioLaBici(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            if (ultimosRetiros.ContainsKey(tarjeta.Id))
            {
                DateTime ultimoRetiro = ultimosRetiros[tarjeta.Id];

                //multas cuando devuelte
                int multas = CalcularMultasAcumuladas(ultimoRetiro, ahora);

                if (multas > 0)
                {

                    multasPendientes[tarjeta.Id] = multas;

                    if (!historialMultas.ContainsKey(tarjeta.Id))
                    {
                        historialMultas[tarjeta.Id] = new List<DateTime>();
                    }
                    historialMultas[tarjeta.Id].Add(ahora);
                }

                ultimosRetiros.Remove(tarjeta.Id);
            }
        }

        private double CalcularMontoAPagar(Tarjeta tarjeta)
        {
            double montoTotal = TARIFA_DIARIA;

            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                montoTotal += multasPendientes[tarjeta.Id] * MULTA_POR_EXCESO;
            }

            return montoTotal;
        }

        private int CalcularMultasAcumuladas(DateTime ultimoRetiro, DateTime ahora)
        {
            TimeSpan tiempoTranscurrido = ahora - ultimoRetiro;
            double totalMinutos = tiempoTranscurrido.TotalMinutes;

            // la primera hora (60mins) se cobra por la tarifa default, dsp si pasan otros 60 mins se cobra multa es decir si pasan +120 mins hay una multa
            if (totalMinutos < 120)
                return 0;

            int multas = (int)Math.Floor(totalMinutos / 60) - 1;
            return multas;
        }

        private void RegistrarRetiro(Tarjeta tarjeta, DateTime ahora, double montoPagado)
        {
            bool huboMulta = false;
            int multasAplicadas = 0;


            double montoMultas = montoPagado - TARIFA_DIARIA;
            if (montoMultas > 0)
            {
                multasAplicadas = (int)(montoMultas / MULTA_POR_EXCESO);
                huboMulta = true;
            }


            ultimosRetiros[tarjeta.Id] = ahora;

            // boleto
            UltimoBoleto = new BoletoBici(
                idTarjeta: tarjeta.Id,
                fechaHora: ahora,
                tipoTarjeta: tarjeta.ObtenerTipo(),
                montoPagado: montoPagado,
                tarifaBase: TARIFA_DIARIA,
                multasAplicadas: multasAplicadas,
                saldoRestante: tarjeta.Saldo,
                huboMulta: huboMulta
            );
        }
        //multas pendientes
        public int ObtenerMultasPendientes(Tarjeta tarjeta)
        {
            int multas = 0;

            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                multas += multasPendientes[tarjeta.Id];
            }

            if (ultimosRetiros.ContainsKey(tarjeta.Id))
            {
                DateTime ultimoRetiro = ultimosRetiros[tarjeta.Id];
                DateTime ahora = clock.Now;
                multas += CalcularMultasAcumuladas(ultimoRetiro, ahora);
            }

            return multas;
        }

        // historial del multas 
        public List<DateTime> ObtenerHistorialMultas(Tarjeta tarjeta)
        {
            if (historialMultas.ContainsKey(tarjeta.Id))
            {
                return new List<DateTime>(historialMultas[tarjeta.Id]);
            }
            return new List<DateTime>();
        }
    }
}