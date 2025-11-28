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

            // Calcular monto a pagar (tarifa + multas pendientes)
            double montoAPagar = CalcularMontoAPagar(tarjeta);

            // Intentar realizar el pago
            bool pagado = tarjeta.Pagar(montoAPagar);

            if (!pagado)
            {
                UltimoBoleto = null;
                return false;
            }

            // Solo limpiar multas pendientes si el pago fue exitoso
            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                multasPendientes.Remove(tarjeta.Id);
            }

            // Registrar el retiro exitoso
            RegistrarRetiro(tarjeta, ahora, montoAPagar);

            return true;
        }

        // Método para registrar la devolución de la bicicleta
        public void DevolvioLaBici(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            if (ultimosRetiros.ContainsKey(tarjeta.Id))
            {
                DateTime ultimoRetiro = ultimosRetiros[tarjeta.Id];

                // Calcular multas acumuladas hasta el momento de la devolución
                int multas = CalcularMultasAcumuladas(ultimoRetiro, ahora);

                if (multas > 0)
                {
                    // Guardar multas pendientes para el próximo retiro
                    multasPendientes[tarjeta.Id] = multas;

                    // Registrar en historial
                    if (!historialMultas.ContainsKey(tarjeta.Id))
                    {
                        historialMultas[tarjeta.Id] = new List<DateTime>();
                    }
                    historialMultas[tarjeta.Id].Add(ahora);
                }

                // Eliminar el último retiro para que no siga acumulando multas
                ultimosRetiros.Remove(tarjeta.Id);
            }
        }

        private double CalcularMontoAPagar(Tarjeta tarjeta)
        {
            double montoTotal = TARIFA_DIARIA;

            // Verificar si hay multas pendientes de devoluciones anteriores
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

            // No hay multa si es < 120 minutos
            if (totalMinutos < 120)
                return 0;

            // Calcular multas: cada 60 minutos después de los primeros 60 genera 1 multa
            // Pero la primera multa aparece a los 120 minutos
            int multas = (int)Math.Floor(totalMinutos / 60) - 1;
            return multas;
        }

        private void RegistrarRetiro(Tarjeta tarjeta, DateTime ahora, double montoPagado)
        {
            bool huboMulta = false;
            int multasAplicadas = 0;

            // Verificar si se cobraron multas en este pago
            double montoMultas = montoPagado - TARIFA_DIARIA;
            if (montoMultas > 0)
            {
                multasAplicadas = (int)(montoMultas / MULTA_POR_EXCESO);
                huboMulta = true;
            }

            // Actualizar el último retiro
            ultimosRetiros[tarjeta.Id] = ahora;

            // Crear boleto
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

        // Método para consultar multas pendientes de una tarjeta
        public int ObtenerMultasPendientes(Tarjeta tarjeta)
        {
            int multas = 0;

            // Multas pendientes por cobrar
            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                multas += multasPendientes[tarjeta.Id];
            }

            // Multas acumulándose actualmente (si hay retiro activo)
            if (ultimosRetiros.ContainsKey(tarjeta.Id))
            {
                DateTime ultimoRetiro = ultimosRetiros[tarjeta.Id];
                DateTime ahora = clock.Now;
                multas += CalcularMultasAcumuladas(ultimoRetiro, ahora);
            }

            return multas;
        }

        // Método para obtener el historial de multas de una tarjeta
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